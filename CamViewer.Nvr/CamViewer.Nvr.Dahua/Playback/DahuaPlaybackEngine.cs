using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Providers;
using CamViewer.Nvr.Dahua.Sdk;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua NVR의 다중채널 녹화영상 재생을 담당하는
    /// 제조사 전용 고수준 재생 엔진.
    ///
    /// 최종적으로 다음 기능은 모두 이 클래스와
    /// DahuaPlaybackSynchronizer 내부에서 처리한다.
    ///
    /// - 여러 채널의 재생 세션 생성
    /// - 채널별 시간 보정값 적용
    /// - Seek 및 키프레임 처리
    /// - 정방향 및 역방향 전환
    /// - 재생속도 변경
    /// - 채널 간 동기화
    /// - Dahua SDK 오류 복구
    ///
    /// 현재 단계에서는 공통 인터페이스 연결 여부만 검증하기 위해
    /// 실제 기능은 아직 구현하지 않는다.
    /// </summary>
    internal sealed class DahuaPlaybackEngine :
        INvrPlaybackEngine
    {
        private readonly DahuaNvrProvider _provider;

        /// <summary>
        /// Dahua 재생 엔진을 초기화한다.
        ///
        /// Provider 초기화와 로그인은
        /// 엔진 생성 전에 완료되어 있어야 한다.
        /// </summary>
        public DahuaPlaybackEngine(
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


        /// <summary>
        /// Dahua 다중채널 재생 그룹을 준비한다.
        ///
        /// 처리 순서:
        /// 1. 요청과 Provider 상태 검증
        /// 2. 채널 구성 중복 검증
        /// 3. 채널별 전체 조회 구간으로 재생 핸들 생성
        /// 4. 생성된 채널을 즉시 일시정지
        /// 5. 최초 재생 위치로 이동
        /// 6. 모든 채널을 하나의 Dahua 그룹 세션으로 반환
        ///
        /// </summary>
        public async Task<NvrResult<INvrPlaybackGroupSession>> OpenAsync(NvrPlaybackGroupRequest request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_OPEN_CANCELLED",
                        "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                        "Open"));
            }

            if (request == null)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹 요청 정보가 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_REQUEST_REQUIRED",
                        "Dahua 재생 그룹 요청 정보가 없습니다.",
                        "Open"));
            }

            if (!_provider.IsInitialized)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua Provider가 초기화되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_PROVIDER_NOT_INITIALIZED",
                        "Dahua Provider가 초기화되지 않았습니다.",
                        "Open"));
            }

            if (!_provider.IsLoggedIn)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR에 로그인되어 있지 않습니다.",
                    CreateError(
                        "DAHUA_GROUP_PROVIDER_NOT_LOGGED_IN",
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        "Open"));
            }

            if (!string.IsNullOrWhiteSpace(request.ProviderKey)
                && !string.Equals(
                    request.ProviderKey,
                    "DAHUA_SDK",
                    StringComparison.OrdinalIgnoreCase))
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "Dahua 재생 엔진에 다른 ProviderKey가 전달되었습니다. "
                    + "ProviderKey="
                    + request.ProviderKey,
                    CreateError(
                        "DAHUA_GROUP_PROVIDER_KEY_MISMATCH",
                        "Dahua 재생 엔진에 다른 ProviderKey가 전달되었습니다.",
                        "Open"));
            }

            if (request.StartTime >= request.EndTime)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Failed,
                    "재생 시작시간은 종료시간보다 이전이어야 합니다.",
                    CreateError(
                        "DAHUA_GROUP_INVALID_RANGE",
                        "재생 시작시간은 종료시간보다 이전이어야 합니다.",
                        "Open"));
            }

            if (request.InitialTime < request.StartTime)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Failed,
                    "최초 재생시간은 조회 시작시간보다 이전일 수 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_INITIAL_BEFORE_START",
                        "최초 재생시간은 조회 시작시간보다 이전일 수 없습니다.",
                        "Open"));
            }

            if (request.InitialTime >= request.EndTime)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Failed,
                    "최초 재생시간은 조회 종료시간보다 이전이어야 합니다.",
                    CreateError(
                        "DAHUA_GROUP_INITIAL_AFTER_END",
                        "최초 재생시간은 조회 종료시간보다 이전이어야 합니다.",
                        "Open"));
            }

            if (request.Channels == null
                || request.Channels.Count == 0)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 포함할 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_REQUIRED",
                        "Dahua 재생 그룹에 포함할 채널이 없습니다.",
                        "Open"));
            }

            /*
             * 현재 단계에서는 정방향 재생 그룹 준비만 지원한다.
             *
             * 역방향 전환은 이후 SetDirectionAsync에서
             * Dahua 전용 방식으로 세션을 재구성한다.
             */
            if (request.InitialDirection != NvrPlaybackDirection.Forward)
            {
                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua 그룹 준비는 정방향 재생만 지원합니다.",
                    CreateError(
                        "DAHUA_GROUP_REVERSE_OPEN_NOT_SUPPORTED",
                        "현재 Dahua 그룹 준비는 정방향 재생만 지원합니다.",
                        "Open"));
            }

            /*
             * SDK 재생 핸들을 생성하기 전에
             * 채널 구성의 중복 여부를 먼저 검사한다.
             */
            var screenPositions = new HashSet<int>();

            var channelNumbers = new HashSet<int>();

            foreach (NvrPlaybackGroupChannelRequest channel in request.Channels)
            {
                if (channel == null)
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹에 null 채널이 포함되어 있습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_NULL",
                            "Dahua 재생 그룹에 null 채널이 포함되어 있습니다.",
                            "Open"));
                }

                if (channel.RenderTargetHandle == IntPtr.Zero)
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 영상 출력 대상 Handle이 없습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        CreateError(
                            "DAHUA_GROUP_RENDER_HANDLE_REQUIRED",
                            "Dahua 영상 출력 대상 Handle이 없습니다.",
                            "Open"));
                }

                if (!screenPositions.Add(
                        channel.ScreenPosition))
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.Failed,
                        "같은 화면 위치의 채널이 중복되었습니다. "
                        + "ScreenPosition="
                        + channel.ScreenPosition,
                        CreateError(
                            "DAHUA_GROUP_DUPLICATE_SCREEN_POSITION",
                            "같은 화면 위치의 채널이 중복되었습니다.",
                            "Open"));
                }

                if (!channelNumbers.Add(
                        channel.ChannelNo))
                {
                    return NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.InvalidChannel,
                        "같은 Dahua 채널번호가 중복되었습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo,
                        CreateError(
                            "DAHUA_GROUP_DUPLICATE_CHANNEL",
                            "같은 Dahua 채널번호가 중복되었습니다.",
                            "Open"));
                }
            }

            var groupSession =
                new DahuaPlaybackGroupSession(
                    request);

            try
            {
                foreach (NvrPlaybackGroupChannelRequest channel in request.Channels)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await CleanupGroupAsync(groupSession);

                        return NvrResult<INvrPlaybackGroupSession>.Fail(
                            NvrResultStatus.Cancelled,
                            "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                            CreateError(
                                "DAHUA_GROUP_OPEN_CANCELLED",
                                "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                                "Open"));
                    }

                    /*
                     * 채널별 시간 보정값은 CamViewer 공통 서비스가
                     * 계산하거나 적용하지 않는다.
                     *
                     * Dahua 엔진 내부에서 공통 영상 시각을
                     * 실제 NVR 채널 시각으로 변환한다.
                     */
                    DateTime providerStartTime = request.StartTime.AddSeconds(channel.TimeOffsetSeconds);

                    DateTime providerEndTime = request.EndTime.AddSeconds(channel.TimeOffsetSeconds);

                    DateTime providerInitialTime = request.InitialTime.AddSeconds(channel.TimeOffsetSeconds);

                    DateTime providerSearchDateTime = request.SearchDateTime.AddSeconds(channel.TimeOffsetSeconds);

                    var channelPlaybackRequest = new NvrPlaybackRequest
                        {
                            CounterNo = request.CounterNo,
                            NvrNo = request.NvrNo,
                            ChannelNo = channel.ChannelNo,
                            ScreenPosition = channel.ScreenPosition,
                            SearchDateTime = providerSearchDateTime,

                            /*
                             * 방향 전환 이후에도 이전 시각으로 Seek할 수 있도록
                             * 재생 세션은 항상 전체 조회 범위로 생성한다.
                             */
                            StartTime = providerStartTime,
                            EndTime = providerEndTime,
                            RenderTargetHandle = channel.RenderTargetHandle,

                            /*
                             * 현재 Dahua SDK 구현은 AutoPlay 값과 관계없이
                             * CLIENT_PlayBackByTimeEx 호출 직후 재생을 시작한다.
                             *
                             * 아래에서 명시적으로 PauseAsync를 호출한다.
                             */
                            AutoPlay = false
                        };

                    NvrResult<INvrPlaybackSession> playResult = await _provider.PlayByTimeAsync(channelPlaybackRequest, cancellationToken);

                    if (playResult == null || !playResult.Success || playResult.Data == null)
                    {
                        await CleanupGroupAsync(groupSession);

                        return NvrResult<INvrPlaybackGroupSession>.Fail(
                            playResult == null
                                ? NvrResultStatus.Failed
                                : playResult.Status,

                            playResult == null
                                ? "Dahua 채널 재생 결과가 없습니다."
                                : playResult.Message,

                            playResult == null
                                ? CreateError(
                                    "DAHUA_GROUP_CHANNEL_RESULT_EMPTY",
                                    "Dahua 채널 재생 결과가 없습니다.",
                                    "Open")
                                : playResult.Error);
                    }

                    DahuaPlaybackSession dahuaSession = playResult.Data as DahuaPlaybackSession;

                    if (dahuaSession == null)
                    {
                        /*
                         * 예상하지 못한 세션 타입이 반환되더라도
                         * 생성된 SDK 재생 리소스를 정리한다.
                         */
                        await _provider.StopAsync(playResult.Data, CancellationToken.None);

                        await CleanupGroupAsync(groupSession);

                        return NvrResult<INvrPlaybackGroupSession>.Fail(
                            NvrResultStatus.Failed,
                            "Dahua Provider가 다른 형식의 재생 세션을 반환했습니다.",
                            CreateError(
                                "INVALID_DAHUA_CHANNEL_SESSION",
                                "Dahua Provider가 다른 형식의 재생 세션을 반환했습니다.",
                                "Open"));
                    }

                    var groupChannel =
                        new DahuaPlaybackGroupChannel(
                            channel,
                            dahuaSession);

                    /*
                     * 이후 Pause 또는 Seek가 실패해도
                     * CleanupGroupAsync에서 현재 채널까지 정리할 수 있도록
                     * 먼저 그룹에 등록한다.
                     */
                    groupSession.AddChannel(groupChannel);

                    /*
                     * CLIENT_PlayBackByTimeEx 호출 직후 재생이 시작되므로
                     * 그룹 준비 단계에서는 즉시 일시정지한다.
                     */
                    NvrResult pauseResult = await _provider.PauseAsync(dahuaSession, CancellationToken.None);

                    if (pauseResult == null || !pauseResult.Success)
                    {
                        await CleanupGroupAsync(groupSession);

                        return NvrResult<INvrPlaybackGroupSession>.Fail(
                            pauseResult == null
                                ? NvrResultStatus.Failed
                                : pauseResult.Status,

                            pauseResult == null
                                ? "Dahua 채널 일시정지 결과가 없습니다."
                                : pauseResult.Message,

                            pauseResult == null
                                ? CreateError(
                                    "DAHUA_GROUP_PAUSE_RESULT_EMPTY",
                                    "Dahua 채널 일시정지 결과가 없습니다.",
                                    "Open")
                                : pauseResult.Error);
                    }

                    /*
                     * 전체 조회 시작 시각과 InitialTime이 다르면
                     * 일시정지된 상태에서 최초 위치로 이동한다.
                     *
                     * 실제 OSD 도착 여부와 채널 간 정렬은
                     * 이후 SynchronizeAsync에서 처리한다.
                     */
                    if (providerInitialTime != providerStartTime)
                    {
                        NvrResult seekResult = await _provider.SeekAsync(dahuaSession, providerInitialTime, cancellationToken);

                        if (seekResult == null || !seekResult.Success)
                        {
                            await CleanupGroupAsync(groupSession);

                            return NvrResult<INvrPlaybackGroupSession>.Fail(
                                seekResult == null
                                    ? NvrResultStatus.Failed
                                    : seekResult.Status,

                                seekResult == null
                                    ? "Dahua 채널 최초 위치 이동 결과가 없습니다."
                                    : seekResult.Message,

                                seekResult == null
                                    ? CreateError(
                                        "DAHUA_GROUP_INITIAL_SEEK_RESULT_EMPTY",
                                        "Dahua 채널 최초 위치 이동 결과가 없습니다.",
                                        "Open")
                                    : seekResult.Error);
                        }

                        /*
                         * 이 시간은 실제 OSD 확인 결과가 아니라
                         * 현재 그룹이 요청한 논리적인 최초 위치다.
                         */
                        dahuaSession.SetCurrentPlaybackTime(providerInitialTime);
                    }
                }

                groupSession.SetCurrentPlaybackTime(request.InitialTime);
                groupSession.SetDirection(NvrPlaybackDirection.Forward);
                groupSession.SetSpeed(request.InitialSpeed);
                groupSession.SetState(NvrPlaybackState.Paused);
                groupSession.SetReady(true, "Dahua 재생 그룹 준비가 완료되었습니다.");

                /*
                 * 단일 채널은 채널 간 시간차가 존재하지 않는다.
                 *
                 * 다중채널은 아직 실제 OSD를 비교하지 않았으므로
                 * 동기화되지 않은 준비 상태로 둔다.
                 */
                if (groupSession.ChannelCount <= 1)
                {
                    groupSession.SetSynchronizationStatus(true, 0d, "단일 Dahua 채널 재생 그룹이 준비되었습니다.");
                }
                else
                {
                    groupSession.SetSynchronizationStatus(false, null, "Dahua 다중채널 재생 그룹이 준비되었습니다. " + "재생 시작 전에 동기화가 필요합니다.");
                }

                return NvrResult<INvrPlaybackGroupSession>.Ok(groupSession, "Dahua 다중채널 재생 그룹을 준비했습니다.");
            }
            catch (OperationCanceledException)
            {
                await CleanupGroupAsync(groupSession);

                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_OPEN_CANCELLED",
                        "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                        "Open"));
            }
            catch (Exception ex)
            {
                await CleanupGroupAsync(groupSession);

                return NvrResult<INvrPlaybackGroupSession>.Fail(
                    NvrResultStatus.UnknownError,
                    "Dahua 재생 그룹 준비 중 예외가 발생했습니다.",
                    CreateError(
                        "DAHUA_GROUP_OPEN_EXCEPTION",
                        ex.Message,
                        "Open"));
            }
        }


        /// <summary>
        /// 준비된 Dahua 재생 그룹의 모든 채널을 재생한다.
        ///
        /// 처리 순서:
        /// 1. 그룹 세션 유효성 확인
        /// 2. 그룹 준비 상태 확인
        /// 3. 모든 채널을 순서대로 재개
        /// 4. 일부 채널 재개 실패 시 이미 시작된 채널을 다시 일시정지
        /// 5. 모든 채널 성공 시 그룹 상태를 Playing으로 변경
        ///
        /// 현재 단계에서는 채널 간 OSD 동기화는 수행하지 않는다.
        /// 실제 동기화는 이후 SynchronizeAsync에서 처리한다.
        /// </summary>
        public async Task<NvrResult> StartAsync(INvrPlaybackGroupSession session, CancellationToken cancellationToken)
        {
            /*
             * 명령이 실행되기 전에 취소된 경우에는
             * 어떠한 Dahua SDK 명령도 호출하지 않는다.
             */
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 시작 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_START_CANCELLED",
                        "Dahua 재생 그룹 시작 요청이 취소되었습니다.",
                        "Start"));
            }

            /*
             * 공통 그룹 세션을 Dahua 전용 그룹 세션으로 변환한다.
             *
             * 다른 제조사의 그룹 세션이 전달되면
             * Dahua SDK 명령을 실행하지 않고 실패 처리한다.
             */
            DahuaPlaybackGroupSession groupSession =
                session as DahuaPlaybackGroupSession;

            if (groupSession == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹 세션이 아닙니다.",
                    CreateError(
                        "INVALID_DAHUA_GROUP_SESSION",
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        "Start"));
            }

            /*
             * 이미 재생 중이면 중복 Start 명령으로 본다.
             *
             * 각 채널에 Resume 명령을 다시 보내지 않고
             * 정상 처리된 것으로 반환한다.
             */
            if (groupSession.State
                == NvrPlaybackState.Playing)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹이 이미 재생 중입니다.");
            }

            /*
             * OpenAsync 준비 과정이 정상적으로 끝난 그룹만
             * 재생을 시작할 수 있다.
             */
            if (!groupSession.IsReady)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_NOT_READY",
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "Start"));
            }

            /*
             * 준비 상태이더라도 실제 채널이 하나도 없다면
             * 재생을 시작할 수 없다.
             */
            if (groupSession.ChannelCount <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_EMPTY",
                        "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                        "Start"));
            }

            /*
             * OpenAsync가 성공하면 그룹은 Paused 상태가 된다.
             *
             * 일시정지된 그룹만 Start할 수 있으며,
             * Stopped, Completed, Faulted 상태라면
             * 새 그룹을 다시 열어야 한다.
             */
            if (groupSession.State
                != NvrPlaybackState.Paused)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹을 시작할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    CreateError(
                        "DAHUA_GROUP_INVALID_START_STATE",
                        "Dahua 재생 그룹을 시작할 수 없는 상태입니다.",
                        "Start"));
            }

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            /*
             * 일부 채널만 Resume된 상태에서 오류가 발생할 수 있으므로
             * 정상적으로 Resume된 채널을 별도로 보관한다.
             *
             * 이후 채널에서 오류가 발생하면 이 목록의 채널들을
             * 다시 Pause하여 전체 그룹 상태를 복원한다.
             */
            var resumedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                /*
                 * 채널 처리 사이에 취소 요청이 들어온 경우,
                 * 이미 재개된 채널을 다시 일시정지한다.
                 */
                if (cancellationToken.IsCancellationRequested)
                {
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetReady(
                        true,
                        "Dahua 재생 그룹 시작 요청이 취소되어 "
                        + "일시정지 상태로 복원했습니다.");

                    return NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 시작 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_START_CANCELLED",
                            "Dahua 재생 그룹 시작 요청이 취소되었습니다.",
                            "Start"));
                }

                /*
                 * 그룹 내부 채널 또는 실제 Dahua 재생 세션이 없으면
                 * 더 이상 정상적인 그룹 재생을 진행할 수 없다.
                 */
                if (channel == null
                    || channel.Session == null)
                {
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetReady(
                        true,
                        "Dahua 채널 세션 오류로 "
                        + "일시정지 상태를 유지합니다.");

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                            "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                            "Start"));
                }

                /*
                 * 채널별 Dahua 재생 세션을 재개한다.
                 */
                NvrResult resumeResult =
                    await _provider.ResumeAsync(
                        channel.Session,
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    /*
                     * 현재 채널 이전에 이미 Resume된 채널들을
                     * 다시 Pause 상태로 복원한다.
                     *
                     * 일부 화면만 재생되고 나머지는 멈춰 있는
                     * 불완전한 그룹 상태를 방지하기 위한 처리다.
                     */
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetReady(
                        true,
                        "일부 Dahua 채널을 시작하지 못해 "
                        + "전체 그룹을 일시정지 상태로 복원했습니다.");

                    return NvrResult.Fail(
                        resumeResult == null
                            ? NvrResultStatus.Failed
                            : resumeResult.Status,

                        resumeResult == null
                            ? "Dahua 채널 재개 결과가 없습니다."
                            : resumeResult.Message,

                        resumeResult == null
                            ? CreateError(
                                "DAHUA_GROUP_RESUME_RESULT_EMPTY",
                                "Dahua 채널 재개 결과가 없습니다.",
                                "Start")
                            : resumeResult.Error);
                }

                /*
                 * 정상적으로 Resume된 채널만 복원 대상 목록에 추가한다.
                 */
                resumedChannels.Add(
                    channel);
            }

            /*
             * 모든 채널의 Resume가 성공한 뒤에만
             * 그룹 전체 상태를 Playing으로 확정한다.
             */
            groupSession.SetState(
                NvrPlaybackState.Playing);

            groupSession.SetReady(
                true,
                "Dahua 재생 그룹의 모든 채널을 시작했습니다.");

            return NvrResult.Ok(
                "Dahua 재생 그룹을 시작했습니다.");
        }



        /// <summary>
        /// Dahua 재생 그룹의 모든 채널을 일시정지한다.
        /// </summary>
        public Task<NvrResult> PauseAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Pause"));
        }

        /// <summary>
        /// 일시정지된 Dahua 재생 그룹을 재개한다.
        /// </summary>
        public Task<NvrResult> ResumeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Resume"));
        }

        /// <summary>
        /// Dahua 재생 그룹을 지정 시각으로 이동한다.
        /// </summary>
        public Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Seek"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 방향을 변경한다.
        /// </summary>
        public Task<NvrResult> SetDirectionAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackDirection direction,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "SetDirection"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 재생속도를 변경한다.
        /// </summary>
        public Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "SetSpeed"));
        }

        /// <summary>
        /// Dahua 제조사 방식으로 채널 간 동기화를 수행한다.
        /// </summary>
        public Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Synchronize"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 현재 상태를 반환한다.
        ///
        /// 그룹 상태는 제조사 프로젝트가 직접 관리한다.
        /// </summary>
        public Task<NvrResult<NvrPlaybackGroupStatus>>
            GetStatusAsync(
                INvrPlaybackGroupSession session,
                CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<NvrPlaybackGroupStatus>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 상태 조회가 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_STATUS_CANCELLED",
                            "Dahua 재생 그룹 상태 조회가 취소되었습니다.",
                            "GetStatus")));
            }

            DahuaPlaybackGroupSession dahuaSession =
                session as DahuaPlaybackGroupSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    NvrResult<NvrPlaybackGroupStatus>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        CreateError(
                            "INVALID_DAHUA_GROUP_SESSION",
                            "Dahua 재생 그룹 세션이 아닙니다.",
                            "GetStatus")));
            }

            var status =
                new NvrPlaybackGroupStatus
                {
                    CurrentPlaybackTime =
                        dahuaSession.CurrentPlaybackTime,

                    State =
                        dahuaSession.State,

                    Direction =
                        dahuaSession.Direction,

                    Speed =
                        dahuaSession.Speed,

                    IsReady =
                        dahuaSession.IsReady,

                    SynchronizationAvailable =
                        dahuaSession.ChannelCount > 1,

                    IsSynchronized =
                        dahuaSession.IsSynchronized,

                    MaximumDriftSeconds =
                        dahuaSession.MaximumDriftSeconds,

                    Message =
                        dahuaSession.StatusMessage
                };

            return Task.FromResult(
                NvrResult<NvrPlaybackGroupStatus>.Ok(
                    status,
                    "Dahua 재생 그룹 상태를 조회했습니다."));
        }

        /// <summary>
        /// Dahua 재생 그룹의 모든 채널을 중지하고
        /// 재생 핸들과 세션 리소스를 정리한다.
        ///
        /// Stop은 리소스 정리 명령이므로 호출이 시작된 뒤에는
        /// 취소 토큰과 관계없이 가능한 모든 채널 정리를 수행한다.
        ///
        /// 일부 채널 중지에 실패하더라도:
        /// - 다른 채널 정리는 계속한다.
        /// - 그룹 내부 채널 참조를 제거한다.
        /// - 그룹 상태는 Stopped로 변경한다.
        /// </summary>
        public async Task<NvrResult> StopAsync(INvrPlaybackGroupSession session, CancellationToken cancellationToken)
        {
            DahuaPlaybackGroupSession groupSession = session as DahuaPlaybackGroupSession;

            if (groupSession == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹 세션이 아닙니다.",
                    CreateError(
                        "INVALID_DAHUA_GROUP_SESSION",
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        "Stop"));
            }

            /*
             * 이미 중지되어 있고 채널도 없다면
             * 중복 Stop 명령으로 보고 성공 처리한다.
             */
            if (groupSession.State
                    == NvrPlaybackState.Stopped
                && groupSession.ChannelCount == 0)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹이 이미 중지되어 있습니다.");
            }

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var cleanupWarnings =
                new List<string>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (channel == null
                    || channel.Session == null)
                {
                    cleanupWarnings.Add(
                        "정리할 Dahua 채널 세션 정보가 없습니다.");

                    continue;
                }

                try
                {
                    /*
                     * Stop은 반드시 완료되어야 하는 리소스 정리이므로
                     * 취소 토큰 대신 CancellationToken.None을 사용한다.
                     */
                    NvrResult stopResult =
                        await _provider.StopAsync(
                            channel.Session,
                            CancellationToken.None);

                    if (stopResult == null)
                    {
                        cleanupWarnings.Add(
                            "Dahua 채널 중지 결과가 없습니다. "
                            + "ChannelNo="
                            + channel.ChannelNo);
                    }
                    else if (!stopResult.Success)
                    {
                        cleanupWarnings.Add(
                            "Dahua 채널 중지에 실패했습니다. "
                            + "ChannelNo="
                            + channel.ChannelNo
                            + ", Message="
                            + stopResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "Dahua 채널 중지 중 예외가 발생했습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo
                        + ", Error="
                        + ex.Message);
                }

                /*
                 * Provider StopAsync가 실패했거나 예외가 발생한 경우를 대비해
                 * 로컬 세션 Dispose를 한 번 더 시도한다.
                 *
                 * DahuaPlaybackSession.Dispose()는 중복 호출을 허용한다.
                 */
                try
                {
                    channel.Session.Dispose();
                }
                catch (Exception ex)
                {
                    cleanupWarnings.Add(
                        "Dahua 채널 세션 해제 중 예외가 발생했습니다. "
                        + "ChannelNo="
                        + channel.ChannelNo
                        + ", Error="
                        + ex.Message);
                }
            }

            /*
             * SDK 채널 정리 성공 여부와 관계없이
             * 그룹 내부 세션 참조는 제거한다.
             */
            groupSession.ClearChannels();

            groupSession.SetState(
                NvrPlaybackState.Stopped);

            groupSession.SetReady(
                false,
                "Dahua 재생 그룹을 중지했습니다.");

            if (cleanupWarnings.Count > 0)
            {
                /*
                 * 일부 정리 경고가 있어도 그룹의 로컬 상태와
                 * 세션 참조는 모두 정리되었으므로 PartialSuccess로 반환한다.
                 */
                return new NvrResult
                {
                    Success =
                        true,

                    Status =
                        NvrResultStatus.PartialSuccess,

                    Message =
                        "Dahua 재생 그룹은 중지되었지만 "
                        + "일부 채널 정리 경고가 발생했습니다."
                        + Environment.NewLine
                        + string.Join(
                            Environment.NewLine,
                            cleanupWarnings)
                };
            }

            return NvrResult.Ok(
                "Dahua 재생 그룹을 중지했습니다.");
        }



        /// <summary>
        /// 그룹 준비 도중 생성된 Dahua 채널 재생 세션을 모두 정리한다.
        ///
        /// 처리 원칙:
        /// - 한 채널의 정리 실패가 다른 채널 정리를 막지 않는다.
        /// - Provider 로그인 세션은 유지한다.
        /// - 재생 핸들과 채널 세션만 정리한다.
        /// - 정리 과정의 예외가 원래 실패 결과를 덮어쓰지 않도록 한다.
        /// </summary>
        private async Task CleanupGroupAsync(DahuaPlaybackGroupSession groupSession)
        {
            if (groupSession == null)
            {
                return;
            }

            /*
             * GetChannels()는 내부 목록의 복사본을 반환하므로
             * 반복 처리 중 ClearChannels() 등의 영향을 받지 않는다.
             */
            IList<DahuaPlaybackGroupChannel> channels = groupSession.GetChannels();

            foreach (DahuaPlaybackGroupChannel channel in channels)
            {
                if (channel == null || channel.Session == null)
                {
                    continue;
                }

                /*
                 * Provider의 StopAsync를 먼저 호출한다.
                 *
                 * Dahua Provider의 StopAsync는 내부적으로
                 * DahuaPlaybackSession의 재생 핸들을 중지하고
                 * 세션을 Dispose한다.
                 */
                try
                {
                    await _provider.StopAsync(channel.Session, CancellationToken.None);
                }
                catch
                {
                    /*
                     * 한 채널의 Stop 처리 중 예외가 발생해도
                     * 나머지 채널 정리를 계속 진행한다.
                     */
                }

                /*
                 * StopAsync가 실패하거나 예외가 발생했을 가능성에 대비해
                 * 로컬 세션 Dispose를 한 번 더 시도한다.
                 *
                 * DahuaPlaybackSession.Dispose()는 중복 호출을 허용하므로
                 * 이미 정리된 세션이라면 바로 반환한다.
                 */
                try
                {
                    channel.Session.Dispose();
                }
                catch
                {
                    /*
                     * 실패 정리 과정에서 발생한 예외는 무시한다.
                     *
                     * 이 예외가 OpenAsync에서 발생한 원래 오류를
                     * 덮어쓰면 안 된다.
                     */
                }
            }

            /*
             * 실제 Dahua 채널 세션 정리가 끝난 뒤
             * 그룹 내부의 채널 참조를 제거한다.
             */
            groupSession.ClearChannels();

            /*
             * 그룹 상태를 중지 상태로 변경한다.
             */
            groupSession.SetState(NvrPlaybackState.Stopped);

            /*
             * 더 이상 명령을 받을 수 없는 상태로 변경한다.
             */
            groupSession.SetReady(false, "Dahua 재생 그룹 준비 실패로 세션을 정리했습니다.");
        }

        /// <summary>
        /// StartAsync 처리 중 이미 재개된 Dahua 채널들을
        /// 다시 일시정지 상태로 복원한다.
        ///
        /// 일부 채널의 Pause 실패가 다른 채널 복원을
        /// 막지 않도록 각 채널을 독립적으로 처리한다.
        /// </summary>
        private async Task PauseStartedChannelsAsync(IList<DahuaPlaybackGroupChannel> channels)
        {
            if (channels == null
                || channels.Count == 0)
            {
                return;
            }

            /*
             * 마지막으로 시작된 채널부터 역순으로 Pause한다.
             *
             * 반드시 역순이어야 하는 것은 아니지만,
             * 작업 롤백은 일반적으로 실행 순서의 역순으로 처리한다.
             */
            for (int index = channels.Count - 1; index >= 0; index--)
            {
                DahuaPlaybackGroupChannel channel = channels[index];

                if (channel == null || channel.Session == null)
                {
                    continue;
                }

                try
                {
                    await _provider.PauseAsync(channel.Session,CancellationToken.None);
                }
                catch
                {
                    /*
                     * 시작 실패 복원 과정의 예외가
                     * 원래 Resume 실패 결과를 덮어쓰지 않도록 한다.
                     */
                }
            }
        }



        /// <summary>
        /// 아직 구현되지 않은 그룹 반환 명령의 결과를 생성한다.
        /// </summary>
        private static NvrResult<INvrPlaybackGroupSession>
            CreateNotImplementedGroupResult(
                string operation)
        {
            return NvrResult<INvrPlaybackGroupSession>.Fail(
                NvrResultStatus.NotSupported,
                "Dahua 다중채널 재생 엔진의 "
                + operation
                + " 기능은 아직 연결되지 않았습니다.",
                CreateError(
                    "DAHUA_GROUP_ENGINE_NOT_IMPLEMENTED",
                    "Dahua 다중채널 재생 엔진의 "
                    + operation
                    + " 기능은 아직 연결되지 않았습니다.",
                    operation));
        }

        /// <summary>
        /// 아직 구현되지 않은 명령의 결과를 생성한다.
        /// </summary>
        private static NvrResult
            CreateNotImplementedResult(
                string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.NotSupported,
                "Dahua 다중채널 재생 엔진의 "
                + operation
                + " 기능은 아직 연결되지 않았습니다.",
                CreateError(
                    "DAHUA_GROUP_ENGINE_NOT_IMPLEMENTED",
                    "Dahua 다중채널 재생 엔진의 "
                    + operation
                    + " 기능은 아직 연결되지 않았습니다.",
                    operation));
        }

        /// <summary>
        /// Dahua 그룹 재생 오류 정보를 생성한다.
        /// </summary>
        private static NvrErrorInfo CreateError(
            string errorCode,
            string errorMessage,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    errorMessage,

                Operation =
                    operation
            };
        }
    }
}