using CamViewer.Models;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerView의 재생 요청을 실제 NVR Provider에 전달하는 서비스이다.
    ///
    /// 처리 흐름:
    /// 1. PlayerPlaybackRequest 수신
    /// 2. NVR번호별 Provider 생성
    /// 3. Provider Initialize
    /// 4. NVR Login
    /// 5. 좌/우 채널 PlayByTimeAsync 실행
    /// 6. 생성된 재생 세션 보관
    /// </summary>
    public sealed class NvrPlayerPlaybackService : IPlayerPlaybackService
    {
        private readonly INvrProviderFactory _providerFactory;

        private readonly Dictionary<int, INvrProvider> _providers;
        private readonly Dictionary<int, INvrPlaybackSession> _sessions;

        private PlayerPlaybackRequest _currentRequest;
        private DateTime? _currentPlaybackTime;
        private DateTime? _playbackClockStartedAtUtc;
        private bool _disposed;
        private PlaybackSpeed _currentSpeed;
        private readonly Dictionary<int, int> _sessionTimeOffsets;

        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        public PlaybackState CurrentState { get; private set; }
        /// <summary>
        /// 일시정지 직전의 재생 방향 상태.
        /// Playing 또는 Rewinding을 보관한다.
        /// </summary>
        private PlaybackState _pausedFromState;


        /// <summary>
        /// NvrPlayerPlaybackService를 초기화한다.
        /// </summary>
        public NvrPlayerPlaybackService(
            INvrProviderFactory providerFactory)
        {
            if (providerFactory == null)
            {
                throw new ArgumentNullException("providerFactory");
            }

            _providerFactory = providerFactory;
            _providers = new Dictionary<int, INvrProvider>();
            _sessions = new Dictionary<int, INvrPlaybackSession>();

            CurrentState = PlaybackState.Stopped;
            _currentSpeed = PlaybackSpeed.Normal;
            _sessionTimeOffsets = new Dictionary<int, int>();
            _pausedFromState = PlaybackState.Playing;
        }

        /// <summary>
        /// 서비스가 이미 해제되었는지 확인한다.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().FullName);
            }
        }

        /// <summary>
        /// NVR 재생 서비스가 보유한 세션과 Provider 리소스를 정리한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StopAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // 종료 정리 중 발생한 예외는 프로그램 종료 흐름을 막지 않는다.
            }

            _disposed = true;
        }

        /// <summary>
        /// 현재 조회 요청 기준으로 NVR 녹화 영상을 재생한다.
        ///
        /// 처리 순서:
        /// 1. 재생 요청값 검증
        /// 2. 기존 재생 세션 정리
        /// 3. 좌/우 채널별 Provider 재생 요청
        /// 4. 현재 재생시간 기준값 설정
        /// 5. 선택된 재생속도 적용
        /// 6. 재생 상태를 Playing으로 변경
        /// </summary>
        public async Task<PlayerPlaybackResult> PlayAsync(
            PlayerPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 요청이 취소되었습니다.",
                    "PLAYBACK_CANCELLED");
            }

            if (request == null)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_REQUIRED");
            }

            if (request.Channels == null || request.Channels.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생할 채널 정보가 없습니다.",
                    "PLAYBACK_CHANNEL_REQUIRED");
            }

            if (request.PlayStartTime >= request.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.",
                    "INVALID_PLAYBACK_RANGE");
            }

            // 재검색 시에도 사용자가 선택한 재생속도는 유지해야 한다.
            PlaybackSpeed selectedSpeed = _currentSpeed;

            try
            {
                // 기존 재생이 있으면 먼저 정리한다.
                /*
                 * 실제 재생 세션이 존재하는 경우에만
                 * 기존 세션과 Provider를 전체 정리한다.
                 *
                 * 재생 전 영상 원본 정보 조회를 위해 생성한
                 * 정상 로그인 Provider는 그대로 재사용한다.
                 */
                if (_sessions.Count > 0
                    || CurrentState != PlaybackState.Stopped)
                {
                    PlayerPlaybackResult stopResult =
                        await StopAsync(
                            CancellationToken.None);

                    if (stopResult == null
                        || !stopResult.Success)
                    {
                        return PlayerPlaybackResult.Fail(
                            "기존 재생 정리 중 오류가 발생했습니다. "
                            + (
                                stopResult == null
                                    ? "정리 결과가 없습니다."
                                    : stopResult.Message
                            ),
                            stopResult == null
                                ? "PLAYBACK_STOP_RESULT_EMPTY"
                                : stopResult.ErrorCode);
                    }
                }
                else
                {
                    /*
                     * 세션은 없지만 원본 정보 조회에서 로그인된 Provider가 있을 수 있다.
                     * Provider는 유지하고 재생 관련 상태만 초기화한다.
                     */
                    _sessions.Clear();
                    _sessionTimeOffsets.Clear();

                    _currentRequest =
                        null;

                    _currentPlaybackTime =
                        null;

                    _playbackClockStartedAtUtc =
                        null;

                    CurrentState =
                        PlaybackState.Stopped;
                }

                // StopAsync에서 내부 상태가 초기화될 수 있으므로 선택 속도를 복원한다.
                _currentSpeed = selectedSpeed;

                _currentRequest = request;
                _currentPlaybackTime = request.PlayStartTime;
                _playbackClockStartedAtUtc = null;
                CurrentState = PlaybackState.Stopped;

                foreach (PlayerChannelTarget channel in request.Channels)
                {
                    PlayerPlaybackResult playChannelResult =
                        await PlayChannelAsync(
                            request,
                            channel,
                            request.PlayStartTime,
                            request.PlayEndTime,
                            cancellationToken);

                    if (!playChannelResult.Success)
                    {
                        await StopAsync(CancellationToken.None);

                        return playChannelResult;
                    }
                }

                /*
                 * 모든 채널 재생 세션 생성이 성공한 뒤
                 * 기본 재생시간과 상태를 설정한다.
                 */
                _currentPlaybackTime =
                    request.PlayStartTime;

                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;

                CurrentState =
                    PlaybackState.Playing;

                /*
                 * 새로 생성된 Dahua 재생 핸들은 실제로 1배속 상태다.
                 *
                 * 사용자가 이전 재생에서 선택한 배속은 별도로 보관하고,
                 * 초기 준비 확인과 동기화는 1배속 기준으로 수행한다.
                 */
                PlaybackSpeed requestedSpeed =
                    _currentSpeed;

                _currentSpeed =
                    PlaybackSpeed.Normal;

                string readinessWarningMessage =
                    string.Empty;

                bool playbackReady =
                    await WaitForPlaybackReadyAsync(
                        cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    PlayerPlaybackResult cancelledResult =
                        PlayerPlaybackResult.Fail(
                            "재생 준비 확인 요청이 취소되었습니다.",
                            "PLAYBACK_READY_CANCELLED");

                    _currentSpeed =
                        requestedSpeed;

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 준비 확인",
                        cancelledResult,
                        "PLAYBACK_READY_CANCELLED");
                }

                /*
                 * 좌우 채널에서 유효한 OSD 시간이 모두 확인된 경우에만
                 * 초기 자동 동기화를 수행한다.
                 */
                if (playbackReady)
                {
                    PlayerPlaybackResult syncResult =
                        await ResyncPlaybackSessionsAsync(
                            cancellationToken);

                    if (syncResult == null
                        || !syncResult.Success)
                    {
                        /*
                         * 기존 선택 배속값은 유지한 상태로 복구한다.
                         */
                        _currentSpeed =
                            requestedSpeed;

                        return await RecoverFromPlaybackFailureAsync(
                            "좌우 영상 동기화",
                            syncResult,
                            "PLAYBACK_SYNC_FAILED");
                    }
                }
                else
                {
                    /*
                     * 3초 안에 좌우 모두 준비되지 않은 경우
                     * 비정상적인 OSD 시간을 사용한 자동 Seek는 실행하지 않는다.
                     *
                     * 재생 자체는 계속 유지한다.
                     */
                    readinessWarningMessage =
                        Environment.NewLine
                        + "일부 채널의 재생 준비가 지연되어 "
                        + "초기 자동 동기화를 생략했습니다.";
                }

                /*
                 * 초기 동기화가 끝난 뒤
                 * 사용자가 선택했던 재생속도를 복원한다.
                 */
                _currentSpeed =
                    requestedSpeed;

                if (_currentSpeed
                    != PlaybackSpeed.Normal)
                {
                    PlayerPlaybackResult speedResult =
                        await ApplyCurrentSpeedToSessionsAsync(
                            cancellationToken);

                    if (speedResult == null || !speedResult.Success)
                    {
                        /*
                         * 일부 채널에만 배속이 적용됐을 가능성이 있으므로
                         * 재생을 계속 유지하지 않고 전체 세션을 정리한다.
                         */
                        _currentSpeed =
                            PlaybackSpeed.Normal;

                        return await RecoverFromPlaybackFailureAsync(
                            "재생 시작 후 배속 적용",
                            speedResult,
                            "PLAYBACK_INITIAL_SPEED_FAILED");
                    }
                }

                return PlayerPlaybackResult.Ok(
                    "NVR 재생을 시작했습니다. "
                    + request.PlayStartTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + " ~ "
                    + request.PlayEndTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + readinessWarningMessage);
            }
            catch (Exception ex)
            {
                try
                {
                    await StopAsync(CancellationToken.None);
                }
                catch
                {
                    // 재생 실패 후 정리 과정의 예외는 원래 예외 메시지를 가리지 않는다.
                }

                _currentRequest = null;
                _currentPlaybackTime = null;
                _playbackClockStartedAtUtc = null;
                CurrentState = PlaybackState.Stopped;

                return PlayerPlaybackResult.Fail(
                    "NVR 재생 시작 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_START_FAILED");
            }
        }

        /// <summary>
        /// 현재 선택된 재생속도.
        /// </summary>
        public PlaybackSpeed CurrentPlaybackSpeed
        {
            get { return _currentSpeed; }
        }

        /// <summary>
        /// Provider 실제 재생시간을 조회하지 못할 때 사용할 추정 영상재생시간을 계산한다.
        /// 
        /// 재생 중이면 시간이 증가하고,
        /// 역재생 중이면 시간이 감소한다.
        /// </summary>
        private DateTime GetEstimatedPlaybackTime()
        {
            if (_currentRequest == null)
            {
                return DateTime.MinValue;
            }

            DateTime baseTime =
                _currentPlaybackTime.HasValue
                    ? _currentPlaybackTime.Value
                    : _currentRequest.PlayStartTime;

            if (!_playbackClockStartedAtUtc.HasValue)
            {
                return ClampPlaybackTime(baseTime);
            }

            double elapsedSeconds =
                (DateTime.UtcNow - _playbackClockStartedAtUtc.Value)
                .TotalSeconds;

            double speedMultiplier =
                GetSpeedMultiplier(_currentSpeed);

            DateTime estimatedTime;

            if (CurrentState == PlaybackState.Rewinding)
            {
                estimatedTime =
                    baseTime.AddSeconds(
                        -elapsedSeconds * speedMultiplier);
            }
            else
            {
                estimatedTime =
                    baseTime.AddSeconds(
                        elapsedSeconds * speedMultiplier);
            }

            return ClampPlaybackTime(estimatedTime);
        }

        /// <summary>
        /// 재생시간이 현재 조회 구간을 벗어나지 않도록 보정한다.
        /// </summary>
        private DateTime ClampPlaybackTime(DateTime playbackTime)
        {
            if (_currentRequest == null)
            {
                return playbackTime;
            }

            if (playbackTime < _currentRequest.PlayStartTime)
            {
                return _currentRequest.PlayStartTime;
            }

            if (playbackTime > _currentRequest.PlayEndTime)
            {
                return _currentRequest.PlayEndTime;
            }

            return playbackTime;
        }

        /// <summary>
        /// 재생속도 enum 값을 실제 시간 계산 배율로 변환한다.
        /// </summary>
        private static double GetSpeedMultiplier(PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return 0.5;

                case PlaybackSpeed.Double:
                    return 2.0;

                case PlaybackSpeed.Quad:
                    return 4.0;

                case PlaybackSpeed.Octuple:
                    return 8.0;

                case PlaybackSpeed.Normal:
                default:
                    return 1.0;
            }
        }

        /// <summary>
        /// 현재 재생 중인 영상 시각.
        /// Provider 실제시간이 갱신되지 않은 구간에서는
        /// 서비스 기준시간과 경과시간으로 현재 위치를 추정한다.
        /// </summary>
        public DateTime? CurrentPlaybackTime
        {
            get
            {
                if (_currentRequest == null)
                {
                    return null;
                }

                return GetEstimatedPlaybackTime();
            }
        }

        /// <summary>
        /// 현재 재생 중인 모든 채널을 일시정지한다.
        ///
        /// 좌우 채널 중 하나라도 Provider를 찾지 못하거나
        /// Pause 명령에 실패하면 부분 일시정지 상태를 허용하지 않고
        /// 전체 재생 세션을 정리한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PauseAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "일시정지할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            /*
             * Pause 명령을 보내기 전에 현재 영상재생시간을 고정한다.
             *
             * 모든 Provider Pause가 끝난 뒤 시간을 계산하면
             * 좌우 채널 처리 시간만큼 재생시간이 더 증가할 수 있으므로
             * 명령 시작 시점의 시간을 기준으로 보관한다.
             */
            DateTime pauseTime =
                GetEstimatedPlaybackTime();

            PlaybackState stateBeforePause =
                CurrentState;

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrPlaybackSession session =
                    item.Value;

                if (session == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "재생 세션 정보가 없습니다.",
                            "PLAYBACK_SESSION_INVALID");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 일시정지",
                        failureResult,
                        "PLAYBACK_PAUSE_FAILED");
                }

                INvrProvider provider =
                    GetProviderByNvrNo(
                        session.NvrNo);

                /*
                 * 기존에는 Provider가 없으면 continue 처리했지만,
                 * 이 경우 일부 채널만 일시정지될 수 있다.
                 *
                 * 좌우 영상 상태가 달라지는 것을 막기 위해
                 * 전체 재생 실패로 처리한다.
                 */
                if (provider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "NVR Provider를 찾을 수 없습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo,
                            "NVR_PROVIDER_NOT_FOUND");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 일시정지",
                        failureResult,
                        "PLAYBACK_PAUSE_FAILED");
                }

                NvrResult result =
                    await provider.PauseAsync(
                        session,
                        cancellationToken);

                if (result == null || !result.Success)
                {
                    PlayerPlaybackResult failureResult =
                        ToPlayerResult(result);

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 일시정지",
                        failureResult,
                        "PLAYBACK_PAUSE_FAILED");
                }
            }

            /*
             * 모든 채널 Pause 성공 후에만 서비스 상태를 Paused로 변경한다.
             */
            _currentPlaybackTime =
                ClampPlaybackTime(
                    pauseTime);

            _playbackClockStartedAtUtc =
                null;

            if (stateBeforePause == PlaybackState.Rewinding)
            {
                _pausedFromState =
                    PlaybackState.Rewinding;
            }
            else
            {
                _pausedFromState =
                    PlaybackState.Playing;
            }

            CurrentState =
                PlaybackState.Paused;

            return PlayerPlaybackResult.Ok(
                "일시정지했습니다.");
        }

        /// <summary>
        /// 일시정지 상태의 모든 재생 채널을 재개한다.
        ///
        /// 좌우 채널 중 하나라도 Provider 누락 또는 Resume 실패가 발생하면
        /// 일부 채널만 재생되는 상태를 허용하지 않고 전체 세션을 정리한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> ResumeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재개할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            if (CurrentState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 재생을 재개할 수 없습니다.",
                    "PLAYBACK_NOT_PAUSED");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrPlaybackSession session =
                    item.Value;

                if (session == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "재생 세션 정보가 없습니다.",
                            "PLAYBACK_SESSION_INVALID");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 재개",
                        failureResult,
                        "PLAYBACK_RESUME_FAILED");
                }

                INvrProvider provider =
                    GetProviderByNvrNo(
                        session.NvrNo);

                if (provider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "NVR Provider를 찾을 수 없습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo,
                            "NVR_PROVIDER_NOT_FOUND");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 재개",
                        failureResult,
                        "PLAYBACK_RESUME_FAILED");
                }

                NvrResult result =
                    await provider.ResumeAsync(
                        session,
                        cancellationToken);

                if (result == null || !result.Success)
                {
                    PlayerPlaybackResult failureResult =
                        ToPlayerResult(result);

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 재개",
                        failureResult,
                        "PLAYBACK_RESUME_FAILED");
                }
            }

            /*
             * 모든 채널 Resume 성공 후에만 재생시간 기준 시계를 다시 시작한다.
             */
            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            if (_pausedFromState == PlaybackState.Rewinding)
            {
                CurrentState =
                    PlaybackState.Rewinding;

                return PlayerPlaybackResult.Ok(
                    "역재생을 재개했습니다.");
            }

            CurrentState =
                PlaybackState.Playing;

            return PlayerPlaybackResult.Ok(
                "재생을 재개했습니다.");
        }

        /// <summary>
        /// 현재 재생 위치를 지정 초만큼 이동한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekSecondsAsync(
            int seconds,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            DateTime? syncedTime =
                await SyncPlaybackTimeAsync(
                    cancellationToken);

            DateTime currentTime =
                syncedTime.HasValue
                    ? syncedTime.Value
                    : GetEstimatedPlaybackTime();

            DateTime targetTime =
                currentTime.AddSeconds(seconds);

            return await SeekToTimeAsync(
                targetTime,
                cancellationToken);
        }

        /// <summary>
        /// 빠른재생을 요청한다.
        /// 현재 Dahua Provider 공통 구현 전 단계에서는 미지원으로 처리한다.
        /// </summary>
        //public Task<PlayerPlaybackResult> FastForwardAsync(
        //    CancellationToken cancellationToken)
        //{
        //    EnsureNotDisposed();

        //    if (_sessions.Count == 0)
        //    {
        //        return Task.FromResult(
        //            PlayerPlaybackResult.Fail(
        //                "빠른재생할 재생 세션이 없습니다.",
        //                "PLAYBACK_NOT_STARTED"));
        //    }

        //    return Task.FromResult(
        //        PlayerPlaybackResult.Fail(
        //            "빠른재생 기능은 아직 지원되지 않습니다.",
        //            "FAST_FORWARD_NOT_SUPPORTED"));
        //}



        /// <summary>
        /// 현재 재생을 중지하고 모든 재생 세션과 Provider 리소스를 정리한다.
        ///
        /// 정리 과정에서 일부 작업이 실패해도 나머지 세션과 Provider 정리를
        /// 계속 수행하며, 발생한 모든 정리 경고를 수집하여 반환한다.
        ///
        /// 처리 순서:
        /// 1. 모든 재생 세션에 Stop 요청
        /// 2. 모든 재생 세션 Dispose
        /// 3. 모든 Provider Logout
        /// 4. 모든 Provider Dispose
        /// 5. 내부 세션/Provider Dictionary 초기화
        /// 6. 현재 재생 요청과 재생시간 상태 초기화
        /// </summary>
        public async Task<PlayerPlaybackResult> StopAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            /*
             * 정리 중 발생한 모든 경고를 수집한다.
             *
             * 기존에는 마지막 예외 메시지 하나만 보관했기 때문에
             * 여러 채널에서 동시에 오류가 발생하면 앞선 오류 정보가 사라졌다.
             */
            List<string> cleanupWarnings = new List<string>();

            /*
             * StopAsync 실행 도중 Dictionary가 변경될 가능성을 막기 위해
             * 복사본을 순회한다.
             */
            List<KeyValuePair<int, INvrPlaybackSession>> sessionItems = _sessions.ToList();

            foreach (KeyValuePair<int, INvrPlaybackSession> item in sessionItems)
            {
                int sessionKey = item.Key;

                INvrPlaybackSession session = item.Value;

                if (session == null)
                {
                    cleanupWarnings.Add(
                        "재생 세션 정보가 없습니다. "
                        + "SessionKey="
                        + sessionKey);

                    continue;
                }

                INvrProvider provider = GetProviderByNvrNo(session.NvrNo);

                /*
                 * Provider Stop 실패와 세션 Dispose 실패를 분리한다.
                 *
                 * Provider Stop에서 예외가 발생하더라도
                 * session.Dispose는 반드시 별도로 시도해야 한다.
                 */
                if (provider == null)
                {
                    cleanupWarnings.Add(
                        "재생 세션을 중지할 NVR Provider를 찾을 수 없습니다. "
                        + "NvrNo="
                        + session.NvrNo
                        + ", ChannelNo="
                        + session.ChannelNo
                        + ", ScreenPosition="
                        + session.ScreenPosition);
                }
                else
                {
                    try
                    {
                        /*
                         * 재생 정리는 요청 취소 여부와 관계없이 끝까지 수행해야 한다.
                         *
                         * 오류 복구 중 전달된 CancellationToken이 이미 취소됐을 수 있으므로
                         * Provider 정리에는 CancellationToken.None을 사용한다.
                         */
                        NvrResult stopResult = await provider.StopAsync(session, CancellationToken.None);

                        if (stopResult == null)
                        {
                            cleanupWarnings.Add(
                                "NVR 재생 중지 결과가 없습니다. "
                                + "NvrNo="
                                + session.NvrNo
                                + ", ChannelNo="
                                + session.ChannelNo
                                + ", ScreenPosition="
                                + session.ScreenPosition);
                        }
                        else if (!stopResult.Success)
                        {
                            cleanupWarnings.Add(
                                "NVR 재생 세션 중지에 실패했습니다. "
                                + "NvrNo="
                                + session.NvrNo
                                + ", ChannelNo="
                                + session.ChannelNo
                                + ", ScreenPosition="
                                + session.ScreenPosition
                                + ", Message="
                                + (
                                    string.IsNullOrWhiteSpace(stopResult.Message)
                                        ? stopResult.Status.ToString()
                                        : stopResult.Message));
                        }
                    }
                    catch (Exception ex)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 세션 중지 중 예외가 발생했습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo
                            + ", ScreenPosition="
                            + session.ScreenPosition
                            + ", Error="
                            + ex.Message);
                    }
                }

                /*
                 * Provider Stop의 성공 여부와 관계없이
                 * 세션 객체의 로컬 리소스는 반드시 해제한다.
                 */
                try
                {
                    session.Dispose();
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "재생 세션 리소스 해제 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + session.NvrNo
                        + ", ChannelNo="
                        + session.ChannelNo
                        + ", ScreenPosition="
                        + session.ScreenPosition
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * 모든 세션 정리 시도가 끝났으므로
             * 세션 Dictionary와 보정값을 초기화한다.
             */
            _sessions.Clear();
            _sessionTimeOffsets.Clear();

            List<KeyValuePair<int, INvrProvider>> providerItems = _providers.ToList();

            foreach (KeyValuePair<int, INvrProvider> item in providerItems)
            {
                int nvrNo = item.Key;

                INvrProvider provider = item.Value;

                if (provider == null)
                {
                    cleanupWarnings.Add(
                        "NVR Provider 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                /*
                 * Provider Logout과 Dispose를 별도의 try 블록으로 처리한다.
                 *
                 * Logout 실패가 발생해도 SDK 및 네이티브 리소스 해제를 위해
                 * Dispose는 반드시 계속 수행한다.
                 */
                try
                {
                    NvrResult logoutResult = await provider.LogoutAsync(CancellationToken.None);

                    if (logoutResult == null)
                    {
                        cleanupWarnings.Add(
                            "NVR 로그아웃 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo);
                    }
                    else if (!logoutResult.Success)
                    {
                        cleanupWarnings.Add(
                            "NVR 로그아웃에 실패했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(logoutResult.Message)
                                    ? logoutResult.Status.ToString()
                                    : logoutResult.Message));
                    }
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "NVR 로그아웃 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }

                try
                {
                    provider.Dispose();
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "NVR Provider 리소스 해제 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * 모든 Provider 정리 시도가 끝났으므로 Dictionary를 초기화한다.
             */
            _providers.Clear();

            /*
             * 실제 Provider 정리 결과와 관계없이
             * 서비스 내부의 논리적인 재생 상태는 반드시 정지 상태로 되돌린다.
             */
            _currentRequest =
                null;

            _currentPlaybackTime =
                null;

            _playbackClockStartedAtUtc =
                null;

            _pausedFromState =
                PlaybackState.Playing;

            CurrentState =
                PlaybackState.Stopped;

            if (cleanupWarnings.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "재생은 중지되었지만 일부 리소스 정리 중 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        cleanupWarnings.Select(
                            warning => "- " + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "재생을 중지했습니다.");
        }

        /// <summary>
        /// 현재 재생 중인 채널들의 시간 동기화 상태를 조회한다.
        /// Provider가 실제 재생시간을 지원하면 실제 시간을 사용하고,
        /// 지원하지 않으면 추정 시간을 사용한다.
        /// </summary>
        public async Task<PlaybackSyncStatus> GetPlaybackSyncStatusAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            var channelStatuses =
                new List<PlaybackChannelTimeStatus>();

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                int sessionKey = item.Key;
                INvrPlaybackSession session = item.Value;
                if (session == null)
                {
                    continue;
                }
                int offsetSeconds =
                    GetSessionTimeOffset(sessionKey);

                DateTime? providerTime =
                    await TryGetProviderPlaybackTimeAsync(
                        session,
                        cancellationToken);

                bool isProviderTime =
                    providerTime.HasValue;

                DateTime? displayBaseTime = null;

                DateTime normalizedTime;

                if (providerTime.HasValue
                    && TryNormalizeProviderPlaybackTime(
                        providerTime.Value,
                        offsetSeconds,
                        out normalizedTime))
                {
                    displayBaseTime =
                        normalizedTime;

                    isProviderTime =
                        true;
                }
                else
                {
                    /*
                     * 비정상적인 Provider 시간을 추정시간으로 위장하지 않는다.
                     * 해당 채널은 시간 측정 실패 상태로 둔다.
                     */
                    displayBaseTime =
                        null;

                    isProviderTime =
                        false;
                }

                channelStatuses.Add(
                    new PlaybackChannelTimeStatus
                    {
                        ScreenPosition =
                            GetScreenPositionText(session.ScreenPosition),
                        NvrNo = session.NvrNo,
                        ChannelNo = session.ChannelNo,
                        PlaybackTime = displayBaseTime,
                        IsProviderTime = isProviderTime,
                        TimeOffsetSeconds = offsetSeconds
                    });
            }

            return PlaybackSyncStatus.FromChannels(channelStatuses);
        }

        /// <summary>
        /// 좌측 화면을 기준으로 좌우 영상의 재생시간을 동기화한다.
        ///
        /// 정방향:
        /// - 시간이 어긋난 채널에 일반 Seek를 적용한다.
        ///
        /// 역방향:
        /// - 일반 Seek를 사용하면 정방향 세션으로 바뀔 수 있으므로
        ///   기준시간에서 모든 역재생 세션을 다시 생성한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> ResyncPlaybackSessionsAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            const double allowedDifferenceSeconds = 1.0;
            const int leftScreenPosition = 1;

            List<PlaybackTimeSnapshot> snapshots = await GetPlaybackTimeSnapshotsAsync(cancellationToken);

            if (snapshots.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "좌우 영상의 실제 재생시간을 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.",
                    "PLAYBACK_SYNC_TIME_UNAVAILABLE",
                    PlaybackFailureCategory.Retryable);
            }

            PlaybackTimeSnapshot master = snapshots.FirstOrDefault(item =>item.Session != null && item.Session.ScreenPosition == leftScreenPosition);

            if (master == null)
            {
                master = snapshots[0];
            }

            Dictionary<int, PlaybackTimeSnapshot> snapshotMap = snapshots.ToDictionary(item => item.SessionKey);
            double maxDifferenceSeconds = snapshots.Max(item => Math.Abs((item.NormalizedTime - master.NormalizedTime).TotalSeconds));
            bool allSessionTimesAvailable = snapshots.Count >= _sessions.Count;
            /*
             * 허용 범위 안이면 세션을 재생성하거나 Seek하지 않고
             * 서비스 기준시간만 실제 Provider 시간으로 맞춘다.
             */
            if (allSessionTimesAvailable && maxDifferenceSeconds <= allowedDifferenceSeconds)
            {
                _currentPlaybackTime = master.NormalizedTime;

                if (CurrentState == PlaybackState.Playing || CurrentState == PlaybackState.Rewinding)
                {
                    _playbackClockStartedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    _playbackClockStartedAtUtc = null;
                }

                return PlayerPlaybackResult.Ok( "좌우 영상 동기화 상태가 정상입니다.");
            }

            /*
             * 역재생 또는 역재생 일시정지 상태에서는
             * 일반 Provider.SeekAsync를 호출하지 않는다.
             *
             * 좌측 기준시간으로 전체 역재생 세션을 다시 생성하여
             * 방향과 배속을 유지하면서 좌우 시간을 맞춘다.
             */
            if (IsReversePlaybackDirection())
            {
                bool keepPaused = CurrentState == PlaybackState.Paused;

                DateTime reverseSyncTime = ClampPlaybackTime(master.NormalizedTime);

                PlayerPlaybackResult reverseSyncResult = await SeekReverseToTimeAsync(reverseSyncTime,keepPaused,cancellationToken);

                if (!reverseSyncResult.Success)
                {
                    return reverseSyncResult;
                }

                return PlayerPlaybackResult.Ok(
                    "역재생 방향을 유지하면서 좌우 영상 싱크를 보정했습니다. "
                    + "기준시간 "
                    + reverseSyncTime.ToString("yyyy-MM-dd HH:mm:ss")
                    + ", 최대 차이 "
                    + maxDifferenceSeconds.ToString("0.0")
                    + "초");
            }

            /*
             * 여기부터는 정방향 또는 정방향 일시정지 상태의 동기화다.
             */
            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions.ToList())
            {
                int sessionKey =
                    item.Key;

                INvrPlaybackSession session =
                    item.Value;

                if (session == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "동기화할 재생 세션 정보가 없습니다.",
                        "PLAYBACK_SYNC_SESSION_INVALID",
                        PlaybackFailureCategory.System);
                }

                /*
                 * 기준 세션은 이동하지 않는다.
                 */
                if (sessionKey == master.SessionKey)
                {
                    continue;
                }

                PlaybackTimeSnapshot currentSnapshot;

                bool hasValidSnapshot =
                    snapshotMap.TryGetValue(
                        sessionKey,
                        out currentSnapshot);

                /*
                 * 정상 시간이 확인된 채널은 차이가 허용 범위 이내면
                 * 별도 Seek를 실행하지 않는다.
                 *
                 * 시간이 유효하지 않은 채널은 기준 채널 시간으로 강제 보정한다.
                 */
                if (hasValidSnapshot)
                {
                    double differenceSeconds =
                        Math.Abs(
                            (
                                currentSnapshot.NormalizedTime
                                - master.NormalizedTime
                            ).TotalSeconds);

                    if (differenceSeconds
                        <= allowedDifferenceSeconds)
                    {
                        continue;
                    }
                }

                INvrProvider provider =
                    GetProviderByNvrNo(
                        session.NvrNo);

                if (provider == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "동기화할 NVR Provider를 찾을 수 없습니다.",
                        "NVR_PROVIDER_NOT_FOUND",
                        PlaybackFailureCategory.Configuration);
                }

                int offsetSeconds =
                    GetSessionTimeOffset(
                        sessionKey);

                DateTime providerTargetTime =
                    master.NormalizedTime.AddSeconds(
                        offsetSeconds);

                NvrResult seekResult =
                    await provider.SeekAsync(
                        session,
                        providerTargetTime,
                        cancellationToken);

                if (seekResult == null
                    || !seekResult.Success)
                {
                    return ToPlayerResult(
                        seekResult);
                }

                if (_currentSpeed != PlaybackSpeed.Normal)
                {
                    NvrResult speedResult =
                        await provider.SetPlaybackSpeedAsync(
                            session,
                            ToNvrPlaybackSpeed(
                                _currentSpeed),
                            cancellationToken);

                    if (speedResult == null
                        || !speedResult.Success)
                    {
                        return ToPlayerResult(
                            speedResult);
                    }
                }

                if (CurrentState == PlaybackState.Paused)
                {
                    NvrResult pauseResult =
                        await provider.PauseAsync(
                            session,
                            cancellationToken);

                    if (pauseResult == null
                        || !pauseResult.Success)
                    {
                        return ToPlayerResult(
                            pauseResult);
                    }
                }
            }

            _currentPlaybackTime = master.NormalizedTime;

            if (CurrentState == PlaybackState.Playing)
            {
                _playbackClockStartedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _playbackClockStartedAtUtc = null;
            }

            return PlayerPlaybackResult.Ok(
                "좌우 영상 싱크를 보정했습니다. 최대 차이 "
                + maxDifferenceSeconds.ToString("0.0")
                + "초");
        }

        /// <summary>
        /// 재생 대상 채널의 영상 원본 정보를 조회한다.
        /// Provider가 지원하지 않으면 실패 결과를 반환한다.
        /// </summary>
        public async Task<PlayerVideoSourceInfoResult> GetVideoSourceInfoAsync(PlayerChannelTarget channel, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (channel == null || channel.NvrConfig == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    ScreenPosition.Left,
                    "NVR 채널 설정이 없습니다.");
            }

            NvrResult<INvrProvider> providerResult =
                await GetOrCreateLoggedInProviderAsync(
                    channel.NvrConfig,
                    cancellationToken);

            if (providerResult == null
                || !providerResult.Success
                || providerResult.Data == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    providerResult == null
                        ? "NVR Provider 연결 결과가 없습니다."
                        : providerResult.Message);
            }

            INvrProvider provider =
                providerResult.Data;

            ProviderCapabilities capabilities =
                provider.GetCapabilities();

            if (capabilities == null || !capabilities.CanGetVideoSourceInfo)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    "현재 NVR Provider는 영상 원본 정보 조회를 지원하지 않습니다.");
            }

            INvrVideoSourceInfoProvider sourceInfoProvider =
                provider as INvrVideoSourceInfoProvider;

            if (sourceInfoProvider == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    "현재 NVR Provider는 영상 원본 정보 조회 인터페이스를 구현하지 않았습니다.");
            }

            NvrResult<NvrVideoSourceInfo> result =
                await sourceInfoProvider.GetVideoSourceInfoAsync(
                    channel.ChannelNo,
                    cancellationToken);

            if (result == null || !result.Success || result.Data == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    result == null
                        ? "영상 원본 정보 조회 결과가 없습니다."
                        : string.IsNullOrWhiteSpace(result.Message)
                            ? "영상 원본 정보 조회에 실패했습니다."
                            : result.Message);
            }

            return PlayerVideoSourceInfoResult.Ok(
                channel.ScreenPosition,
                result.Data.Width,
                result.Data.Height);
        }


        /// <summary>
        /// 현재 재생 위치를 기준으로 역재생을 시작한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> RewindAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "역재생할 재생 세션이 없습니다. 먼저 영상을 재생해 주세요.",
                    "PLAYBACK_SESSION_EMPTY");
            }

            DateTime? syncedTime =
                await SyncPlaybackTimeAsync(
                    cancellationToken);

            DateTime reverseStartTime =
                syncedTime.HasValue
                    ? syncedTime.Value
                    : GetEstimatedPlaybackTime();

            if (reverseStartTime <= _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간보다 이전으로 역재생할 수 없습니다.",
                    "REWIND_BEFORE_START");
            }

            /*
             * 현재 정방향 재생 세션만 정리한다.
             * Provider 로그인과 현재 재생 요청은 유지한다.
             */
            PlayerPlaybackResult stopResult =
                await StopCurrentPlaybackSessionsOnlyAsync(
                    CancellationToken.None);

            if (!stopResult.Success)
            {
                return stopResult;
            }

            foreach (PlayerChannelTarget channel
                in _currentRequest.Channels)
            {
                NvrResult<INvrProvider> providerResult =await GetOrCreateLoggedInProviderAsync(channel.NvrConfig, cancellationToken);

                if (providerResult == null || !providerResult.Success || providerResult.Data == null)
                {
                    await StopCurrentPlaybackSessionsOnlyAsync(CancellationToken.None);

                    return ToPlayerResult(providerResult);
                }

                INvrProvider provider =providerResult.Data;

                ProviderCapabilities capabilities =provider.GetCapabilities();

                if (capabilities == null || !capabilities.CanReversePlayback)
                {
                    await StopCurrentPlaybackSessionsOnlyAsync(CancellationToken.None);

                    return PlayerPlaybackResult.Fail(
                        "현재 NVR Provider는 역재생을 지원하지 않습니다.",
                        "REVERSE_PLAYBACK_NOT_SUPPORTED");
                }

                INvrReversePlaybackProvider reverseProvider = provider as INvrReversePlaybackProvider;

                if (reverseProvider == null)
                {
                    await StopCurrentPlaybackSessionsOnlyAsync(
                        CancellationToken.None);

                    return PlayerPlaybackResult.Fail(
                        "현재 NVR Provider는 역재생 인터페이스를 구현하지 않았습니다.",
                        "REVERSE_PROVIDER_NOT_IMPLEMENTED");
                }

                NvrPlaybackRequest nvrRequest =
                    ToNvrPlaybackRequest(
                        _currentRequest,
                        channel,
                        _currentRequest.PlayStartTime,
                        _currentRequest.PlayEndTime);

                int offsetSeconds =
                    channel.TimeOffsetSeconds;

                DateTime providerReverseStartTime =
                    reverseStartTime.AddSeconds(
                        offsetSeconds);

                NvrResult<INvrPlaybackSession> reverseResult =
                    await reverseProvider.PlayReverseByTimeAsync(
                        nvrRequest,
                        providerReverseStartTime,
                        cancellationToken);

                if (reverseResult == null || !reverseResult.Success || reverseResult.Data == null)
                {
                    PlayerPlaybackResult failureResult = ToPlayerResult(reverseResult);

                    /*
                     * 세션 정리 전에 실패 정보를 기록한다.
                     * 정리 후에는 Provider 또는 요청 정보가 사라질 수 있다.
                     */
                    WriteNvrFailureLog(
                        "역재생 시작",
                        channel.NvrConfig,
                        channel,
                        null,
                        provider,
                        reverseResult,
                        failureResult,
                        "ReverseStartTime="
                        + providerReverseStartTime.ToString(
                            "yyyy-MM-dd HH:mm:ss"));

                    await StopCurrentPlaybackSessionsOnlyAsync(CancellationToken.None);

                    return failureResult;
                }

                int sessionKey =
                    BuildSessionKey(
                        channel.NvrNo,
                        channel.ChannelNo,
                        (int)channel.ScreenPosition);

                _sessions[sessionKey] =
                    reverseResult.Data;

                _sessionTimeOffsets[sessionKey] =
                    offsetSeconds;
            }

            /*
             * 모든 채널 역재생 세션이 정상 생성된 뒤
             * 서비스 상태를 역재생으로 확정한다.
             */
            _currentPlaybackTime =
                reverseStartTime;

            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            CurrentState =
                PlaybackState.Rewinding;

            /*
             * 사용자가 선택한 속도가 1배속이 아니면
             * 새로 생성된 역재생 세션에도 적용한다.
             */
            if (_currentSpeed != PlaybackSpeed.Normal)
            {
                PlayerPlaybackResult speedResult =
                    await ApplyCurrentSpeedToSessionsAsync(
                        cancellationToken);

                if (speedResult == null || !speedResult.Success)
                {
                    _currentSpeed =
                        PlaybackSpeed.Normal;

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 시작 후 배속 적용",
                        speedResult,
                        "REVERSE_SPEED_APPLY_FAILED");
                }
            }

            return PlayerPlaybackResult.Ok(
                "역재생을 시작했습니다.");
        }

        /// <summary>
        /// 현재 영상재생시간을 기준으로 정방향 재생으로 전환한다.
        /// 
        /// 사용 시점:
        /// - 역재생 중 재생 버튼 클릭
        /// 
        /// 처리 순서:
        /// 1. 현재 Provider 재생시간 또는 추정 재생시간 조회
        /// 2. 기존 역재생 세션만 정리
        /// 3. 기존 Provider/Login은 유지
        /// 4. 현재 시간부터 조회 종료시간까지 정방향 재생 세션 재생성
        /// 5. 기존 선택 배속을 새 세션에 다시 적용
        /// </summary>
        public async Task<PlayerPlaybackResult> PlayForwardFromCurrentTimeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "정방향 전환 요청이 취소되었습니다.",
                    "FORWARD_PLAYBACK_CANCELLED");
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "정방향으로 전환할 재생 세션이 없습니다.",
                    "PLAYBACK_SESSION_EMPTY");
            }

            DateTime? syncedTime =
                await SyncPlaybackTimeAsync(
                    cancellationToken);

            DateTime forwardStartTime =
                syncedTime.HasValue
                    ? syncedTime.Value
                    : GetEstimatedPlaybackTime();

            forwardStartTime =
                ClampPlaybackTime(
                    forwardStartTime);

            if (forwardStartTime >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 종료시간 이후로 정방향 재생을 시작할 수 없습니다.",
                    "FORWARD_AFTER_END");
            }

            PlaybackSpeed selectedSpeed =
                _currentSpeed;

            PlayerPlaybackRequest request =
                _currentRequest;

            PlayerPlaybackResult stopResult =
                await StopCurrentPlaybackSessionsOnlyAsync(
                    cancellationToken);

            if (!stopResult.Success)
            {
                return stopResult;
            }

            _currentSpeed =
                selectedSpeed;

            foreach (PlayerChannelTarget channel in request.Channels)
            {
                PlayerPlaybackResult playResult =
                    await PlayChannelAsync(
                        request,
                        channel,
                        forwardStartTime,
                        request.PlayEndTime,
                        cancellationToken);

                if (!playResult.Success)
                {
                    await StopCurrentPlaybackSessionsOnlyAsync(
                        CancellationToken.None);

                    return playResult;
                }
            }

            _currentPlaybackTime =
                forwardStartTime;

            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            CurrentState =
                PlaybackState.Playing;

            /*
             * 기존 선택속도가 1배속이 아니면
             * 새 정방향 세션에 동일하게 적용한다.
             */
            if (_currentSpeed
                != PlaybackSpeed.Normal)
            {
                PlayerPlaybackResult speedResult =
                    await ApplyCurrentSpeedToSessionsAsync(
                        cancellationToken);

                if (speedResult == null
                    || !speedResult.Success)
                {
                    _currentSpeed =
                        PlaybackSpeed.Normal;

                    return await RecoverFromPlaybackFailureAsync(
                        "정방향 전환 후 배속 적용",
                        speedResult,
                        "FORWARD_SPEED_APPLY_FAILED");
                }

                /*
                 * 확정된 운영 정책:
                 * 배속 상태에서는 자동 동기화와 Seek를 실행하지 않는다.
                 */
                return PlayerPlaybackResult.Ok(
                    "정방향 재생으로 전환했습니다. "
                    + forwardStartTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + " / "
                    + GetPlaybackSpeedText(
                        _currentSpeed)
                    + " / 배속 재생 중에는 자동 영상 동기화를 실행하지 않습니다.");
            }

            /*
             * 1배속 정방향 전환인 경우에만
             * 좌우 영상 동기화를 실행한다.
             */
            PlayerPlaybackResult syncResult =
                await ResyncPlaybackSessionsAsync(
                    cancellationToken);

            if (syncResult == null
                || !syncResult.Success)
            {
                return await RecoverFromPlaybackFailureAsync(
                    "정방향 전환 후 좌우 영상 동기화",
                    syncResult,
                    "FORWARD_SYNC_FAILED");
            }

            return PlayerPlaybackResult.Ok(
                "정방향 재생으로 전환했습니다. "
                + forwardStartTime.ToString(
                    "yyyy-MM-dd HH:mm:ss")
                + " / "
                + syncResult.Message);
        }

        /// <summary>
        /// 현재 재생 세션만 정리한다.
        /// Provider와 로그인 상태, 현재 재생 요청 정보는 유지한다.
        /// 
        /// 사용 시점:
        /// - 정방향 재생에서 역재생으로 전환
        /// - 역재생에서 정방향 재생으로 전환
        /// </summary>
        private async Task<PlayerPlaybackResult> StopCurrentPlaybackSessionsOnlyAsync(
            CancellationToken cancellationToken)
        {
            string warningMessage =
                string.Empty;

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions.ToList())
            {
                try
                {
                    INvrProvider provider =
                        GetProviderByNvrNo(
                            item.Value.NvrNo);

                    if (provider != null)
                    {
                        await provider.StopAsync(
                            item.Value,
                            cancellationToken);
                    }

                    item.Value.Dispose();
                }
                catch (Exception ex)
                {
                    warningMessage =
                        ex.Message;
                }
            }

            _sessions.Clear();
            _sessionTimeOffsets.Clear();

            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                return PlayerPlaybackResult.Ok(
                    "기존 재생 세션은 정리되었지만 일부 경고가 발생했습니다. "
                    + warningMessage);
            }

            return PlayerPlaybackResult.Ok(
                "기존 재생 세션을 정리했습니다.");
        }

        /// <summary>
        /// 현재 선택된 재생속도를 모든 재생 세션에 적용한다.
        ///
        /// 한 채널이라도 세션, Provider 또는 속도 적용에 문제가 있으면
        /// 성공으로 처리하지 않는다.
        /// </summary>
        private async Task<PlayerPlaybackResult> ApplyCurrentSpeedToSessionsAsync(
            CancellationToken cancellationToken)
        {
            if (_currentSpeed == PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Ok(
                    "1배속 상태입니다.");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item
                in _sessions.ToList())
            {
                INvrPlaybackSession session =
                    item.Value;

                if (session == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생속도를 적용할 세션 정보가 없습니다.",
                        "PLAYBACK_SESSION_INVALID",
                        PlaybackFailureCategory.System);
                }

                INvrProvider provider =
                    GetProviderByNvrNo(
                        session.NvrNo);

                if (provider == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생속도를 적용할 NVR Provider를 찾을 수 없습니다. "
                        + "NvrNo="
                        + session.NvrNo
                        + ", ChannelNo="
                        + session.ChannelNo
                        + ", ScreenPosition="
                        + session.ScreenPosition,
                        "NVR_PROVIDER_NOT_FOUND",
                        PlaybackFailureCategory.Configuration);
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null
                    || !capabilities.CanChangeSpeed)
                {
                    return PlayerPlaybackResult.Fail(
                        "현재 NVR Provider는 재생속도 변경을 지원하지 않습니다. "
                        + "NvrNo="
                        + session.NvrNo
                        + ", ChannelNo="
                        + session.ChannelNo,
                        "PLAYBACK_SPEED_NOT_SUPPORTED",
                        PlaybackFailureCategory.NotSupported);
                }

                NvrResult speedResult =
                    await provider.SetPlaybackSpeedAsync(
                        session,
                        ToNvrPlaybackSpeed(
                            _currentSpeed),
                        cancellationToken);

                if (speedResult == null
                    || !speedResult.Success)
                {
                    return ToPlayerResult(
                        speedResult);
                }
            }

            return PlayerPlaybackResult.Ok(
                GetPlaybackSpeedText(
                    _currentSpeed)
                + "을 모든 재생 채널에 적용했습니다.");
        }
        /// <summary>
        /// 단일 좌/우 채널 재생을 지정된 시간 구간으로 시작한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> PlayChannelAsync(
            PlayerPlaybackRequest request,
            PlayerChannelTarget channel,
            DateTime playStartTime,
            DateTime playEndTime,
            CancellationToken cancellationToken)
        {
            if (channel == null || channel.NvrConfig == null)
            {
                return PlayerPlaybackResult.Fail(
                    "채널에 연결된 NVR 설정이 없습니다.",
                    "NVR_CONFIG_REQUIRED");
            }

            NvrResult<INvrProvider> providerResult =
                await GetOrCreateLoggedInProviderAsync(
                    channel.NvrConfig,
                    cancellationToken);

            if (providerResult == null
                || !providerResult.Success
                || providerResult.Data == null)
            {
                return ToPlayerResult(
                    providerResult);
            }

            INvrProvider provider =
                providerResult.Data;

            NvrPlaybackRequest nvrRequest =
                ToNvrPlaybackRequest(
                    request, channel, playStartTime, playEndTime);

            NvrResult<INvrPlaybackSession> playResult =
                await provider.PlayByTimeAsync(
                    nvrRequest,
                    cancellationToken);

            if (playResult == null  || !playResult.Success || playResult.Data == null)
            {
                PlayerPlaybackResult failureResult = ToPlayerResult(playResult);

                WriteNvrFailureLog
                    (
                    "채널 재생 시작",
                    channel.NvrConfig,
                    channel,
                    null,
                    provider,
                    playResult,
                    failureResult,
                    "StartTime="
                    + playStartTime.ToString("yyyy-MM-dd HH:mm:ss")
                    + ", EndTime="
                    + playEndTime.ToString("yyyy-MM-dd HH:mm:ss")
                    );

                return failureResult;

            }

            int sessionKey =
                BuildSessionKey(
                    channel.NvrNo,
                    channel.ChannelNo,
                    (int)channel.ScreenPosition);

            _sessions[sessionKey] =
                playResult.Data;

            _sessionTimeOffsets[sessionKey] =
                channel.TimeOffsetSeconds;

            return PlayerPlaybackResult.Ok(
                "채널 재생을 시작했습니다.");
        }

        /// <summary>
        /// NVR번호 기준으로 Provider를 생성하고 로그인한다.
        /// 이미 정상 로그인된 Provider가 있으면 재사용한다.
        ///
        /// 연결, 로그인, 초기화 실패는 예외를 던지지 않고
        /// NvrResult로 반환한다.
        /// </summary>
        private async Task<NvrResult<INvrProvider>>
            GetOrCreateLoggedInProviderAsync(
                NvrConfig nvrConfig,
                CancellationToken cancellationToken)
        {
            if (nvrConfig == null)
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.Failed,
                    "NVR 설정 정보가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "NVR_CONFIG_REQUIRED",
                        ErrorMessage = "NVR 설정 정보가 없습니다.",
                        Operation = "GetOrCreateLoggedInProvider"
                    });
            }

            INvrProvider provider;

            /*
             * 기존 Provider가 정상 로그인된 상태일 때만 재사용한다.
             */
            if (_providers.TryGetValue(
                nvrConfig.NvrNo,
                out provider))
            {
                if (provider != null
                    && provider.IsInitialized
                    && provider.IsLoggedIn)
                {
                    return NvrResult<INvrProvider>.Ok(
                        provider,
                        "기존 NVR Provider를 재사용합니다.");
                }

                /*
                 * Dictionary에 남아 있지만 정상 상태가 아닌 Provider는
                 * 제거하고 새로 생성한다.
                 */
                _providers.Remove(
                    nvrConfig.NvrNo);

                if (provider != null)
                {
                    try
                    {
                        provider.Dispose();
                    }
                    catch
                    {
                        // 비정상 Provider 정리 실패는 새 연결 시도를 막지 않는다.
                    }
                }
            }

            try
            {
                NvrResult<INvrProvider> createResult =
                    _providerFactory.Create(
                        nvrConfig.ProviderKey);

                if (createResult == null
                    || !createResult.Success
                    || createResult.Data == null)
                {
                    NvrResult<INvrProvider> failureResult =
                        createResult
                        ?? NvrResult<INvrProvider>.Fail(
                            NvrResultStatus.ProviderNotFound,
                            "NVR Provider 생성 결과가 없습니다.",
                            new NvrErrorInfo
                            {
                                ErrorCode = "NVR_PROVIDER_RESULT_EMPTY",
                                ErrorMessage =
                                    "NVR Provider 생성 결과가 없습니다.",
                                Operation =
                                    "ProviderFactory.Create"
                            });

                    WriteNvrFailureLog(
                        "NVR Provider 생성",
                        nvrConfig,
                        null,
                        null,
                        null,
                        failureResult,
                        ToPlayerResult(failureResult));

                    return failureResult;
                }

                provider =
                    createResult.Data;

                NvrResult initializeResult =
                    provider.Initialize();

                if (initializeResult == null
                    || !initializeResult.Success)
                {
                    NvrResult<INvrProvider> failureResult =
                        NvrResult<INvrProvider>.Fail(
                            initializeResult == null
                                ? NvrResultStatus.SdkError
                                : initializeResult.Status,
                            initializeResult == null
                                ? "NVR Provider 초기화 결과가 없습니다."
                                : initializeResult.Message,
                            initializeResult == null
                                ? new NvrErrorInfo
                                {
                                    ErrorCode =
                                        "NVR_INITIALIZE_RESULT_EMPTY",
                                    ErrorMessage =
                                        "NVR Provider 초기화 결과가 없습니다.",
                                    Operation =
                                        "Provider.Initialize"
                                }
                                : initializeResult.Error);

                    WriteNvrFailureLog(
                        "NVR Provider 초기화",
                        nvrConfig,
                        null,
                        null,
                        provider,
                        initializeResult,
                        ToPlayerResult(failureResult));

                    try
                    {
                        provider.Dispose();
                    }
                    catch
                    {
                    }

                    return failureResult;
                }

                NvrConnectionInfo connectionInfo =
                    ToConnectionInfo(
                        nvrConfig);

                NvrResult loginResult =
                    await provider.LoginAsync(
                        connectionInfo,
                        cancellationToken);

                if (loginResult == null
                    || !loginResult.Success)
                {
                    NvrResult<INvrProvider> failureResult =
                        NvrResult<INvrProvider>.Fail(
                            loginResult == null
                                ? NvrResultStatus.ConnectionFailed
                                : loginResult.Status,
                            loginResult == null
                                ? "NVR 로그인 결과가 없습니다."
                                : loginResult.Message,
                            loginResult == null
                                ? new NvrErrorInfo
                                {
                                    ErrorCode =
                                        "NVR_LOGIN_RESULT_EMPTY",
                                    ErrorMessage =
                                        "NVR 로그인 결과가 없습니다.",
                                    Operation =
                                        "Provider.LoginAsync"
                                }
                                : loginResult.Error);

                    WriteNvrFailureLog(
                        "NVR 로그인",
                        nvrConfig,
                        null,
                        null,
                        provider,
                        loginResult,
                        ToPlayerResult(failureResult));

                    try
                    {
                        provider.Dispose();
                    }
                    catch
                    {
                    }

                    /*
                     * 예상 가능한 로그인 실패이므로 예외를 던지지 않는다.
                     */
                    return failureResult;
                }

                /*
                 * Provider 초기화와 로그인이 모두 성공한 경우에만
                 * 재사용 Dictionary에 등록한다.
                 */
                _providers[nvrConfig.NvrNo] =
                    provider;

                return NvrResult<INvrProvider>.Ok(
                    provider,
                    "NVR Provider 연결에 성공했습니다.");
            }
            catch (OperationCanceledException)
            {
                if (provider != null)
                {
                    try
                    {
                        provider.Dispose();
                    }
                    catch
                    {
                    }
                }

                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.Cancelled,
                    "NVR 연결 요청이 취소되었습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "NVR_CONNECTION_CANCELLED",
                        ErrorMessage = "NVR 연결 요청이 취소되었습니다.",
                        Operation = "GetOrCreateLoggedInProvider"
                    });
            }
            catch (Exception ex)
            {
                if (provider != null)
                {
                    try
                    {
                        provider.Dispose();
                    }
                    catch
                    {
                    }
                }

                NvrResult<INvrProvider> failureResult =
                    NvrResult<INvrProvider>.Fail(
                        NvrResultStatus.UnknownError,
                        "NVR Provider 연결 처리 중 오류가 발생했습니다. "
                        + ex.Message,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "NVR_PROVIDER_CONNECTION_EXCEPTION",
                            ErrorMessage =
                                ex.Message,
                            Operation =
                                "GetOrCreateLoggedInProvider"
                        });

                WriteNvrFailureLog(
                    "NVR Provider 연결",
                    nvrConfig,
                    null,
                    null,
                    null,
                    failureResult,
                    ToPlayerResult(failureResult));

                return failureResult;
            }
        }

        /// <summary>
        /// 로컬 NVR 설정을 NVR Core 접속 정보로 변환한다.
        /// </summary>
        private static NvrConnectionInfo ToConnectionInfo(
            NvrConfig source)
        {
            NvrConnectionType connectionType;

            if (!Enum.TryParse(
                source.ConnectionType,
                true,
                out connectionType))
            {
                connectionType = NvrConnectionType.Sdk;
            }

            var target = new NvrConnectionInfo
            {
                NvrNo = source.NvrNo,
                ProviderKey = source.ProviderKey,
                Vendor = source.Vendor,
                ConnectionType = connectionType,
                Host = source.Host,
                Port = source.Port,
                UserId = source.UserId,
                Password = source.Password,
                ChannelCount = source.ChannelCount
            };

            if (source.ProviderSettings != null)
            {
                foreach (KeyValuePair<string, string> item in source.ProviderSettings)
                {
                    target.ProviderSettings[item.Key] = item.Value;
                }
            }

            return target;
        }

        /// <summary>
        /// Player 재생 요청을 NVR Core 재생 요청으로 변환한다.
        /// </summary>
        private static NvrPlaybackRequest ToNvrPlaybackRequest(
                PlayerPlaybackRequest request,
                PlayerChannelTarget channel,
                DateTime playStartTime,
                DateTime playEndTime)
        {
            return new NvrPlaybackRequest
            {
                CounterNo = request.CounterNo,
                NvrNo = channel.NvrNo,
                ChannelNo = channel.ChannelNo,
                ScreenPosition = (int)channel.ScreenPosition,
                SearchDateTime = request.SearchDateTime,
                StartTime = playStartTime,
                EndTime = playEndTime,
                RenderTargetHandle = channel.OutputHandle,
                AutoPlay = true
            };
        }

        /// <summary>
        /// 세션 Dictionary Key를 생성한다.
        /// </summary>
        private static int BuildSessionKey(
            int nvrNo,
            int channelNo,
            int screenPosition)
        {
            return (nvrNo * 10000)
                + (channelNo * 10)
                + screenPosition;
        }

        /// <summary>
        /// 세션의 NVR번호에 해당하는 Provider를 반환한다.
        /// </summary>
        private INvrProvider GetProviderByNvrNo(
            int nvrNo)
        {
            INvrProvider provider;

            return _providers.TryGetValue(nvrNo, out provider)
                ? provider
                : null;
        }

        /// <summary>
        /// NVR 공통 처리 상태를 CamViewer 사용자 대응 유형으로 변환한다.
        /// </summary>
        private static PlaybackFailureCategory ClassifyNvrFailure(
            NvrResultStatus status)
        {
            switch (status)
            {
                /*
                 * 연결 실패와 일반적인 장비 처리 실패는
                 * 네트워크 또는 장비 상태가 복구된 뒤 다시 시도할 수 있다.
                 */
                case NvrResultStatus.ConnectionFailed:
                case NvrResultStatus.ApiError:
                case NvrResultStatus.Failed:
                case NvrResultStatus.PartialSuccess:
                case NvrResultStatus.UnknownError:
                    return PlaybackFailureCategory.Retryable;

                /*
                 * 로그인, 채널번호, Provider 선택 오류는
                 * 설정 확인이 선행돼야 한다.
                 */
                case NvrResultStatus.LoginFailed:
                case NvrResultStatus.InvalidChannel:
                case NvrResultStatus.ProviderNotFound:
                    return PlaybackFailureCategory.Configuration;

                case NvrResultStatus.NoRecordFound:
                    return PlaybackFailureCategory.NoRecord;

                case NvrResultStatus.NotSupported:
                    return PlaybackFailureCategory.NotSupported;

                case NvrResultStatus.Cancelled:
                    return PlaybackFailureCategory.Cancelled;

                /*
                 * SDK 초기화 또는 DLL 문제는
                 * 단순 재시도보다는 프로그램 구성을 확인해야 한다.
                 */
                case NvrResultStatus.SdkError:
                    return PlaybackFailureCategory.System;

                default:
                    return PlaybackFailureCategory.System;
            }
        }

        /// <summary>
        /// 현재 세션에 해당하는 재생 채널 설정을 찾는다.
        /// </summary>
        private PlayerChannelTarget FindChannelTarget(
            INvrPlaybackSession session)
        {
            if (session == null
                || _currentRequest == null
                || _currentRequest.Channels == null)
            {
                return null;
            }

            return _currentRequest.Channels
                .FirstOrDefault(
                    channel =>
                        channel != null
                        && channel.NvrNo == session.NvrNo
                        && channel.ChannelNo == session.ChannelNo
                        && (int)channel.ScreenPosition
                            == session.ScreenPosition);
        }

        /// <summary>
        /// NVR 실패 로그에 기록할 진단 정보를 생성한다.
        ///
        /// 비밀번호, 사용자 ID, 인증 토큰은 기록하지 않는다.
        /// </summary>
        private string BuildNvrLogDetails(
            NvrConfig nvrConfig,
            PlayerChannelTarget channel,
            INvrPlaybackSession session,
            INvrProvider provider,
            NvrResult nvrResult,
            string additionalDetails)
        {
            PlayerChannelTarget resolvedChannel = channel;

            if (resolvedChannel == null && session != null)
            {
                resolvedChannel = FindChannelTarget(session);
            }

            NvrConfig resolvedConfig = nvrConfig;

            if (resolvedConfig == null && resolvedChannel != null)
            {
                resolvedConfig = resolvedChannel.NvrConfig;
            }

            var details = new List<string>();

            int? nvrNo = resolvedChannel != null
                    ? (int?)resolvedChannel.NvrNo
                    : session != null
                        ? (int?)session.NvrNo
                        : resolvedConfig != null
                            ? (int?)resolvedConfig.NvrNo
                            : null;

            int? channelNo =
                resolvedChannel != null
                    ? (int?)resolvedChannel.ChannelNo
                    : session != null
                        ? (int?)session.ChannelNo
                        : null;

            int? screenPosition =
                resolvedChannel != null
                    ? (int?)resolvedChannel.ScreenPosition
                    : session != null
                        ? (int?)session.ScreenPosition
                        : null;

            details.Add(
                "NvrNo="
                + (
                    nvrNo.HasValue
                        ? nvrNo.Value.ToString()
                        : "-"
                ));

            details.Add(
                "ChannelNo="
                + (
                    channelNo.HasValue
                        ? channelNo.Value.ToString()
                        : "-"
                ));

            details.Add(
                "ScreenPosition="
                + (
                    screenPosition.HasValue
                        ? GetScreenPositionText(
                            screenPosition.Value)
                        : "-"
                ));

            string providerKey =
                resolvedConfig == null
                    ? null
                    : resolvedConfig.ProviderKey;

            /*
             * 설정의 ProviderKey가 없으면
             * 실제 생성된 Provider 메타데이터를 사용한다.
             */
            if (string.IsNullOrWhiteSpace(providerKey)
                && provider != null
                && provider.Metadata != null)
            {
                providerKey =
                    provider.Metadata.ProviderKey;
            }

            details.Add(
                "ProviderKey="
                + (
                    string.IsNullOrWhiteSpace(providerKey)
                        ? "-"
                        : providerKey
                ));

            if (provider != null
                && provider.Metadata != null)
            {
                details.Add(
                    "ProviderName="
                    + (
                        string.IsNullOrWhiteSpace(
                            provider.Metadata.DisplayName)
                            ? "-"
                            : provider.Metadata.DisplayName
                    ));

                details.Add(
                    "Vendor="
                    + (
                        string.IsNullOrWhiteSpace(
                            provider.Metadata.Vendor)
                            ? "-"
                            : provider.Metadata.Vendor
                    ));

                details.Add(
                    "ProviderVersion="
                    + (
                        string.IsNullOrWhiteSpace(
                            provider.Metadata.Version)
                            ? "-"
                            : provider.Metadata.Version
                    ));
            }

            if (resolvedConfig != null)
            {
                details.Add(
                    "Host="
                    + (
                        string.IsNullOrWhiteSpace(
                            resolvedConfig.Host)
                            ? "-"
                            : resolvedConfig.Host
                    ));

                details.Add(
                    "Port="
                    + resolvedConfig.Port);
            }

            if (nvrResult != null)
            {
                details.Add(
                    "NvrStatus="
                    + nvrResult.Status);

                if (nvrResult.Error != null)
                {
                    details.Add(
                        "ProviderErrorCode="
                        + (
                            string.IsNullOrWhiteSpace(
                                nvrResult.Error.ErrorCode)
                                ? "-"
                                : nvrResult.Error.ErrorCode
                        ));

                    details.Add(
                        "NativeErrorCode="
                        + (
                            string.IsNullOrWhiteSpace(
                                nvrResult.Error.NativeErrorCode)
                                ? "-"
                                : nvrResult.Error.NativeErrorCode
                        ));

                    details.Add(
                        "ProviderOperation="
                        + (
                            string.IsNullOrWhiteSpace(
                                nvrResult.Error.Operation)
                                ? "-"
                                : nvrResult.Error.Operation
                        ));
                }
            }

            if (!string.IsNullOrWhiteSpace(additionalDetails))
            {
                details.Add(additionalDetails);
            }

            return string.Join(
                ", ",
                details);
        }

        /// <summary>
        /// Provider 또는 NVR 명령 실패를 상세 정보와 함께 기록한다.
        /// </summary>
        private void WriteNvrFailureLog(
            string operationName,
            NvrConfig nvrConfig,
            PlayerChannelTarget channel,
            INvrPlaybackSession session,
            INvrProvider provider,
            NvrResult nvrResult,
            PlayerPlaybackResult playerResult,
            string additionalDetails = null)
        {
            PlayerPlaybackResult failureResult =
                playerResult
                ?? ToPlayerResult(
                    nvrResult);

            string details =
                BuildNvrLogDetails(
                    nvrConfig,
                    channel,
                    session,
                    provider,
                    nvrResult,
                    additionalDetails);

            PlaybackLogWriter.WriteResult(
                operationName,
                failureResult,
                details);
        }

        /// <summary>
        /// NvrResult를 PlayerPlaybackResult로 변환한다.
        ///
        /// Provider가 반환한 상세 오류 코드가 있으면 우선 사용하고,
        /// 없으면 공통 NvrResultStatus 값을 오류 코드로 사용한다.
        /// </summary>
        private static PlayerPlaybackResult ToPlayerResult(
            NvrResult result)
        {
            if (result == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 처리 결과가 없습니다.",
                    "NVR_RESULT_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (result.Success)
            {
                return PlayerPlaybackResult.Ok(
                    result.Message);
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.Message)
                    ? "NVR 처리에 실패했습니다."
                    : result.Message;

            string errorCode =
                result.Status.ToString();

            /*
             * Provider 또는 제조사 SDK에서 더 구체적인 오류 코드를
             * 반환한 경우 공통 상태보다 상세 코드를 우선 사용한다.
             */
            if (result.Error != null
                && !string.IsNullOrWhiteSpace(
                    result.Error.ErrorCode))
            {
                errorCode =
                    result.Error.ErrorCode;
            }

            return PlayerPlaybackResult.Fail(
                message,
                errorCode,
                ClassifyNvrFailure(
                    result.Status));
        }

        /// <summary>
        /// 재생속도를 변경한다.
        ///
        /// 운영 정책:
        /// - 재생 전이면 선택값만 저장한다.
        /// - 재생 중이면 모든 채널에 동일한 속도를 적용한다.
        /// - 1배속이 아닌 경우 자동 동기화와 Seek를 실행하지 않는다.
        /// - 1배속으로 변경한 경우에만 좌우 영상 동기화를 실행한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SetPlaybackSpeedAsync(PlaybackSpeed speed, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생속도 변경 요청이 취소되었습니다.",
                    "PLAYBACK_SPEED_CANCELLED");
            }

            /*
             * 아직 재생 세션이 없으면 선택값만 저장한다.
             * 이후 재생 시작 시 PlayAsync에서 적용한다.
             */
            if (_sessions.Count == 0)
            {
                _currentSpeed = speed;

                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(speed)
                    + "으로 설정되었습니다. 다음 재생부터 적용됩니다.");
            }

            PlaybackState stateBeforeChange = CurrentState;

            /*
             * 속도를 변경하기 전에 현재 재생 위치를 고정한다.
             */
            DateTime? syncedPlaybackTime = await SyncPlaybackTimeAsync(cancellationToken);

            DateTime currentPlaybackTimeBeforeChange =
                syncedPlaybackTime.HasValue
                    ? syncedPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            /*
             * 한 채널이라도 속도 변경에 실패하면
             * 좌우 속도가 서로 달라질 수 있으므로 전체 재생을 정리한다.
             */
            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions.ToList())
            {
                INvrPlaybackSession session =item.Value;

                if (session == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "재생 세션 정보가 없습니다.",
                            "PLAYBACK_SESSION_INVALID");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생속도 변경",
                        failureResult,
                        "PLAYBACK_SPEED_CHANGE_FAILED");
                }

                INvrProvider provider = GetProviderByNvrNo(session.NvrNo);

                if (provider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "NVR Provider를 찾을 수 없습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo
                            + ", ScreenPosition="
                            + session.ScreenPosition,
                            "NVR_PROVIDER_NOT_FOUND");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생속도 변경",
                        failureResult,
                        "PLAYBACK_SPEED_CHANGE_FAILED");
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null
                    || !capabilities.CanChangeSpeed)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "현재 NVR Provider는 재생속도 변경을 지원하지 않습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo,
                            "PLAYBACK_SPEED_NOT_SUPPORTED");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생속도 변경",
                        failureResult,
                        "PLAYBACK_SPEED_CHANGE_FAILED");
                }

                NvrResult result =
                    await provider.SetPlaybackSpeedAsync(session, ToNvrPlaybackSpeed(speed), cancellationToken);

                if (result == null
                    || !result.Success)
                {
                    PlayerPlaybackResult failureResult = ToPlayerResult(result);

                    return await RecoverFromPlaybackFailureAsync(
                        "재생속도 변경",
                        failureResult,
                        "PLAYBACK_SPEED_CHANGE_FAILED");
                }
            }

            /*
             * 모든 채널에 속도가 정상 적용된 뒤
             * 서비스 상태를 갱신한다.
             */
            _currentPlaybackTime = ClampPlaybackTime(currentPlaybackTimeBeforeChange);

            _currentSpeed =speed;

            if (stateBeforeChange == PlaybackState.Playing || stateBeforeChange == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc =DateTime.UtcNow;

                CurrentState = stateBeforeChange;
            }
            else if (stateBeforeChange == PlaybackState.Paused)
            {
                _playbackClockStartedAtUtc = null;

                CurrentState = PlaybackState.Paused;
            }
            else
            {
                _playbackClockStartedAtUtc = null;

                CurrentState = stateBeforeChange;
            }

            /*
             * 확정된 운영 정책:
             * 0.5·2·4·8배속에서는 자동 동기화와 Seek를 실행하지 않는다.
             */
            if (speed != PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(speed)
                    + "으로 재생속도를 변경했습니다. "
                    + "배속 재생 중에는 자동 영상 동기화를 실행하지 않습니다.");
            }

            /*
             * 1배속으로 변경했을 때만 좌우 시간을 동기화한다.
             */
            PlayerPlaybackResult syncResult = await ResyncPlaybackSessionsAsync(cancellationToken);

            if (syncResult == null || !syncResult.Success)
            {
                return await RecoverFromPlaybackFailureAsync(
                    "1배속 변경 후 좌우 영상 동기화",
                    syncResult,
                    "PLAYBACK_SPEED_SYNC_FAILED");
            }

            return PlayerPlaybackResult.Ok(
                "1배속으로 재생속도를 변경했습니다. "
                + syncResult.Message);
        }

        /// <summary>
        /// Provider가 실제 재생시간 조회를 지원하면 실제 시간을 동기화한다.
        /// 지원하지 않거나 실패하면 추정 시간을 반환한다.
        /// </summary>
        public async Task<DateTime?> SyncPlaybackTimeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_currentRequest == null)
            {
                return null;
            }

            if (CurrentState == PlaybackState.Stopped)
            {
                return null;
            }

            DateTime? providerPlaybackTime =
                await TryGetAnyProviderPlaybackTimeAsync(
                    cancellationToken);

            if (providerPlaybackTime.HasValue)
            {
                _currentPlaybackTime = providerPlaybackTime.Value;

                if (CurrentState == PlaybackState.Playing
                    || CurrentState == PlaybackState.Rewinding)
                {
                    _playbackClockStartedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    _playbackClockStartedAtUtc = null;
                }

                return _currentPlaybackTime;
            }

            return GetEstimatedPlaybackTime();
        }

        /// <summary>
        /// 타임라인에서 선택한 절대 시각으로 이동한다.
        ///
        /// 운영 정책:
        /// - 타임라인은 모든 배속에서 사용할 수 있다.
        /// - 1배속이 아닌 상태에서 이동하면 이동 결과는 1배속으로 통일한다.
        /// - 기존 세션에 1배속 변경 명령과 동기화를 먼저 실행하지 않는다.
        /// - Seek 과정에서 새로 생성되는 재생 핸들의 기본 속도를 사용한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekTimelineToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "타임라인 이동 요청이 취소되었습니다.",
                    "TIMELINE_SEEK_CANCELLED");
            }

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            /*
             * 유효하지 않은 위치를 클릭한 경우에는
             * 기존 재생속도를 변경하지 않는다.
             */
            if (targetTime < _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간보다 이전으로 이동할 수 없습니다.",
                    "SEEK_BEFORE_START");
            }

            if (targetTime >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 종료시간 이후로 이동할 수 없습니다.",
                    "SEEK_AFTER_END");
            }

            bool changedToNormalSpeed =
                _currentSpeed != PlaybackSpeed.Normal;

            /*
             * 기존 8배속 세션에 Normal 명령을 먼저 보내지 않는다.
             *
             * SeekToTimeAsync가 좌우 채널의 새 재생 핸들을 생성하므로,
             * 새 핸들이 생성될 때 사용할 논리 속도만 1배속으로 변경한다.
             */
            _currentSpeed =
                PlaybackSpeed.Normal;

            PlayerPlaybackResult seekResult =
                await SeekToTimeAsync(
                    targetTime,
                    cancellationToken);

            if (seekResult == null)
            {
                return PlayerPlaybackResult.Fail(
                    "타임라인 이동 처리 결과가 없습니다.",
                    "TIMELINE_SEEK_RESULT_EMPTY");
            }

            if (!seekResult.Success)
            {
                /*
                 * 부분 Seek 실패 시 기존 복구 정책에 따라
                 * 전체 세션이 정리될 수 있으므로 1배속 상태를 유지한다.
                 */
                return seekResult;
            }

            string message =
                changedToNormalSpeed
                    ? "재생속도를 1배속으로 전환했습니다. "
                      + seekResult.Message
                    : seekResult.Message;

            return PlayerPlaybackResult.Ok(
                message);
        }

        /// <summary>
        /// 지정한 영상재생시간으로 이동한다.
        /// 이동 후 좌/우 영상 싱크를 동기화한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekToTimeAsync(DateTime targetTime, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            if (targetTime < _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간보다 이전으로 이동할 수 없습니다.",
                    "SEEK_BEFORE_START");
            }

            if (targetTime >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 종료시간 이후로 이동할 수 없습니다.",
                    "SEEK_AFTER_END");
            }

            PlaybackState stateBeforeSeek =
                CurrentState;

            /*
             * 역재생 중 또는 역재생 일시정지 상태에서 위치를 이동할 때는
             * 일반 Provider.SeekAsync를 사용하면 안 된다.
             *
             * Dahua의 일반 SeekAsync는 정방향 PlayByTime 세션을 다시 만들기 때문에
             * 서비스 상태만 Rewinding이고 실제 영상은 정방향이 되는 문제가 발생한다.
             */
            bool isReverseSeek =
                stateBeforeSeek == PlaybackState.Rewinding
                || (
                    stateBeforeSeek == PlaybackState.Paused
                    && _pausedFromState == PlaybackState.Rewinding
                );

            if (isReverseSeek)
            {
                return await SeekReverseToTimeAsync(
                    targetTime,
                    stateBeforeSeek == PlaybackState.Paused,
                    cancellationToken);
            }

            /*
             * Dictionary 원본을 직접 순회하지 않고 복사본을 사용한다.
             *
             * Seek 실패 시 RecoverFromPlaybackFailureAsync 내부에서
             * _sessions가 초기화될 수 있으므로, 원본 Dictionary를 순회하면
             * 컬렉션 변경 오류가 발생할 가능성이 있다.
             */
            foreach (KeyValuePair<int, INvrPlaybackSession> item
                in _sessions.ToList())
            {
                INvrPlaybackSession session =
                    item.Value;

                /*
                 * 세션 정보가 손실된 상태에서는 정상적인 Seek를 보장할 수 없다.
                 * 일부 채널만 이동하지 않도록 전체 재생을 정리한다.
                 */
                if (session == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "재생 세션 정보가 없습니다.",
                            "PLAYBACK_SESSION_INVALID");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 위치 이동",
                        failureResult,
                        "PLAYBACK_SEEK_FAILED");
                }

                INvrProvider provider =
                    GetProviderByNvrNo(
                        session.NvrNo);

                /*
                 * 기존에는 Provider가 없으면 continue하여
                 * 나머지 채널만 이동할 수 있었다.
                 *
                 * 좌우 화면의 재생 위치가 달라지는 것을 막기 위해
                 * Provider 누락도 전체 재생 실패로 처리한다.
                 */
                if (provider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "NVR Provider를 찾을 수 없습니다. "
                            + "NvrNo="
                            + session.NvrNo
                            + ", ChannelNo="
                            + session.ChannelNo
                            + ", ScreenPosition="
                            + session.ScreenPosition,
                            "NVR_PROVIDER_NOT_FOUND");

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 위치 이동",
                        failureResult,
                        "PLAYBACK_SEEK_FAILED");
                }

                /*
                 * 채널마다 설정된 시간 보정값을 적용한다.
                 *
                 * 예:
                 * 공통 UI 목표시간 10:00:00
                 * 채널 보정값 +2초
                 * 실제 Provider Seek 시간 10:00:02
                 */
                int offsetSeconds =
                    GetSessionTimeOffset(
                        item.Key);

                DateTime providerTargetTime =
                    targetTime.AddSeconds(
                        offsetSeconds);

                NvrResult result =
                    await provider.SeekAsync(
                        session,
                        providerTargetTime,
                        cancellationToken);

                /*
                 * 한 채널이라도 Seek에 실패하면 다른 채널은 이미 이동했을 수 있다.
                 * 부분 이동 상태를 유지하지 않고 전체 세션을 정리한다.
                 */
                if (result == null || !result.Success)
                {
                    PlayerPlaybackResult failureResult =
                        ToPlayerResult(result);

                    return await RecoverFromPlaybackFailureAsync(
                        "재생 위치 이동",
                        failureResult,
                        "PLAYBACK_SEEK_FAILED");
                }
            }

            _currentPlaybackTime = targetTime;

            if (stateBeforeSeek == PlaybackState.Playing
                || stateBeforeSeek == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc = DateTime.UtcNow;
                CurrentState = stateBeforeSeek;
            }
            else if (stateBeforeSeek == PlaybackState.Paused)
            {
                _playbackClockStartedAtUtc = null;
                CurrentState = PlaybackState.Paused;
            }
            else
            {
                _playbackClockStartedAtUtc = null;
                CurrentState = stateBeforeSeek;
            }

            PlayerPlaybackResult syncResult = await ResyncPlaybackSessionsAsync( cancellationToken);

            if (!syncResult.Success)
            {
                /*
                 * 모든 채널의 개별 Seek는 성공했지만
                 * 최종 좌우 시간 동기화에 실패한 상태다.
                 *
                 * 실제 채널 위치가 서로 다를 수 있으므로
                 * 세션을 유지하지 않고 전체 재생 상태를 초기화한다.
                 */
                return await RecoverFromPlaybackFailureAsync(
                    "이동 후 좌우 영상 동기화",
                    syncResult,
                    "PLAYBACK_SEEK_SYNC_FAILED");
            }

            return PlayerPlaybackResult.Ok(
                "재생 위치를 이동했습니다. "
                + targetTime.ToString("yyyy-MM-dd HH:mm:ss")
                + " / "
                + syncResult.Message);
        }

        /// <summary>
        /// 역재생 상태에서 지정한 시각부터 역재생 세션을 다시 생성한다.
        ///
        /// 일반 SeekAsync는 정방향 세션을 생성할 수 있으므로 사용하지 않는다.
        /// 기존 역재생 세션을 정리한 뒤 모든 채널을 역재생 API로 다시 연다.
        /// </summary>
        /// <param name="targetTime">
        /// 사용자가 타임라인에서 선택한 화면 기준 재생시각.
        /// </param>
        /// <param name="keepPaused">
        /// 역재생 일시정지 상태에서 이동한 경우 true.
        /// 새 세션 생성 후 다시 일시정지한다.
        /// </param>
        private async Task<PlayerPlaybackResult> SeekReverseToTimeAsync(
            DateTime targetTime,
            bool keepPaused,
            CancellationToken cancellationToken)
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            PlayerPlaybackRequest request =
                _currentRequest;

            /*
             * 기존 역재생 세션만 정리한다.
             * Provider 로그인은 재사용한다.
             */
            PlayerPlaybackResult stopResult =
                await StopCurrentPlaybackSessionsOnlyAsync(
                    CancellationToken.None);

            if (!stopResult.Success)
            {
                return await RecoverFromPlaybackFailureAsync(
                    "역재생 위치 이동을 위한 기존 세션 정리",
                    stopResult,
                    "REVERSE_SEEK_CLEANUP_FAILED");
            }

            foreach (PlayerChannelTarget channel
                in request.Channels)
            {
                if (channel == null || channel.NvrConfig == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "역재생 채널의 NVR 설정이 없습니다.",
                            "NVR_CONFIG_REQUIRED");

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동",
                        failureResult,
                        "REVERSE_SEEK_FAILED");
                }

                NvrResult<INvrProvider> providerResult = await GetOrCreateLoggedInProviderAsync(channel.NvrConfig, cancellationToken);

                if (providerResult == null || !providerResult.Success || providerResult.Data == null)
                {
                    PlayerPlaybackResult failureResult = ToPlayerResult(providerResult);

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동",
                        failureResult,
                        "REVERSE_SEEK_PROVIDER_FAILED");
                }

                INvrProvider provider = providerResult.Data;

                if (provider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "역재생할 NVR Provider를 찾을 수 없습니다. "
                            + "NvrNo="
                            + channel.NvrNo
                            + ", ChannelNo="
                            + channel.ChannelNo,
                            "NVR_PROVIDER_NOT_FOUND");

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동",
                        failureResult,
                        "REVERSE_SEEK_FAILED");
                }

                INvrReversePlaybackProvider reverseProvider =
                    provider as INvrReversePlaybackProvider;

                if (reverseProvider == null)
                {
                    PlayerPlaybackResult failureResult =
                        PlayerPlaybackResult.Fail(
                            "현재 Provider는 역재생 위치 이동을 지원하지 않습니다. "
                            + "NvrNo="
                            + channel.NvrNo
                            + ", ChannelNo="
                            + channel.ChannelNo,
                            "REVERSE_PROVIDER_NOT_IMPLEMENTED");

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동",
                        failureResult,
                        "REVERSE_SEEK_FAILED");
                }

                NvrPlaybackRequest nvrRequest =
                    ToNvrPlaybackRequest(
                        request,
                        channel,
                        request.PlayStartTime,
                        request.PlayEndTime);

                int offsetSeconds =
                    channel.TimeOffsetSeconds;

                DateTime providerTargetTime =
                    targetTime.AddSeconds(
                        offsetSeconds);

                /*
                 * 일반 PlayByTimeAsync가 아니라
                 * 반드시 역재생 API로 새 세션을 생성한다.
                 */
                NvrResult<INvrPlaybackSession> reverseResult =
                    await reverseProvider.PlayReverseByTimeAsync(
                        nvrRequest,
                        providerTargetTime,
                        cancellationToken);

                if (reverseResult == null || !reverseResult.Success || reverseResult.Data == null)
                {
                    PlayerPlaybackResult failureResult = ToPlayerResult(reverseResult);

                    WriteNvrFailureLog(
                        "역재생 위치 이동",
                        channel.NvrConfig,
                        channel,
                        null,
                        provider,
                        reverseResult,
                        failureResult,
                        "TargetTime="
                        + providerTargetTime.ToString(
                            "yyyy-MM-dd HH:mm:ss"));

                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동",
                        failureResult,
                        "REVERSE_SEEK_FAILED");
                }

                int sessionKey =
                    BuildSessionKey(
                        channel.NvrNo,
                        channel.ChannelNo,
                        (int)channel.ScreenPosition);

                _sessions[sessionKey] =
                    reverseResult.Data;

                _sessionTimeOffsets[sessionKey] =
                    offsetSeconds;
            }

            /*
             * 새로 생성된 역재생 세션에도 현재 선택 배속을 다시 적용한다.
             */
            if (_currentSpeed != PlaybackSpeed.Normal)
            {
                PlayerPlaybackResult speedResult =
                    await ApplyCurrentSpeedToSessionsAsync(
                        cancellationToken);

                if (!speedResult.Success)
                {
                    return await RecoverFromPlaybackFailureAsync(
                        "역재생 위치 이동 후 속도 적용",
                        speedResult,
                        "REVERSE_SEEK_SPEED_FAILED");
                }
            }

            _currentPlaybackTime =
                targetTime;

            if (keepPaused)
            {
                /*
                 * 역재생 일시정지 상태에서 타임라인을 이동했다면
                 * 새로 생성된 세션도 다시 일시정지한다.
                 */
                foreach (KeyValuePair<int, INvrPlaybackSession> item
                    in _sessions.ToList())
                {
                    INvrPlaybackSession session =
                        item.Value;

                    INvrProvider provider =
                        session == null
                            ? null
                            : GetProviderByNvrNo(
                                session.NvrNo);

                    if (provider == null)
                    {
                        PlayerPlaybackResult failureResult =
                            PlayerPlaybackResult.Fail(
                                "역재생 일시정지 상태를 복원할 Provider가 없습니다.",
                                "NVR_PROVIDER_NOT_FOUND");

                        return await RecoverFromPlaybackFailureAsync(
                            "역재생 위치 이동 후 일시정지 복원",
                            failureResult,
                            "REVERSE_SEEK_PAUSE_FAILED");
                    }

                    NvrResult pauseResult =
                        await provider.PauseAsync(
                            session,
                            cancellationToken);

                    if (pauseResult == null ||
                        !pauseResult.Success)
                    {
                        PlayerPlaybackResult failureResult =
                            ToPlayerResult(
                                pauseResult);

                        return await RecoverFromPlaybackFailureAsync(
                            "역재생 위치 이동 후 일시정지 복원",
                            failureResult,
                            "REVERSE_SEEK_PAUSE_FAILED");
                    }
                }

                _playbackClockStartedAtUtc =
                    null;

                _pausedFromState =
                    PlaybackState.Rewinding;

                CurrentState =
                    PlaybackState.Paused;
            }
            else
            {
                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;

                CurrentState =
                    PlaybackState.Rewinding;
            }

            return PlayerPlaybackResult.Ok(
                "역재생 위치를 이동했습니다. "
                + targetTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// PlayerView의 재생속도 값을 NVR Core 재생속도 값으로 변환한다.
        /// </summary>
        private static NvrPlaybackSpeed ToNvrPlaybackSpeed(
            PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return NvrPlaybackSpeed.Half;

                case PlaybackSpeed.Double:
                    return NvrPlaybackSpeed.Double;

                case PlaybackSpeed.Quad:
                    return NvrPlaybackSpeed.Quad;

                case PlaybackSpeed.Octuple:
                    return NvrPlaybackSpeed.Octuple;

                default:
                    return NvrPlaybackSpeed.Normal;
            }
        }

        private static string GetPlaybackSpeedText(PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return "0.5배속";

                case PlaybackSpeed.Double:
                    return "2배속";

                case PlaybackSpeed.Quad:
                    return "4배속";

                case PlaybackSpeed.Octuple:
                    return "8배속";

                default:
                    return "1배속";
            }
        }


        /// <summary>
        /// 세션별 시간 보정 초를 반환한다.
        /// </summary>
        private int GetSessionTimeOffset(int sessionKey)
        {
            int offsetSeconds;

            return _sessionTimeOffsets.TryGetValue(
                sessionKey,
                out offsetSeconds)
                ? offsetSeconds
                : 0;
        }

        /// <summary>
        /// Provider가 지원하는 경우 특정 세션의 실제 재생시간을 조회한다.
        /// 지원하지 않거나 실패하면 null을 반환한다.
        /// </summary>
        private async Task<DateTime?> TryGetProviderPlaybackTimeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return null;
            }

            INvrProvider provider =
                GetProviderByNvrNo(session.NvrNo);

            if (provider == null)
            {
                return null;
            }

            ProviderCapabilities capabilities =
                provider.GetCapabilities();

            if (capabilities == null || !capabilities.CanGetPlaybackPosition)
            {
                return null;
            }

            INvrPlaybackPositionProvider positionProvider =
                provider as INvrPlaybackPositionProvider;

            if (positionProvider == null)
            {
                return null;
            }

            NvrResult<DateTime> result =
                await positionProvider.GetPlaybackTimeAsync(
                    session,
                    cancellationToken);

            if (result == null || !result.Success)
            {
                return null;
            }

            return result.Data;
        }

        /// <summary>
        /// 화면 위치 값을 표시 문자열로 변환한다.
        /// </summary>
        private static string GetScreenPositionText(int screenPosition)
        {
            if (screenPosition == 1)
            {
                return "좌측";
            }

            if (screenPosition == 2)
            {
                return "우측";
            }

            return screenPosition.ToString();
        }

        /// <summary>
        /// 현재 재생 중인 세션에서 조회한 유효한 Provider 시간을 반환한다.
        ///
        /// 현재 조회 범위를 벗어난 오래된 OSD 시간은 사용하지 않는다.
        /// </summary>
        private async Task<DateTime?> TryGetAnyProviderPlaybackTimeAsync(
            CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                int sessionKey = item.Key;

                INvrPlaybackSession session = item.Value;

                if (session == null)
                {
                    continue;
                }

                DateTime? providerTime =
                    await TryGetProviderPlaybackTimeAsync(session, cancellationToken);

                if (!providerTime.HasValue)
                {
                    continue;
                }

                int offsetSeconds = GetSessionTimeOffset(sessionKey);

                DateTime normalizedTime;

                /*
                 * SDK 호출이 성공했더라도 현재 조회 구간을 벗어난 값은
                 * 실제 재생시간으로 사용하지 않는다.
                 */
                if (!TryNormalizeProviderPlaybackTime(providerTime.Value, offsetSeconds, out normalizedTime))
                {
                    continue;
                }

                return normalizedTime;
            }

            return null;
        }

        /// <summary>
        /// 하나의 재생 세션에서 조회한 실제 재생시간 정보이다.
        /// </summary>
        private sealed class PlaybackTimeSnapshot
        {
            /// <summary>
            /// 세션 Dictionary Key.
            /// </summary>
            public int SessionKey { get; set; }

            /// <summary>
            /// 재생 세션.
            /// </summary>
            public INvrPlaybackSession Session { get; set; }

            /// <summary>
            /// 세션을 처리하는 Provider.
            /// </summary>
            public INvrProvider Provider { get; set; }

            /// <summary>
            /// 채널별 시간 보정 초.
            /// </summary>
            public int OffsetSeconds { get; set; }

            /// <summary>
            /// Provider에서 조회한 실제 NVR 재생시간.
            /// </summary>
            public DateTime ProviderTime { get; set; }

            /// <summary>
            /// 화면 기준으로 보정된 재생시간.
            /// ProviderTime - OffsetSeconds.
            /// </summary>
            public DateTime NormalizedTime { get; set; }
        }

        /// <summary>
        /// 현재 생성된 모든 재생 세션에서 정상적인 Provider 시간이
        /// 확인될 때까지 기다린다.
        ///
        /// 고정 3초 대기가 아니라 최대 3초 대기이며,
        /// 모든 채널이 준비되면 즉시 종료한다.
        /// </summary>
        private async Task<bool> WaitForPlaybackReadyAsync(
            CancellationToken cancellationToken)
        {
            const int maximumWaitMilliseconds =
                3000;

            const int checkIntervalMilliseconds =
                200;

            if (_sessions.Count == 0
                || _currentRequest == null)
            {
                return false;
            }

            int expectedSessionCount =
                _sessions.Count;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds
                < maximumWaitMilliseconds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                /*
                 * GetPlaybackTimeSnapshotsAsync에서
                 * 조회 구간을 벗어난 비정상 시간은 이미 제외된다.
                 *
                 * 따라서 반환된 snapshot 개수만 확인하면 된다.
                 */
                List<PlaybackTimeSnapshot> snapshots =
                    await GetPlaybackTimeSnapshotsAsync(
                        cancellationToken);

                /*
                 * 현재 생성된 모든 세션의 정상 시간이 확인되면
                 * 최대 3초를 기다리지 않고 즉시 종료한다.
                 */
                if (snapshots != null
                    && snapshots.Count
                        >= expectedSessionCount)
                {
                    return true;
                }

                int remainingMilliseconds =
                    maximumWaitMilliseconds
                    - Convert.ToInt32(
                        stopwatch.ElapsedMilliseconds);

                if (remainingMilliseconds <= 0)
                {
                    break;
                }

                int delayMilliseconds =
                    Math.Min(
                        checkIntervalMilliseconds,
                        remainingMilliseconds);

                try
                {
                    await Task.Delay(
                        delayMilliseconds,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 현재 재생 중인 모든 세션에서 Provider 실제 재생시간을 조회한다.
        /// 
        /// Provider가 실제 재생시간 조회를 지원하지 않는 세션은 제외한다.
        /// </summary>
        private async Task<List<PlaybackTimeSnapshot>> GetPlaybackTimeSnapshotsAsync(
            CancellationToken cancellationToken)
        {
            var snapshots = new List<PlaybackTimeSnapshot>();

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                int sessionKey = item.Key;
                INvrPlaybackSession session = item.Value;
                if (session == null)
                {
                    continue;
                }
                INvrProvider provider =
                    GetProviderByNvrNo(session.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null || !capabilities.CanGetPlaybackPosition)
                {
                    continue;
                }

                INvrPlaybackPositionProvider positionProvider =
                    provider as INvrPlaybackPositionProvider;

                if (positionProvider == null)
                {
                    continue;
                }

                NvrResult<DateTime> timeResult =
                    await positionProvider.GetPlaybackTimeAsync(
                        session,
                        cancellationToken);

                if (timeResult == null || !timeResult.Success)
                {
                    continue;
                }

                int offsetSeconds =
                    GetSessionTimeOffset(
                        sessionKey);

                DateTime normalizedTime;

                /*
                 * SDK 호출 자체는 성공했더라도
                 * 현재 조회 구간에서 크게 벗어난 값은 동기화 계산에 사용하지 않는다.
                 */
                if (!TryNormalizeProviderPlaybackTime(
                    timeResult.Data,
                    offsetSeconds,
                    out normalizedTime))
                {
                    continue;
                }

                snapshots.Add(
                    new PlaybackTimeSnapshot
                    {
                        SessionKey = sessionKey,
                        Session = session,
                        Provider = provider,
                        OffsetSeconds = offsetSeconds,
                        ProviderTime = timeResult.Data,
                        NormalizedTime = normalizedTime
                    });
            }

            return snapshots;
        }

        /// <summary>
        /// 재생 명령 실패 후 현재 재생 세션과 Provider를 모두 정리하고
        /// 사용자에게 반환할 실패 결과를 생성한다.
        ///
        /// 처리 순서:
        /// 1. 원래 실패 메시지와 오류 코드를 보관한다.
        /// 2. 취소 여부와 관계없이 StopAsync를 실행한다.
        /// 3. StopAsync 자체가 예외를 발생시키면 논리적인 재생 상태를 강제로 초기화한다.
        /// 4. 원래 오류 정보와 정리 오류 정보를 함께 반환한다.
        /// </summary>
        /// <param name="operationName">
        /// 실패한 작업 이름.
        /// 예: 좌우 영상 동기화, 일시정지, 재생 재개
        /// </param>
        /// <param name="failureResult">
        /// Provider 또는 재생 서비스에서 반환된 원래 실패 결과.
        /// </param>
        /// <param name="fallbackErrorCode">
        /// 원래 실패 결과에 오류 코드가 없을 때 사용할 기본 오류 코드.
        /// </param>
        /// <returns>
        /// 재생 정리 결과가 반영된 실패 결과.
        /// </returns>
        private async Task<PlayerPlaybackResult> RecoverFromPlaybackFailureAsync(
            string operationName,
            PlayerPlaybackResult failureResult,
            string fallbackErrorCode)
        {
            /*
             * 원래 오류 메시지를 먼저 보관한다.
             * StopAsync를 실행하면 현재 요청과 재생 상태가 초기화되므로
             * 오류 정보는 정리 작업 전에 확보해야 한다.
             */
            string failureMessage =
                failureResult == null
                    ? "상세 오류 정보를 확인할 수 없습니다."
                    : failureResult.Message;

            if (string.IsNullOrWhiteSpace(failureMessage))
            {
                failureMessage =
                    "상세 오류 정보를 확인할 수 없습니다.";
            }

            string errorCode =
                failureResult == null
                    ? fallbackErrorCode
                    : failureResult.ErrorCode;

            if (string.IsNullOrWhiteSpace(errorCode))
            {
                errorCode =
                    string.IsNullOrWhiteSpace(fallbackErrorCode)
                        ? "PLAYBACK_OPERATION_FAILED"
                        : fallbackErrorCode;
            }

            string cleanupWarning =
                string.Empty;

            try
            {
                /*
                 * 재생 명령에 사용된 CancellationToken이 이미 취소된 경우에도
                 * 세션과 Provider 정리는 반드시 수행해야 한다.
                 *
                 * 따라서 복구 작업에는 CancellationToken.None을 사용한다.
                 */
                PlayerPlaybackResult stopResult =
                    await StopAsync(
                        CancellationToken.None);

                /*
                 * 현재 StopAsync는 일부 정리 경고가 있어도 Success=true를
                 * 반환할 수 있다.
                 *
                 * 향후 정리 결과 정책을 세분화할 수 있도록
                 * 실패 결과가 반환되는 경우도 방어적으로 처리한다.
                 */
                if (stopResult != null &&
                    !stopResult.Success)
                {
                    cleanupWarning =
                        " 정리 과정에서도 오류가 발생했습니다. "
                        + stopResult.Message;
                }
            }
            catch (Exception ex)
            {
                /*
                 * StopAsync에서 예상하지 못한 예외가 발생한 경우
                 * Dispose가 완전히 끝났다고 보장할 수는 없다.
                 *
                 * 그러나 손상되었을 수 있는 세션과 Provider를
                 * 다음 재생에서 다시 사용하면 안 되므로
                 * 서비스의 논리적인 상태는 강제로 초기화한다.
                 */
                _sessions.Clear();
                _providers.Clear();
                _sessionTimeOffsets.Clear();

                _currentRequest = null;
                _currentPlaybackTime = null;
                _playbackClockStartedAtUtc = null;

                CurrentState =
                    PlaybackState.Stopped;

                cleanupWarning =
                    " 재생 리소스 정리 중 예외가 발생했습니다. "
                    + ex.Message;
            }

            /*
             * 실패 복구 이후 일시정지 이전 방향 정보도 기본값으로 되돌린다.
             * 재생속도 선택값은 사용자 선택값이므로 초기화하지 않는다.
             */
            _pausedFromState =
                PlaybackState.Playing;

            return PlayerPlaybackResult.Fail(
                operationName
                + "에 실패하여 재생을 중지하고 상태를 초기화했습니다. "
                + failureMessage
                + cleanupWarning,
                errorCode);
        }

        /// <summary>
        /// Provider에서 조회한 재생시간이 현재 조회 구간 안의
        /// 정상적인 시간인지 확인하고 화면 기준시간으로 변환한다.
        ///
        /// Dahua 재생 시작 직후에는 1~2초 정도 경계 오차가 발생할 수 있으므로
        /// 조회 구간 앞뒤로 2초의 허용 범위를 둔다.
        /// </summary>
        private bool TryNormalizeProviderPlaybackTime(
            DateTime providerTime,
            int offsetSeconds,
            out DateTime normalizedTime)
        {
            normalizedTime =
                DateTime.MinValue;

            if (_currentRequest == null)
            {
                return false;
            }

            if (providerTime == DateTime.MinValue
                || providerTime == DateTime.MaxValue)
            {
                return false;
            }

            DateTime candidateTime =
                providerTime.AddSeconds(
                    -offsetSeconds);

            DateTime minimumTime =
                _currentRequest.PlayStartTime.AddSeconds(
                    -2);

            DateTime maximumTime =
                _currentRequest.PlayEndTime.AddSeconds(
                    2);

            /*
             * 현재 조회 구간에서 크게 벗어난 시간은
             * SDK가 반환한 오래된 값 또는 아직 안정화되지 않은 값으로 본다.
             */
            if (candidateTime < minimumTime
                || candidateTime > maximumTime)
            {
                return false;
            }

            normalizedTime =
                ClampPlaybackTime(
                    candidateTime);

            return true;
        }

        /// <summary>
        /// 현재 재생 방향이 역방향인지 확인한다.
        ///
        /// 역재생 일시정지 상태에서도 방향은 역방향으로 간주한다.
        /// </summary>
        private bool IsReversePlaybackDirection()
        {
            return CurrentState == PlaybackState.Rewinding
                || (
                    CurrentState == PlaybackState.Paused
                    && _pausedFromState == PlaybackState.Rewinding
                );
        }
    }
}