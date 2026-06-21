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
    /// PlayerView의 재생 요청을
    /// NVR 번호별 제조사 재생 엔진에 전달하는 서비스이다.
    ///
    /// 처리 흐름:
    /// 1. PlayerPlaybackRequest 검증
    /// 2. NVR 번호별 로그인 Provider 확보
    /// 3. 제조사별 재생 엔진 생성 또는 재사용
    /// 4. NVR 번호별 다중채널 재생 그룹 준비
    /// 5. 재생·일시정지·이동·방향·속도 제어
    /// 6. 제조사 그룹 상태와 영상재생시간 동기화
    /// 7. 정지 또는 해제 시 그룹·엔진·Provider 정리
    /// </summary>
    public sealed class NvrPlayerPlaybackService : IPlayerPlaybackService
    {
        private readonly INvrProviderFactory _providerFactory;

        private readonly Dictionary<int, INvrProvider> _providers;
        
        //NVR 번호별 제조사 전용 고수준 재생 엔진
        private readonly Dictionary<int, INvrPlaybackEngine> _playbackEngines;
        //NVR 번호별 다중채널 재생 그룹 세션.
        private readonly Dictionary<int, INvrPlaybackGroupSession> _playbackGroupSessions;

        private PlayerPlaybackRequest _currentRequest;
        private DateTime? _currentPlaybackTime;
        private DateTime? _playbackClockStartedAtUtc;
        private bool _disposed;
        private PlaybackSpeed _currentSpeed;
        

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
            _playbackEngines = new Dictionary<int, INvrPlaybackEngine>(); //해당 NVR의 제조사별 고수준 재생 엔진은 해당 NVR의 로그인된 Provider에서 생성한다.
            _playbackGroupSessions = new Dictionary<int, INvrPlaybackGroupSession>(); //OpenAsync로 준비된 그룹 세션을 NVR 번호 기준으로 보관한다.
            CurrentState = PlaybackState.Stopped;
            _currentSpeed = PlaybackSpeed.Normal;
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
        /// NVR 재생 서비스가 보유한
        /// 그룹 세션, 재생 엔진 및 Provider 리소스를 정리한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                /*
                 * 정상 종료에서는 공개 StopAsync를 사용한다.
                 *
                 * StopAsync는 다음 순서로 정리한다.
                 * - 제조사별 재생 그룹 중지
                 * - 재생 엔진 정리
                 * - Provider 로그아웃 및 정리
                 * - 서비스 내부 상태 초기화
                 */
                StopAsync(
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                /*
                 * StopAsync 자체에서 예상하지 못한 예외가 발생한 경우
                 * 다음 실행에서 손상된 그룹, 엔진 또는 Provider가
                 * 재사용되지 않도록 로컬 리소스를 강제로 정리한다.
                 */
                ForceReleasePlaybackResources();
            }
            finally
            {
                /*
                 * StopAsync 자체에서 예상하지 못한 예외가 발생하면
                 * 손상된 그룹, 엔진 또는 Provider가 다음 실행에서
                 * 재사용되지 않도록 로컬 리소스를 강제로 정리한다.
                 */
                _disposed =
                    true;
            }
        }

        /// <summary>
        /// 현재 조회 요청 기준으로 NVR 녹화 영상을 재생한다.
        ///
        /// 처리 순서:
        /// 1. 재생 요청값 검증
        /// 2. 기존 NVR 재생 그룹 정리
        /// 3. NVR 번호별 제조사 재생 그룹 준비 및 시작
        /// 4. 현재 영상재생시간 기준값 설정
        /// 5. 사용자가 선택한 재생속도 적용
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
                
                /*
                 * 이전 그룹 재생 세션만 정리한다.
                 * 
                 * provider 로그인과 재생 엔진은 유지하므로
                 * 새 조회 그룹 생성 시 재사용할 수 있다.
                 */
                PlayerPlaybackResult stopGroupResult =
                         await StopCurrentPlaybackGroupsOnlyAsync();

                if (stopGroupResult == null
                    || !stopGroupResult.Success)
                {
                    return stopGroupResult
                        ?? PlayerPlaybackResult.Fail(
                            "기존 채널별 재생 세션 정리 결과가 없습니다.",
                            "LEGACY_PLAYBACK_STOP_RESULT_EMPTY",
                            PlaybackFailureCategory.System);
                }

                /*
                 * 이전 요청 및 재생시간 상태를 초기화한 뒤
                 * 새 요청을 서비스 기준 요청으로 등록한다.
                 */
                _currentRequest =
                    null;

                _currentPlaybackTime =
                    null;

                _playbackClockStartedAtUtc =
                    null;

                CurrentState =
                    PlaybackState.Stopped;

                /*
                 * 사용자가 재생 전에 선택한 속도는 유지한다.
                 *
                 * 실제 제조사 그룹은 정방향 1배속으로 준비되며,
                 * Open과 Start가 끝난 후 선택 속도를 다시 적용한다.
                 */
                _currentSpeed =
                    selectedSpeed;

                _currentRequest =
                    request;

                _currentPlaybackTime =
                    request.PlayStartTime;

                /*
                 * NVR 번호별로 다음 처리를 실행한다.
                 *
                 * - 로그인된 Provider 확보
                 * - 제조사별 INvrPlaybackEngine 준비
                 * - 그룹 요청 변환
                 * - Engine.OpenAsync
                 * - Engine.StartAsync
                 */
                PlayerPlaybackResult groupStartResult =
                    await OpenAndStartPlaybackGroupsAsync(
                        request,
                        request.PlayStartTime,
                        true,
                        cancellationToken);

                if (groupStartResult == null
                    || !groupStartResult.Success)
                {
                    /*
                     *  일부 그룹이 시작된 상태가 남지 않도록
                     *  전역 그룹 세션을 다시 정리한다.
                     */
                    await StopCurrentPlaybackGroupsOnlyAsync();

                    _currentRequest =
                        null;

                    _currentPlaybackTime =
                        null;

                    _playbackClockStartedAtUtc =
                        null;

                    CurrentState =
                        PlaybackState.Stopped;

                    _currentSpeed =
                        selectedSpeed;

                    return groupStartResult
                        ?? PlayerPlaybackResult.Fail(
                            "NVR 그룹 재생 시작 결과가 없습니다.",
                            "PLAYBACK_GROUP_START_RESULT_EMPTY",
                            PlaybackFailureCategory.System);
                }

                /*
                 * 모든 제조사 그룹의 Open과 Start가 성공한 시점이다.
                 *
                 * 제조사 엔진은 이미 최초 위치 정렬과 채널 동기화를
                 * 처리했으므로 기존 WaitForPlaybackReadyAsync와
                 * ResyncPlaybackSessionsAsync를 다시 호출하지 않는다.
                 */
                _currentPlaybackTime =
                    request.PlayStartTime;

                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;

                CurrentState =
                    PlaybackState.Playing;

                _pausedFromState =
                    PlaybackState.Playing;

                /*
                 * 그룹은 최초 1배속으로 준비된다.
                 *
                 * 사용자가 미리 선택한 속도가 1배속이 아니면
                 * 제조사 엔진을 통해 각 그룹에 동일하게 적용한다.
                 */
                if (selectedSpeed
                    != PlaybackSpeed.Normal)
                {
                    PlayerPlaybackResult speedResult =
                        await ApplyPlaybackSpeedToGroupsAsync(
                            selectedSpeed,
                            cancellationToken);

                    if (speedResult == null
                        || !speedResult.Success)
                    {
                        /*
                         * 일부 NVR 그룹에만 속도가 적용됐을 수 있으므로
                         * 서로 다른 속도의 그룹을 계속 재생하지 않는다.
                         */
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        _currentRequest =
                            null;

                        _currentPlaybackTime =
                            null;

                        _playbackClockStartedAtUtc =
                            null;

                        CurrentState =
                            PlaybackState.Stopped;

                        /*
                         * 실제 재생 그룹이 모두 중지되었으므로
                         * 서비스 속도도 기본값으로 복구한다.
                         */
                        _currentSpeed =
                            PlaybackSpeed.Normal;

                        return speedResult
                            ?? PlayerPlaybackResult.Fail(
                                "그룹 재생속도 적용 결과가 없습니다.",
                                "PLAYBACK_GROUP_SPEED_RESULT_EMPTY",
                                PlaybackFailureCategory.System);
                    }
                }

                /*
                 * 실제 제조사 그룹에 선택 속도 적용이 완료된 뒤
                 * 서비스의 현재 속도와 시간 기준 시계를 확정한다.
                 */
                _currentSpeed =
                    selectedSpeed;

                _currentPlaybackTime =
                    request.PlayStartTime;

                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;

                CurrentState =
                    PlaybackState.Playing;

                return PlayerPlaybackResult.Ok(
                    "NVR 그룹 재생을 시작했습니다. "
                    + request.PlayStartTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + " ~ "
                    + request.PlayEndTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + (
                        selectedSpeed == PlaybackSpeed.Normal
                            ? string.Empty
                            : Environment.NewLine
                                + GetPlaybackSpeedText(
                                    selectedSpeed)
                                + "을 적용했습니다."
                    ));
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await StopCurrentPlaybackGroupsOnlyAsync();
                }
                catch
                {
                    // 취소 후 정리 예외가 원래 취소 결과를 덮어쓰지 않게 한다.
                }

                _currentRequest =
                    null;

                _currentPlaybackTime =
                    null;

                _playbackClockStartedAtUtc =
                    null;

                CurrentState =
                    PlaybackState.Stopped;

                _currentSpeed =
                    selectedSpeed;

                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 재생 요청이 취소되었습니다.",
                    "PLAYBACK_GROUP_START_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }
            catch (Exception ex)
            {
                try
                {
                    await StopCurrentPlaybackGroupsOnlyAsync();
                }
                catch
                {
                    // 재생 실패 후 정리 예외는 원래 예외 메시지를 가리지 않는다.
                }

                _currentRequest =
                    null;

                _currentPlaybackTime =
                    null;

                _playbackClockStartedAtUtc =
                    null;

                CurrentState =
                    PlaybackState.Stopped;

                _currentSpeed =
                    selectedSpeed;

                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 재생 시작 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_GROUP_START_FAILED",
                    PlaybackFailureCategory.System);
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
        /// 지정한 재생속도를
        /// 실행 중인 모든 NVR 재생 그룹에 적용한다.
        ///
        /// 처리 정책:
        /// - 모든 그룹에 동일한 속도를 순차 적용한다.
        /// - 한 그룹이라도 실패하면 실패 결과를 반환한다.
        /// - 이전 속도 복원은 호출부에서 수행한다.
        /// - PartialSuccess는 성공으로 처리하되 경고 메시지를 수집한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> ApplyPlaybackSpeedToGroupsAsync(
                PlaybackSpeed speed,
                CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "그룹 재생속도 적용 요청이 취소되었습니다.",
                    "PLAYBACK_GROUP_SPEED_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생속도를 적용할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            NvrPlaybackSpeed nvrSpeed =
                ToNvrPlaybackSpeed(
                    speed);

            var warningMessages =
                new List<string>();

            /*
             * 동일한 속도를 모든 NVR 그룹에 순차적으로 적용한다.
             */
            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(
                        pair => pair.Key))
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생속도를 적용할 그룹 세션 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_GROUP_SESSION_INVALID",
                        PlaybackFailureCategory.System);
                }

                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생속도를 적용할 NVR 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_ENGINE_NOT_FOUND",
                        PlaybackFailureCategory.System);
                }

                NvrResult speedResult =
                    await engine.SetSpeedAsync(
                        groupSession,
                        nvrSpeed,
                        cancellationToken);

                if (speedResult == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "NVR 그룹 재생속도 적용 결과가 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_GROUP_SPEED_RESULT_EMPTY",
                        PlaybackFailureCategory.System);
                }

                if (!speedResult.Success)
                {
                    return ToPlayerResult(
                        speedResult);
                }

                /*
                 * 제조사 엔진에서 속도 적용은 성공했지만
                 * 동기화 등의 경고가 있는 경우 메시지를 보관한다.
                 */
                if (speedResult.Status
                        == NvrResultStatus.PartialSuccess
                    && !string.IsNullOrWhiteSpace(
                        speedResult.Message))
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + speedResult.Message);
                }
            }

            if (warningMessages.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(
                        speed)
                    + "을 NVR 재생 그룹에 적용했지만 "
                    + "일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warningMessages.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                GetPlaybackSpeedText(
                    speed)
                + "을 모든 NVR 재생 그룹에 적용했습니다.");
        }

        /// <summary>
        /// 정방향 1배속으로 변경한 뒤
        /// 다중채널 NVR 그룹의 제조사별 동기화를 실행한다.
        ///
        /// 단일 채널 그룹은 비교할 다른 채널이 없으므로 생략한다.
        ///
        /// 처리 정책:
        /// - PartialSuccess는 동기화 성공으로 보고 경고만 수집한다.
        /// - 그룹 정보 누락, 취소, 예외, 동기화 실패는 실제 실패로 처리한다.
        /// - 실제 실패가 발생하면 모든 그룹을 변경 전 Playing 또는 Paused 상태로 복원한다.
        /// - 상태 복원에 실패하면 전체 그룹을 중지한다.
        /// - 1배속 변경 자체는 유지한다.
        /// </summary>
        private async Task<PlayerPlaybackResult>
            SynchronizePlaybackGroupsAfterNormalSpeedAsync(
                PlaybackState stateBeforeChange,
                CancellationToken cancellationToken)
        {
            /*
             * 동기화는 완료됐지만 제조사 엔진에서
             * 일부 채널 오차 등의 경고를 반환한 경우 보관한다.
             *
             * PartialSuccess는 실제 실패와 구분해야 한다.
             */
            var warningMessages =
                new List<string>();

            /*
             * 다중채널 그룹에 대해 실제 SynchronizeAsync를
             * 한 * 다중채널 그룹에 대해 실제 Synchron 번이라도 호출했는지 나타낸다.
             */
            bool synchronizationExecuted =
                false;

            /*
             * 동기화 도중 실제 실패가 발생하여
             * 모든 그룹의 Playing 또는 Paused 상태를
             * 다시 통일해야 하는지 나타낸다.
             *
             * PartialSuccess만 발생한 경우에는 false를 유지한다.
             */
            bool requiresStateRestore =
                false;

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(
                        pair => pair.Key))
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                /*
                 * 단일 채널 그룹은 비교할 채널이 없으므로
                 * 제조사 동기화 명령을 실행하지 않는다.
                 */
                if (groupSession != null
                    && groupSession.ChannelCount <= 1)
                {
                    continue;
                }

                INvrPlaybackEngine engine;

                /*
                 * 다중채널 그룹인데 세션 또는 엔진이 없다면
                 * 정상적인 동기화를 수행할 수 없는 실제 실패다.
                 */
                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": 동기화할 그룹 또는 재생 엔진이 없습니다.");

                    requiresStateRestore =
                        true;

                    break;
                }

                /*
                 * 동기화 시작 전 요청이 취소됐다면
                 * 일부 앞쪽 그룹만 동기화됐을 수 있으므로
                 * 전체 그룹 상태를 다시 통일한다.
                 */
                if (cancellationToken.IsCancellationRequested)
                {
                    warningMessages.Add(
                        "사용자 요청 취소로 그룹 동기화를 중단했습니다.");

                    requiresStateRestore =
                        true;

                    break;
                }

                synchronizationExecuted =
                    true;

                NvrResult synchronizeResult;

                try
                {
                    synchronizeResult =
                        await engine.SynchronizeAsync(
                            groupSession,
                            cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": 그룹 동기화가 취소되었습니다.");

                    requiresStateRestore =
                        true;

                    break;
                }
                catch (Exception ex)
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": 그룹 동기화 중 오류가 발생했습니다. "
                        + ex.Message);

                    requiresStateRestore =
                        true;

                    break;
                }

                /*
                 * 결과가 없거나 Success=false이면 실제 실패다.
                 *
                 * 제조사 Synchronizer가 일부 채널을 Pause 상태로
                 * 남겼을 수 있으므로 전체 상태 복원이 필요하다.
                 */
                if (synchronizeResult == null
                    || !synchronizeResult.Success)
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + (
                            synchronizeResult == null
                                ? "그룹 동기화 결과가 없습니다."
                                : string.IsNullOrWhiteSpace(
                                    synchronizeResult.Message)
                                    ? "그룹 동기화에 실패했습니다."
                                    : synchronizeResult.Message
                        ));

                    requiresStateRestore =
                        true;

                    break;
                }

                /*
                 * PartialSuccess는 동기화 명령 자체는 성공한 상태다.
                 *
                 * 이 경우에는 Pause 또는 Resume을 다시 실행하지 않고
                 * 경고 메시지만 사용자에게 전달한다.
                 */
                if (synchronizeResult.Status
                        == NvrResultStatus.PartialSuccess
                    && !string.IsNullOrWhiteSpace(
                        synchronizeResult.Message))
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + synchronizeResult.Message);
                }
            }

            /*
             * 실제 동기화 실패가 발생한 경우에만
             * 모든 그룹의 재생 상태를 변경 전 상태로 통일한다.
             *
             * SynchronizeAsync 실패 시 제조사 엔진이
             * 안전을 위해 그룹을 Paused 상태로 남길 수 있다.
             */
            if (requiresStateRestore)
            {
                var allGroups =
                    new List<PreparedPlaybackGroup>();

                foreach (
                    KeyValuePair<int, INvrPlaybackGroupSession>
                    item in _playbackGroupSessions
                        .OrderBy(
                            pair => pair.Key))
                {
                    INvrPlaybackEngine engine;

                    if (item.Value != null
                        && _playbackEngines.TryGetValue(
                            item.Key,
                            out engine)
                        && engine != null)
                    {
                        allGroups.Add(
                            new PreparedPlaybackGroup
                            {
                                NvrNo =
                                    item.Key,

                                Engine =
                                    engine,

                                Session =
                                    item.Value
                            });
                    }
                }

                bool stateRestored;

                /*
                 * 속도 변경 전 일시정지 상태였다면
                 * 모든 그룹을 다시 Paused 상태로 맞춘다.
                 *
                 * 재생 중이었다면 모든 그룹을 다시 Resume한다.
                 */
                if (stateBeforeChange
                    == PlaybackState.Paused)
                {
                    stateRestored =
                        await PauseResumedPlaybackGroupsAsync(
                            allGroups);
                }
                else
                {
                    stateRestored =
                        await ResumePausedPlaybackGroupsAsync(
                            allGroups);
                }

                if (!stateRestored)
                {
                    /*
                     * 일부 그룹만 Playing 또는 Paused로 남은 상태는
                     * 계속 사용할 수 없으므로 전체 그룹을 중지한다.
                     */
                    await StopCurrentPlaybackGroupsOnlyAsync();

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

                    return PlayerPlaybackResult.Fail(
                        "1배속 변경 후 그룹 동기화 실패 상태를 "
                        + "복원하지 못해 재생을 중지했습니다."
                        + (
                            warningMessages.Count == 0
                                ? string.Empty
                                : Environment.NewLine
                                    + string.Join(
                                        Environment.NewLine,
                                        warningMessages.Select(
                                            warning =>
                                                "- "
                                                + warning))
                        ),
                        "PLAYBACK_SPEED_SYNC_STATE_RESTORE_FAILED",
                        PlaybackFailureCategory.System);
                }

                /*
                 * 그룹 상태 복원은 성공했다.
                 *
                 * 재생속도 1배속 변경은 이미 완료됐으므로
                 * 속도 변경 자체는 성공으로 반환하고,
                 * 자동 동기화 미완료 내용을 경고로 전달한다.
                 */
                return PlayerPlaybackResult.Ok(
                    "재생속도는 1배속으로 변경됐지만 "
                    + "일부 그룹의 자동 동기화를 완료하지 못했습니다."
                    + (
                        warningMessages.Count == 0
                            ? string.Empty
                            : Environment.NewLine
                                + string.Join(
                                    Environment.NewLine,
                                    warningMessages.Select(
                                        warning =>
                                            "- "
                                            + warning))
                    ));
            }

            /*
             * 모든 그룹이 단일 채널이라면
             * SynchronizeAsync를 실행할 필요가 없다.
             */
            if (!synchronizationExecuted)
            {
                return PlayerPlaybackResult.Ok(
                    "단일 채널 재생이므로 별도의 동기화가 필요하지 않습니다.");
            }

            /*
             * 실제 실패는 없지만 PartialSuccess 경고가 발생한 경우다.
             *
             * 이미 제조사 동기화는 완료됐으므로
             * 그룹의 Pause 또는 Resume 상태를 다시 변경하지 않는다.
             */
            if (warningMessages.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "정방향 1배속 전환 후 "
                    + "NVR 그룹 동기화를 완료했지만 "
                    + "일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warningMessages.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "정방향 1배속 전환 후 "
                + "NVR 그룹 동기화를 완료했습니다.");
        }

        /// <summary>
        /// Provider 실제 재생시간을 조회하지 못할 때 사용할
        /// 추정 영상재생시간을 계산한다.
        ///
        /// 계산 정책:
        /// - Playing 상태에서는 현재 속도에 따라 시간이 증가한다.
        /// - Rewinding 상태에서는 현재 속도에 따라 시간이 감소한다.
        /// - Paused 또는 Stopped 상태에서는 기준시간을 그대로 반환한다.
        /// - 조회 시작·종료 범위를 벗어나지 않도록 보정한다.
        /// </summary>
        private DateTime GetEstimatedPlaybackTime()
        {
            if (_currentRequest == null)
            {
                return DateTime.MinValue;
            }

            /*
             * 현재 서비스가 기억하고 있는 기준 영상재생시간을 사용한다.
             *
             * 아직 실제 시간이 저장되지 않았다면
             * 최초 조회 시작시간을 기준값으로 사용한다.
             */
            DateTime baseTime =
                _currentPlaybackTime.HasValue
                    ? _currentPlaybackTime.Value
                    : _currentRequest.PlayStartTime;

            /*
             * Playing과 Rewinding 상태가 아니면
             * 영상재생시간이 움직이면 안 된다.
             *
             * _playbackClockStartedAtUtc 값이 실수로 남아 있더라도
             * Paused 또는 Stopped 상태에서는 경과시간을 계산하지 않는다.
             */
            if (CurrentState != PlaybackState.Playing
                && CurrentState != PlaybackState.Rewinding)
            {
                return ClampPlaybackTime(
                    baseTime);
            }

            /*
             * 재생 중이더라도 시계 시작 기준값이 없다면
             * 저장된 기준시간을 그대로 반환한다.
             */
            if (!_playbackClockStartedAtUtc.HasValue)
            {
                return ClampPlaybackTime(
                    baseTime);
            }

            double elapsedSeconds =
                (
                    DateTime.UtcNow
                    - _playbackClockStartedAtUtc.Value
                ).TotalSeconds;

            double speedMultiplier =
                GetSpeedMultiplier(
                    _currentSpeed);

            DateTime estimatedTime;

            if (CurrentState == PlaybackState.Rewinding)
            {
                estimatedTime =
                    baseTime.AddSeconds(
                        -elapsedSeconds
                        * speedMultiplier);
            }
            else
            {
                estimatedTime =
                    baseTime.AddSeconds(
                        elapsedSeconds
                        * speedMultiplier);
            }

            return ClampPlaybackTime(
                estimatedTime);
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
        /// 현재 재생 중인 모든 NVR 재생 그룹을 일시정지한다.
        ///
        /// 처리 순서:
        /// 1. 현재 실제 또는 추정 영상재생시간 확보
        /// 2. NVR 그룹별 PauseAsync 실행
        /// 3. 일부 그룹 실패 시 이미 일시정지된 그룹 재개
        /// 4. 모든 그룹 성공 후 서비스 상태를 Paused로 변경
        ///
        /// Playing과 Rewinding 상태 모두 지원하며,
        /// 일시정지 직전 방향은 _pausedFromState에 보관한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PauseAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 일시정지 요청이 취소되었습니다.",
                    "PLAYBACK_PAUSE_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "일시정지할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            /*
             * 이미 일시정지 상태이면
             * 중복 Pause 명령을 보내지 않는다.
             */
            if (CurrentState == PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Ok(
                    "이미 일시정지 상태입니다.");
            }

            if (CurrentState != PlaybackState.Playing
                && CurrentState != PlaybackState.Rewinding)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 일시정지할 수 없습니다. "
                    + "State="
                    + CurrentState,
                    "PLAYBACK_PAUSE_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            PlaybackState stateBeforePause =
                CurrentState;

            /*
             * Provider의 실제 OSD 시간이 확인되면 해당 시간을 사용하고,
             * 확인할 수 없으면 서비스 추정 시간을 사용한다.
             */
            DateTime pauseTime =
                GetEstimatedPlaybackTime();

            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 일시정지 요청이 취소되었습니다.",
                    "PLAYBACK_PAUSE_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (actualPlaybackTime.HasValue)
            {
                pauseTime =
                    actualPlaybackTime.Value;
            }

            var pausedGroups =
                new List<PreparedPlaybackGroup>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    bool restored =
                        await ResumePausedPlaybackGroupsAsync(
                            pausedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "일시정지 취소 후 일부 NVR 그룹을 "
                            + "재생 상태로 복원하지 못했습니다.",
                            "PLAYBACK_PAUSE_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return PlayerPlaybackResult.Fail(
                        "재생 일시정지 요청이 취소되었습니다.",
                        "PLAYBACK_PAUSE_CANCELLED",
                        PlaybackFailureCategory.Cancelled);
                }

                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null)
                {
                    bool restored =
                        await ResumePausedPlaybackGroupsAsync(
                            pausedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }

                    return PlayerPlaybackResult.Fail(
                        "일시정지할 NVR 그룹 세션 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_GROUP_SESSION_INVALID",
                        PlaybackFailureCategory.System);
                }

                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    bool restored =
                        await ResumePausedPlaybackGroupsAsync(
                            pausedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }

                    return PlayerPlaybackResult.Fail(
                        "일시정지할 NVR 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_ENGINE_NOT_FOUND",
                        PlaybackFailureCategory.System);
                }

                NvrResult pauseResult =
                    await engine.PauseAsync(
                        groupSession,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    bool restored =
                        await ResumePausedPlaybackGroupsAsync(
                            pausedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "일부 NVR 그룹의 일시정지 실패 후 "
                            + "기존 재생 상태를 복원하지 못했습니다.",
                            "PLAYBACK_PAUSE_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return pauseResult == null
                        ? PlayerPlaybackResult.Fail(
                            "NVR 그룹 일시정지 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_GROUP_PAUSE_RESULT_EMPTY",
                            PlaybackFailureCategory.System)
                        : ToPlayerResult(
                            pauseResult);
                }

                pausedGroups.Add(
                     new PreparedPlaybackGroup
                     {
                         NvrNo =
                                item.Key,

                         Engine =
                                engine,

                         Session =
                                item.Value
                     });
            }

            /*
             * 모든 NVR 그룹이 정상적으로 일시정지된 뒤에만
             * 공통 서비스 상태를 Paused로 변경한다.
             */
            _currentPlaybackTime =
                ClampPlaybackTime(
                    pauseTime);

            _playbackClockStartedAtUtc =
                null;

            _pausedFromState =
                stateBeforePause == PlaybackState.Rewinding
                    ? PlaybackState.Rewinding
                    : PlaybackState.Playing;

            CurrentState =
                PlaybackState.Paused;

            return PlayerPlaybackResult.Ok(
                stateBeforePause == PlaybackState.Rewinding
                    ? "역재생을 일시정지했습니다."
                    : "재생을 일시정지했습니다.");
        }

        /// <summary>
        /// 일시정지된 모든 NVR 재생 그룹을 재개한다.
        ///
        /// 처리 순서:
        /// 1. 일시정지 상태 검증
        /// 2. 현재 실제 영상재생시간 확인
        /// 3. NVR 그룹별 ResumeAsync 실행
        /// 4. 일부 그룹 실패 시 이미 재개된 그룹 다시 Pause
        /// 5. 이전 방향에 따라 Playing 또는 Rewinding 복원
        /// </summary>
        public async Task<PlayerPlaybackResult> ResumeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 재개 요청이 취소되었습니다.",
                    "PLAYBACK_RESUME_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재개할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (CurrentState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 재생을 재개할 수 없습니다. "
                    + "State="
                    + CurrentState,
                    "PLAYBACK_NOT_PAUSED",
                    PlaybackFailureCategory.System);
            }

            /*
             * 일시정지 중 Seek 또는 방향 전환이 수행됐을 수 있으므로
             * Resume 직전 제조사 그룹의 실제 시간을 다시 확인한다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 재개 요청이 취소되었습니다.",
                    "PLAYBACK_RESUME_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (actualPlaybackTime.HasValue)
            {
                _currentPlaybackTime =
                    ClampPlaybackTime(
                        actualPlaybackTime.Value);
            }

            var resumedGroups =
                new List<PreparedPlaybackGroup>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    bool restored =
                        await PauseResumedPlaybackGroupsAsync(
                            resumedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "재생 재개 취소 후 일부 NVR 그룹을 "
                            + "일시정지 상태로 복원하지 못했습니다.",
                            "PLAYBACK_RESUME_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return PlayerPlaybackResult.Fail(
                        "재생 재개 요청이 취소되었습니다.",
                        "PLAYBACK_RESUME_CANCELLED",
                        PlaybackFailureCategory.Cancelled);
                }

                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null)
                {
                    bool restored =
                        await PauseResumedPlaybackGroupsAsync(
                            resumedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }

                    return PlayerPlaybackResult.Fail(
                        "재개할 NVR 그룹 세션 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_GROUP_SESSION_INVALID",
                        PlaybackFailureCategory.System);
                }

                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    bool restored =
                        await PauseResumedPlaybackGroupsAsync(
                            resumedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }

                    return PlayerPlaybackResult.Fail(
                        "재개할 NVR 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_ENGINE_NOT_FOUND",
                        PlaybackFailureCategory.System);
                }

                NvrResult resumeResult =
                    await engine.ResumeAsync(
                        groupSession,
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    bool restored =
                        await PauseResumedPlaybackGroupsAsync(
                            resumedGroups);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "일부 NVR 그룹의 재생 재개 실패 후 "
                            + "일시정지 상태를 복원하지 못했습니다.",
                            "PLAYBACK_RESUME_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return resumeResult == null
                        ? PlayerPlaybackResult.Fail(
                            "NVR 그룹 재생 재개 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_GROUP_RESUME_RESULT_EMPTY",
                            PlaybackFailureCategory.System)
                        : ToPlayerResult(
                            resumeResult);
                }

                resumedGroups.Add(
                     new PreparedPlaybackGroup
                     {
                         NvrNo = item.Key,
                         Engine = engine,
                         Session = item.Value
                     });
            }

            /*
             * 모든 NVR 그룹이 정상적으로 재개된 후에만
             * 서비스 기준 시계를 다시 시작한다.
             */
            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            if (_pausedFromState
                == PlaybackState.Rewinding)
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
        /// 현재 영상재생시간을 기준으로 지정 초만큼 이동한다.
        ///
        /// 실제 제조사 OSD 시간이 확인되면 해당 시간을 기준으로 하고,
        /// 확인할 수 없으면 서비스 추정 시간을 사용한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekSecondsAsync(
            int seconds,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (seconds == 0)
            {
                return PlayerPlaybackResult.Ok(
                    "영상재생시간을 변경하지 않았습니다.");
            }

            /*
             * 기준 NVR 그룹에서 실제 OSD 시간을 조회한다.
             *
             * 조회에 실패하면 서비스의 경과시간 기준 추정값을 사용한다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            DateTime currentTime =
                actualPlaybackTime.HasValue
                    ? actualPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            DateTime targetTime =
                currentTime.AddSeconds(
                    seconds);

            return await SeekToTimeAsync(
                targetTime,
                cancellationToken);
        }

        /// <summary>
        /// 현재 재생을 중지하고
        /// 재생 그룹, 재생 엔진 및 Provider 리소스를 모두 정리한다.
        ///
        /// 처리 순서:
        /// 1. 서비스 영상재생시간 정지
        /// 2. 제조사별 재생 그룹 중지
        /// 3. 재생 엔진 해제
        /// 4. Provider 로그아웃 및 해제
        /// 5. 서비스 내부 상태 초기화
        ///
        /// 정리 도중 일부 작업이 실패하더라도
        /// 나머지 리소스 정리는 계속 수행한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> StopAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            var cleanupWarnings =
                new List<string>();

            /*
             * Stop은 리소스 정리 명령이다.
             *
             * 전달된 CancellationToken이 이미 취소되었더라도
             * 반드시 끝까지 실행해야 하므로 실제 정리 호출에는
             * CancellationToken.None을 사용한다.
             */

            /*
             * 실제 정리가 진행되는 동안에도
             * 서비스 영상재생시간이 계속 움직이지 않도록
             * 먼저 논리적인 재생 시계를 정지한다.
             */
            if (_currentRequest != null)
            {
                try
                {
                    _currentPlaybackTime =
                        ClampPlaybackTime(
                            GetEstimatedPlaybackTime());
                }
                catch
                {
                    /*
                     * 종료 정리 중 시간 계산 실패는
                     * 실제 NVR 리소스 정리를 막지 않는다.
                     */
                }
            }

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            /*
             * 1. 현재 제조사별 재생 그룹을 정리한다.
             */
            List<KeyValuePair<int, INvrPlaybackGroupSession>>
                groupItems =
                    _playbackGroupSessions
                        .ToList();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in groupItems)
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null)
                {
                    cleanupWarnings.Add(
                        "NVR 재생 그룹 세션 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    cleanupWarnings.Add(
                        "NVR 재생 그룹을 정리할 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                try
                {
                    NvrResult stopResult =
                        await engine.StopAsync(
                            groupSession,
                            CancellationToken.None);

                    if (stopResult == null)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 그룹 중지 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo);
                    }
                    else if (!stopResult.Success)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 그룹 중지에 실패했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Status="
                            + stopResult.Status
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(
                                    stopResult.Message)
                                    ? "-"
                                    : stopResult.Message
                            ));
                    }
                    else if (
                        stopResult.Status
                        == NvrResultStatus.PartialSuccess)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 그룹 중지 중 일부 경고가 발생했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(
                                    stopResult.Message)
                                    ? "-"
                                    : stopResult.Message
                            ));
                    }
                }
                catch (Exception ex)
                {
                    /*
                     * 하나의 NVR 그룹 정리 실패가
                     * 다른 NVR 그룹 정리를 막지 않게 한다.
                     */
                    cleanupWarnings.Add(
                        "NVR 재생 그룹 중지 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * 그룹 Stop 성공 여부와 관계없이
             * 공통 서비스가 가진 세션 참조는 모두 제거한다.
             */
            _playbackGroupSessions.Clear();

            

            /*
             * 2.재생 엔진이 IDisposable을 구현했다면 해제한다.
             *
             * INvrPlaybackEngine 자체는 IDisposable을 요구하지 않으므로
             * 선택적으로 구현된 경우에만 Dispose를 호출한다.
             */
            List<KeyValuePair<int, INvrPlaybackEngine>>
                engineItems =
                    _playbackEngines
                        .ToList();

            foreach (
                KeyValuePair<int, INvrPlaybackEngine>
                item in engineItems)
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackEngine engine =
                    item.Value;

                if (engine == null)
                {
                    continue;
                }

                IDisposable disposableEngine =
                    engine as IDisposable;

                if (disposableEngine == null)
                {
                    continue;
                }

                try
                {
                    disposableEngine.Dispose();
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "NVR 재생 엔진 해제 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * 재생 엔진은 로그인된 Provider에 연결되어 있으므로
             * Provider를 해제하기 전에 엔진 참조를 제거한다.
             */
            _playbackEngines.Clear();

            /*
             * 3. 모든 Provider를 로그아웃하고 해제한다.
             *
             * 영상 원본 정보 조회만 수행한 Provider도
             * _providers에 남아 있을 수 있으므로 전체를 정리한다.
             */
            List<KeyValuePair<int, INvrProvider>>
                providerItems =
                    _providers
                        .ToList();

            foreach (
                KeyValuePair<int, INvrProvider>
                item in providerItems)
            {
                int nvrNo =
                    item.Key;

                INvrProvider provider =
                    item.Value;

                if (provider == null)
                {
                    cleanupWarnings.Add(
                        "NVR Provider 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                /*
                 * Logout 실패 여부와 관계없이
                 * Dispose는 반드시 별도로 실행한다.
                 */
                try
                {
                    NvrResult logoutResult =
                        await provider.LogoutAsync(
                            CancellationToken.None);

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
                            + ", Status="
                            + logoutResult.Status
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(
                                    logoutResult.Message)
                                    ? "-"
                                    : logoutResult.Message
                            ));
                    }
                    else if (
                        logoutResult.Status
                        == NvrResultStatus.PartialSuccess)
                    {
                        cleanupWarnings.Add(
                            "NVR 로그아웃 중 일부 경고가 발생했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(
                                    logoutResult.Message)
                                    ? "-"
                                    : logoutResult.Message
                            ));
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
                        "NVR Provider 리소스 해제 중 "
                        + "예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }
            }

            _providers.Clear();

            /*
             * 4. 서비스 내부의 논리 상태를 초기화한다.
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

            /*
             * 사용자가 선택한 재생속도는 초기화하지 않는다.
             *
             * 정지 후 다시 재생하더라도
             * 기존에 선택한 속도를 유지하는 정책이다.
             */

            if (cleanupWarnings.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "재생은 중지되었지만 "
                    + "일부 리소스 정리 중 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        cleanupWarnings.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "재생을 중지하고 NVR 리소스를 정리했습니다.");
        }

        /// <summary>
        /// 현재 NVR 재생 그룹들의 영상 동기화 상태를 조회한다.
        ///
        /// 제조사 그룹별 상태에서 다음 값을 사용한다.
        /// - 공통 영상재생시간
        /// - 실제 채널 간 최대 시간차
        /// - 동기화 가능 여부
        ///
        /// 여러 NVR 그룹이 존재하면
        /// 각 그룹 기준시각 간 차이도 함께 계산한다.
        /// </summary>
        public async Task<PlaybackSyncStatus>
            GetPlaybackSyncStatusAsync(
                CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            var result =
                new PlaybackSyncStatus();

            if (_currentRequest == null
                || _currentRequest.Channels == null
                || _currentRequest.Channels.Count == 0)
            {
                return result;
            }

            bool usesProviderTime =
                false;

            bool hasMeasuredDifference =
                false;

            double maximumDifferenceSeconds =
                0d;

            var groupPlaybackTimes =
                new List<DateTime>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                List<PlayerChannelTarget> groupChannels =
                    _currentRequest.Channels
                        .Where(
                            channel =>
                                channel != null
                                && channel.NvrNo == nvrNo)
                        .OrderBy(
                            channel =>
                                (int)channel.ScreenPosition)
                        .ToList();

                INvrPlaybackEngine engine;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    /*
                     * 그룹 상태를 확인하지 못했더라도
                     * UI에서 채널 자체가 사라지지 않도록
                     * 시간 미확인 상태로 목록에 포함한다.
                     */
                    foreach (PlayerChannelTarget channel
                        in groupChannels)
                    {
                        result.Channels.Add(
                            new PlaybackChannelTimeStatus
                            {
                                ScreenPosition =
                                    GetScreenPositionText(
                                        (int)channel.ScreenPosition),

                                NvrNo =
                                    channel.NvrNo,

                                ChannelNo =
                                    channel.ChannelNo,

                                PlaybackTime =
                                    null,

                                IsProviderTime =
                                    false,

                                TimeOffsetSeconds =
                                    channel.TimeOffsetSeconds
                            });
                    }

                    continue;
                }

                NvrResult<NvrPlaybackGroupStatus>
                    statusResult =
                        null;

                try
                {
                    statusResult =
                        await engine.GetStatusAsync(
                            groupSession,
                            cancellationToken);
                }
                catch
                {
                    /*
                     * 상태 조회 예외가 전체 상태 화면 조회를
                     * 중단시키지 않게 한다.
                     */
                }

                NvrPlaybackGroupStatus groupStatus =
                    statusResult != null
                    && statusResult.Success
                        ? statusResult.Data
                        : null;

                DateTime? groupPlaybackTime =
                    groupStatus == null
                        ? (DateTime?)null
                        : groupStatus.CurrentPlaybackTime;

                if (groupPlaybackTime.HasValue)
                {
                    DateTime normalizedTime =
                        ClampPlaybackTime(
                            groupPlaybackTime.Value);

                    groupPlaybackTime =
                        normalizedTime;

                    groupPlaybackTimes.Add(
                        normalizedTime);

                    usesProviderTime =
                        true;
                }

                /*
                 * 제조사 엔진이 그룹 내부 실제 최대 시간차를
                 * 측정한 경우 최종 차이에 반영한다.
                 */
                if (groupStatus != null
                    && groupStatus.MaximumDriftSeconds.HasValue)
                {
                    double groupDriftSeconds =
                        Math.Abs(
                            groupStatus.MaximumDriftSeconds.Value);

                    if (groupDriftSeconds
                        > maximumDifferenceSeconds)
                    {
                        maximumDifferenceSeconds =
                            groupDriftSeconds;
                    }

                    hasMeasuredDifference =
                        true;
                }

                foreach (PlayerChannelTarget channel
                    in groupChannels)
                {
                    result.Channels.Add(
                        new PlaybackChannelTimeStatus
                        {
                            ScreenPosition =
                                GetScreenPositionText(
                                    (int)channel.ScreenPosition),

                            NvrNo =
                                channel.NvrNo,

                            ChannelNo =
                                channel.ChannelNo,

                            /*
                             * 제조사 엔진이 보정값을 적용해 계산한
                             * 공통 그룹 시각을 표시한다.
                             */
                            PlaybackTime =
                                groupPlaybackTime,

                            IsProviderTime =
                                groupPlaybackTime.HasValue,

                            TimeOffsetSeconds =
                                channel.TimeOffsetSeconds
                        });
                }
            }

            /*
             * 그룹 엔진이 만들어지기 전의 채널이나
             * 그룹에 포함되지 않은 채널도 상태 목록에 포함한다.
             */
            foreach (PlayerChannelTarget channel
                in _currentRequest.Channels)
            {
                if (channel == null)
                {
                    continue;
                }

                bool alreadyAdded =
                    result.Channels.Any(
                        status =>
                            status.NvrNo == channel.NvrNo
                            && status.ChannelNo
                                == channel.ChannelNo
                            && status.ScreenPosition
                                == GetScreenPositionText(
                                    (int)channel.ScreenPosition));

                if (alreadyAdded)
                {
                    continue;
                }

                result.Channels.Add(
                    new PlaybackChannelTimeStatus
                    {
                        ScreenPosition =
                            GetScreenPositionText(
                                (int)channel.ScreenPosition),

                        NvrNo =
                            channel.NvrNo,

                        ChannelNo =
                            channel.ChannelNo,

                        PlaybackTime =
                            null,

                        IsProviderTime =
                            false,

                        TimeOffsetSeconds =
                            channel.TimeOffsetSeconds
                    });
            }

            /*
             * 서로 다른 NVR 그룹의 기준시간 차이도 계산한다.
             *
             * 한 그룹 내부 시간차는 MaximumDriftSeconds로,
             * 그룹 사이 시간차는 그룹별 CurrentPlaybackTime으로 계산한다.
             */
            if (groupPlaybackTimes.Count >= 2)
            {
                DateTime minimumTime =
                    groupPlaybackTimes.Min();

                DateTime maximumTime =
                    groupPlaybackTimes.Max();

                double groupDifferenceSeconds =
                    Math.Abs(
                        (
                            maximumTime
                            - minimumTime
                        ).TotalSeconds);

                if (groupDifferenceSeconds
                    > maximumDifferenceSeconds)
                {
                    maximumDifferenceSeconds =
                        groupDifferenceSeconds;
                }

                hasMeasuredDifference =
                    true;
            }

            result.UsesProviderTime =
                usesProviderTime;

            if (hasMeasuredDifference)
            {
                result.MaxDifference =
                    TimeSpan.FromSeconds(
                        maximumDifferenceSeconds);
            }

            return result;
        }

        /// <summary>
        /// 현재 재생 중인 제조사별 NVR 그룹을 수동 동기화한다.
        ///
        /// 정책:
        /// - 정방향 1배속에서만 실행한다.
        /// - Playing 또는 Paused 상태에서만 실행한다.
        /// - 단일 채널 그룹은 동기화 대상에서 제외한다.
        /// - 제조사별 Pause·Seek·검증 순서는 Engine이 처리한다.
        /// - 일부 그룹 실패 시 전체 그룹의 재생 상태를 통일한다.
        /// </summary>
        public async Task<PlayerPlaybackResult>
            ResyncPlaybackSessionsAsync(
                CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 동기화 요청이 취소되었습니다.",
                    "PLAYBACK_SYNC_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "동기화할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_currentSpeed
                != PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 동기화는 1배속에서만 실행할 수 있습니다.",
                    "PLAYBACK_SYNC_NORMAL_SPEED_ONLY",
                    PlaybackFailureCategory.NotSupported);
            }

            bool isReverseDirection =
                CurrentState == PlaybackState.Rewinding
                || (
                    CurrentState == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Rewinding
                );

            if (isReverseDirection)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 자동 영상 동기화는 정방향 재생만 지원합니다.",
                    "PLAYBACK_SYNC_FORWARD_ONLY",
                    PlaybackFailureCategory.NotSupported);
            }

            if (CurrentState != PlaybackState.Playing
                && CurrentState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 영상 동기화를 실행할 수 없습니다. "
                    + "State="
                    + CurrentState,
                    "PLAYBACK_SYNC_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            PlaybackState stateBeforeSynchronization =
                CurrentState;

            bool synchronizationExecuted =
                false;

            var warningMessages =
                new List<string>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                /*
                 * 단일 채널 그룹은 비교 대상이 없으므로
                 * 제조사 동기화 명령을 보내지 않는다.
                 */
                if (groupSession != null
                    && groupSession.ChannelCount <= 1)
                {
                    continue;
                }

                INvrPlaybackEngine engine;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    bool stateRestored =
                        await RestorePlaybackGroupStateAfterSyncFailureAsync(
                            stateBeforeSynchronization);

                    if (!stateRestored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "영상 동기화 실패 후 그룹 상태를 복원하지 못해 "
                            + "재생을 중지했습니다.",
                            "PLAYBACK_SYNC_STATE_RESTORE_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return PlayerPlaybackResult.Fail(
                        "동기화할 NVR 그룹 또는 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_SYNC_GROUP_INVALID",
                        PlaybackFailureCategory.System);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    bool stateRestored =
                        await RestorePlaybackGroupStateAfterSyncFailureAsync(
                            stateBeforeSynchronization);

                    if (!stateRestored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "영상 동기화 취소 후 그룹 상태를 복원하지 못해 "
                            + "재생을 중지했습니다.",
                            "PLAYBACK_SYNC_STATE_RESTORE_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return PlayerPlaybackResult.Fail(
                        "영상 동기화 요청이 취소되었습니다.",
                        "PLAYBACK_SYNC_CANCELLED",
                        PlaybackFailureCategory.Cancelled);
                }

                synchronizationExecuted =
                    true;

                NvrResult synchronizeResult =
                    await engine.SynchronizeAsync(
                        groupSession,
                        cancellationToken);

                if (synchronizeResult == null
                    || !synchronizeResult.Success)
                {
                    /*
                     * 제조사 Synchronizer는 실패 시
                     * 안전을 위해 그룹을 Paused로 둘 수 있다.
                     *
                     * 서비스 전체 그룹을 동기화 전 상태로 다시 통일한다.
                     */
                    bool stateRestored =
                        await RestorePlaybackGroupStateAfterSyncFailureAsync(
                            stateBeforeSynchronization);

                    if (!stateRestored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "영상 동기화 실패 후 그룹 상태를 복원하지 못해 "
                            + "재생을 중지했습니다.",
                            "PLAYBACK_SYNC_STATE_RESTORE_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    return synchronizeResult == null
                        ? PlayerPlaybackResult.Fail(
                            "NVR 그룹 동기화 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_GROUP_SYNC_RESULT_EMPTY",
                            PlaybackFailureCategory.System)
                        : ToPlayerResult(
                            synchronizeResult);
                }

                if (synchronizeResult.Status
                        == NvrResultStatus.PartialSuccess
                    && !string.IsNullOrWhiteSpace(
                        synchronizeResult.Message))
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + synchronizeResult.Message);
                }
            }

            DateTime? synchronizedPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    CancellationToken.None);

            if (synchronizedPlaybackTime.HasValue)
            {
                _currentPlaybackTime =
                    ClampPlaybackTime(
                        synchronizedPlaybackTime.Value);
            }

            CurrentState =
                stateBeforeSynchronization;

            _playbackClockStartedAtUtc =
                stateBeforeSynchronization
                    == PlaybackState.Playing
                        ? (DateTime?)DateTime.UtcNow
                        : null;

            if (!synchronizationExecuted)
            {
                return PlayerPlaybackResult.Ok(
                    "단일 채널 재생이므로 별도의 영상 동기화가 필요하지 않습니다.");
            }

            if (warningMessages.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "NVR 그룹 동기화를 완료했지만 "
                    + "일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warningMessages.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "NVR 재생 그룹의 영상 동기화를 완료했습니다.");
        }

        /// <summary>
        /// 제조사 그룹 동기화 실패 후
        /// 전체 NVR 그룹을 동기화 전 Playing 또는 Paused 상태로 통일한다.
        /// </summary>
        private async Task<bool>
            RestorePlaybackGroupStateAfterSyncFailureAsync(
                PlaybackState originalState)
        {
            bool allSucceeded =
                true;

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                INvrPlaybackEngine engine;
                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult stateResult =
                        originalState == PlaybackState.Playing
                            ? await engine.ResumeAsync(
                                groupSession,
                                CancellationToken.None)
                            : await engine.PauseAsync(
                                groupSession,
                                CancellationToken.None);

                    if (stateResult == null
                        || !stateResult.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            if (allSucceeded)
            {
                CurrentState =
                    originalState;

                _playbackClockStartedAtUtc =
                    originalState == PlaybackState.Playing
                        ? (DateTime?)DateTime.UtcNow
                        : null;
            }

            return allSucceeded;
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
        /// 현재 영상재생시간을 기준으로 역재생 방향으로 전환한다.
        ///
        /// 정책:
        /// - 정방향 재생 중이면 역재생을 계속 실행한다.
        /// - 일시정지 중이면 역방향으로만 변경하고 Paused를 유지한다.
        /// - 사용자가 선택한 재생속도는 유지한다.
        /// - 이미 역방향이면 중복 명령으로 처리한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> RewindAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "역재생 요청이 취소되었습니다.",
                    "REVERSE_PLAYBACK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "역재생할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            bool isAlreadyReverse =
                CurrentState == PlaybackState.Rewinding
                || (
                    CurrentState == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Rewinding
                );

            if (isAlreadyReverse)
            {
                return PlayerPlaybackResult.Ok(
                    CurrentState == PlaybackState.Paused
                        ? "이미 역재생 방향으로 일시정지되어 있습니다."
                        : "이미 역재생 중입니다.");
            }

            return await ChangePlaybackGroupDirectionAsync(
                NvrPlaybackDirection.Reverse,
                cancellationToken);
        }

        /// <summary>
        /// 현재 영상재생시간을 기준으로 정방향 재생으로 전환한다.
        ///
        /// 정책:
        /// - 역재생 중이면 정방향 재생을 계속 실행한다.
        /// - 일시정지 중이면 정방향으로만 변경하고 Paused를 유지한다.
        /// - 사용자가 선택한 재생속도는 유지한다.
        /// - 최초 조회 시작·종료 범위는 변경하지 않는다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PlayForwardFromCurrentTimeAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "정방향 전환 요청이 취소되었습니다.",
                    "FORWARD_PLAYBACK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "정방향으로 전환할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            bool isAlreadyForward =
                CurrentState == PlaybackState.Playing
                || (
                    CurrentState == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Playing
                );

            if (isAlreadyForward)
            {
                return PlayerPlaybackResult.Ok(
                    CurrentState == PlaybackState.Paused
                        ? "이미 정방향으로 일시정지되어 있습니다."
                        : "이미 정방향 재생 중입니다.");
            }

            return await ChangePlaybackGroupDirectionAsync(
                NvrPlaybackDirection.Forward,
                cancellationToken);
        }

        /// <summary>
        /// NVR 번호별로 제조사 재생 그룹을 생성한다.
        ///
        /// initialTime:
        /// - 일반 재생에서는 조회 시작시간
        /// - 타임라인 이동에서는 사용자가 선택한 시각
        ///
        /// startPlayback:
        /// - true: 그룹 준비 후 즉시 재생 시작
        /// - false: 그룹을 Paused 상태로 준비만 수행
        /// </summary>
        private async Task<PlayerPlaybackResult> OpenAndStartPlaybackGroupsAsync(
                PlayerPlaybackRequest request,
                DateTime initialTime,
                bool startPlayback,
                CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 재생 요청이 취소되었습니다.",
                    "PLAYBACK_GROUP_START_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (request == null)
            {
                return PlayerPlaybackResult.Fail(
                    "Player 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_REQUIRED",
                    PlaybackFailureCategory.Configuration);
            }

            if (request.Channels == null
                || request.Channels.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 그룹에 포함할 채널이 없습니다.",
                    "PLAYBACK_GROUP_CHANNEL_REQUIRED",
                    PlaybackFailureCategory.Configuration);
            }

            if (request.PlayStartTime
                >= request.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.",
                    "INVALID_PLAYBACK_RANGE",
                    PlaybackFailureCategory.Configuration);
            }

            /*
             * 최초 재생 위치는 전체 조회 범위 안에 있어야 한다.
             *
             * 시작시간은 포함하고 종료시간은 포함하지 않는다.
             *
             * 이 검증을 Provider 로그인과 엔진 생성 전에 수행하여
             * 잘못된 시간 요청으로 불필요한 NVR 연결이 발생하지 않게 한다.
             */
            if (initialTime
                < request.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "최초 재생시간은 조회 시작시간보다 이전일 수 없습니다.",
                    "PLAYBACK_GROUP_INITIAL_BEFORE_START",
                    PlaybackFailureCategory.Configuration);
            }

            if (initialTime
                >= request.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "최초 재생시간은 조회 종료시간보다 이전이어야 합니다.",
                    "PLAYBACK_GROUP_INITIAL_AFTER_END",
                    PlaybackFailureCategory.Configuration);
            }

            /*
             * 기존 그룹 세션이 남아 있다면
             * 새로운 그룹 재생을 시작하면 안 된다.
             *
             * PlayAsync에서 기존 그룹을 먼저 정리한 뒤
             * 이 메서드를 호출해야 한다.
             */
            if (_playbackGroupSessions.Count > 0)
            {
                return PlayerPlaybackResult.Fail(
                    "기존 NVR 재생 그룹이 아직 정리되지 않았습니다.",
                    "PLAYBACK_GROUP_ALREADY_EXISTS",
                    PlaybackFailureCategory.System);
            }

            /*
             * GroupBy를 실행하기 전에 null 채널을 검사한다.
             *
             * null 채널이 포함된 상태에서 channel.NvrNo에 접근하면
             * NullReferenceException이 발생할 수 있다.
             */
            foreach (PlayerChannelTarget channel
                in request.Channels)
            {
                if (channel == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생 요청에 null 채널이 포함되어 있습니다.",
                        "PLAYBACK_GROUP_CHANNEL_NULL",
                        PlaybackFailureCategory.Configuration);
                }

                if (channel.NvrConfig == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "채널에 연결된 NVR 설정이 없습니다. "
                        + "NvrNo="
                        + channel.NvrNo
                        + ", ChannelNo="
                        + channel.ChannelNo,
                        "NVR_CONFIG_REQUIRED",
                        PlaybackFailureCategory.Configuration);
                }
            }

            /*
             * 모든 NVR 그룹이 성공하기 전까지는
             * 전역 _playbackGroupSessions에 바로 등록하지 않는다.
             *
             * 중간 실패 시 이미 생성된 그룹을 정리하기 위해
             * 임시 목록에 보관한다.
             */
            var preparedGroups =
                new List<PreparedPlaybackGroup>();

            try
            {
                IEnumerable<IGrouping<int, PlayerChannelTarget>>
                    channelGroups =
                        request.Channels
                            .GroupBy(
                                channel => channel.NvrNo)
                            .OrderBy(
                                group => group.Key);

                foreach (IGrouping<int, PlayerChannelTarget>
                    channelGroup in channelGroups)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await CleanupPreparedPlaybackGroupsAsync(
                            preparedGroups);

                        return PlayerPlaybackResult.Fail(
                            "NVR 그룹 재생 요청이 취소되었습니다.",
                            "PLAYBACK_GROUP_START_CANCELLED",
                            PlaybackFailureCategory.Cancelled);
                    }

                    int nvrNo =
                        channelGroup.Key;

                    List<PlayerChannelTarget> groupChannels =
                        channelGroup.ToList();

                    PlayerChannelTarget firstChannel =
                        groupChannels.FirstOrDefault();

                    if (firstChannel == null
                        || firstChannel.NvrConfig == null)
                    {
                        await CleanupPreparedPlaybackGroupsAsync(
                            preparedGroups);

                        return PlayerPlaybackResult.Fail(
                            "NVR 그룹의 기준 채널 설정이 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_GROUP_CONFIG_REQUIRED",
                            PlaybackFailureCategory.Configuration);
                    }

                    NvrConfig nvrConfig =
                        firstChannel.NvrConfig;

                    /*
                     * 로그인된 Provider를 확보하고
                     * 해당 Provider가 제공하는 제조사별 재생 엔진을 준비한다.
                     */
                    NvrResult<INvrPlaybackEngine> engineResult =
                        await GetOrCreatePlaybackEngineAsync(
                            nvrConfig,
                            cancellationToken);

                    if (engineResult == null
                        || !engineResult.Success
                        || engineResult.Data == null)
                    {
                        await CleanupPreparedPlaybackGroupsAsync(
                            preparedGroups);

                        return ToPlayerResult(
                            engineResult);
                    }

                    INvrPlaybackEngine engine =
                        engineResult.Data;

                    /*
                     * Player 재생 요청을 제조사 엔진이 받을
                     * 공통 그룹 재생 요청으로 변환한다.
                     */
                    NvrResult<NvrPlaybackGroupRequest>
                        groupRequestResult =
                            ToNvrPlaybackGroupRequest(
                                request,
                                nvrNo,
                                groupChannels,
                                initialTime);

                    if (groupRequestResult == null || !groupRequestResult.Success || groupRequestResult.Data == null)
                    {
                        await CleanupPreparedPlaybackGroupsAsync(preparedGroups);

                        return ToPlayerResult(groupRequestResult);
                    }

                    /*
                     * OpenAsync는 채널별 재생 핸들을 생성하고,
                     * 최초 재생 위치로 정렬한 뒤 Paused 상태로 반환한다.
                     */
                    NvrResult<INvrPlaybackGroupSession> openResult = 
                        await engine.OpenAsync(
                            groupRequestResult.Data,
                            cancellationToken);

                    if (openResult == null
                        || !openResult.Success
                        || openResult.Data == null)
                    {
                        /*
                         * 앞에서 이미 준비된 다른 NVR 그룹이 있다면
                         * 모두 중지하여 부분 준비 상태를 남기지 않는다.
                         */
                        await CleanupPreparedPlaybackGroupsAsync(
                            preparedGroups);

                        return openResult == null
                            ? PlayerPlaybackResult.Fail(
                                "NVR 재생 그룹 준비 결과가 없습니다. "
                                + "NvrNo="
                                + nvrNo,
                                "PLAYBACK_GROUP_OPEN_RESULT_EMPTY",
                                PlaybackFailureCategory.System)
                            : ToPlayerResult(
                                openResult);
                    }

                    INvrPlaybackGroupSession groupSession =
                        openResult.Data;

                    /*
                     * OpenAsync 직후 preparedGroups에 먼저 등록한다.
                     *
                     * 이후 StartAsync가 실패하더라도
                     * 현재 그룹까지 Cleanup 대상에 포함돼야 한다.
                     */
                    var preparedGroup =
                        new PreparedPlaybackGroup
                        {
                            NvrNo =
                                nvrNo,

                            Engine =
                                engine,

                            Session =
                                groupSession
                        };

                    preparedGroups.Add(
                        preparedGroup);

                    /*
                     * 일반 재생 요청인 경우에만 재생을 시작한다.
                     *
                     * startPlayback=false인 타임라인 이동에서는
                     * OpenAsync가 만든 Paused 상태를 그대로 유지한다.
                     */
                    if (startPlayback)
                    {
                        NvrResult startResult =
                            await engine.StartAsync(
                                groupSession,
                                cancellationToken);

                        if (startResult == null
                            || !startResult.Success)
                        {
                            await CleanupPreparedPlaybackGroupsAsync(
                                preparedGroups);

                            return startResult == null
                                ? PlayerPlaybackResult.Fail(
                                    "NVR 재생 그룹 시작 결과가 없습니다. "
                                    + "NvrNo="
                                    + nvrNo,
                                    "PLAYBACK_GROUP_START_RESULT_EMPTY",
                                    PlaybackFailureCategory.System)
                                : ToPlayerResult(
                                    startResult);
                        }
                    }
                }

                /*
                 * 모든 NVR 그룹의 Open과 Start가 성공한 경우에만
                 * 전역 그룹 세션 Dictionary에 등록한다.
                 */
                foreach (PreparedPlaybackGroup preparedGroup
                    in preparedGroups)
                {
                    _playbackGroupSessions[
                        preparedGroup.NvrNo] =
                            preparedGroup.Session;
                }

                return PlayerPlaybackResult.Ok(
                    startPlayback
                        ? "모든 NVR 재생 그룹을 시작했습니다."
                        : "모든 NVR 재생 그룹을 일시정지 상태로 준비했습니다.");
            }
            catch (OperationCanceledException)
            {
                await CleanupPreparedPlaybackGroupsAsync(
                    preparedGroups);

                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 재생 요청이 취소되었습니다.",
                    "PLAYBACK_GROUP_START_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }
            catch (Exception ex)
            {
                /*
                 * 예외 발생 시 이미 생성된 모든 그룹을 정리한다.
                 *
                 * 정리 과정의 예외는 원래 예외 메시지를
                 * 덮어쓰지 않도록 내부에서 처리한다.
                 */
                await CleanupPreparedPlaybackGroupsAsync(
                    preparedGroups);

                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 재생 시작 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_GROUP_START_EXCEPTION",
                    PlaybackFailureCategory.System);
            }
        }


        /// <summary>
        /// NVR번호 기준으로 Provider를 생성하고 로그인한다.
        /// 이미 정상 로그인된 Provider가 있으면 재사용한다.
        ///
        /// 연결, 로그인, 초기화 실패는 예외를 던지지 않고
        /// NvrResult로 반환한다.
        /// </summary>
        private async Task<NvrResult<INvrProvider>> GetOrCreateLoggedInProviderAsync(NvrConfig nvrConfig, CancellationToken cancellationToken)
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
                    failureResult,
                    ToPlayerResult(failureResult));

                return failureResult;
            }
        }

        /// <summary>
        /// NVR 설정을 기준으로 로그인된 Provider와
        /// 제조사 전용 고수준 재생 엔진을 준비한다.
        ///
        /// 처리 순서:
        /// 1. NVR 설정 검증
        /// 2. 로그인된 Provider 확보
        /// 3. 기존 엔진 재사용 가능 여부 확인
        /// 4. Provider의 INvrPlaybackEngineProvider 구현 확인
        /// 5. 제조사별 재생 엔진 생성
        /// 6. NVR 번호별 엔진 캐시에 저장
        ///
        /// 공통 서비스는 DahuaPlaybackEngine 등의 실제 구현 타입을
        /// 직접 참조하지 않고 INvrPlaybackEngine만 보관한다.
        /// </summary>
        private async Task<NvrResult<INvrPlaybackEngine>> GetOrCreatePlaybackEngineAsync(NvrConfig nvrConfig, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.Cancelled,
                    "NVR 재생 엔진 준비 요청이 취소되었습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "NVR_PLAYBACK_ENGINE_CANCELLED",

                        ErrorMessage =
                            "NVR 재생 엔진 준비 요청이 취소되었습니다.",

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
            }

            if (nvrConfig == null)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.Failed,
                    "NVR 설정 정보가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "NVR_CONFIG_REQUIRED",

                        ErrorMessage =
                            "NVR 설정 정보가 없습니다.",

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
            }

            if (nvrConfig.NvrNo <= 0)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.Failed,
                    "NVR 번호가 올바르지 않습니다. "
                    + "NvrNo="
                    + nvrConfig.NvrNo,
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "INVALID_NVR_NO",

                        ErrorMessage =
                            "NVR 번호가 올바르지 않습니다.",

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    nvrConfig.ProviderKey))
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "NVR ProviderKey가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "NVR_PROVIDER_KEY_REQUIRED",

                        ErrorMessage =
                            "NVR ProviderKey가 없습니다.",

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
            }

            /*
             * GetOrCreateLoggedInProviderAsync를 호출하기 전에
             * 현재 캐시에 저장된 Provider를 보관한다.
             *
             * 로그인 처리 과정에서 기존 Provider가 폐기되고
             * 새로운 Provider가 생성될 수 있으므로,
             * 호출 후 인스턴스가 변경되었는지 확인해야 한다.
             */
            INvrProvider previousProvider =
                null;

            _providers.TryGetValue(
                nvrConfig.NvrNo,
                out previousProvider);

            NvrResult<INvrProvider> providerResult =
                await GetOrCreateLoggedInProviderAsync(
                    nvrConfig,
                    cancellationToken);

            if (providerResult == null
                || !providerResult.Success
                || providerResult.Data == null)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    providerResult == null
                        ? NvrResultStatus.Failed
                        : providerResult.Status,

                    providerResult == null
                        ? "로그인된 NVR Provider 준비 결과가 없습니다."
                        : providerResult.Message,

                    providerResult == null
                        ? new NvrErrorInfo
                        {
                            ErrorCode =
                                "NVR_PROVIDER_RESULT_EMPTY",

                            ErrorMessage =
                                "로그인된 NVR Provider 준비 결과가 없습니다.",

                            Operation =
                                "GetOrCreatePlaybackEngine"
                        }
                        : providerResult.Error);
            }

            INvrProvider provider =
                providerResult.Data;

            /*
             * 이전 Provider와 현재 로그인된 Provider가 같은 인스턴스인지 확인한다.
             *
             * previousProvider가 null인데 재생 엔진만 남아 있는 경우도
             * 기존 엔진을 안전하게 재사용할 수 없는 상태로 본다.
             */
            bool providerChanged =
                !object.ReferenceEquals(
                    previousProvider,
                    provider);

            if (providerChanged)
            {
                /*
                 * 기존 엔진은 이전 Provider 또는 이미 해제된 Provider에
                 * 연결되어 있을 수 있으므로 새 Provider에서 재사용하면 안 된다.
                 *
                 * Dictionary에서 참조만 제거하기 전에
                 * IDisposable을 구현한 엔진은 먼저 해제한다.
                 */
                INvrPlaybackEngine previousEngine;

                if (_playbackEngines.TryGetValue(
                        nvrConfig.NvrNo,
                        out previousEngine)
                    && previousEngine != null)
                {
                    IDisposable disposableEngine =
                        previousEngine as IDisposable;

                    if (disposableEngine != null)
                    {
                        try
                        {
                            disposableEngine.Dispose();
                        }
                        catch
                        {
                            /*
                             * 이전 엔진 정리 실패가
                             * 새 Provider와 새 엔진 준비를 막지는 않는다.
                             *
                             * 정상 정리 경로는 StopAsync이며,
                             * 이 구간은 비정상 캐시 교체를 위한 방어 처리다.
                             */
                        }
                    }
                }

                /*
                 * 이전 Provider에 연결됐을 수 있는 엔진과
                 * 그룹 세션 참조를 모두 제거한다.
                 *
                 * 그룹 세션의 실제 네이티브 리소스는
                 * 이전 엔진 또는 Provider Dispose 과정에서 정리돼야 한다.
                 */
                _playbackEngines.Remove(
                    nvrConfig.NvrNo);

                _playbackGroupSessions.Remove(
                    nvrConfig.NvrNo);
            }

            /*
             * 동일한 로그인 Provider에 연결된 기존 엔진이 있으면
             * 새로 생성하지 않고 재사용한다.
             */
            INvrPlaybackEngine existingEngine;

            if (_playbackEngines.TryGetValue(
                    nvrConfig.NvrNo,
                    out existingEngine))
            {
                if (existingEngine != null)
                {
                    return NvrResult<INvrPlaybackEngine>.Ok(
                        existingEngine,
                        "기존 NVR 재생 엔진을 재사용합니다.");
                }

                /*
                 * null 엔진이 Dictionary에 남아 있으면 제거한다.
                 */
                _playbackEngines.Remove(
                    nvrConfig.NvrNo);
            }

            /*
             * 모든 Provider가 고수준 그룹 재생 엔진을
             * 지원하는 것은 아니다.
             *
             * INvrPlaybackEngineProvider를 구현한 Provider만
             * 새로운 그룹 재생 구조를 사용할 수 있다.
             */
            INvrPlaybackEngineProvider engineProvider =
                provider as INvrPlaybackEngineProvider;

            if (engineProvider == null)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.NotSupported,
                    "현재 NVR Provider는 고수준 그룹 재생 엔진을 "
                    + "지원하지 않습니다. "
                    + "ProviderKey="
                    + nvrConfig.ProviderKey,
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "NVR_PLAYBACK_ENGINE_PROVIDER_NOT_IMPLEMENTED",

                        ErrorMessage =
                            "현재 NVR Provider가 "
                            + "INvrPlaybackEngineProvider를 "
                            + "구현하지 않았습니다.",

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
            }

            try
            {
                NvrResult<INvrPlaybackEngine> engineResult =
                    engineProvider.CreatePlaybackEngine();

                if (engineResult == null)
                {
                    return NvrResult<INvrPlaybackEngine>.Fail(
                        NvrResultStatus.Failed,
                        "NVR 재생 엔진 생성 결과가 없습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "NVR_PLAYBACK_ENGINE_RESULT_EMPTY",

                            ErrorMessage =
                                "NVR 재생 엔진 생성 결과가 없습니다.",

                            Operation =
                                "CreatePlaybackEngine"
                        });
                }

                if (!engineResult.Success
                    || engineResult.Data == null)
                {
                    return NvrResult<INvrPlaybackEngine>.Fail(
                        engineResult.Status,
                        string.IsNullOrWhiteSpace(
                            engineResult.Message)
                            ? "NVR 재생 엔진 생성에 실패했습니다."
                            : engineResult.Message,
                        engineResult.Error);
                }

                INvrPlaybackEngine engine =
                    engineResult.Data;

                /*
                 * 엔진 생성이 완전히 성공한 경우에만
                 * NVR 번호별 재사용 Dictionary에 등록한다.
                 */
                _playbackEngines[nvrConfig.NvrNo] =
                    engine;

                return NvrResult<INvrPlaybackEngine>.Ok(
                    engine,
                    string.IsNullOrWhiteSpace(
                        engineResult.Message)
                        ? "NVR 재생 엔진을 생성했습니다."
                        : engineResult.Message);
            }
            catch (Exception ex)
            {
                /*
                 * 제조사 Provider 내부 엔진 생성 중 발생한 예외를
                 * 공통 NvrResult 실패 형식으로 변환한다.
                 */
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.UnknownError,
                    "NVR 재생 엔진 생성 중 오류가 발생했습니다. "
                    + ex.Message,
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "NVR_PLAYBACK_ENGINE_CREATE_EXCEPTION",

                        ErrorMessage =
                            ex.Message,

                        Operation =
                            "GetOrCreatePlaybackEngine"
                    });
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
        /// Player 재생 요청과 동일 NVR에 속한 채널 목록을
        /// NVR Core 다중채널 재생 그룹 요청으로 변환한다.
        ///
        /// 처리 원칙:
        /// - 하나의 그룹에는 동일한 NVR 번호의 채널만 포함한다.
        /// - 하나의 그룹에는 동일한 ProviderKey의 채널만 포함한다.
        /// - 공통 조회시간에는 채널별 보정값을 적용하지 않는다.
        /// - 채널별 TimeOffsetSeconds는 제조사 엔진에 그대로 전달한다.
        /// - 최초 그룹 준비는 정방향 1배속으로 수행한다.
        /// </summary>
        private static NvrResult<NvrPlaybackGroupRequest> ToNvrPlaybackGroupRequest(
                PlayerPlaybackRequest request,
                int nvrNo,
                IList<PlayerChannelTarget> channels,
                DateTime initialTime)
        {
            if (request == null)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "Player 재생 요청 정보가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "PLAYER_PLAYBACK_REQUEST_REQUIRED",

                        ErrorMessage = "Player 재생 요청 정보가 없습니다.",

                        Operation = "ToNvrPlaybackGroupRequest"
                    });
            }

            if (nvrNo <= 0)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "NVR 번호가 올바르지 않습니다. "
                    + "NvrNo="
                    + nvrNo,
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "INVALID_NVR_NO",

                        ErrorMessage =
                            "NVR 번호가 올바르지 않습니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            if (request.PlayStartTime
                >= request.PlayEndTime)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "INVALID_PLAYBACK_RANGE",

                        ErrorMessage =
                            "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            /*
             * 최초 준비 위치는 조회 범위 안이어야 한다. 
             * 시작시간은 포함하고 종료시간은 포함하지 않는다.
             */
            if (initialTime
                < request.PlayStartTime)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "최초 재생시간은 조회 시작시간보다 이전일 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "PLAYBACK_GROUP_INITIAL_BEFORE_START",

                        ErrorMessage =
                            "최초 재생시간은 조회 시작시간보다 이전일 수 없습니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            if (initialTime
                >= request.PlayEndTime)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "최초 재생시간은 조회 종료시간보다 이전이어야 합니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "PLAYBACK_GROUP_INITIAL_AFTER_END",

                        ErrorMessage =
                            "최초 재생시간은 조회 종료시간보다 이전이어야 합니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            if (channels == null
                || channels.Count == 0)
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "재생 그룹에 포함할 채널이 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "PLAYBACK_GROUP_CHANNEL_REQUIRED",

                        ErrorMessage =
                            "재생 그룹에 포함할 채널이 없습니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            PlayerChannelTarget firstChannel =
                null;

            foreach (PlayerChannelTarget channel in channels)
            {
                if (channel != null)
                {
                    firstChannel =
                        channel;

                    break;
                }
            }

            if (firstChannel == null || firstChannel.NvrConfig == null)
    {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.Failed,
                    "재생 그룹의 기준 NVR 설정 정보가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "PLAYBACK_GROUP_NVR_CONFIG_REQUIRED",

                        ErrorMessage =
                            "재생 그룹의 기준 NVR 설정 정보가 없습니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            string providerKey =
                firstChannel.NvrConfig.ProviderKey;

            if (string.IsNullOrWhiteSpace(
                    providerKey))
            {
                return NvrResult<NvrPlaybackGroupRequest>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "재생 그룹의 ProviderKey가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "PLAYBACK_GROUP_PROVIDER_KEY_REQUIRED",

                        ErrorMessage =
                            "재생 그룹의 ProviderKey가 없습니다.",

                        Operation =
                            "ToNvrPlaybackGroupRequest"
                    });
            }

            var screenPositions =
                new HashSet<int>();

            var channelNumbers =
                new HashSet<int>();

            var groupRequest =
                new NvrPlaybackGroupRequest
                {
                    CounterNo =
                        request.CounterNo,

                    NvrNo =
                        nvrNo,

                    ProviderKey =
                        providerKey,

                    SearchDateTime =
                        request.SearchDateTime,

                    /*
                     * 그룹 전체의 공통 조회 범위다.
                     *
                     * 여기에는 채널별 TimeOffsetSeconds를 적용하지 않는다.
                     * 제조사 엔진이 각 채널의 원본 시각으로 변환한다.
                     */
                    StartTime =
                        request.PlayStartTime,

                    EndTime =
                        request.PlayEndTime,

                    /* 
                     * 그룹 재생 핸들은 전체 조회 구간으로 생성하지만, 
                     * 최초 화면은 호출부에서 전달한 시각으로 준비한다.  
                     * 일반 재생: 
                     * initialTime = request.PlayStartTime 
                     * 타임라인 이동: 
                     * initialTime = 사용자가 선택한 시각 
                     */
                    InitialTime = initialTime,

                    /*
                     * 최초 준비와 채널 동기화는
                     * 정방향 1배속을 기준으로 수행한다.
                     */
                    InitialDirection =
                        NvrPlaybackDirection.Forward,

                    InitialSpeed =
                        NvrPlaybackSpeed.Normal
                };

            foreach (PlayerChannelTarget channel
                in channels)
            {
                if (channel == null)
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.Failed,
                        "재생 그룹에 null 채널이 포함되어 있습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "PLAYBACK_GROUP_CHANNEL_NULL",

                            ErrorMessage =
                                "재생 그룹에 null 채널이 포함되어 있습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                if (channel.NvrConfig == null)
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.Failed,
                        "채널의 NVR 설정 정보가 없습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "PLAYBACK_GROUP_CHANNEL_CONFIG_REQUIRED",

                            ErrorMessage =
                                "채널의 NVR 설정 정보가 없습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                /*
                 * 호출부에서 NVR 번호별로 그룹화하더라도
                 * 잘못된 채널이 섞이는 상황을 방어적으로 검사한다.
                 */
                if (channel.NvrNo != nvrNo
                    || channel.NvrConfig.NvrNo != nvrNo)
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.Failed,
                        "서로 다른 NVR의 채널을 하나의 그룹에 "
                        + "포함할 수 없습니다. "
                        + "GroupNvrNo="
                        + nvrNo
                        + ", ChannelNvrNo="
                        + channel.NvrNo
                        + ", ConfigNvrNo="
                        + channel.NvrConfig.NvrNo,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "PLAYBACK_GROUP_NVR_MISMATCH",

                            ErrorMessage =
                                "서로 다른 NVR의 채널이 "
                                + "하나의 그룹에 포함되었습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                if (!string.Equals(
                        providerKey,
                        channel.NvrConfig.ProviderKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.ProviderNotFound,
                        "서로 다른 Provider의 채널을 하나의 그룹에 "
                        + "포함할 수 없습니다. "
                        + "ExpectedProviderKey="
                        + providerKey
                        + ", ActualProviderKey="
                        + channel.NvrConfig.ProviderKey,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "PLAYBACK_GROUP_PROVIDER_MISMATCH",

                            ErrorMessage =
                                "서로 다른 Provider의 채널이 "
                                + "하나의 그룹에 포함되었습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                if (channel.ChannelNo <= 0)
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.InvalidChannel,
                        "NVR 채널번호가 올바르지 않습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "INVALID_PLAYBACK_GROUP_CHANNEL",

                            ErrorMessage =
                                "NVR 채널번호가 올바르지 않습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                int screenPosition =
                    (int)channel.ScreenPosition;

                if (!screenPositions.Add(
                        screenPosition))
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.Failed,
                        "같은 화면 위치의 채널이 중복되었습니다. "
                        + "ScreenPosition="
                        + screenPosition,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "DUPLICATE_PLAYBACK_SCREEN_POSITION",

                            ErrorMessage =
                                "같은 화면 위치의 채널이 중복되었습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                if (!channelNumbers.Add(
                        channel.ChannelNo))
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.InvalidChannel,
                        "같은 NVR 채널번호가 중복되었습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "DUPLICATE_PLAYBACK_CHANNEL",

                            ErrorMessage =
                                "같은 NVR 채널번호가 중복되었습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                if (channel.OutputHandle
                    == IntPtr.Zero)
                {
                    return NvrResult<NvrPlaybackGroupRequest>.Fail(
                        NvrResultStatus.Failed,
                        "영상 출력 대상 Handle이 없습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "PLAYBACK_RENDER_HANDLE_REQUIRED",

                            ErrorMessage =
                                "영상 출력 대상 Handle이 없습니다.",

                            Operation =
                                "ToNvrPlaybackGroupRequest"
                        });
                }

                groupRequest.Channels.Add(
                    new NvrPlaybackGroupChannelRequest
                    {
                        ChannelNo =
                            channel.ChannelNo,

                        ScreenPosition =
                            screenPosition,

                        RenderTargetHandle =
                            channel.OutputHandle,

                        /*
                         * 시간 보정값은 여기서 계산하거나 적용하지 않는다.
                         * 제조사 엔진이 공통 시각과 Provider 원본 시각을
                         * 상호 변환할 때 사용한다.
                         */
                        TimeOffsetSeconds =
                            channel.TimeOffsetSeconds
                    });
            }

            return NvrResult<NvrPlaybackGroupRequest>.Ok(groupRequest, "NVR 다중채널 재생 그룹 요청을 생성했습니다.");
        }


        /// <summary>
        /// NVR 실패 로그에 기록할 진단 정보를 생성한다.
        ///
        /// 비밀번호, 사용자 ID, 인증 토큰은 기록하지 않는다.
        ///
        /// 그룹 재생 구조에서는 기존 INvrPlaybackSession을 사용하지 않고
        /// 전달받은 NVR 설정과 채널 설정만으로 진단 정보를 구성한다.
        /// </summary>
        private string BuildNvrLogDetails(
            NvrConfig nvrConfig,
            PlayerChannelTarget channel,
            INvrProvider provider,
            NvrResult nvrResult,
            string additionalDetails)
        {
            /*
             * 호출부에서 NVR 설정을 직접 전달하지 않았더라도
             * 채널에 연결된 NVR 설정을 사용할 수 있다.
             */
            NvrConfig resolvedConfig =
                nvrConfig;

            if (resolvedConfig == null
                && channel != null)
            {
                resolvedConfig =
                    channel.NvrConfig;
            }

            var details =
                new List<string>();

            /*
             * 채널 정보가 있으면 채널 값을 우선 사용하고,
             * 채널 정보가 없으면 NVR 설정의 NvrNo를 사용한다.
             */
            int? nvrNo =
                channel != null
                    ? (int?)channel.NvrNo
                    : resolvedConfig != null
                        ? (int?)resolvedConfig.NvrNo
                        : null;

            int? channelNo =
                channel != null
                    ? (int?)channel.ChannelNo
                    : null;

            int? screenPosition =
                channel != null
                    ? (int?)channel.ScreenPosition
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
            if (string.IsNullOrWhiteSpace(
                    providerKey)
                && provider != null
                && provider.Metadata != null)
            {
                providerKey =
                    provider.Metadata.ProviderKey;
            }

            details.Add(
                "ProviderKey="
                + (
                    string.IsNullOrWhiteSpace(
                        providerKey)
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

            if (!string.IsNullOrWhiteSpace(
                    additionalDetails))
            {
                details.Add(
                    additionalDetails);
            }

            return string.Join(
                ", ",
                details);
        }

        /// <summary>
        /// Provider 또는 NVR 명령 실패를
        /// 상세 진단 정보와 함께 로그에 기록한다.
        ///
        /// 그룹 재생 구조에서는 개별 재생 세션을 전달하지 않는다.
        /// </summary>
        private void WriteNvrFailureLog(
            string operationName,
            NvrConfig nvrConfig,
            PlayerChannelTarget channel,
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
        /// 현재 선택된 재생속도를 변경한다.
        ///
        /// 정책:
        /// - 재생 그룹이 없으면 선택값만 저장한다.
        /// - Playing 상태에서 변경하면 Playing을 유지한다.
        /// - Rewinding 상태에서 변경하면 Rewinding을 유지한다.
        /// - Paused 상태에서 변경하면 Paused를 유지한다.
        /// - 속도 변경만으로 재생 방향이나 상태를 변경하지 않는다.
        /// - 일부 NVR 그룹 실패 시 전체 그룹을 이전 속도로 복원한다.
        /// - 정방향 1배속으로 변경한 경우에만 그룹 동기화를 실행한다.
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
                    "PLAYBACK_SPEED_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (!Enum.IsDefined(
                    typeof(PlaybackSpeed),
                    speed))
            {
                return PlayerPlaybackResult.Fail(
                    "지원하지 않는 재생속도입니다. "
                    + "Speed="
                    + speed,
                    "PLAYBACK_SPEED_INVALID",
                    PlaybackFailureCategory.Configuration);
            }

            /*
             * 아직 재생 그룹이 없으면 실제 NVR 명령을 보내지 않고
             * 다음 재생에 사용할 선택값만 저장한다.
             */
            if (_playbackGroupSessions.Count == 0)
            {
                _currentSpeed =
                    speed;

                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(
                        speed)
                    + "으로 설정되었습니다. "
                    + "다음 재생부터 적용됩니다.");
            }

            if (CurrentState != PlaybackState.Playing
                && CurrentState != PlaybackState.Rewinding
                && CurrentState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 속도를 변경할 수 없습니다. "
                    + "State="
                    + CurrentState,
                    "PLAYBACK_SPEED_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            /*
             * 동일한 속도라면 제조사 SDK 명령을 다시 호출하지 않는다.
             */
            if (_currentSpeed == speed)
            {
                return PlayerPlaybackResult.Ok(
                    "이미 "
                    + GetPlaybackSpeedText(
                        speed)
                    + "으로 설정되어 있습니다.");
            }

            PlaybackSpeed previousSpeed =
                _currentSpeed;

            PlaybackState stateBeforeChange =
                CurrentState;

            /*
             * 기존 속도가 적용된 상태에서 실제 영상재생시간을 먼저 확보한다.
             *
             * 속도 변경 후 기존 경과시간 기준을 그대로 사용하면
             * 이전 속도와 새 속도의 계산 구간이 섞일 수 있다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생속도 변경 요청이 취소되었습니다.",
                    "PLAYBACK_SPEED_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            DateTime playbackTimeBeforeChange =
                actualPlaybackTime.HasValue
                    ? actualPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            playbackTimeBeforeChange =
                ClampPlaybackTime(
                    playbackTimeBeforeChange);

            /*
             * 모든 NVR 그룹에 새 속도를 적용한다.
             *
             * ApplyPlaybackSpeedToGroupsAsync는
             * 제조사 엔진의 SetSpeedAsync를 호출한다.
             */
            PlayerPlaybackResult applyResult =
                await ApplyPlaybackSpeedToGroupsAsync(
                    speed,
                    cancellationToken);

            if (applyResult == null
                || !applyResult.Success)
            {
                /*
                 * 일부 NVR 그룹만 새 속도로 변경됐을 수 있으므로
                 * 모든 그룹에 이전 속도를 다시 적용한다.
                 */
                PlayerPlaybackResult rollbackResult =
                    await ApplyPlaybackSpeedToGroupsAsync(
                        previousSpeed,
                        CancellationToken.None);

                if (rollbackResult == null
                    || !rollbackResult.Success)
                {
                    /*
                     * NVR 그룹별 속도가 서로 달라진 상태를
                     * 계속 재생하도록 두지 않는다.
                     */
                    await StopCurrentPlaybackGroupsOnlyAsync();

                    _currentRequest =
                        null;

                    _currentPlaybackTime =
                        null;

                    _playbackClockStartedAtUtc =
                        null;

                    _currentSpeed =
                        previousSpeed;

                    CurrentState =
                        PlaybackState.Stopped;

                    return PlayerPlaybackResult.Fail(
                        "일부 NVR 그룹의 재생속도 변경 실패 후 "
                        + "이전 속도를 복원하지 못해 재생을 중지했습니다.",
                        "PLAYBACK_SPEED_ROLLBACK_FAILED",
                        PlaybackFailureCategory.System);
                }

                /*
                 * 실제 NVR 속도가 이전 값으로 복원됐으므로
                 * 서비스 시간과 상태도 변경 전 기준으로 되돌린다.
                 */
                _currentSpeed =
                    previousSpeed;

                _currentPlaybackTime =
                    playbackTimeBeforeChange;

                CurrentState =
                    stateBeforeChange;

                _playbackClockStartedAtUtc =
                    stateBeforeChange == PlaybackState.Playing
                    || stateBeforeChange == PlaybackState.Rewinding
                        ? (DateTime?)DateTime.UtcNow
                        : null;

                return applyResult
                    ?? PlayerPlaybackResult.Fail(
                        "NVR 그룹 재생속도 변경 결과가 없습니다.",
                        "PLAYBACK_GROUP_SPEED_RESULT_EMPTY",
                        PlaybackFailureCategory.System);
            }

            /*
             * 모든 제조사 그룹에 새 속도가 적용된 이후에만
             * 공통 서비스의 속도와 시간 기준을 변경한다.
             */
            _currentSpeed =
                speed;

            _currentPlaybackTime =
                playbackTimeBeforeChange;

            CurrentState =
                stateBeforeChange;

            _playbackClockStartedAtUtc =
                stateBeforeChange == PlaybackState.Playing
                || stateBeforeChange == PlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            /*
             * 0.5·2·4·8배속에서는 자동 동기화를 실행하지 않는다.
             */
            if (speed != PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Ok(
                    GetPlaybackSpeedText(
                        speed)
                    + "으로 재생속도를 변경했습니다. "
                    + "배속 재생 중에는 자동 영상 동기화를 실행하지 않습니다.");
            }

            /*
             * 역방향 1배속으로 변경한 경우에도
             * 현재 Synchronizer는 정방향만 지원하므로 동기화하지 않는다.
             */
            bool isForwardDirection =
                stateBeforeChange == PlaybackState.Playing
                || (
                    stateBeforeChange == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Playing
                );

            if (!isForwardDirection)
            {
                return PlayerPlaybackResult.Ok(
                    "1배속으로 재생속도를 변경했습니다. "
                    + "역재생 상태에서는 자동 영상 동기화를 실행하지 않습니다.");
            }

            /*
             * 정방향 1배속으로 복귀한 경우
             * 제조사별 그룹 동기화를 실행한다.
             */
            PlayerPlaybackResult synchronizeResult =
                await SynchronizePlaybackGroupsAfterNormalSpeedAsync(
                    stateBeforeChange,
                    cancellationToken);

            if (synchronizeResult == null)
            {
                synchronizeResult =
                    PlayerPlaybackResult.Ok(
                        "재생속도는 1배속으로 변경됐지만 "
                        + "그룹 동기화 결과가 없습니다.");
            }

            /*
             * 동기화 과정에서 실제 재생 위치가 조금 변경될 수 있으므로
             * 완료 후 기준 그룹의 OSD 시간을 다시 확인한다.
             */
            DateTime? synchronizedPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    CancellationToken.None);

            if (synchronizedPlaybackTime.HasValue)
            {
                _currentPlaybackTime =
                    ClampPlaybackTime(
                        synchronizedPlaybackTime.Value);
            }

            CurrentState =
                stateBeforeChange;

            _playbackClockStartedAtUtc =
                stateBeforeChange == PlaybackState.Playing
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            return PlayerPlaybackResult.Ok(
                "1배속으로 재생속도를 변경했습니다. "
                + synchronizeResult.Message);
        }

        /// <summary>
        /// 기준 NVR 그룹의 실제 영상재생시간을 서비스 시간에 반영한다.
        ///
        /// 실제 그룹 재생시간을 확인하지 못하면
        /// 서비스의 경과시간 기준 추정값을 반환한다.
        /// </summary>
        public async Task<DateTime?> SyncPlaybackTimeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_currentRequest == null)
            {
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            /*
             * 그룹 재생이 아직 준비되지 않았다면
             * Provider 조회 없이 서비스 추정시간을 반환한다.
             */
            if (_playbackGroupSessions.Count == 0)
            {
                return GetEstimatedPlaybackTime();
            }

            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (!actualPlaybackTime.HasValue)
            {
                return GetEstimatedPlaybackTime();
            }

            DateTime synchronizedTime =
                ClampPlaybackTime(
                    actualPlaybackTime.Value);

            /*
             * 실제 시간을 새 시간 기준점으로 저장한다.
             *
             * 이후 UI에서 CurrentPlaybackTime을 조회하면
             * 이 시각과 현재 속도, 경과시간을 기준으로 계산한다.
             */
            _currentPlaybackTime =
                synchronizedTime;

            if (CurrentState == PlaybackState.Playing
                || CurrentState == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;
            }
            else
            {
                _playbackClockStartedAtUtc =
                    null;
            }

            return synchronizedTime;
        }

        /// <summary>
        /// 타임라인에서 선택한 절대 영상 시각으로 이동한다.
        ///
        /// 운영 정책:
        /// - 타임라인은 모든 배속에서 사용할 수 있다.
        /// - 이동 후 재생속도는 1배속으로 통일한다.
        /// - 전체 조회 시작·종료 범위는 변경하지 않는다.
        /// - 새 그룹은 선택한 시각에서 Paused 상태로 준비한다.
        /// - 이동 전 방향과 재생 상태는 복원한다.
        /// </summary>
        public async Task<PlayerPlaybackResult>
            SeekTimelineToTimeAsync(
                DateTime targetTime,
                CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "타임라인 이동 요청이 취소되었습니다.",
                    "TIMELINE_SEEK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "타임라인에서 이동할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            /*
             * 조회 시작시간은 포함한다.
             */
            if (targetTime
                < _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간보다 이전으로 이동할 수 없습니다.",
                    "SEEK_BEFORE_START",
                    PlaybackFailureCategory.Configuration);
            }

            /*
             * 조회 종료시간은 재생 범위에 포함하지 않는다.
             */
            if (targetTime
                >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 종료시간 이후로 이동할 수 없습니다.",
                    "SEEK_AFTER_END",
                    PlaybackFailureCategory.Configuration);
            }

            PlaybackState stateBeforeSeek =
                CurrentState;

            if (stateBeforeSeek != PlaybackState.Playing
                && stateBeforeSeek != PlaybackState.Rewinding
                && stateBeforeSeek != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 타임라인 이동을 실행할 수 없습니다. "
                    + "State="
                    + stateBeforeSeek,
                    "TIMELINE_SEEK_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            /*
             * 일시정지 상태에서는 _pausedFromState를 기준으로
             * 실제 재생 방향을 판단한다.
             */
            bool wasReverse =
                stateBeforeSeek == PlaybackState.Rewinding
                || (
                    stateBeforeSeek == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Rewinding
                );

            bool keepPaused =
                stateBeforeSeek == PlaybackState.Paused;

            bool changedToNormalSpeed =
                _currentSpeed != PlaybackSpeed.Normal;

            PlayerPlaybackRequest currentRequest =
                _currentRequest;

            /*
             * 역방향 상태에서 조회 시작시간으로 이동하면
             * 더 이전으로 재생할 시간이 없으므로 실행하지 않는다.
             *
             * 기존 그룹을 정리하기 전에 검사해야 한다.
             */
            if (wasReverse
                && targetTime
                    <= currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간에서는 역재생을 시작할 수 없습니다.",
                    "REWIND_BEFORE_START",
                    PlaybackFailureCategory.NotSupported);
            }

            /*
             * 타임라인 이동은 기존 배속 그룹에
             * 1배속 명령과 Seek 명령을 순차적으로 보내지 않는다.
             *
             * 기존 그룹을 완전히 정리하고,
             * 선택 시각에서 새 정방향 1배속 그룹을 준비한다.
             *
             * Provider 로그인과 재생 엔진은 유지한다.
             */
            PlayerPlaybackResult stopResult =
                await StopCurrentPlaybackGroupsOnlyAsync();

            if (stopResult == null
                || !stopResult.Success)
            {
                return stopResult
                    ?? PlayerPlaybackResult.Fail(
                        "기존 NVR 재생 그룹 정리 결과가 없습니다.",
                        "TIMELINE_SEEK_STOP_RESULT_EMPTY",
                        PlaybackFailureCategory.System);
            }

            /*
             * 새 그룹은 항상 1배속으로 생성된다.
             */
            _currentSpeed =
                PlaybackSpeed.Normal;

            _currentPlaybackTime =
                null;

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            /*
             * startPlayback=false:
             *
             * targetTime에서 그룹을 준비하지만
             * OpenAsync가 반환한 Paused 상태를 유지한다.
             */
            PlayerPlaybackResult openResult =
                await OpenAndStartPlaybackGroupsAsync(
                    currentRequest,
                    targetTime,
                    false,
                    cancellationToken);

            if (openResult == null
                || !openResult.Success)
            {
                await StopCurrentPlaybackGroupsOnlyAsync();

                _currentPlaybackTime =
                    null;

                _playbackClockStartedAtUtc =
                    null;

                CurrentState =
                    PlaybackState.Stopped;

                /*
                 * 타임라인 이동 시도 이후에는
                 * 선택 속도를 1배속으로 유지한다.
                 */
                _currentSpeed =
                    PlaybackSpeed.Normal;

                return openResult
                    ?? PlayerPlaybackResult.Fail(
                        "타임라인 위치에서 NVR 그룹을 준비한 결과가 없습니다.",
                        "TIMELINE_SEEK_OPEN_RESULT_EMPTY",
                        PlaybackFailureCategory.System);
            }

            /*
             * OpenAsync 성공 시 제조사 그룹의 실제 상태는
             * 정방향·1배속·Paused이다.
             */
            _currentPlaybackTime =
                targetTime;

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Paused;

            _pausedFromState =
                PlaybackState.Playing;

            /*
             * 이동 전 재생 방향이 역방향이었다면
             * Paused 상태에서 방향만 Reverse로 변경한다.
             *
             * ChangePlaybackGroupDirectionAsync는
             * Paused 상태를 유지하고 _pausedFromState를
             * Rewinding으로 변경한다.
             */
            if (wasReverse)
            {
                PlayerPlaybackResult directionResult =
                    await ChangePlaybackGroupDirectionAsync(
                        NvrPlaybackDirection.Reverse,
                        cancellationToken);

                if (directionResult == null
                    || !directionResult.Success)
                {
                    await StopCurrentPlaybackGroupsOnlyAsync();

                    _currentPlaybackTime =
                        null;

                    _playbackClockStartedAtUtc =
                        null;

                    CurrentState =
                        PlaybackState.Stopped;

                    _currentSpeed =
                        PlaybackSpeed.Normal;

                    return directionResult
                        ?? PlayerPlaybackResult.Fail(
                            "타임라인 이동 후 역방향 전환 결과가 없습니다.",
                            "TIMELINE_REVERSE_RESULT_EMPTY",
                            PlaybackFailureCategory.System);
                }
            }

            /*
             * 이동 전 상태가 Playing 또는 Rewinding이었다면
             * 선택한 위치에서 재생을 다시 시작한다.
             *
             * 이동 전 상태가 Paused였다면 Resume하지 않고
             * 일시정지 상태를 그대로 유지한다.
             */
            if (!keepPaused)
            {
                PlayerPlaybackResult resumeResult =
                    await ResumeAsync(
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    await StopCurrentPlaybackGroupsOnlyAsync();

                    _currentPlaybackTime =
                        null;

                    _playbackClockStartedAtUtc =
                        null;

                    CurrentState =
                        PlaybackState.Stopped;

                    _currentSpeed =
                        PlaybackSpeed.Normal;

                    return resumeResult
                        ?? PlayerPlaybackResult.Fail(
                            "타임라인 이동 후 재생 재개 결과가 없습니다.",
                            "TIMELINE_RESUME_RESULT_EMPTY",
                            PlaybackFailureCategory.System);
                }
            }

            /*
             * 그룹 준비와 방향·상태 복원이 끝난 뒤
             * 실제 제조사 OSD 시간을 서비스 기준시간으로 반영한다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    CancellationToken.None);

            _currentPlaybackTime =
                actualPlaybackTime.HasValue
                    ? ClampPlaybackTime(
                        actualPlaybackTime.Value)
                    : targetTime;

            if (CurrentState == PlaybackState.Playing
                || CurrentState == PlaybackState.Rewinding)
            {
                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;
            }
            else
            {
                _playbackClockStartedAtUtc =
                    null;
            }

            string speedMessage =
                changedToNormalSpeed
                    ? "재생속도를 1배속으로 전환했습니다. "
                    : string.Empty;

            string directionMessage =
                wasReverse
                    ? "역재생 방향을 유지했습니다. "
                    : "정방향 재생을 유지했습니다. ";

            string stateMessage =
                keepPaused
                    ? "일시정지 상태를 유지했습니다."
                    : "재생을 계속합니다.";

            return PlayerPlaybackResult.Ok(
                speedMessage
                + "영상재생시간을 "
                + _currentPlaybackTime.Value.ToString(
                    "yyyy-MM-dd HH:mm:ss")
                + "으로 이동했습니다. "
                + directionMessage
                + stateMessage);
        }

        /// <summary>
        /// 모든 NVR 재생 그룹을 지정한 공통 영상재생시간으로 이동한다.
        ///
        /// 정책:
        /// - 정방향 재생에서만 허용한다.
        /// - 1배속에서만 허용한다.
        /// - Playing 또는 Paused 상태에서만 허용한다.
        /// - 이동 전 재생 상태를 유지한다.
        /// - 일부 NVR 그룹 실패 시 전체 그룹을 이전 시각으로 복원한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 NVR 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_SESSION_EMPTY",
                    PlaybackFailureCategory.System);
            }

            /*
             * 현재 제조사 그룹 Seek는 정방향 재생만 지원한다.
             *
             * 역재생 중 또는 역재생을 일시정지한 상태에서는
             * 별도의 역방향 세션 재구성 방식이 필요하다.
             */
            bool isReverseState =
                CurrentState == PlaybackState.Rewinding
                || (
                    CurrentState == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Rewinding
                );

            if (isReverseState)
            {
                return PlayerPlaybackResult.Fail(
                    "역재생 상태에서는 10초 이동을 지원하지 않습니다. "
                    + "정방향 재생으로 전환한 뒤 다시 시도해 주세요.",
                    "REVERSE_PLAYBACK_SEEK_NOT_SUPPORTED",
                    PlaybackFailureCategory.NotSupported);
            }

            /*
             * Dahua 정렬 Provider는 현재 1배속 Seek만 보장한다.
             */
            if (_currentSpeed
                != PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 이동은 1배속에서만 사용할 수 있습니다.",
                    "PLAYBACK_SEEK_NORMAL_SPEED_ONLY",
                    PlaybackFailureCategory.NotSupported);
            }

            if (CurrentState != PlaybackState.Playing
                && CurrentState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 영상재생시간을 이동할 수 없습니다. "
                    + "State="
                    + CurrentState,
                    "PLAYBACK_SEEK_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            PlaybackState stateBeforeSeek =
                CurrentState;

            /*
             * Seek 실패 시 원래 위치로 되돌릴 수 있도록
             * 명령 실행 전 실제 영상재생시간을 확보한다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            DateTime playbackTimeBeforeSeek =
                actualPlaybackTime.HasValue
                    ? actualPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            playbackTimeBeforeSeek =
                ClampPlaybackTime(
                    playbackTimeBeforeSeek);

            /*
             * 조회 시작시간은 포함하지만
             * 조회 종료시간은 재생 범위에 포함되지 않는다.
             *
             * 기존 ClampPlaybackTime은 종료시간과 같은 값을 반환할 수 있으므로
             * Seek 전용 상한은 종료시간 1초 전으로 제한한다.
             */
            DateTime latestSeekTime =
                _currentRequest.PlayEndTime.AddSeconds(
                    -1);

            if (latestSeekTime
                < _currentRequest.PlayStartTime)
            {
                latestSeekTime =
                    _currentRequest.PlayStartTime;
            }

            DateTime normalizedTargetTime =
                targetTime;

            if (normalizedTargetTime
                < _currentRequest.PlayStartTime)
            {
                normalizedTargetTime =
                    _currentRequest.PlayStartTime;
            }

            if (normalizedTargetTime
                >= _currentRequest.PlayEndTime)
            {
                normalizedTargetTime =
                    latestSeekTime;
            }

            /*
             * 현재 위치와 목표 위치가 같은 초라면
             * 불필요한 SDK Seek를 실행하지 않는다.
             */
            if (Math.Abs(
                    (
                        normalizedTargetTime
                        - playbackTimeBeforeSeek
                    ).TotalSeconds)
                < 1.0)
            {
                _currentPlaybackTime =
                    normalizedTargetTime;

                _playbackClockStartedAtUtc =
                    stateBeforeSeek == PlaybackState.Playing
                        ? (DateTime?)DateTime.UtcNow
                        : null;

                return PlayerPlaybackResult.Ok(
                    "이미 선택한 영상재생시간에 위치해 있습니다.");
            }

            var warningMessages =
                new List<string>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    bool restored =
                        await RestorePlaybackGroupsAfterSeekFailureAsync(
                            playbackTimeBeforeSeek,
                            stateBeforeSeek);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "영상 이동 취소 후 기존 재생 위치를 "
                            + "복원하지 못해 재생을 중지했습니다.",
                            "PLAYBACK_SEEK_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    RestoreServiceStateAfterSeek(
                        playbackTimeBeforeSeek,
                        stateBeforeSeek);

                    return PlayerPlaybackResult.Fail(
                        "영상 이동 요청이 취소되었습니다.",
                        "PLAYBACK_SEEK_CANCELLED",
                        PlaybackFailureCategory.Cancelled);
                }

                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                INvrPlaybackEngine engine;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    bool restored =
                        await RestorePlaybackGroupsAfterSeekFailureAsync(
                            playbackTimeBeforeSeek,
                            stateBeforeSeek);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }
                    else
                    {
                        RestoreServiceStateAfterSeek(
                            playbackTimeBeforeSeek,
                            stateBeforeSeek);
                    }

                    return PlayerPlaybackResult.Fail(
                        "영상 이동에 필요한 NVR 그룹 또는 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_SEEK_GROUP_INVALID",
                        PlaybackFailureCategory.System);
                }

                NvrResult seekResult =
                    await engine.SeekAsync(
                        groupSession,
                        normalizedTargetTime,
                        cancellationToken);

                if (seekResult == null
                    || !seekResult.Success)
                {
                    /*
                     * 한 NVR 그룹이라도 실패하면
                     * 성공한 그룹과 실패한 그룹 모두 원래 시각으로 되돌린다.
                     *
                     * 실패한 제조사 그룹은 일부 채널만 이동한 뒤
                     * Paused 상태로 남았을 가능성이 있기 때문이다.
                     */
                    bool restored =
                        await RestorePlaybackGroupsAfterSeekFailureAsync(
                            playbackTimeBeforeSeek,
                            stateBeforeSeek);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "일부 NVR 그룹의 영상 이동 실패 후 "
                            + "기존 위치를 복원하지 못해 재생을 중지했습니다.",
                            "PLAYBACK_SEEK_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    RestoreServiceStateAfterSeek(
                        playbackTimeBeforeSeek,
                        stateBeforeSeek);

                    return seekResult == null
                        ? PlayerPlaybackResult.Fail(
                            "NVR 그룹 영상 이동 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_GROUP_SEEK_RESULT_EMPTY",
                            PlaybackFailureCategory.System)
                        : ToPlayerResult(
                            seekResult);
                }

                if (seekResult.Status
                        == NvrResultStatus.PartialSuccess
                    && !string.IsNullOrWhiteSpace(
                        seekResult.Message))
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + seekResult.Message);
                }
            }

            /*
             * 모든 제조사 그룹 이동이 성공한 뒤
             * 공통 서비스의 영상재생시간 기준을 변경한다.
             */
            RestoreServiceStateAfterSeek(
                normalizedTargetTime,
                stateBeforeSeek);

            if (warningMessages.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "영상재생시간을 "
                    + normalizedTargetTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + "으로 이동했지만 일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warningMessages.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "영상재생시간을 "
                + normalizedTargetTime.ToString(
                    "yyyy-MM-dd HH:mm:ss")
                + "으로 이동했습니다.");
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
        /// 화면 위치 값을 표시 문자열로 변환한다.
        /// </summary>
        private static string GetScreenPositionText(int screenPosition)
        {
            if (screenPosition
                == (int)ScreenPosition.Left)
            {
                return "좌측";
            }

            if (screenPosition
                == (int)ScreenPosition.Right)
            {
                return "우측";
            }

            return screenPosition.ToString();
        }

        /// <summary>
        /// 그룹 재생 준비 또는 시작 도중 실패했을 때
        /// 이미 생성된 제조사 재생 그룹을 모두 정리한다.
        ///
        /// 한 그룹의 정리 실패가 다른 그룹 정리를 막지 않도록
        /// 각 그룹을 독립적으로 처리한다.
        /// </summary>
        private async Task CleanupPreparedPlaybackGroupsAsync(
            IList<PreparedPlaybackGroup> preparedGroups)
        {
            if (preparedGroups == null
                || preparedGroups.Count == 0)
            {
                return;
            }

            /*
             * 가장 마지막에 생성된 그룹부터 역순으로 정리한다.
             */
            for (int index = preparedGroups.Count - 1;
                index >= 0;
                index--)
            {
                PreparedPlaybackGroup preparedGroup =
                    preparedGroups[index];

                if (preparedGroup == null
                    || preparedGroup.Engine == null
                    || preparedGroup.Session == null)
                {
                    continue;
                }

                try
                {
                    /*
                     * 그룹 Stop은 반드시 정리가 완료되어야 하므로
                     * 호출자가 전달한 취소 토큰 대신 None을 사용한다.
                     */
                    await preparedGroup.Engine.StopAsync(
                        preparedGroup.Session,
                        CancellationToken.None);
                }
                catch
                {
                    /*
                     * 실패 복구 과정의 예외가
                     * 원래 Open 또는 Start 실패 결과를 덮어쓰지 않게 한다.
                     */
                }

                /*
                 * StopAsync 구현에서 그룹 내부의 채널 세션까지
                 * 정리하므로 여기서 그룹 세션을 별도로 Dispose하지 않는다.
                 *
                 * INvrPlaybackGroupSession은 IDisposable을 요구하지 않는다.
                 */
            }
        }

        /// <summary>
        /// NVR 재생 그룹을 준비하는 동안 임시로 보관하는 정보이다.
        ///
        /// 모든 NVR 그룹이 정상적으로 준비된 경우에만
        /// _playbackGroupSessions에 최종 반영한다.
        ///
        /// 준비 도중 하나라도 실패하면
        /// CleanupPreparedPlaybackGroupsAsync에서 역순으로 정리한다.
        /// </summary>
        private sealed class PreparedPlaybackGroup
        {
            /// <summary>
            /// 그룹이 연결된 NVR 번호.
            /// </summary>
            public int NvrNo { get; set; }

            /// <summary>
            /// 그룹을 생성하고 제어하는 제조사 재생 엔진.
            /// </summary>
            public INvrPlaybackEngine Engine { get; set; }

            /// <summary>
            /// OpenAsync로 준비된 제조사 재생 그룹 세션.
            /// </summary>
            public INvrPlaybackGroupSession Session { get; set; }
        }



        /// <summary>
        /// 현재 실행 중인 제조사별 재생 그룹만 중지하고 정리한다.
        ///
        /// Provider 로그인과 재생 엔진은 유지한다.
        /// 따라서 재조회 시 Provider 초기화와 로그인을 다시 하지 않고
        /// 기존 재생 엔진을 재사용할 수 있다.
        ///
        /// 처리 순서:
        /// 1. 서비스 영상재생시간 고정
        /// 2. 논리 재생 시계 정지
        /// 3. NVR별 그룹 StopAsync 실행
        /// 4. 그룹 세션 Dictionary 초기화
        ///
        /// 일부 그룹 중지에 실패하더라도
        /// 다른 그룹의 정리는 계속 수행한다.
        /// </summary>
        private async Task<PlayerPlaybackResult>
            StopCurrentPlaybackGroupsOnlyAsync()
        {
            /*
             * 정리할 그룹이 없으면 중복 Stop 요청으로 보고
             * 정상 성공 처리한다.
             */
            if (_playbackGroupSessions.Count == 0)
            {
                _playbackClockStartedAtUtc =
                    null;

                CurrentState =
                    PlaybackState.Stopped;

                return PlayerPlaybackResult.Ok(
                    "정리할 NVR 재생 그룹이 없습니다.");
            }

            var cleanupWarnings =
                new List<string>();

            /*
             * Dictionary를 반복하는 중에 원본 컬렉션을 변경하지 않도록
             * 현재 그룹 목록의 복사본을 생성한다.
             */
            List<KeyValuePair<int, INvrPlaybackGroupSession>>
                groupItems =
                    _playbackGroupSessions
                        .ToList();

            /*
             * 실제 그룹 중지 처리 시간이 걸리더라도
             * 서비스 영상재생시간이 계속 증가하거나 감소하지 않도록
             * 먼저 현재 시간을 고정한다.
             */
            if (_currentRequest != null)
            {
                _currentPlaybackTime =
                    ClampPlaybackTime(
                        GetEstimatedPlaybackTime());
            }

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in groupItems)
            {
                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null)
                {
                    cleanupWarnings.Add(
                        "NVR 재생 그룹 세션 정보가 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    cleanupWarnings.Add(
                        "NVR 재생 그룹을 중지할 재생 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo);

                    continue;
                }

                try
                {
                    /*
                     * 그룹 Stop은 리소스 정리 명령이므로
                     * 사용자 취소 여부와 관계없이 완료되어야 한다.
                     */
                    NvrResult stopResult =
                        await engine.StopAsync(
                            groupSession,
                            CancellationToken.None);

                    if (stopResult == null)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 그룹 중지 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo);
                    }
                    else if (!stopResult.Success)
                    {
                        cleanupWarnings.Add(
                            "NVR 재생 그룹 중지에 실패했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Status="
                            + stopResult.Status
                            + ", Message="
                            + (
                                string.IsNullOrWhiteSpace(
                                    stopResult.Message)
                                    ? "-"
                                    : stopResult.Message
                            ));
                    }
                    else if (
                        stopResult.Status
                        == NvrResultStatus.PartialSuccess)
                    {
                        /*
                         * Stop 자체는 완료되었지만
                         * 제조사 엔진 내부에서 일부 정리 경고가 발생한 경우다.
                         */
                        cleanupWarnings.Add(
                            "NVR 재생 그룹은 중지되었지만 "
                            + "일부 정리 경고가 발생했습니다. "
                            + "NvrNo="
                            + nvrNo
                            + ", Message="
                            + stopResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    /*
                     * 한 NVR 그룹의 정리 예외 때문에
                     * 다른 NVR 그룹의 정리가 중단되면 안 된다.
                     */
                    cleanupWarnings.Add(
                        "NVR 재생 그룹 중지 중 예외가 발생했습니다. "
                        + "NvrNo="
                        + nvrNo
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * 제조사 엔진 Stop 성공 여부와 관계없이
             * 공통 서비스가 보유한 그룹 세션 참조는 제거한다.
             *
             * 각 제조사 엔진은 StopAsync 내부에서
             * 실제 채널 세션과 네이티브 핸들을 정리해야 한다.
             */
            _playbackGroupSessions.Clear();

            /*
             * _playbackEngines는 제거하지 않는다.
             *
             * 재생 엔진은 로그인된 Provider에 연결되어 있으므로
             * 다음 조회에서 다시 사용할 수 있다.
             */
            if (cleanupWarnings.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "NVR 재생 그룹은 정리되었지만 "
                    + "일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        cleanupWarnings.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "현재 NVR 재생 그룹을 정리했습니다.");
        }

        /// <summary>
        /// UI 영상재생시간에 사용할 기준 NVR 그룹의
        /// 실제 공통 재생시간을 조회한다.
        ///
        /// 좌측 화면이 속한 NVR 그룹을 우선 사용하고,
        /// 찾을 수 없으면 NVR 번호가 가장 낮은 그룹을 사용한다.
        ///
        /// 실제 시간 조회에 실패하면 null을 반환하며,
        /// 호출부는 기존 추정 시간을 사용할 수 있다.
        /// </summary>
        private async Task<DateTime?>
            GetReferencePlaybackGroupTimeAsync(
                CancellationToken cancellationToken)
        {
            if (_playbackGroupSessions.Count == 0)
            {
                return null;
            }

            int? referenceNvrNo =
                null;

            /*
             * 좌측 화면이 속한 NVR을
             * 영상재생시간 기준 그룹으로 우선 선택한다.
             */
            if (_currentRequest != null
                && _currentRequest.Channels != null)
            {
                PlayerChannelTarget leftChannel =
                    _currentRequest.Channels
                        .FirstOrDefault(
                            channel =>
                                channel != null
                                && channel.ScreenPosition
                                    == ScreenPosition.Left);

                if (leftChannel != null)
                {
                    referenceNvrNo =
                        leftChannel.NvrNo;
                }
            }

            /*
             * 좌측 채널 그룹이 없으면
             * 등록된 첫 번째 NVR 그룹을 사용한다.
             */
            if (!referenceNvrNo.HasValue
                || !_playbackGroupSessions.ContainsKey(
                    referenceNvrNo.Value))
            {
                referenceNvrNo =
                    _playbackGroupSessions.Keys
                        .OrderBy(nvrNo => nvrNo)
                        .Select(nvrNo => (int?)nvrNo)
                        .FirstOrDefault();
            }

            if (!referenceNvrNo.HasValue)
            {
                return null;
            }

            INvrPlaybackGroupSession groupSession;

            if (!_playbackGroupSessions.TryGetValue(
                    referenceNvrNo.Value,
                    out groupSession)
                || groupSession == null)
            {
                return null;
            }

            INvrPlaybackEngine engine;

            if (!_playbackEngines.TryGetValue(
                    referenceNvrNo.Value,
                    out engine)
                || engine == null)
            {
                return null;
            }

            try
            {
                NvrResult<NvrPlaybackGroupStatus> statusResult =
                    await engine.GetStatusAsync(
                        groupSession,
                        cancellationToken);

                /*
                 * PartialSuccess도 Data에 마지막 정상 시간이 포함될 수 있으므로
                 * Success 값과 Data 존재 여부를 기준으로 판단한다.
                 */
                if (statusResult == null
                    || !statusResult.Success
                    || statusResult.Data == null
                    || !statusResult.Data.CurrentPlaybackTime.HasValue)
                {
                    return null;
                }

                return ClampPlaybackTime(
                    statusResult.Data.CurrentPlaybackTime.Value);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                /*
                 * 상태 조회 실패가 Pause 또는 Resume 전체를
                 * 즉시 실패시키지는 않는다.
                 *
                 * 호출부에서 서비스 추정 시간을 사용한다.
                 */
                return null;
            }
        }

        /// <summary>
        /// 여러 NVR 그룹 Pause 도중 실패했을 때
        /// 이미 일시정지된 그룹을 다시 재개한다.
        /// </summary>
        private async Task<bool>
            ResumePausedPlaybackGroupsAsync(
                IList<PreparedPlaybackGroup> pausedGroups)
        {
            if (pausedGroups == null
                || pausedGroups.Count == 0)
            {
                return true;
            }

            bool allSucceeded =
                true;

            for (int index = pausedGroups.Count - 1;
                index >= 0;
                index--)
            {
                PreparedPlaybackGroup group =
                    pausedGroups[index];

                if (group == null
                    || group.Engine == null
                    || group.Session == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult result =
                        await group.Engine.ResumeAsync(
                            group.Session,
                            CancellationToken.None);

                    if (result == null
                        || !result.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// 여러 NVR 그룹 Resume 도중 실패했을 때
        /// 이미 재개된 그룹을 다시 일시정지한다.
        /// </summary>
        private async Task<bool>
            PauseResumedPlaybackGroupsAsync(
                IList<PreparedPlaybackGroup> resumedGroups)
        {
            if (resumedGroups == null
                || resumedGroups.Count == 0)
            {
                return true;
            }

            bool allSucceeded =
                true;

            for (int index = resumedGroups.Count - 1;
                index >= 0;
                index--)
            {
                PreparedPlaybackGroup group =
                    resumedGroups[index];

                if (group == null
                    || group.Engine == null
                    || group.Session == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult result =
                        await group.Engine.PauseAsync(
                            group.Session,
                            CancellationToken.None);

                    if (result == null
                        || !result.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// 여러 NVR 그룹 중 일부 Seek가 실패했을 때
        /// 모든 그룹을 Seek 이전의 공통 영상재생시간과 상태로 복원한다.
        /// </summary>
        private async Task<bool>
            RestorePlaybackGroupsAfterSeekFailureAsync(
                DateTime restoreTime,
                PlaybackState originalState)
        {
            bool allSucceeded =
                true;

            /*
             * 실패한 그룹도 일부 채널이 이미 이동했을 수 있으므로
             * 등록된 모든 그룹에 원래 시각 Seek를 다시 실행한다.
             */
            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                INvrPlaybackEngine engine;
                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult seekResult =
                        await engine.SeekAsync(
                            groupSession,
                            restoreTime,
                            CancellationToken.None);

                    if (seekResult == null
                        || !seekResult.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            /*
             * Seek 실패 시 제조사 엔진은 안전을 위해
             * Paused 상태로 남길 수 있다.
             *
             * 원래 재생 중이었다면 모든 그룹을 다시 Resume하고,
             * 원래 Paused 상태였다면 모든 그룹을 Pause로 통일한다.
             */
            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                INvrPlaybackEngine engine;
                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult stateResult =
                        originalState == PlaybackState.Playing
                            ? await engine.ResumeAsync(
                                groupSession,
                                CancellationToken.None)
                            : await engine.PauseAsync(
                                groupSession,
                                CancellationToken.None);

                    if (stateResult == null
                        || !stateResult.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// Seek 완료 또는 복원 후
        /// 공통 서비스의 영상재생시간과 논리 상태를 갱신한다.
        /// </summary>
        private void RestoreServiceStateAfterSeek(
            DateTime playbackTime,
            PlaybackState state)
        {
            _currentPlaybackTime =
                ClampPlaybackTime(
                    playbackTime);

            CurrentState =
                state;

            _playbackClockStartedAtUtc =
                state == PlaybackState.Playing
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            if (state == PlaybackState.Paused)
            {
                _pausedFromState =
                    PlaybackState.Playing;
            }
        }

        /// <summary>
        /// 현재 실행 중인 모든 NVR 재생 그룹의 방향을 변경한다.
        ///
        /// 제조사 엔진은 다음 작업을 내부적으로 처리한다.
        /// - 현재 실제 영상재생시간 확인
        /// - 필요 시 기존 재생 세션 일시정지
        /// - 새 방향 세션 생성
        /// - 기존 속도 재적용
        /// - 기존 Playing 또는 Paused 상태 복원
        ///
        /// 여러 NVR 그룹 중 하나라도 실패하면
        /// 모든 그룹을 이전 방향과 이전 상태로 복원한다.
        /// </summary>
        private async Task<PlayerPlaybackResult>
            ChangePlaybackGroupDirectionAsync(
                NvrPlaybackDirection targetDirection,
                CancellationToken cancellationToken)
        {
            PlaybackState originalState =
                CurrentState;

            if (originalState != PlaybackState.Playing
                && originalState != PlaybackState.Rewinding
                && originalState != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 방향을 변경할 수 없습니다. "
                    + "State="
                    + originalState,
                    "PLAYBACK_DIRECTION_INVALID_STATE",
                    PlaybackFailureCategory.System);
            }

            NvrPlaybackDirection previousDirection =
                originalState == PlaybackState.Rewinding
                || (
                    originalState == PlaybackState.Paused
                    && _pausedFromState
                        == PlaybackState.Rewinding
                )
                    ? NvrPlaybackDirection.Reverse
                    : NvrPlaybackDirection.Forward;

            if (previousDirection == targetDirection)
            {
                return PlayerPlaybackResult.Ok(
                    "이미 요청한 재생 방향으로 설정되어 있습니다.");
            }

            /*
             * 공통 서비스의 영상재생시간 기준을 갱신하기 위해
             * 방향 변경 직전 실제 OSD 시간을 먼저 확인한다.
             *
             * 각 제조사 엔진도 내부에서 실제 시간을 다시 확인하므로
             * 이 값은 서비스 UI 시계 갱신 용도로 사용한다.
             */
            DateTime? actualPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 방향 변경 요청이 취소되었습니다.",
                    "PLAYBACK_DIRECTION_CANCELLED",
                    PlaybackFailureCategory.Cancelled);
            }

            DateTime directionChangeTime =
                actualPlaybackTime.HasValue
                    ? actualPlaybackTime.Value
                    : GetEstimatedPlaybackTime();

            directionChangeTime =
                ClampPlaybackTime(
                    directionChangeTime);

            if (targetDirection
                    == NvrPlaybackDirection.Reverse
                && directionChangeTime
                    <= _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간보다 이전으로 역재생할 수 없습니다.",
                    "REWIND_BEFORE_START",
                    PlaybackFailureCategory.NotSupported);
            }

            if (targetDirection
                    == NvrPlaybackDirection.Forward
                && directionChangeTime
                    >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 종료시간 이후로 정방향 재생을 시작할 수 없습니다.",
                    "FORWARD_AFTER_END",
                    PlaybackFailureCategory.NotSupported);
            }

            var warningMessages =
                new List<string>();

            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    bool restored =
                        await RestorePlaybackGroupsAfterDirectionFailureAsync(
                            previousDirection,
                            originalState);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "방향 변경 취소 후 기존 방향을 복원하지 못해 "
                            + "재생을 중지했습니다.",
                            "PLAYBACK_DIRECTION_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    ApplyServiceDirectionState(
                        directionChangeTime,
                        originalState,
                        previousDirection);

                    return PlayerPlaybackResult.Fail(
                        "재생 방향 변경 요청이 취소되었습니다.",
                        "PLAYBACK_DIRECTION_CANCELLED",
                        PlaybackFailureCategory.Cancelled);
                }

                int nvrNo =
                    item.Key;

                INvrPlaybackGroupSession groupSession =
                    item.Value;

                INvrPlaybackEngine engine;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        nvrNo,
                        out engine)
                    || engine == null)
                {
                    bool restored =
                        await RestorePlaybackGroupsAfterDirectionFailureAsync(
                            previousDirection,
                            originalState);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();
                    }
                    else
                    {
                        ApplyServiceDirectionState(
                            directionChangeTime,
                            originalState,
                            previousDirection);
                    }

                    return PlayerPlaybackResult.Fail(
                        "방향 변경에 필요한 NVR 그룹 또는 엔진이 없습니다. "
                        + "NvrNo="
                        + nvrNo,
                        "PLAYBACK_DIRECTION_GROUP_INVALID",
                        PlaybackFailureCategory.System);
                }

                NvrResult directionResult =
                    await engine.SetDirectionAsync(
                        groupSession,
                        targetDirection,
                        cancellationToken);

                if (directionResult == null
                    || !directionResult.Success)
                {
                    /*
                     * 실패한 제조사 엔진도 세션 전환 도중
                     * Paused 또는 새 방향 상태가 되었을 수 있다.
                     *
                     * 따라서 성공한 그룹만이 아니라
                     * 현재 등록된 전체 그룹의 방향을 복원한다.
                     */
                    bool restored =
                        await RestorePlaybackGroupsAfterDirectionFailureAsync(
                            previousDirection,
                            originalState);

                    if (!restored)
                    {
                        await StopCurrentPlaybackGroupsOnlyAsync();

                        return PlayerPlaybackResult.Fail(
                            "일부 NVR 그룹의 방향 변경 실패 후 "
                            + "기존 상태를 복원하지 못해 재생을 중지했습니다.",
                            "PLAYBACK_DIRECTION_ROLLBACK_FAILED",
                            PlaybackFailureCategory.System);
                    }

                    ApplyServiceDirectionState(
                        directionChangeTime,
                        originalState,
                        previousDirection);

                    return directionResult == null
                        ? PlayerPlaybackResult.Fail(
                            "NVR 재생 방향 변경 결과가 없습니다. "
                            + "NvrNo="
                            + nvrNo,
                            "PLAYBACK_DIRECTION_RESULT_EMPTY",
                            PlaybackFailureCategory.System)
                        : ToPlayerResult(
                            directionResult);
                }

                if (directionResult.Status
                        == NvrResultStatus.PartialSuccess
                    && !string.IsNullOrWhiteSpace(
                        directionResult.Message))
                {
                    warningMessages.Add(
                        "NvrNo="
                        + nvrNo
                        + ": "
                        + directionResult.Message);
                }
            }

            /*
             * 방향 전환 후 기준 그룹의 실제 시간을 다시 확인한다.
             *
             * 새 세션 생성 과정에서 실제 위치가 약간 달라졌을 수 있으므로
             * 가능하면 변경 후 OSD 시간을 서비스 기준으로 사용한다.
             */
            DateTime? changedPlaybackTime =
                await GetReferencePlaybackGroupTimeAsync(
                    CancellationToken.None);

            DateTime finalPlaybackTime =
                changedPlaybackTime.HasValue
                    ? changedPlaybackTime.Value
                    : directionChangeTime;

            ApplyServiceDirectionState(
                finalPlaybackTime,
                originalState,
                targetDirection);

            string directionText =
                targetDirection
                    == NvrPlaybackDirection.Reverse
                        ? "역방향"
                        : "정방향";

            string stateText;

            if (originalState
                == PlaybackState.Paused)
            {
                stateText =
                    "일시정지 상태를 유지했습니다.";
            }
            else
            {
                stateText =
                    targetDirection
                        == NvrPlaybackDirection.Reverse
                            ? "역재생을 시작했습니다."
                            : "정방향 재생을 시작했습니다.";
            }

            if (warningMessages.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "재생 방향을 "
                    + directionText
                    + "으로 변경하고 "
                    + stateText
                    + Environment.NewLine
                    + "일부 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warningMessages.Select(
                            warning =>
                                "- "
                                + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "재생 방향을 "
                + directionText
                + "으로 변경하고 "
                + stateText);
        }

        /// <summary>
        /// 여러 NVR 그룹의 방향 전환 중 실패했을 때
        /// 이전 방향과 이전 재생 상태를 복원한다.
        /// </summary>
        private async Task<bool>
            RestorePlaybackGroupsAfterDirectionFailureAsync(
                NvrPlaybackDirection previousDirection,
                PlaybackState originalState)
        {
            bool allSucceeded =
                true;

            /*
             * 우선 모든 그룹을 이전 방향으로 되돌린다.
             *
             * 이미 이전 방향인 그룹은 제조사 엔진에서
             * 중복 요청으로 성공 처리한다.
             */
            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                INvrPlaybackEngine engine;
                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult directionResult =
                        await engine.SetDirectionAsync(
                            groupSession,
                            previousDirection,
                            CancellationToken.None);

                    if (directionResult == null
                        || !directionResult.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            /*
             * 방향 복원 도중 일부 제조사 그룹이 안전을 위해
             * Paused로 남을 수 있으므로 원래 상태도 다시 맞춘다.
             */
            foreach (
                KeyValuePair<int, INvrPlaybackGroupSession>
                item in _playbackGroupSessions
                    .OrderBy(pair => pair.Key))
            {
                INvrPlaybackEngine engine;
                INvrPlaybackGroupSession groupSession =
                    item.Value;

                if (groupSession == null
                    || !_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult stateResult;

                    if (originalState
                        == PlaybackState.Paused)
                    {
                        stateResult =
                            await engine.PauseAsync(
                                groupSession,
                                CancellationToken.None);
                    }
                    else
                    {
                        stateResult =
                            await engine.ResumeAsync(
                                groupSession,
                                CancellationToken.None);
                    }

                    if (stateResult == null
                        || !stateResult.Success)
                    {
                        allSucceeded =
                            false;
                    }
                }
                catch
                {
                    allSucceeded =
                        false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// 방향 전환 또는 방향 복원 후
        /// 공통 서비스의 영상재생시간과 방향 상태를 갱신한다.
        /// </summary>
        private void ApplyServiceDirectionState(
            DateTime playbackTime,
            PlaybackState originalState,
            NvrPlaybackDirection direction)
        {
            _currentPlaybackTime =
                ClampPlaybackTime(
                    playbackTime);

            if (originalState
                == PlaybackState.Paused)
            {
                /*
                 * 일시정지 상태에서 방향을 변경해도
                 * 실제 재생은 시작하지 않는다.
                 *
                 * 다음 ResumeAsync가 어느 방향으로 재개할지
                 * _pausedFromState에 저장한다.
                 */
                CurrentState =
                    PlaybackState.Paused;

                _pausedFromState =
                    direction == NvrPlaybackDirection.Reverse
                        ? PlaybackState.Rewinding
                        : PlaybackState.Playing;

                _playbackClockStartedAtUtc =
                    null;

                return;
            }

            if (direction
                == NvrPlaybackDirection.Reverse)
            {
                CurrentState =
                    PlaybackState.Rewinding;

                _pausedFromState =
                    PlaybackState.Rewinding;
            }
            else
            {
                CurrentState =
                    PlaybackState.Playing;

                _pausedFromState =
                    PlaybackState.Playing;
            }

            _playbackClockStartedAtUtc =
                DateTime.UtcNow;
        }


        /// <summary>
        /// StopAsync 자체에서 예상하지 못한 예외가 발생했을 때
        /// 서비스가 보유한 로컬 재생 리소스를 강제로 정리한다.
        ///
        /// 주의:
        /// - 정상 정리 경로는 반드시 StopAsync를 사용한다.
        /// - 이 메서드는 비동기 Stop 또는 Logout을 수행하지 않는다.
        /// - 이미 일부 리소스가 해제됐을 수 있으므로
        ///   모든 Dispose 호출은 개별적으로 예외를 무시한다.
        /// - 사용자가 선택한 재생속도는 초기화하지 않는다.
        /// </summary>
        private void ForceReleasePlaybackResources()
        {
            /*
             * 그룹 재생 세션 Dictionary를 먼저 비운다.
             *
             * INvrPlaybackGroupSession은 IDisposable을 강제하지 않으므로
             * 실제 네이티브 정리는 엔진 또는 Provider Dispose에 맡긴다.
             */
            _playbackGroupSessions.Clear();

            /*
             * 제조사별 재생 엔진이 IDisposable을 구현한 경우 해제한다.
             */
            foreach (INvrPlaybackEngine engine
                in _playbackEngines.Values.ToList())
            {
                if (engine == null)
                {
                    continue;
                }

                IDisposable disposableEngine =
                    engine as IDisposable;

                if (disposableEngine == null)
                {
                    continue;
                }

                try
                {
                    disposableEngine.Dispose();
                }
                catch
                {
                    /*
                     * 비상 정리 중 하나의 엔진 Dispose 실패가
                     * 다른 리소스 정리를 막지 않게 한다.
                     */
                }
            }

            _playbackEngines.Clear();

            /*
             * Provider를 마지막으로 해제한다.
             *
             * StopAsync 실패 후의 비상 경로이므로
             * 비동기 Logout은 실행하지 않고 Dispose만 시도한다.
             */
            foreach (INvrProvider provider
                in _providers.Values.ToList())
            {
                if (provider == null)
                {
                    continue;
                }

                try
                {
                    provider.Dispose();
                }
                catch
                {
                    // 다른 Provider 정리를 계속한다.
                }
            }

            _providers.Clear();

            /*
             * 5. 서비스의 논리적인 재생 상태를 초기화한다.
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
        }
    }
}