using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Providers;
using CamViewer.Nvr.Dahua.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua 녹화영상 그룹 재생 엔진이다.
    ///
    /// 운영 원칙:
    /// - 단일채널은 개별 PlaybackHandle로 제어한다.
    /// - 다중채널은 Dahua 공식 PlayGroup으로만 제어한다.
    /// - PlayGroup 등록 후 개별 Pause/Resume/Speed/Direction을 호출하지 않는다.
    /// - Seek는 기존 핸들을 직접 이동하지 않고 목표 시각에서
    ///   전체 그룹을 재구성한다.
    /// </summary>
    internal sealed class DahuaPlaybackEngine :
        INvrPlaybackEngine,
        IDisposable
    {
        private readonly DahuaNvrProvider _provider;

        private readonly SemaphoreSlim _operationGate =
            new SemaphoreSlim(
                1,
                1);

        private readonly HashSet<DahuaPlaybackGroupSession>
            _sessions =
                new HashSet<DahuaPlaybackGroupSession>();

        private bool _disposed;

        internal DahuaPlaybackEngine(
            DahuaNvrProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(
                    "provider");
            }

            _provider =
                provider;
        }

        public async Task<NvrResult<INvrPlaybackGroupSession>> OpenAsync(
            NvrPlaybackGroupRequest request,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                NvrResult validationResult =
                    ValidateGroupRequest(
                        request);

                if (!validationResult.Success)
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        validationResult.Status,
                        validationResult.Message,
                        validationResult.Error);
                }

                if (!_provider.IsInitialized)
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다.",
                        Error(
                            "DAHUA_PROVIDER_NOT_INITIALIZED",
                            "Dahua Provider가 초기화되지 않았습니다.",
                            "Open"));
                }

                if (!_provider.IsLoggedIn)
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        Error(
                            "DAHUA_NOT_LOGGED_IN",
                            "Dahua NVR에 로그인되어 있지 않습니다.",
                            "Open"));
                }

                var groupSession =
                    new DahuaPlaybackGroupSession(
                        request);

                NvrResult buildResult =
                    BuildNativeResources(
                        groupSession,
                        request.InitialTime,
                        request.InitialDirection,
                        request.InitialSpeed,
                        true);

                if (!buildResult.Success)
                {
                    StopNativeResources(
                        groupSession);

                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        buildResult.Status,
                        buildResult.Message,
                        buildResult.Error);
                }

                lock (_sessions)
                {
                    _sessions.Add(
                        groupSession);
                }

                return NvrResult<INvrPlaybackGroupSession>.Ok(
                    groupSession,
                    groupSession.UsesPlayGroup
                        ? "Dahua 다중채널 PlayGroup을 일시정지 상태로 준비했습니다."
                        : "Dahua 단일채널 재생을 일시정지 상태로 준비했습니다.");
            }
            catch (OperationCanceledException)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 준비가 취소되었습니다.",
                    Error(
                        "DAHUA_GROUP_OPEN_CANCELLED",
                        "Dahua 재생 그룹 준비가 취소되었습니다.",
                        "Open"));
            }
            catch (Exception ex)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.UnknownError,
                    "Dahua 재생 그룹 준비 중 오류가 발생했습니다.",
                    Error(
                        "DAHUA_GROUP_OPEN_EXCEPTION",
                        ex.Message,
                        "Open"));
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> StartAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Start");
                }

                if (!groupSession.IsReady)
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "DAHUA_GROUP_NOT_READY",
                        "Start");
                }

                if (groupSession.State
                    == NvrPlaybackState.Playing
                    || groupSession.State
                        == NvrPlaybackState.Rewinding)
                {
                    return NvrResult.Ok(
                        "Dahua 재생 그룹이 이미 재생 중입니다.");
                }

                return ResumeNative(
                    groupSession);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> PauseAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Pause");
                }

                if (groupSession.State
                    == NvrPlaybackState.Paused)
                {
                    return NvrResult.Ok(
                        "Dahua 재생 그룹이 이미 일시정지 상태입니다.");
                }

                return PauseNative(
                    groupSession);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> ResumeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Resume");
                }

                if (groupSession.State
                    == NvrPlaybackState.Playing
                    || groupSession.State
                        == NvrPlaybackState.Rewinding)
                {
                    return NvrResult.Ok(
                        "Dahua 재생 그룹이 이미 재생 중입니다.");
                }

                return ResumeNative(
                    groupSession);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> StopAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            /*
             * Stop은 리소스 정리 명령이므로 호출 취소 여부와 관계없이
             * 네이티브 리소스를 끝까지 정리한다.
             */
            await _operationGate.WaitAsync(
                CancellationToken.None)
                .ConfigureAwait(false);

            try
            {
                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Stop");
                }

                NvrResult result =
                    StopNativeResources(
                        groupSession);

                groupSession.SetState(
                    NvrPlaybackState.Stopped);

                groupSession.SetReady(
                    false,
                    "Dahua 재생 그룹이 중지되었습니다.");

                groupSession.SetSynchronizationStatus(
                    false,
                    null,
                    "Dahua 재생 그룹이 중지되었습니다.");

                lock (_sessions)
                {
                    _sessions.Remove(
                        groupSession);
                }

                return result;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Seek");
                }

                if (targetTime < groupSession.StartTime
                    || targetTime >= groupSession.EndTime)
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "이동할 영상시간이 조회 범위를 벗어났습니다.",
                        "DAHUA_GROUP_SEEK_OUT_OF_RANGE",
                        "Seek");
                }

                NvrPlaybackState previousState =
                    groupSession.State;

                NvrPlaybackDirection previousDirection =
                    groupSession.Direction;

                NvrPlaybackSpeed previousSpeed =
                    groupSession.Speed;

                DateTime previousTime =
                    GetCommonPlaybackTime(
                        groupSession,
                        false);

                bool shouldResume =
                    previousState == NvrPlaybackState.Playing
                    || previousState
                        == NvrPlaybackState.Rewinding;

                NvrResult stopResult =
                    StopNativeResources(
                        groupSession);

                if (!stopResult.Success)
                {
                    groupSession.SetState(
                        NvrPlaybackState.Faulted);

                    return stopResult;
                }

                NvrResult buildResult =
                    BuildNativeResources(
                        groupSession,
                        targetTime,
                        previousDirection,
                        previousSpeed,
                        true);

                if (!buildResult.Success)
                {
                    /*
                     * 목표 시각 재구성 실패 시 원래 시각 복원을 한 번 시도한다.
                     */
                    StopNativeResources(
                        groupSession);

                    NvrResult rollbackResult =
                        BuildNativeResources(
                            groupSession,
                            previousTime,
                            previousDirection,
                            previousSpeed,
                            !shouldResume);

                    if (rollbackResult.Success
                        && shouldResume)
                    {
                        rollbackResult =
                            ResumeNative(
                                groupSession);
                    }

                    if (!rollbackResult.Success)
                    {
                        groupSession.SetState(
                            NvrPlaybackState.Faulted);

                        groupSession.SetReady(
                            false,
                            "Dahua Seek 실패 후 기존 위치도 복원하지 못했습니다.");

                        return Fail(
                            NvrResultStatus.Failed,
                            "Dahua 그룹 이동에 실패했고 "
                            + "기존 위치도 복원하지 못했습니다."
                            + Environment.NewLine
                            + "이동 오류: "
                            + buildResult.Message
                            + Environment.NewLine
                            + "복원 오류: "
                            + rollbackResult.Message,
                            "DAHUA_GROUP_SEEK_ROLLBACK_FAILED",
                            "Seek");
                    }

                    return buildResult;
                }

                if (shouldResume)
                {
                    NvrResult resumeResult =
                        ResumeNative(
                            groupSession);

                    if (!resumeResult.Success)
                    {
                        return resumeResult;
                    }
                }

                return NvrResult.Ok(
                    "Dahua 재생 그룹을 "
                    + targetTime.ToString(
                        "yyyy-MM-dd HH:mm:ss")
                    + " 위치에서 재구성했습니다.");
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> SetDirectionAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackDirection direction,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "SetDirection");
                }

                if (groupSession.Direction
                    == direction)
                {
                    return NvrResult.Ok(
                        "Dahua 재생 그룹이 이미 요청한 방향입니다.");
                }

                DateTime currentTime =
                    GetCommonPlaybackTime(
                        groupSession,
                        false);

                if (direction == NvrPlaybackDirection.Reverse
                    && currentTime <= groupSession.StartTime)
                {
                    return Fail(
                        NvrResultStatus.NotSupported,
                        "조회 시작시간에서는 역재생을 시작할 수 없습니다.",
                        "DAHUA_REVERSE_AT_START_NOT_SUPPORTED",
                        "SetDirection");
                }

                bool wasPaused =
                    groupSession.State
                        == NvrPlaybackState.Paused;

                if (groupSession.UsesPlayGroup)
                {
                    NvrResult result =
                        DahuaPlayGroupClient.SetDirection(
                            groupSession.PlayGroupHandle,
                            direction);

                    if (!result.Success)
                    {
                        return result;
                    }

                    foreach (DahuaPlaybackGroupChannel channel
                        in groupSession.GetChannels())
                    {
                        channel.Session.SetDirection(
                            direction);

                        if (!wasPaused)
                        {
                            channel.Session.SetState(
                                direction == NvrPlaybackDirection.Reverse
                                    ? NvrPlaybackState.Rewinding
                                    : NvrPlaybackState.Playing);
                        }
                    }

                    groupSession.SetCurrentPlaybackTime(
                        currentTime);

                    groupSession.SetDirection(
                        direction);

                    groupSession.SetState(
                        wasPaused
                            ? NvrPlaybackState.Paused
                            : direction == NvrPlaybackDirection.Reverse
                                ? NvrPlaybackState.Rewinding
                                : NvrPlaybackState.Playing);

                    return result;
                }

                /*
                 * 단일채널은 방향 변경 그룹 API가 없으므로
                 * 현재 시각에서 핸들을 다시 만든다.
                 */
                NvrPlaybackSpeed speed =
                    groupSession.Speed;

                NvrResult stopResult =
                    StopNativeResources(
                        groupSession);

                if (!stopResult.Success)
                {
                    return stopResult;
                }

                NvrResult buildResult =
                    BuildNativeResources(
                        groupSession,
                        currentTime,
                        direction,
                        speed,
                        true);

                if (!buildResult.Success)
                {
                    return buildResult;
                }

                if (!wasPaused)
                {
                    return ResumeNative(
                        groupSession);
                }

                return NvrResult.Ok(
                    direction == NvrPlaybackDirection.Reverse
                        ? "Dahua 단일채널을 역재생 방향으로 변경했습니다."
                        : "Dahua 단일채널을 정방향으로 변경했습니다.");
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "SetSpeed");
                }

                if (groupSession.Speed
                    == speed)
                {
                    return NvrResult.Ok(
                        "Dahua 재생 그룹이 이미 요청한 속도입니다.");
                }

                DateTime currentTime =
                    GetCommonPlaybackTime(
                        groupSession,
                        false);

                NvrResult result;

                if (groupSession.UsesPlayGroup)
                {
                    result =
                        DahuaPlayGroupClient.SetSpeed(
                            groupSession.PlayGroupHandle,
                            speed);
                }
                else
                {
                    DahuaPlaybackGroupChannel channel =
                        groupSession.GetChannels()
                            .FirstOrDefault();

                    if (channel == null
                        || channel.Session == null)
                    {
                        return Fail(
                            NvrResultStatus.Failed,
                            "Dahua 단일채널 재생 세션이 없습니다.",
                            "DAHUA_SINGLE_SESSION_REQUIRED",
                            "SetSpeed");
                    }

                    result =
                        DahuaPlaybackClient.SetSpeed(
                            channel.Session,
                            speed);
                }

                if (!result.Success)
                {
                    return result;
                }

                foreach (DahuaPlaybackGroupChannel channel
                    in groupSession.GetChannels())
                {
                    channel.Session.SetSpeed(
                        speed);
                }

                groupSession.SetCurrentPlaybackTime(
                    currentTime);

                groupSession.SetSpeed(
                    speed);

                return result;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return InvalidGroupSession(
                        "Synchronize");
                }

                if (!groupSession.UsesPlayGroup)
                {
                    groupSession.SetSynchronizationStatus(
                        true,
                        0d,
                        "단일채널은 별도의 동기화가 필요하지 않습니다.");

                    return NvrResult.Ok(
                        "단일채널은 별도의 동기화가 필요하지 않습니다.");
                }

                NvrResult<DateTime> timeResult =
                    DahuaPlayGroupClient.QueryTime(
                        groupSession.PlayGroupHandle);

                if (!timeResult.Success)
                {
                    groupSession.SetSynchronizationStatus(
                        true,
                        null,
                        "Dahua PlayGroup이 동기 재생을 관리하지만 "
                        + "현재 그룹 시간은 조회하지 못했습니다.");

                    /*
                     * PlayGroup 구성 자체가 유지되고 있으므로
                     * 시간 조회 실패를 재생 실패로 확대하지 않는다.
                     */
                    return NvrResult.Ok(
                        "Dahua PlayGroup이 채널 동기 재생을 관리합니다. "
                        + "이번 호출에서는 그룹 시간을 갱신하지 못했습니다.");
                }

                DahuaPlaybackGroupChannel baseChannel =
                    groupSession.GetBaseChannel();

                DateTime commonTime =
                    baseChannel == null
                        ? timeResult.Data
                        : baseChannel.ToCommonTime(
                            timeResult.Data);

                groupSession.SetCurrentPlaybackTime(
                    commonTime);

                groupSession.SetSynchronizationStatus(
                    true,
                    null,
                    "Dahua 공식 PlayGroup 동기 재생 상태입니다.");

                return NvrResult.Ok(
                    "Dahua 공식 PlayGroup이 채널 동기 재생을 관리합니다.");
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public async Task<NvrResult<NvrPlaybackGroupStatus>> GetStatusAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            await _operationGate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                EnsureNotDisposed();

                DahuaPlaybackGroupSession groupSession =
                    CastSession(
                        session);

                if (groupSession == null)
                {
                    return NvrResult<NvrPlaybackGroupStatus>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        Error(
                            "INVALID_DAHUA_GROUP_SESSION",
                            "Dahua 재생 그룹 세션이 아닙니다.",
                            "GetStatus"));
                }

                DateTime fallbackTime =
                    groupSession.CurrentPlaybackTime;

                NvrResult<DateTime> timeResult =
                    QueryCommonPlaybackTime(
                        groupSession);

                bool providerTimeAvailable =
                    timeResult != null
                    && timeResult.Success;

                DateTime playbackTime =
                    providerTimeAvailable
                        ? timeResult.Data
                        : fallbackTime;

                groupSession.SetCurrentPlaybackTime(
                    playbackTime);

                var status =
                    new NvrPlaybackGroupStatus
                    {
                        CurrentPlaybackTime =
                            playbackTime,

                        State =
                            groupSession.State,

                        Direction =
                            groupSession.Direction,

                        Speed =
                            groupSession.Speed,

                        IsReady =
                            groupSession.IsReady,

                        SynchronizationAvailable =
                            groupSession.UsesPlayGroup
                            && groupSession.IsPlayGroupReady,

                        IsSynchronized =
                            groupSession.IsSynchronized,

                        MaximumDriftSeconds =
                            groupSession.MaximumDriftSeconds,

                        Message =
                            providerTimeAvailable
                                ? groupSession.StatusMessage
                                : "제조사 시간을 확인하지 못해 "
                                    + "마지막 정상시간 또는 추정시간을 사용합니다."
                    };

                if (providerTimeAvailable)
                {
                    return NvrResult<NvrPlaybackGroupStatus>.Ok(
                        status,
                        "Dahua 재생 그룹 상태를 조회했습니다.");
                }

                /*
                 * 상태 데이터는 사용할 수 있으므로 PartialSuccess로 반환한다.
                 * 공통 서비스는 Success와 Data를 기준으로 추정시간을 유지할 수 있다.
                 */
                return new NvrResult<NvrPlaybackGroupStatus>
                {
                    Success =
                        true,

                    Status =
                        NvrResultStatus.PartialSuccess,

                    Message =
                        status.Message,

                    Data =
                        status,

                    Error =
                        timeResult == null
                            ? null
                            : timeResult.Error
                };
            }
            finally
            {
                _operationGate.Release();
            }
        }

        /// <summary>
        /// 요청 시각에서 단일 또는 다중채널 네이티브 리소스를 생성한다.
        ///
        /// 다중채널:
        /// PlaybackHandle 생성 → PlayGroup 등록 → 기준 채널 지정
        /// → 그룹 방향/속도 설정 → 그룹 Pause
        ///
        /// 단일채널:
        /// PlaybackHandle 생성 → 개별 속도 설정 → 개별 Pause
        /// </summary>
        private NvrResult BuildNativeResources(
            DahuaPlaybackGroupSession groupSession,
            DateTime targetTime,
            NvrPlaybackDirection direction,
            NvrPlaybackSpeed speed,
            bool remainPaused)
        {
            if (groupSession == null)
            {
                return InvalidGroupSession(
                    "BuildNativeResources");
            }

            targetTime =
                groupSession.ClampTime(
                    targetTime);

            IList<NvrPlaybackGroupChannelRequest> requests =
                groupSession.GetChannelRequests();

            if (requests == null
                || requests.Count == 0)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 그룹에 포함할 채널 요청이 없습니다.",
                    "DAHUA_GROUP_CHANNEL_REQUEST_EMPTY",
                    "BuildNativeResources");
            }

            var channels =
                new List<DahuaPlaybackGroupChannel>();

            IntPtr groupHandle =
                IntPtr.Zero;

            try
            {
                foreach (NvrPlaybackGroupChannelRequest channelRequest
                    in requests)
                {
                    DateTime providerTargetTime =
                        targetTime.AddSeconds(
                            channelRequest.TimeOffsetSeconds);

                    DateTime providerStartTime =
                        direction == NvrPlaybackDirection.Reverse
                            ? groupSession.StartTime.AddSeconds(
                                channelRequest.TimeOffsetSeconds)
                            : providerTargetTime;

                    DateTime providerEndTime =
                        direction == NvrPlaybackDirection.Reverse
                            ? providerTargetTime
                            : groupSession.EndTime.AddSeconds(
                                channelRequest.TimeOffsetSeconds);

                    if (providerStartTime
                        >= providerEndTime)
                    {
                        CleanupTemporaryResources(
                            groupHandle,
                            channels);

                        return Fail(
                            NvrResultStatus.Failed,
                            "Dahua 채널 재생 구간이 올바르지 않습니다. "
                            + "ChannelNo="
                            + channelRequest.ChannelNo,
                            "DAHUA_CHANNEL_NATIVE_RANGE_INVALID",
                            "BuildNativeResources");
                    }

                    var playbackRequest =
                        new NvrPlaybackRequest
                        {
                            CounterNo =
                                groupSession.CounterNo,

                            NvrNo =
                                groupSession.NvrNo,

                            ChannelNo =
                                channelRequest.ChannelNo,

                            ScreenPosition =
                                channelRequest.ScreenPosition,

                            SearchDateTime =
                                groupSession.SearchDateTime.AddSeconds(
                                    channelRequest.TimeOffsetSeconds),

                            StartTime =
                                providerStartTime,

                            EndTime =
                                providerEndTime,

                            RenderTargetHandle =
                                channelRequest.RenderTargetHandle,

                            /*
                             * PlayBackByTime은 호출 즉시 네이티브 재생을 시작한다.
                             * 일시정지는 모든 핸들을 만든 뒤 아래에서 수행한다.
                             */
                            AutoPlay =
                                true
                        };

                    NvrResult<DahuaPlaybackSession> openResult =
                        _provider.OpenPlaybackSession(
                            playbackRequest,
                            direction);

                    if (!openResult.Success
                        || openResult.Data == null)
                    {
                        CleanupTemporaryResources(
                            groupHandle,
                            channels);

                        return NvrResult.Fail(
                            openResult.Status,
                            openResult.Message,
                            openResult.Error);
                    }

                    DahuaPlaybackSession playbackSession =
                        openResult.Data;

                    playbackSession.SetCurrentPlaybackTime(
                        providerTargetTime);

                    channels.Add(
                        new DahuaPlaybackGroupChannel(
                            channelRequest,
                            playbackSession));
                }

                if (channels.Count > 1)
                {
                    NvrResult<IntPtr> openGroupResult =
                        DahuaPlayGroupClient.Open();

                    if (!openGroupResult.Success
                        || openGroupResult.Data == IntPtr.Zero)
                    {
                        CleanupTemporaryResources(
                            groupHandle,
                            channels);

                        return NvrResult.Fail(
                            openGroupResult.Status,
                            openGroupResult.Message,
                            openGroupResult.Error);
                    }

                    groupHandle =
                        openGroupResult.Data;

                    foreach (DahuaPlaybackGroupChannel channel
                        in channels)
                    {
                        NvrResult addResult =
                            DahuaPlayGroupClient.Add(
                                groupHandle,
                                channel.Session);

                        if (!addResult.Success)
                        {
                            CleanupTemporaryResources(
                                groupHandle,
                                channels);

                            return addResult;
                        }
                    }

                    DahuaPlaybackGroupChannel baseChannel =
                        channels.FirstOrDefault(
                            item =>
                                item.ScreenPosition == 0)
                        ?? channels.First();

                    NvrResult baseResult =
                        DahuaPlayGroupClient.SetBaseChannel(
                            groupHandle,
                            baseChannel.Session);

                    if (!baseResult.Success)
                    {
                        CleanupTemporaryResources(
                            groupHandle,
                            channels);

                        return baseResult;
                    }

                    /*
                     * 모든 PlaybackHandle 등록과 기준 채널 지정이 끝난 뒤
                     * PlayGroup 전체를 먼저 일시정지한다.
                     *
                     * 개별 PlaybackHandle에는 Pause 명령을 보내지 않는다.
                     */
                    if (remainPaused)
                    {
                        NvrResult pauseResult =
                            DahuaPlayGroupClient.Pause(
                                groupHandle,
                                true);

                        if (!pauseResult.Success)
                        {
                            CleanupTemporaryResources(
                                groupHandle,
                                channels);

                            return pauseResult;
                        }
                    }

                    /*
                     * 정방향 1배속은 PlaybackHandle 생성 시점의 기본 상태다.
                     * 불필요한 그룹 제어 명령을 줄이기 위해
                     * 기본값이 아닌 경우에만 제조사 API를 호출한다.
                     */
                    if (direction
                        != NvrPlaybackDirection.Forward)
                    {
                        NvrResult directionResult =
                            DahuaPlayGroupClient.SetDirection(
                                groupHandle,
                                direction);

                        if (!directionResult.Success)
                        {
                            CleanupTemporaryResources(
                                groupHandle,
                                channels);

                            return directionResult;
                        }
                    }

                    if (speed
                        != NvrPlaybackSpeed.Normal)
                    {
                        NvrResult speedResult =
                            DahuaPlayGroupClient.SetSpeed(
                                groupHandle,
                                speed);

                        if (!speedResult.Success)
                        {
                            CleanupTemporaryResources(
                                groupHandle,
                                channels);

                            return speedResult;
                        }
                    }

                    groupSession.ReplaceChannels(
                        channels);

                    groupSession.SetPlayGroup(
                        groupHandle,
                        baseChannel);

                    foreach (DahuaPlaybackGroupChannel channel
                        in channels)
                    {
                        channel.Session.SetDirection(
                            direction);

                        channel.Session.SetSpeed(
                            speed);

                        channel.Session.SetState(
                            remainPaused
                                ? NvrPlaybackState.Paused
                                : direction == NvrPlaybackDirection.Reverse
                                    ? NvrPlaybackState.Rewinding
                                    : NvrPlaybackState.Playing);
                    }
                }
                else
                {
                    DahuaPlaybackGroupChannel channel =
                        channels[0];

                    /*
                     * 단일채널도 기본 1배속에서는 별도 제어 명령을
                     * 보내지 않는다.
                     */
                    if (speed
                        != NvrPlaybackSpeed.Normal)
                    {
                        NvrResult speedResult =
                            DahuaPlaybackClient.SetSpeed(
                                channel.Session,
                                speed);

                        if (!speedResult.Success)
                        {
                            CleanupTemporaryResources(
                                IntPtr.Zero,
                                channels);

                            return speedResult;
                        }
                    }

                    if (remainPaused)
                    {
                        NvrResult pauseResult =
                            DahuaPlaybackClient.Pause(
                                channel.Session);

                        if (!pauseResult.Success)
                        {
                            CleanupTemporaryResources(
                                IntPtr.Zero,
                                channels);

                            return pauseResult;
                        }
                    }

                    groupSession.ReplaceChannels(
                        channels);
                }

                groupSession.SetDirection(
                    direction);

                groupSession.SetSpeed(
                    speed);

                groupSession.SetState(
                    remainPaused
                        ? NvrPlaybackState.Paused
                        : direction == NvrPlaybackDirection.Reverse
                            ? NvrPlaybackState.Rewinding
                            : NvrPlaybackState.Playing);

                groupSession.SetCurrentPlaybackTime(
                    targetTime);

                groupSession.SetReady(
                    true,
                    channels.Count > 1
                        ? "Dahua 공식 PlayGroup 준비가 완료되었습니다."
                        : "Dahua 단일채널 준비가 완료되었습니다.");

                groupSession.SetSynchronizationStatus(
                    true,
                    channels.Count > 1
                        ? (double?)null
                        : 0d,
                    channels.Count > 1
                        ? "Dahua 공식 PlayGroup이 채널 동기화를 관리합니다."
                        : "단일채널은 별도의 동기화가 필요하지 않습니다.");

                return NvrResult.Ok(
                    channels.Count > 1
                        ? "Dahua PlayGroup 네이티브 리소스를 생성했습니다."
                        : "Dahua 단일채널 네이티브 리소스를 생성했습니다.");
            }
            catch (Exception ex)
            {
                CleanupTemporaryResources(
                    groupHandle,
                    channels);

                return Fail(
                    NvrResultStatus.UnknownError,
                    "Dahua 네이티브 재생 리소스 생성 중 오류가 발생했습니다. "
                    + ex.Message,
                    "DAHUA_NATIVE_BUILD_EXCEPTION",
                    "BuildNativeResources");
            }
        }

        private NvrResult PauseNative(
            DahuaPlaybackGroupSession groupSession)
        {
            DateTime commonTime =
                GetCommonPlaybackTime(
                    groupSession,
                    false);

            NvrResult result;

            if (groupSession.UsesPlayGroup)
            {
                result =
                    DahuaPlayGroupClient.Pause(
                        groupSession.PlayGroupHandle,
                        true);
            }
            else
            {
                DahuaPlaybackGroupChannel channel =
                    groupSession.GetChannels()
                        .FirstOrDefault();

                result =
                    channel == null
                        ? Fail(
                            NvrResultStatus.Failed,
                            "일시정지할 Dahua 채널이 없습니다.",
                            "DAHUA_CHANNEL_REQUIRED",
                            "Pause")
                        : DahuaPlaybackClient.Pause(
                            channel.Session);
            }

            if (!result.Success)
            {
                return result;
            }

            foreach (DahuaPlaybackGroupChannel channel
                in groupSession.GetChannels())
            {
                channel.Session.SetState(
                    NvrPlaybackState.Paused);
            }

            groupSession.SetState(
                NvrPlaybackState.Paused);

            groupSession.SetCurrentPlaybackTime(
                commonTime);

            return result;
        }

        private NvrResult ResumeNative(
            DahuaPlaybackGroupSession groupSession)
        {
            NvrResult result;

            if (groupSession.UsesPlayGroup)
            {
                if (!groupSession.IsPlayGroupReady)
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup이 준비되지 않았습니다.",
                        "DAHUA_PLAYGROUP_NOT_READY",
                        "Resume");
                }

                result =
                    DahuaPlayGroupClient.Pause(
                        groupSession.PlayGroupHandle,
                        false);
            }
            else
            {
                DahuaPlaybackGroupChannel channel =
                    groupSession.GetChannels()
                        .FirstOrDefault();

                result =
                    channel == null
                        ? Fail(
                            NvrResultStatus.Failed,
                            "재개할 Dahua 채널이 없습니다.",
                            "DAHUA_CHANNEL_REQUIRED",
                            "Resume")
                        : DahuaPlaybackClient.Resume(
                            channel.Session);
            }

            if (!result.Success)
            {
                return result;
            }

            NvrPlaybackState playingState =
                groupSession.Direction
                    == NvrPlaybackDirection.Reverse
                        ? NvrPlaybackState.Rewinding
                        : NvrPlaybackState.Playing;

            foreach (DahuaPlaybackGroupChannel channel
                in groupSession.GetChannels())
            {
                channel.Session.SetState(
                    playingState);
            }

            groupSession.SetState(
                playingState);

            groupSession.SetReady(
                true,
                groupSession.UsesPlayGroup
                    ? "Dahua PlayGroup을 재생 중입니다."
                    : "Dahua 단일채널을 재생 중입니다.");

            return result;
        }

        private NvrResult StopNativeResources(
            DahuaPlaybackGroupSession groupSession)
        {
            if (groupSession == null)
            {
                return NvrResult.Ok(
                    "정리할 Dahua 그룹이 없습니다.");
            }

            var failures =
                new List<string>();

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            IntPtr groupHandle =
                groupSession.TakePlayGroupHandle();

            if (groupHandle != IntPtr.Zero)
            {
                /*
                 * PlayGroup을 먼저 정리한 뒤 개별 PlaybackHandle을 중지한다.
                 */
                DahuaPlayGroupClient.Pause(
                    groupHandle,
                    true);

                foreach (DahuaPlaybackGroupChannel channel
                    in channels)
                {
                    NvrResult removeResult =
                        DahuaPlayGroupClient.Remove(
                            groupHandle,
                            channel.Session);

                    if (!removeResult.Success)
                    {
                        failures.Add(
                            removeResult.Message);
                    }
                }

                NvrResult closeResult =
                    DahuaPlayGroupClient.Close(
                        groupHandle);

                if (!closeResult.Success)
                {
                    failures.Add(
                        closeResult.Message);
                }
            }

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (channel == null
                    || channel.Session == null)
                {
                    continue;
                }

                NvrResult stopResult =
                    DahuaPlaybackClient.Stop(
                        channel.Session);

                if (!stopResult.Success)
                {
                    failures.Add(
                        stopResult.Message);
                }

                channel.Session.Dispose();
            }

            groupSession.ClearChannels();

            if (failures.Count > 0)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 리소스를 완전히 정리하지 못했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        failures),
                    "DAHUA_NATIVE_CLEANUP_FAILED",
                    "StopNativeResources");
            }

            return NvrResult.Ok(
                "Dahua 네이티브 재생 리소스를 정리했습니다.");
        }

        private void CleanupTemporaryResources(
            IntPtr groupHandle,
            IList<DahuaPlaybackGroupChannel> channels)
        {
            if (groupHandle != IntPtr.Zero)
            {
                DahuaPlayGroupClient.Pause(
                    groupHandle,
                    true);

                if (channels != null)
                {
                    foreach (DahuaPlaybackGroupChannel channel
                        in channels)
                    {
                        if (channel != null
                            && channel.Session != null)
                        {
                            DahuaPlayGroupClient.Remove(
                                groupHandle,
                                channel.Session);
                        }
                    }
                }

                DahuaPlayGroupClient.Close(
                    groupHandle);
            }

            if (channels == null)
            {
                return;
            }

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (channel == null
                    || channel.Session == null)
                {
                    continue;
                }

                DahuaPlaybackClient.Stop(
                    channel.Session);

                channel.Session.Dispose();
            }
        }

        private NvrResult<DateTime> QueryCommonPlaybackTime(
            DahuaPlaybackGroupSession groupSession)
        {
            if (groupSession == null
                || groupSession.ChannelCount == 0)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "시간을 조회할 Dahua 재생 그룹이 없습니다.",
                    Error(
                        "DAHUA_GROUP_TIME_SESSION_REQUIRED",
                        "시간을 조회할 Dahua 재생 그룹이 없습니다.",
                        "QueryCommonPlaybackTime"));
            }

            DahuaPlaybackGroupChannel baseChannel =
                groupSession.GetBaseChannel();

            if (baseChannel == null
                || baseChannel.Session == null)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 기준 채널을 찾지 못했습니다.",
                    Error(
                        "DAHUA_BASE_CHANNEL_REQUIRED",
                        "Dahua 기준 채널을 찾지 못했습니다.",
                        "QueryCommonPlaybackTime"));
            }

            NvrResult<DateTime> providerTimeResult =
                groupSession.UsesPlayGroup
                    ? DahuaPlayGroupClient.QueryTime(
                        groupSession.PlayGroupHandle)
                    : DahuaPlaybackClient.QueryTime(
                        baseChannel.Session);

            if (!providerTimeResult.Success)
            {
                return providerTimeResult;
            }

            DateTime commonTime =
                baseChannel.ToCommonTime(
                    providerTimeResult.Data);

            commonTime =
                groupSession.ClampTime(
                    commonTime);

            return NvrResult<DateTime>.Ok(
                commonTime,
                "Dahua 공통 재생시간을 조회했습니다.");
        }

        private DateTime GetCommonPlaybackTime(
            DahuaPlaybackGroupSession groupSession,
            bool failWhenUnavailable)
        {
            NvrResult<DateTime> result =
                QueryCommonPlaybackTime(
                    groupSession);

            if (result.Success)
            {
                groupSession.SetCurrentPlaybackTime(
                    result.Data);

                return result.Data;
            }

            if (failWhenUnavailable)
            {
                throw new InvalidOperationException(
                    result.Message);
            }

            return groupSession.CurrentPlaybackTime;
        }

        private static NvrResult ValidateGroupRequest(
            NvrPlaybackGroupRequest request)
        {
            if (request == null)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹 요청 정보가 없습니다.",
                    "DAHUA_GROUP_REQUEST_REQUIRED",
                    "ValidateGroupRequest");
            }

            if (request.StartTime
                >= request.EndTime)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 시작시간은 종료시간보다 이전이어야 합니다.",
                    "DAHUA_GROUP_RANGE_INVALID",
                    "ValidateGroupRequest");
            }

            if (request.InitialTime < request.StartTime
                || request.InitialTime >= request.EndTime)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 최초 재생시간이 조회 범위를 벗어났습니다.",
                    "DAHUA_GROUP_INITIAL_TIME_INVALID",
                    "ValidateGroupRequest");
            }

            if (request.Channels == null
                || request.Channels.Count == 0)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 그룹에 포함할 채널이 없습니다.",
                    "DAHUA_GROUP_CHANNEL_REQUIRED",
                    "ValidateGroupRequest");
            }

            var channels =
                new HashSet<int>();

            var screens =
                new HashSet<int>();

            foreach (NvrPlaybackGroupChannelRequest channel
                in request.Channels)
            {
                if (channel == null)
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "Dahua 그룹에 null 채널이 포함되어 있습니다.",
                        "DAHUA_GROUP_CHANNEL_NULL",
                        "ValidateGroupRequest");
                }

                if (channel.ChannelNo <= 0)
                {
                    return Fail(
                        NvrResultStatus.InvalidChannel,
                        "Dahua 채널번호는 1 이상이어야 합니다.",
                        "DAHUA_GROUP_CHANNEL_INVALID",
                        "ValidateGroupRequest");
                }

                if (channel.RenderTargetHandle
                    == IntPtr.Zero)
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "Dahua 영상 출력 대상 Handle이 없습니다.",
                        "DAHUA_GROUP_RENDER_HANDLE_REQUIRED",
                        "ValidateGroupRequest");
                }

                if (!channels.Add(
                        channel.ChannelNo))
                {
                    return Fail(
                        NvrResultStatus.InvalidChannel,
                        "같은 Dahua 채널번호가 중복되었습니다.",
                        "DAHUA_GROUP_CHANNEL_DUPLICATED",
                        "ValidateGroupRequest");
                }

                if (!screens.Add(
                        channel.ScreenPosition))
                {
                    return Fail(
                        NvrResultStatus.Failed,
                        "같은 화면 위치가 중복되었습니다.",
                        "DAHUA_GROUP_SCREEN_DUPLICATED",
                        "ValidateGroupRequest");
                }
            }

            return NvrResult.Ok();
        }

        private static DahuaPlaybackGroupSession CastSession(
            INvrPlaybackGroupSession session)
        {
            return session
                as DahuaPlaybackGroupSession;
        }

        private static NvrResult InvalidGroupSession(
            string operation)
        {
            return Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 그룹 세션 형식이 올바르지 않습니다.",
                "INVALID_DAHUA_GROUP_SESSION",
                operation);
        }

        private static NvrResult Fail(
            NvrResultStatus status,
            string message,
            string errorCode,
            string operation)
        {
            return NvrResult.Fail(
                status,
                message,
                Error(
                    errorCode,
                    message,
                    operation));
        }

        private static NvrErrorInfo Error(
            string errorCode,
            string message,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    message,

                NativeErrorCode =
                    DahuaSdkRuntime.GetLastErrorSafe(),

                Operation =
                    operation
            };
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().FullName);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _operationGate.Wait();

            try
            {
                List<DahuaPlaybackGroupSession> sessions;

                lock (_sessions)
                {
                    sessions =
                        _sessions.ToList();

                    _sessions.Clear();
                }

                foreach (DahuaPlaybackGroupSession session
                    in sessions)
                {
                    try
                    {
                        StopNativeResources(
                            session);

                        session.SetState(
                            NvrPlaybackState.Stopped);

                        session.SetReady(
                            false,
                            "Dahua 재생 엔진이 해제되었습니다.");
                    }
                    catch
                    {
                    }
                }

                _disposed =
                    true;
            }
            finally
            {
                _operationGate.Release();
                _operationGate.Dispose();

                _provider.NotifyEngineDisposed(
                    this);
            }
        }
    }
}
