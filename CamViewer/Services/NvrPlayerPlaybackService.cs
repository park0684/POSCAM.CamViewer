using CamViewer.Models;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
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
                PlayerPlaybackResult stopResult =
                    await StopAsync(CancellationToken.None);

                if (!stopResult.Success)
                {
                    return PlayerPlaybackResult.Fail(
                        "기존 재생 정리 중 오류가 발생했습니다. "
                        + stopResult.Message,
                        stopResult.ErrorCode);
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
                            cancellationToken);

                    if (!playChannelResult.Success)
                    {
                        await StopAsync(CancellationToken.None);

                        return playChannelResult;
                    }
                }

                // 모든 채널 재생 성공 후 현재 재생 기준시간을 설정한다.
                _currentPlaybackTime = request.PlayStartTime;
                _playbackClockStartedAtUtc = DateTime.UtcNow;
                CurrentState = PlaybackState.Playing;

                string speedWarningMessage = string.Empty;

                // 재생 전 선택된 속도가 1배속이 아니면 재생 시작 후 즉시 적용한다.
                if (_currentSpeed != PlaybackSpeed.Normal)
                {
                    PlaybackSpeed requestedSpeed = _currentSpeed;

                    PlayerPlaybackResult speedResult =
                        await SetPlaybackSpeedAsync(
                            requestedSpeed,
                            cancellationToken);

                    if (!speedResult.Success)
                    {
                        // 속도 적용 실패가 재생 자체를 중단시키지는 않는다.
                        // 대신 1배속으로 되돌리고 사용자에게 안내할 메시지를 남긴다.
                        _currentSpeed = PlaybackSpeed.Normal;
                        _currentPlaybackTime = GetEstimatedPlaybackTime();
                        _playbackClockStartedAtUtc = DateTime.UtcNow;

                        speedWarningMessage =
                            Environment.NewLine
                            + "단, 재생속도 적용에는 실패하여 1배속으로 재생합니다. "
                            + speedResult.Message;
                    }
                }
                PlayerPlaybackResult syncResult =
                    await ResyncPlaybackSessionsAsync(
                        cancellationToken);

                if (!syncResult.Success)
                {
                    return syncResult;
                }
                return PlayerPlaybackResult.Ok(
                    "NVR 재생을 시작했습니다. "
                    + request.PlayStartTime.ToString("yyyy-MM-dd HH:mm:ss")
                    + " ~ "
                    + request.PlayEndTime.ToString("yyyy-MM-dd HH:mm:ss")
                    + speedWarningMessage);
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
        /// 현재 영상재생시간을 추정한다.
        /// 
        /// NVR SDK에서 현재 재생 위치를 직접 제공하지 않는 단계에서는
        /// 마지막 기준 재생시간 + 실제 경과시간 x 재생속도로 계산한다.
        /// </summary>
        private DateTime GetEstimatedPlaybackTime()
        {
            if (!_currentPlaybackTime.HasValue)
            {
                return _currentRequest == null
                    ? DateTime.Now
                    : _currentRequest.PlayStartTime;
            }

            if (CurrentState != PlaybackState.Playing)
            {
                return _currentPlaybackTime.Value;
            }

            if (!_playbackClockStartedAtUtc.HasValue)
            {
                return _currentPlaybackTime.Value;
            }

            TimeSpan elapsed =
                DateTime.UtcNow - _playbackClockStartedAtUtc.Value;

            double speedMultiplier =
                GetPlaybackSpeedMultiplier(_currentSpeed);

            double playbackElapsedSeconds =
                elapsed.TotalSeconds * speedMultiplier;

            return _currentPlaybackTime.Value.AddSeconds(
                playbackElapsedSeconds);
        }

        /// <summary>
        /// 현재 재생 중인 영상 시각.
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
        /// 재생을 일시정지한다.
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

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider = GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                NvrResult result =
                    await provider.PauseAsync(
                        item.Value,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _currentPlaybackTime = GetEstimatedPlaybackTime();
            _playbackClockStartedAtUtc = null;
            CurrentState = PlaybackState.Paused;

            return PlayerPlaybackResult.Ok("일시정지했습니다.");
        }

        /// <summary>
        /// 일시정지 상태에서 재생을 재개한다.
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

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider = GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                NvrResult result =
                    await provider.ResumeAsync(
                        item.Value,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _playbackClockStartedAtUtc = DateTime.UtcNow;
            CurrentState = PlaybackState.Playing;

            return PlayerPlaybackResult.Ok("재생을 재개했습니다.");
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

            DateTime currentTime =
                GetEstimatedPlaybackTime();

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
        /// 역재생을 요청한다.
        /// 
        /// </summary>
        public Task<PlayerPlaybackResult> RewindAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return Task.FromResult(
                    PlayerPlaybackResult.Fail(
                        "역재생할 재생 세션이 없습니다.",
                        "PLAYBACK_NOT_STARTED"));
            }

            return Task.FromResult(
                PlayerPlaybackResult.Fail(
                    "역재생 기능은 아직 지원되지 않습니다.",
                    "REWIND_NOT_SUPPORTED"));
        }

        /// <summary>
        /// 재생을 중지하고 모든 세션/Provider 리소스를 정리한다.
        /// 일부 정리 실패가 있어도 나머지 리소스 정리는 계속 수행한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> StopAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            string warningMessage = string.Empty;
            _sessionTimeOffsets.Clear();

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions.ToList())
            {
                try
                {
                    INvrProvider provider =
                        GetProviderByNvrNo(item.Value.NvrNo);

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
                    warningMessage = ex.Message;
                }
            }

            _sessions.Clear();

            foreach (KeyValuePair<int, INvrProvider> item in _providers.ToList())
            {
                try
                {
                    await item.Value.LogoutAsync(cancellationToken);
                }
                catch
                {
                    // 로그아웃 실패 시에도 Dispose는 계속 진행한다.
                }

                try
                {
                    item.Value.Dispose();
                }
                catch
                {
                    // 종료 과정에서 Dispose 실패는 무시한다.
                }
            }

            _providers.Clear();

            _currentRequest = null;
            _currentPlaybackTime = null;
            _playbackClockStartedAtUtc = null;
            CurrentState = PlaybackState.Stopped;

            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                return PlayerPlaybackResult.Ok(
                    "재생은 중지되었지만 일부 정리 중 경고가 발생했습니다. "
                    + warningMessage);
            }

            return PlayerPlaybackResult.Ok("재생을 중지했습니다.");
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

                int offsetSeconds =
                    GetSessionTimeOffset(sessionKey);

                DateTime? providerTime =
                    await TryGetProviderPlaybackTimeAsync(
                        session,
                        cancellationToken);

                bool isProviderTime =
                    providerTime.HasValue;

                DateTime? displayBaseTime = null;

                if (providerTime.HasValue)
                {
                    // Provider 시간은 해당 채널의 실제 NVR 시간이다.
                    // UI 비교용으로는 채널별 offset을 제거해서 공통 기준 시간으로 맞춘다.
                    displayBaseTime =
                        providerTime.Value.AddSeconds(-offsetSeconds);
                }
                else if (_currentRequest != null)
                {
                    displayBaseTime =
                        GetEstimatedPlaybackTime();
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
        /// 좌측 화면을 기준으로 다른 재생 세션의 시간을 보정한다.
        /// 
        /// 이 메서드는 재생 시작, Seek, 재생속도 변경 직후처럼
        /// 싱크가 벌어질 수 있는 명령 이후에만 호출한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> ResyncPlaybackSessionsAsync(
            CancellationToken cancellationToken)
        {
            const double allowedDifferenceSeconds = 1.0;
            const int leftScreenPosition = 1;

            List<PlaybackTimeSnapshot> snapshots =
                await GetPlaybackTimeSnapshotsAsync(cancellationToken);

            if (snapshots.Count < 2)
            {
                return PlayerPlaybackResult.Ok(
                    "동기화 비교 대상이 부족합니다.");
            }

            PlaybackTimeSnapshot master =
                snapshots.FirstOrDefault(x =>
                    x.Session.ScreenPosition == leftScreenPosition);

            if (master == null)
            {
                master = snapshots[0];
            }

            double maxDifferenceSeconds =
                snapshots
                    .Max(x => Math.Abs(
                        (x.NormalizedTime - master.NormalizedTime)
                        .TotalSeconds));

            if (maxDifferenceSeconds <= allowedDifferenceSeconds)
            {
                _currentPlaybackTime = master.NormalizedTime;

                if (CurrentState == PlaybackState.Playing
                    || CurrentState == PlaybackState.Rewinding)
                {
                    _playbackClockStartedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    _playbackClockStartedAtUtc = null;
                }

                return PlayerPlaybackResult.Ok(
                    "좌/우 영상 동기화 상태가 정상입니다.");
            }

            foreach (PlaybackTimeSnapshot snapshot in snapshots)
            {
                if (snapshot.SessionKey == master.SessionKey)
                {
                    continue;
                }

                double differenceSeconds =
                    Math.Abs(
                        (snapshot.NormalizedTime - master.NormalizedTime)
                        .TotalSeconds);

                if (differenceSeconds <= allowedDifferenceSeconds)
                {
                    continue;
                }

                DateTime providerTargetTime =
                    master.NormalizedTime.AddSeconds(
                        snapshot.OffsetSeconds);

                NvrResult seekResult =
                    await snapshot.Provider.SeekAsync(
                        snapshot.Session,
                        providerTargetTime,
                        cancellationToken);

                if (!seekResult.Success)
                {
                    return ToPlayerResult(seekResult);
                }

                // Seek 후 재생속도가 1배속으로 돌아갈 수 있으므로 현재 선택 속도를 다시 적용한다.
                if (_currentSpeed != PlaybackSpeed.Normal)
                {
                    NvrResult speedResult =
                        await snapshot.Provider.SetPlaybackSpeedAsync(
                            snapshot.Session,
                            ToNvrPlaybackSpeed(_currentSpeed),
                            cancellationToken);

                    if (!speedResult.Success)
                    {
                        return ToPlayerResult(speedResult);
                    }
                }

                // 일시정지 상태였다면 Seek 이후 다시 Pause를 걸어 상태를 보존한다.
                if (CurrentState == PlaybackState.Paused)
                {
                    NvrResult pauseResult =
                        await snapshot.Provider.PauseAsync(
                            snapshot.Session,
                            cancellationToken);

                    if (!pauseResult.Success)
                    {
                        return ToPlayerResult(pauseResult);
                    }
                }
            }

            _currentPlaybackTime = master.NormalizedTime;

            if (CurrentState == PlaybackState.Playing
                || CurrentState == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _playbackClockStartedAtUtc = null;
            }

            return PlayerPlaybackResult.Ok(
                "좌/우 영상 싱크를 보정했습니다. 최대 차이 "
                + maxDifferenceSeconds.ToString("0.0")
                + "초");
        }

        /// <summary>
        /// 재생 대상 채널의 영상 원본 정보를 조회한다.
        /// Provider가 지원하지 않으면 실패 결과를 반환한다.
        /// </summary>
        public async Task<PlayerVideoSourceInfoResult> GetVideoSourceInfoAsync(
            PlayerChannelTarget channel,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (channel == null || channel.NvrConfig == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    ScreenPosition.Left,
                    "NVR 채널 설정이 없습니다.");
            }

            INvrProvider provider =
                await GetOrCreateLoggedInProviderAsync(
                    channel.NvrConfig,
                    cancellationToken);

            if (provider == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    "NVR Provider를 생성하지 못했습니다.");
            }

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

            if (!result.Success || result.Data == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    string.IsNullOrWhiteSpace(result.Message)
                        ? "영상 원본 정보 조회에 실패했습니다."
                        : result.Message);
            }

            return PlayerVideoSourceInfoResult.Ok(
                channel.ScreenPosition,
                result.Data.Width,
                result.Data.Height);
        }

        /// <summary>
        /// 단일 좌/우 채널 재생을 시작한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> PlayChannelAsync(
            PlayerPlaybackRequest request,
            PlayerChannelTarget channel,
            CancellationToken cancellationToken)
        {
            if (channel == null || channel.NvrConfig == null)
            {
                return PlayerPlaybackResult.Fail(
                    "채널에 연결된 NVR 설정이 없습니다.",
                    "NVR_CONFIG_REQUIRED");
            }

            INvrProvider provider =
                await GetOrCreateLoggedInProviderAsync(
                    channel.NvrConfig,
                    cancellationToken);

            if (provider == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR Provider를 생성하지 못했습니다.",
                    "NVR_PROVIDER_CREATE_FAILED");
            }

            NvrPlaybackRequest nvrRequest =
                ToNvrPlaybackRequest(
                    request,
                    channel);

            NvrResult<INvrPlaybackSession> playResult =
                await provider.PlayByTimeAsync(
                    nvrRequest,
                    cancellationToken);

            if (!playResult.Success || playResult.Data == null)
            {
                return PlayerPlaybackResult.Fail(
                    string.IsNullOrWhiteSpace(playResult.Message)
                        ? "NVR 재생 요청에 실패했습니다."
                        : playResult.Message,
                    playResult.Status.ToString());
            }

            int sessionKey =
                BuildSessionKey(
                    channel.NvrNo,
                    channel.ChannelNo,
                    (int)channel.ScreenPosition);

            _sessions[sessionKey] = playResult.Data;
            _sessionTimeOffsets[sessionKey] = channel.TimeOffsetSeconds;

            return PlayerPlaybackResult.Ok("채널 재생을 시작했습니다.");
        }

        /// <summary>
        /// NVR번호 기준으로 Provider를 생성하고 로그인한다.
        /// 이미 로그인된 Provider가 있으면 재사용한다.
        /// </summary>
        private async Task<INvrProvider> GetOrCreateLoggedInProviderAsync(
            NvrConfig nvrConfig,
            CancellationToken cancellationToken)
        {
            INvrProvider provider;

            if (_providers.TryGetValue(nvrConfig.NvrNo, out provider))
            {
                return provider;
            }

            NvrResult<INvrProvider> createResult =
                _providerFactory.Create(nvrConfig.ProviderKey);

            if (!createResult.Success || createResult.Data == null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(createResult.Message)
                        ? "NVR Provider를 찾을 수 없습니다."
                        : createResult.Message);
            }

            provider = createResult.Data;

            NvrResult initializeResult =
                provider.Initialize();

            if (!initializeResult.Success)
            {
                provider.Dispose();

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(initializeResult.Message)
                        ? "NVR Provider 초기화에 실패했습니다."
                        : initializeResult.Message);
            }

            NvrConnectionInfo connectionInfo =
                ToConnectionInfo(nvrConfig);

            NvrResult loginResult =
                await provider.LoginAsync(
                    connectionInfo,
                    cancellationToken);

            if (!loginResult.Success)
            {
                provider.Dispose();

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(loginResult.Message)
                        ? "NVR 로그인에 실패했습니다."
                        : loginResult.Message);
            }

            _providers[nvrConfig.NvrNo] = provider;

            return provider;
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
            PlayerChannelTarget channel)
        {
            return new NvrPlaybackRequest
            {
                CounterNo = request.CounterNo,
                NvrNo = channel.NvrNo,
                ChannelNo = channel.ChannelNo,
                ScreenPosition = (int)channel.ScreenPosition,
                SearchDateTime = request.SearchDateTime,
                StartTime = request.PlayStartTime,
                EndTime = request.PlayEndTime,
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
        /// NvrResult를 PlayerPlaybackResult로 변환한다.
        /// </summary>
        private static PlayerPlaybackResult ToPlayerResult(
            NvrResult result)
        {
            if (result == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 처리 결과가 없습니다.",
                    "NVR_RESULT_EMPTY");
            }

            if (result.Success)
            {
                return PlayerPlaybackResult.Ok(result.Message);
            }

            return PlayerPlaybackResult.Fail(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "NVR 처리에 실패했습니다."
                    : result.Message,
                result.Status.ToString());
        }

        /// <summary>
        /// 재생속도를 변경한다.
        /// 재생 전이면 선택값만 보관하고,
        /// 재생 중이면 Provider에 적용한다.
        /// 
        /// 주의:
        /// - 속도 변경 전 현재 영상재생시간을 먼저 고정해야 한다.
        /// - 일시정지 상태에서 속도를 변경해도 재생 상태로 바뀌면 안 된다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SetPlaybackSpeedAsync(
            PlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생속도 변경 요청이 취소되었습니다.",
                    "PLAYBACK_SPEED_CANCELLED");
            }

            if (_sessions.Count == 0)
            {
                _currentSpeed = speed;

                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(speed)
                    + "으로 설정되었습니다. 다음 재생부터 적용됩니다.");
            }

            PlaybackState stateBeforeChange =
                CurrentState;

            DateTime? syncedPlaybackTime =
                await SyncPlaybackTimeAsync(cancellationToken);

            DateTime currentPlaybackTimeBeforeChange =
                syncedPlaybackTime.HasValue
                    ? syncedPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider =
                    GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null || !capabilities.CanChangeSpeed)
                {
                    return PlayerPlaybackResult.Fail(
                        "현재 NVR Provider는 재생속도 변경을 지원하지 않습니다.",
                        "PLAYBACK_SPEED_NOT_SUPPORTED");
                }

                NvrPlaybackSpeed nvrSpeed =
                    ToNvrPlaybackSpeed(speed);

                NvrResult result =
                    await provider.SetPlaybackSpeedAsync(
                        item.Value,
                        nvrSpeed,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _currentPlaybackTime = currentPlaybackTimeBeforeChange;
            _currentSpeed = speed;

            if (stateBeforeChange == PlaybackState.Playing
                || stateBeforeChange == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc = DateTime.UtcNow;
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

            PlayerPlaybackResult syncResult =
                await ResyncPlaybackSessionsAsync(cancellationToken);

            if (!syncResult.Success)
            {
                return syncResult;
            }

            return PlayerPlaybackResult.Ok(
                GetPlaybackSpeedText(speed)
                + "으로 재생속도를 변경했습니다. "
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
        /// 지정한 영상재생시간으로 이동한다.
        /// 이동 후 좌/우 영상 싱크를 동기화한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
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

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider =
                    GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                int offsetSeconds =
                    GetSessionTimeOffset(item.Key);

                DateTime providerTargetTime =
                    targetTime.AddSeconds(offsetSeconds);

                NvrResult result =
                    await provider.SeekAsync(
                        item.Value,
                        providerTargetTime,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
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

            PlayerPlaybackResult syncResult =
                await ResyncPlaybackSessionsAsync(
                    cancellationToken);

            if (!syncResult.Success)
            {
                return syncResult;
            }

            return PlayerPlaybackResult.Ok(
                "재생 위치를 이동했습니다. "
                + targetTime.ToString("yyyy-MM-dd HH:mm:ss")
                + " / "
                + syncResult.Message);
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
        /// 재생속도 enum 값을 실제 시간 배율로 변환한다.
        /// </summary>
        private static double GetPlaybackSpeedMultiplier(
            PlaybackSpeed speed)
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

                default:
                    return 1.0;
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

            if (!result.Success)
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
        /// 현재 재생 중인 세션 중 Provider 실제 재생시간을 조회할 수 있는 세션의 시간을 반환한다.
        /// 
        /// 여러 채널이 재생 중일 경우 첫 번째로 성공한 세션의 시간을 사용한다.
        /// 좌/우 시간 차이 비교는 GetPlaybackSyncStatusAsync에서 별도로 처리한다.
        /// </summary>
        private async Task<DateTime?> TryGetAnyProviderPlaybackTimeAsync(
            CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                int sessionKey = item.Key;
                INvrPlaybackSession session = item.Value;

                DateTime? providerTime =
                    await TryGetProviderPlaybackTimeAsync(
                        session,
                        cancellationToken);

                if (!providerTime.HasValue)
                {
                    continue;
                }

                int offsetSeconds =
                    GetSessionTimeOffset(sessionKey);

                // Provider 시간은 실제 NVR 채널 시간이다.
                // UI 기준 시간으로 사용하기 위해 채널별 보정값을 제거한다.
                return providerTime.Value.AddSeconds(-offsetSeconds);
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

                if (!timeResult.Success)
                {
                    continue;
                }

                int offsetSeconds =
                    GetSessionTimeOffset(sessionKey);

                snapshots.Add(
                    new PlaybackTimeSnapshot
                    {
                        SessionKey = sessionKey,
                        Session = session,
                        Provider = provider,
                        OffsetSeconds = offsetSeconds,
                        ProviderTime = timeResult.Data,
                        NormalizedTime = timeResult.Data.AddSeconds(-offsetSeconds)
                    });
            }

            return snapshots;
        }

        
    }
}