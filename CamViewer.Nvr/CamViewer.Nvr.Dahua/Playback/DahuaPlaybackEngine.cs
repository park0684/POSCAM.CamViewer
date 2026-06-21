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
        private readonly DahuaPlaybackSynchronizer _synchronizer;
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
                     * Dahua 정렬 기능을 사용하여 실제 OSD가 목표 시각 부근에
                     * 도착할 때까지 확인한다.
                     *
                     * 단순 SeekAsync만 호출하면 SDK 내부 위치는 이동했더라도
                     * 일시정지 화면의 OSD가 이전 시각에 머물 수 있다.
                     *
                     * 이후 SynchronizeAsync가 정확한 OSD 시각을 읽을 수 있도록
                     * Open 단계에서 실제 재생 위치까지 확정한다.
                     */
                    if (providerInitialTime != providerStartTime)
                    {
                        var alignmentRequest = new NvrPlaybackAlignmentRequest
                            {
                                TargetTime = providerInitialTime,

                                Direction = NvrPlaybackDirection.Forward,

                                Speed = NvrPlaybackSpeed.Normal,

                                /*
                                 * OpenAsync의 성공 상태는 Paused + Ready이므로
                                 * 정렬이 끝난 채널도 일시정지 상태를 유지한다.
                                 */
                                RemainPaused = true
                            };

                        NvrResult<INvrPlaybackSession> alignmentResult =
                            await _provider.AlignPlaybackAsync(
                                dahuaSession,
                                alignmentRequest,
                                cancellationToken);

                        if (alignmentResult == null
                            || !alignmentResult.Success
                            || alignmentResult.Data == null)
                        {
                            await CleanupGroupAsync(
                                groupSession);

                            return NvrResult<INvrPlaybackGroupSession>.Fail(
                                alignmentResult == null
                                    ? NvrResultStatus.Failed
                                    : alignmentResult.Status,

                                alignmentResult == null
                                    ? "Dahua 채널 최초 위치 정렬 결과가 없습니다."
                                    : alignmentResult.Message,

                                alignmentResult == null
                                    ? CreateError(
                                        "DAHUA_GROUP_INITIAL_ALIGNMENT_RESULT_EMPTY",
                                        "Dahua 채널 최초 위치 정렬 결과가 없습니다.",
                                        "Open")
                                    : alignmentResult.Error);
                        }

                        DahuaPlaybackSession alignedSession =
                            alignmentResult.Data
                                as DahuaPlaybackSession;

                        if (alignedSession == null)
                        {
                            await CleanupGroupAsync(
                                groupSession);

                            return NvrResult<INvrPlaybackGroupSession>.Fail(
                                NvrResultStatus.Failed,
                                "Dahua 최초 위치 정렬 결과의 세션 형식이 올바르지 않습니다.",
                                CreateError(
                                    "INVALID_DAHUA_INITIAL_ALIGNMENT_SESSION",
                                    "Dahua 최초 위치 정렬 결과의 세션 형식이 올바르지 않습니다.",
                                    "Open"));
                        }

                        /*
                         * 현재 Dahua AlignPlaybackAsync는 기존 세션을 그대로 반환하지만,
                         * 향후 Provider가 재생 세션을 새로 생성하여 반환할 수도 있다.
                         *
                         * INvrPlaybackAlignmentProvider 계약에 따라 새 세션이 반환된 경우
                         * 기존 네이티브 세션 정리는 Provider 내부에서 수행된다.
                         */
                        if (!object.ReferenceEquals(
                                groupChannel.Session,
                                alignedSession))
                        {
                            groupChannel.ReplaceSession(
                                alignedSession);
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

            /*
             * 다중채널 재생 그룹은 실제 재생을 시작하기 전에
             * Dahua 제조사 방식으로 채널 시간을 동기화한다.
             *
             * 현재 그룹은 OpenAsync 완료 후 Paused 상태이므로
             * SynchronizeAsync는 다음 작업을 수행한다.
             *
             * 1. 각 채널의 실제 OSD 영상재생시간 조회
             * 2. 채널별 시간 보정값을 제거하여 공통 시각으로 변환
             * 3. 허용 시간차를 초과하는 채널 정렬
             * 4. 실제 OSD 시간을 다시 확인
             * 5. 모든 채널을 Paused 상태로 유지
             *
             * 동기화가 완료된 후에만 아래 Resume 루프에서
             * 모든 채널을 순서대로 시작한다.
             */
            if (groupSession.ChannelCount > 1)
            {
                NvrResult synchronizeResult =
                    await _synchronizer.SynchronizeAsync(
                        groupSession,
                        cancellationToken);

                if (synchronizeResult == null
                    || !synchronizeResult.Success)
                {
                    /*
                     * 동기화 중 일부 채널의 위치가 변경되었을 수 있으므로
                     * 실패 시 자동으로 재생을 시작하지 않는다.
                     *
                     * 사용자가 다시 시도할 수 있도록
                     * 그룹은 Paused + Ready 상태를 유지한다.
                     */
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetReady(
                        true,
                        "Dahua 채널 동기화에 실패하여 "
                        + "일시정지 상태로 유지합니다.");

                    return NvrResult.Fail(
                        synchronizeResult == null
                            ? NvrResultStatus.Failed
                            : synchronizeResult.Status,

                        synchronizeResult == null
                            ? "Dahua 재생 그룹 동기화 결과가 없습니다."
                            : synchronizeResult.Message,

                        synchronizeResult == null
                            ? CreateError(
                                "DAHUA_GROUP_START_SYNC_RESULT_EMPTY",
                                "Dahua 재생 그룹 동기화 결과가 없습니다.",
                                "Start")
                            : synchronizeResult.Error);
                }

                /*
                 * StartAsync에서 호출한 동기화는
                 * 재생을 직접 시작하지 않고 Paused 상태로 반환해야 한다.
                 *
                 * 예상하지 못한 상태가 반환되면
                 * 일부 채널에 Resume 명령을 보내지 않고 중단한다.
                 */
                if (groupSession.State
                    != NvrPlaybackState.Paused)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 채널 동기화 후 그룹 상태가 올바르지 않습니다. "
                        + "State="
                        + groupSession.State,
                        CreateError(
                            "DAHUA_GROUP_INVALID_STATE_AFTER_SYNC",
                            "Dahua 채널 동기화 후 그룹 상태가 "
                            + "Paused가 아닙니다.",
                            "Start"));
                }
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
        /// 재생 중인 Dahua 그룹의 모든 채널을 일시정지한다.
        ///
        /// 처리 순서:
        /// 1. 그룹 세션과 현재 상태 확인
        /// 2. 채널별 Pause 명령 수행
        /// 3. 일부 채널 Pause 실패 시 이미 멈춘 채널을 다시 Resume
        /// 4. 모든 채널 성공 시 그룹 상태를 Paused로 변경
        ///
        /// 일시정지하더라도 Direction은 변경하지 않는다.
        /// 이후 ResumeAsync가 기존 방향을 기준으로 상태를 복원한다.
        /// </summary>
        public async Task<NvrResult> PauseAsync(INvrPlaybackGroupSession session, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 일시정지 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_PAUSE_CANCELLED",
                        "Dahua 재생 그룹 일시정지 요청이 취소되었습니다.",
                        "Pause"));
            }

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
                        "Pause"));
            }

            /*
             * 이미 일시정지된 상태라면 중복 명령으로 보고
             * SDK Pause 명령을 다시 호출하지 않는다.
             */
            if (groupSession.State
                == NvrPlaybackState.Paused)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹이 이미 일시정지되어 있습니다.");
            }

            if (!groupSession.IsReady)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_NOT_READY",
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "Pause"));
            }

            if (groupSession.ChannelCount <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_EMPTY",
                        "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                        "Pause"));
            }

            /*
             * 정방향 재생 또는 역방향 재생 중일 때만
             * 일시정지 명령을 허용한다.
             */
            if (groupSession.State != NvrPlaybackState.Playing
                && groupSession.State != NvrPlaybackState.Rewinding)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹을 일시정지할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    CreateError(
                        "DAHUA_GROUP_INVALID_PAUSE_STATE",
                        "Dahua 재생 그룹을 일시정지할 수 없는 상태입니다.",
                        "Pause"));
            }

            /*
             * Pause 실패 시 원래 상태로 돌려놓기 위해
             * 현재 그룹 상태를 보관한다.
             */
            NvrPlaybackState originalState =
                groupSession.State;

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var pausedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    /*
                     * 이미 Pause된 채널을 다시 Resume하여
                     * 취소 전의 재생 상태로 복원한다.
                     */
                    await ResumePausedChannelsAsync(
                        pausedChannels);

                    groupSession.SetState(
                        originalState);

                    return NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 일시정지 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_PAUSE_CANCELLED",
                            "Dahua 재생 그룹 일시정지 요청이 취소되었습니다.",
                            "Pause"));
                }

                if (channel == null
                    || channel.Session == null)
                {
                    await ResumePausedChannelsAsync(
                        pausedChannels);

                    groupSession.SetState(
                        originalState);

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                            "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                            "Pause"));
                }

                NvrResult pauseResult =
                    await _provider.PauseAsync(
                        channel.Session,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    /*
                     * 일부 채널만 정지된 상태를 방지하기 위해
                     * 앞에서 Pause된 채널들을 다시 Resume한다.
                     */
                    await ResumePausedChannelsAsync(
                        pausedChannels);

                    groupSession.SetState(
                        originalState);

                    return NvrResult.Fail(
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
                                "Pause")
                            : pauseResult.Error);
                }

                pausedChannels.Add(
                    channel);
            }

            /*
             * 모든 채널의 Pause가 성공한 이후에만
             * 그룹 전체 상태를 Paused로 확정한다.
             *
             * Direction은 유지한다.
             */
            groupSession.SetState(
                NvrPlaybackState.Paused);

            groupSession.SetReady(
                true,
                "Dahua 재생 그룹의 모든 채널을 일시정지했습니다.");

            return NvrResult.Ok(
                "Dahua 재생 그룹을 일시정지했습니다.");
        }



        /// <summary>
        /// 일시정지된 Dahua 그룹의 모든 채널을 재개한다.
        ///
        /// 처리 순서:
        /// 1. 그룹 세션과 현재 상태 확인
        /// 2. 채널별 Resume 명령 수행
        /// 3. 일부 채널 Resume 실패 시 이미 시작된 채널을 다시 Pause
        /// 4. 기존 Direction에 따라 Playing 또는 Rewinding 상태로 복원
        /// </summary>
        public async Task<NvrResult> ResumeAsync(INvrPlaybackGroupSession session, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 재개 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_RESUME_CANCELLED",
                        "Dahua 재생 그룹 재개 요청이 취소되었습니다.",
                        "Resume"));
            }

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
                        "Resume"));
            }

            /*
             * 이미 현재 방향으로 재생 중이면
             * 중복 Resume 요청으로 처리한다.
             */
            if (groupSession.State == NvrPlaybackState.Playing
                || groupSession.State == NvrPlaybackState.Rewinding)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹이 이미 재생 중입니다.");
            }

            if (!groupSession.IsReady)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_NOT_READY",
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "Resume"));
            }

            if (groupSession.ChannelCount <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_EMPTY",
                        "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                        "Resume"));
            }

            if (groupSession.State
                != NvrPlaybackState.Paused)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹을 재개할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    CreateError(
                        "DAHUA_GROUP_INVALID_RESUME_STATE",
                        "Dahua 재생 그룹을 재개할 수 없는 상태입니다.",
                        "Resume"));
            }

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var resumedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    /*
                     * 이미 Resume된 채널을 다시 Pause하여
                     * 일시정지 상태로 복원한다.
                     */
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    return NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 재개 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_RESUME_CANCELLED",
                            "Dahua 재생 그룹 재개 요청이 취소되었습니다.",
                            "Resume"));
                }

                if (channel == null
                    || channel.Session == null)
                {
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                            "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                            "Resume"));
                }

                NvrResult resumeResult =
                    await _provider.ResumeAsync(
                        channel.Session,
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    await PauseStartedChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

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
                                "Resume")
                            : resumeResult.Error);
                }

                resumedChannels.Add(
                    channel);
            }

            /*
             * Pause 중에도 Direction은 유지되므로
             * 현재 방향을 기준으로 그룹 상태를 복원한다.
             */
            if (groupSession.Direction
                == NvrPlaybackDirection.Reverse)
            {
                groupSession.SetState(
                    NvrPlaybackState.Rewinding);
            }
            else
            {
                groupSession.SetState(
                    NvrPlaybackState.Playing);
            }

            groupSession.SetReady(
                true,
                "Dahua 재생 그룹의 모든 채널을 재개했습니다.");

            return NvrResult.Ok(
                "Dahua 재생 그룹을 재개했습니다.");
        }


        /// <summary>
        /// Dahua 재생 그룹의 모든 채널을 지정된 공통 영상 시각으로 이동한다.
        ///
        /// 처리 순서:
        /// 1. 그룹과 목표 시각 검증
        /// 2. 현재 재생 상태 보관
        /// 3. 재생 중이면 전체 채널 일시정지
        /// 4. 채널별 시간 보정값을 적용한 Provider 목표 시각 계산
        /// 5. Dahua AlignPlaybackAsync를 이용해 실제 OSD 시각까지 정렬
        /// 6. 그룹의 공통 영상재생시간 갱신
        /// 7. Seek 전 재생 중이었다면 전체 채널 재개
        ///
        /// Seek 실패 시 일부 채널만 재생되는 상태를 방지하기 위해
        /// 그룹은 일시정지 상태로 유지한다.
        /// </summary>
        public async Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 이동 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_SEEK_CANCELLED",
                        "Dahua 재생 그룹 이동 요청이 취소되었습니다.",
                        "Seek"));
            }

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
                        "Seek"));
            }

            if (!groupSession.IsReady)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_NOT_READY",
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "Seek"));
            }

            if (groupSession.ChannelCount <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_EMPTY",
                        "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                        "Seek"));
            }

            /*
             * 종료시간은 재생 구간에 포함되지 않는다.
             *
             * 따라서 StartTime <= targetTime < EndTime 조건을 사용한다.
             */
            if (targetTime < groupSession.StartTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "이동할 영상 시각은 조회 시작시간보다 이전일 수 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_SEEK_BEFORE_START",
                        "이동할 영상 시각은 조회 시작시간보다 이전일 수 없습니다.",
                        "Seek"));
            }

            if (targetTime >= groupSession.EndTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "이동할 영상 시각은 조회 종료시간보다 이전이어야 합니다.",
                    CreateError(
                        "DAHUA_GROUP_SEEK_AFTER_END",
                        "이동할 영상 시각은 조회 종료시간보다 이전이어야 합니다.",
                        "Seek"));
            }

            /*
             * 현재 Dahua AlignPlaybackAsync는 정방향 정렬만 지원한다.
             *
             * 역방향 Seek는 이후 SetDirectionAsync와 함께
             * 역재생 세션 재생성 방식으로 구현한다.
             */
            if (groupSession.Direction
                != NvrPlaybackDirection.Forward)
            {
                return NvrResult.Fail(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua 그룹 Seek는 정방향 재생만 지원합니다.",
                    CreateError(
                        "DAHUA_GROUP_REVERSE_SEEK_NOT_SUPPORTED",
                        "현재 Dahua 그룹 Seek는 정방향 재생만 지원합니다.",
                        "Seek"));
            }

            /*
             * 기존 Dahua AlignPlaybackAsync는 현재 1배속 정렬만 지원한다.
             *
             * 배속 상태의 Seek는 SetSpeedAsync 구현 이후
             * 임시 1배속 전환 및 기존 배속 복원 방식으로 확장한다.
             */
            if (groupSession.Speed
                != NvrPlaybackSpeed.Normal)
            {
                return NvrResult.Fail(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua 그룹 Seek는 1배속에서만 지원합니다.",
                    CreateError(
                        "DAHUA_GROUP_SEEK_SPEED_NOT_SUPPORTED",
                        "현재 Dahua 그룹 Seek는 1배속에서만 지원합니다.",
                        "Seek"));
            }

            /*
             * 재생 중 또는 일시정지 상태에서만 Seek를 허용한다.
             */
            if (groupSession.State != NvrPlaybackState.Playing
                && groupSession.State != NvrPlaybackState.Paused)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹을 이동할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    CreateError(
                        "DAHUA_GROUP_INVALID_SEEK_STATE",
                        "Dahua 재생 그룹을 이동할 수 없는 상태입니다.",
                        "Seek"));
            }

            bool wasPlaying =
                groupSession.State
                    == NvrPlaybackState.Playing;

            /*
             * 재생 중 Seek라면 먼저 전체 채널을 Pause한다.
             *
             * 채널이 움직이는 상태에서 순차적으로 Seek하면
             * 채널마다 기준 시각이 달라질 수 있기 때문이다.
             */
            if (wasPlaying)
            {
                NvrResult pauseResult =
                    await PauseAsync(
                        groupSession,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    return NvrResult.Fail(
                        pauseResult == null
                            ? NvrResultStatus.Failed
                            : pauseResult.Status,

                        pauseResult == null
                            ? "Dahua 재생 그룹 일시정지 결과가 없습니다."
                            : pauseResult.Message,

                        pauseResult == null
                            ? CreateError(
                                "DAHUA_GROUP_SEEK_PAUSE_RESULT_EMPTY",
                                "Dahua 재생 그룹 일시정지 결과가 없습니다.",
                                "Seek")
                            : pauseResult.Error);
                }
            }

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    /*
                     * 일부 채널의 위치가 이미 변경되었을 수 있으므로
                     * 자동 재개하지 않고 전체 그룹을 Paused 상태로 유지한다.
                     */
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "Dahua 그룹 Seek가 취소되어 "
                        + "일시정지 상태로 유지합니다.");

                    return NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 이동 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_SEEK_CANCELLED",
                            "Dahua 재생 그룹 이동 요청이 취소되었습니다.",
                            "Seek"));
                }

                if (channel == null
                    || channel.Session == null)
                {
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "Dahua 채널 세션 오류로 "
                        + "그룹을 일시정지 상태로 유지합니다.");

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                            "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                            "Seek"));
                }

                /*
                 * CamViewer의 공통 영상 시각을
                 * 해당 채널의 실제 NVR 영상 시각으로 변환한다.
                 */
                DateTime providerTargetTime =
                    channel.ToProviderTime(
                        targetTime);

                var alignmentRequest =
                    new NvrPlaybackAlignmentRequest
                    {
                        TargetTime =
                            providerTargetTime,

                        Direction =
                            NvrPlaybackDirection.Forward,

                        Speed =
                            NvrPlaybackSpeed.Normal,

                        /*
                         * 모든 채널 정렬이 끝난 뒤 한 번에 Resume할 수 있도록
                         * 각 채널은 정렬 후 Paused 상태로 유지한다.
                         */
                        RemainPaused =
                            true
                    };

                NvrResult<INvrPlaybackSession> alignmentResult =
                    await _provider.AlignPlaybackAsync(
                        channel.Session,
                        alignmentRequest,
                        cancellationToken);

                if (alignmentResult == null
                    || !alignmentResult.Success
                    || alignmentResult.Data == null)
                {
                    /*
                     * 일부 채널만 새로운 위치에 도착했을 수 있으므로
                     * 자동 재개하지 않는다.
                     *
                     * 사용자가 같은 Seek 명령을 다시 실행하거나
                     * 그룹을 다시 열 수 있도록 Paused + Ready 상태로 유지한다.
                     */
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetReady(
                        true,
                        "일부 Dahua 채널의 위치 이동에 실패하여 "
                        + "그룹을 일시정지 상태로 유지합니다.");

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "일부 Dahua 채널의 Seek 정렬에 실패했습니다.");

                    return NvrResult.Fail(
                        alignmentResult == null
                            ? NvrResultStatus.Failed
                            : alignmentResult.Status,

                        alignmentResult == null
                            ? "Dahua 채널 정렬 결과가 없습니다."
                            : alignmentResult.Message,

                        alignmentResult == null
                            ? CreateError(
                                "DAHUA_GROUP_ALIGNMENT_RESULT_EMPTY",
                                "Dahua 채널 정렬 결과가 없습니다.",
                                "Seek")
                            : alignmentResult.Error);
                }

                DahuaPlaybackSession alignedSession =
                    alignmentResult.Data
                        as DahuaPlaybackSession;

                if (alignedSession == null)
                {
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "Dahua 채널 정렬 결과의 세션 형식이 올바르지 않습니다.");

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua Provider가 다른 형식의 재생 세션을 반환했습니다.",
                        CreateError(
                            "INVALID_DAHUA_ALIGNMENT_SESSION",
                            "Dahua Provider가 다른 형식의 재생 세션을 반환했습니다.",
                            "Seek"));
                }

                /*
                 * 현재 구현은 같은 DahuaPlaybackSession을 반환하지만,
                 * 향후 Provider 내부에서 세션을 재생성할 가능성에 대비한다.
                 */
                if (!object.ReferenceEquals(
                        channel.Session,
                        alignedSession))
                {
                    DahuaPlaybackSession previousSession =
                        channel.Session;

                    try
                    {
                        await _provider.StopAsync(
                            previousSession,
                            CancellationToken.None);
                    }
                    catch
                    {
                        /*
                         * 이전 세션 정리 실패가
                         * 새 세션 등록을 막지 않게 한다.
                         */
                    }

                    channel.ReplaceSession(
                        alignedSession);
                }
            }

            /*
             * 모든 채널 정렬이 성공한 경우에만
             * 그룹의 공통 영상재생시간을 변경한다.
             */
            groupSession.SetCurrentPlaybackTime(
                targetTime);

            if (groupSession.ChannelCount <= 1)
            {
                groupSession.SetSynchronizationStatus(
                    true,
                    0d,
                    "단일 Dahua 채널의 위치 이동이 완료되었습니다.");
            }
            else
            {
                /*
                 * 각 채널은 목표 시각 부근까지 정렬되었지만
                 * 실제 채널 간 최대 시간차는 아직 계산하지 않았다.
                 *
                 * 이후 SynchronizeAsync에서 실제 OSD를 비교한다.
                 */
                groupSession.SetSynchronizationStatus(
                    false,
                    null,
                    "Dahua 채널 위치 이동이 완료되었습니다. "
                    + "실제 채널 간 동기화 확인이 필요합니다.");
            }

            /*
             * Seek 이전에 재생 중이었다면
             * 모든 채널 정렬이 완료된 후 다시 재생한다.
             *
             * Seek 이전이 Paused 상태였다면 그대로 유지한다.
             */
            if (wasPlaying)
            {
                NvrResult resumeResult =
                    await ResumeAsync(
                        groupSession,
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    /*
                     * ResumeAsync 실패 시에도 모든 채널은
                     * Paused 상태로 복원되므로 안전한 상태가 유지된다.
                     */
                    return NvrResult.Fail(
                        resumeResult == null
                            ? NvrResultStatus.Failed
                            : resumeResult.Status,

                        resumeResult == null
                            ? "Dahua 재생 그룹 재개 결과가 없습니다."
                            : resumeResult.Message,

                        resumeResult == null
                            ? CreateError(
                                "DAHUA_GROUP_SEEK_RESUME_RESULT_EMPTY",
                                "Dahua 재생 그룹 재개 결과가 없습니다.",
                                "Seek")
                            : resumeResult.Error);
                }

                return NvrResult.Ok(
                    "Dahua 재생 그룹의 위치를 이동하고 재생을 재개했습니다.");
            }

            groupSession.SetState(
                NvrPlaybackState.Paused);

            groupSession.SetReady(
                true,
                "Dahua 재생 그룹의 위치를 이동했습니다.");

            return NvrResult.Ok(
                "Dahua 재생 그룹의 위치를 이동했습니다.");
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
        /// Dahua 재생 그룹의 모든 채널 재생속도를 변경한다.
        ///
        /// 처리 순서:
        /// 1. 그룹과 현재 상태 검증
        /// 2. 속도 변경 전 실제 영상재생시간 확인
        /// 3. 채널별 재생속도 변경
        /// 4. 일부 채널 실패 시 이전 속도로 복원
        /// 5. 모든 채널 성공 후 그룹 속도 확정
        ///
        /// 정책:
        /// - Playing 상태에서 변경하면 Playing 상태를 유지한다.
        /// - Rewinding 상태에서 변경하면 Rewinding 상태를 유지한다.
        /// - Paused 상태에서 변경하면 Paused 상태를 유지한다.
        /// - 속도 변경만으로 재생 상태를 변경하지 않는다.
        /// </summary>
        public async Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생속도 변경 요청이 취소되었습니다.",
                    CreateError(
                        "DAHUA_GROUP_SPEED_CANCELLED",
                        "Dahua 재생속도 변경 요청이 취소되었습니다.",
                        "SetSpeed"));
            }

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
                        "SetSpeed"));
            }

            if (!groupSession.IsReady)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    CreateError(
                        "DAHUA_GROUP_NOT_READY",
                        "Dahua 재생 그룹이 준비되지 않았습니다.",
                        "SetSpeed"));
            }

            if (groupSession.ChannelCount <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_CHANNEL_EMPTY",
                        "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                        "SetSpeed"));
            }

            /*
             * 정의되지 않은 enum 값이 전달되면
             * Dahua SDK 명령을 실행하지 않는다.
             */
            if (!Enum.IsDefined(
                    typeof(NvrPlaybackSpeed),
                    speed))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "지원하지 않는 Dahua 재생속도입니다. "
                    + "Speed="
                    + speed,
                    CreateError(
                        "DAHUA_GROUP_INVALID_SPEED",
                        "지원하지 않는 Dahua 재생속도입니다.",
                        "SetSpeed"));
            }

            /*
             * 재생, 역재생 또는 일시정지 상태에서만
             * 속도 변경을 허용한다.
             */
            if (groupSession.State != NvrPlaybackState.Playing
                && groupSession.State != NvrPlaybackState.Rewinding
                && groupSession.State != NvrPlaybackState.Paused)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생속도를 변경할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    CreateError(
                        "DAHUA_GROUP_INVALID_SPEED_STATE",
                        "Dahua 재생속도를 변경할 수 없는 상태입니다.",
                        "SetSpeed"));
            }

            /*
             * 동일한 속도가 다시 전달되면
             * SDK 명령을 반복 호출하지 않는다.
             */
            if (groupSession.Speed == speed)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹이 이미 요청한 속도로 설정되어 있습니다.");
            }

            NvrPlaybackSpeed previousSpeed =
                groupSession.Speed;

            NvrPlaybackState previousState =
                groupSession.State;

            /*
             * 재생 중 속도를 변경하는 경우,
             * 이전 속도가 적용된 마지막 실제 OSD 시간을 먼저 읽는다.
             *
             * 이 시간을 그룹의 새로운 시간 기준점으로 저장한 후
             * 채널 속도를 변경한다.
             *
             * Paused 상태에서는 시간이 진행되지 않으므로
             * 별도의 OSD 기준점 갱신이 필요하지 않다.
             */
            if (previousState == NvrPlaybackState.Playing
                || previousState == NvrPlaybackState.Rewinding)
            {
                NvrResult captureTimeResult =
                    await CapturePlaybackTimeBeforeSpeedChangeAsync(
                        groupSession,
                        cancellationToken);

                if (captureTimeResult == null
                    || !captureTimeResult.Success)
                {
                    return captureTimeResult
                        ?? NvrResult.Fail(
                            NvrResultStatus.Failed,
                            "속도 변경 전 영상재생시간 확인 결과가 없습니다.",
                            CreateError(
                                "DAHUA_GROUP_SPEED_TIME_RESULT_EMPTY",
                                "속도 변경 전 영상재생시간 확인 결과가 없습니다.",
                                "SetSpeed"));
                }
            }

            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            /*
             * 일부 채널만 속도 변경에 성공할 가능성에 대비해
             * 성공한 채널을 별도로 보관한다.
             */
            var changedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    bool rollbackSucceeded =
                        await RestoreChannelSpeedsAsync(
                            changedChannels,
                            previousSpeed);

                    if (!rollbackSucceeded)
                    {
                        SetSpeedRollbackFailedState(
                            groupSession);
                    }
                    else
                    {
                        groupSession.SetSpeed(
                            previousSpeed);

                        groupSession.SetState(
                            previousState);
                    }

                    return NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생속도 변경 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_SPEED_CANCELLED",
                            "Dahua 재생속도 변경 요청이 취소되었습니다.",
                            "SetSpeed"));
                }

                if (channel == null
                    || channel.Session == null)
                {
                    bool rollbackSucceeded =
                        await RestoreChannelSpeedsAsync(
                            changedChannels,
                            previousSpeed);

                    if (!rollbackSucceeded)
                    {
                        SetSpeedRollbackFailedState(
                            groupSession);
                    }
                    else
                    {
                        groupSession.SetSpeed(
                            previousSpeed);

                        groupSession.SetState(
                            previousState);
                    }

                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                            "Dahua 재생 그룹의 채널 세션 정보가 없습니다.",
                            "SetSpeed"));
                }

                /*
                 * Dahua Provider는 채널이 Paused 상태였다면
                 * 속도 변경 후 다시 Pause를 적용한다.
                 *
                 * 따라서 이 엔진에서 별도로 Resume하거나 Pause하지 않는다.
                 */
                NvrResult speedResult =
                    await _provider.SetPlaybackSpeedAsync(
                        channel.Session,
                        speed,
                        cancellationToken);

                if (speedResult == null
                    || !speedResult.Success)
                {
                    bool rollbackSucceeded =
                        await RestoreChannelSpeedsAsync(
                            changedChannels,
                            previousSpeed);

                    if (!rollbackSucceeded)
                    {
                        SetSpeedRollbackFailedState(
                            groupSession);
                    }
                    else
                    {
                        groupSession.SetSpeed(
                            previousSpeed);

                        groupSession.SetState(
                            previousState);

                        groupSession.SetReady(
                            true,
                            "Dahua 재생속도 변경에 실패하여 "
                            + "이전 속도로 복원했습니다.");
                    }

                    return NvrResult.Fail(
                        speedResult == null
                            ? NvrResultStatus.Failed
                            : speedResult.Status,

                        speedResult == null
                            ? "Dahua 채널 속도 변경 결과가 없습니다."
                            : speedResult.Message,

                        speedResult == null
                            ? CreateError(
                                "DAHUA_GROUP_SPEED_RESULT_EMPTY",
                                "Dahua 채널 속도 변경 결과가 없습니다.",
                                "SetSpeed")
                            : speedResult.Error);
                }

                changedChannels.Add(
                    channel);
            }

            /*
             * 모든 채널의 속도 변경이 성공한 경우에만
             * 그룹 전체 속도를 변경한다.
             */
            groupSession.SetSpeed(
                speed);

            /*
             * 속도 변경 전 상태를 그대로 유지한다.
             *
             * 채널별 실제 상태도 Dahua Provider가 보존하므로
             * 여기서는 그룹 상태만 이전 상태로 확정한다.
             */
            groupSession.SetState(
                previousState);

            groupSession.SetReady(
                true,
                "Dahua 재생 그룹의 속도를 변경했습니다.");

            /*
             * 단일 채널은 채널 간 시간차가 존재하지 않으므로
             * 즉시 동기화 완료 상태로 처리한다.
             */
            if (groupSession.ChannelCount <= 1)
            {
                groupSession.SetSynchronizationStatus(
                    true,
                    0d,
                    "단일 Dahua 채널의 재생속도를 변경했습니다.");

                return NvrResult.Ok(
                    "Dahua 재생 그룹의 속도를 변경했습니다. "
                    + GetPlaybackSpeedText(speed));
            }

            /*
             * 배속 재생 중에는 Dahua 정렬 기능을 실행하지 않는다.
             *
             * 현재 DahuaPlaybackSynchronizer는 정방향 1배속 상태에서만
             * 실제 OSD 시각을 기준으로 채널을 정렬한다.
             */
            if (speed != NvrPlaybackSpeed.Normal
                || groupSession.Direction
                    != NvrPlaybackDirection.Forward)
            {
                groupSession.SetSynchronizationStatus(
                    false,
                    null,
                    "Dahua 재생속도가 변경되었습니다. "
                    + "1배속 정방향으로 복귀한 뒤 "
                    + "채널 동기화가 필요합니다.");

                return NvrResult.Ok(
                    "Dahua 재생 그룹의 속도를 변경했습니다. "
                    + GetPlaybackSpeedText(speed));
            }

            /*
             * 다중채널이 1배속 정방향으로 복귀한 경우
             * 실제 OSD 시각을 다시 확인하여 채널을 동기화한다.
             *
             * SynchronizeAsync는:
             * - Playing이면 일시정지 후 동기화하고 다시 Playing으로 복원
             * - Paused이면 동기화 후 Paused 상태 유지
             *
             * 속도 변경 직전 상태는 previousState에 보관되어 있다.
             */
            NvrResult synchronizeResult =
                await _synchronizer.SynchronizeAsync(
                    groupSession,
                    cancellationToken);

            if (synchronizeResult != null
                && synchronizeResult.Success)
            {
                return NvrResult.Ok(
                    "Dahua 재생 그룹의 속도를 "
                    + GetPlaybackSpeedText(speed)
                    + "으로 변경하고 채널 동기화를 완료했습니다.");
            }

            /*
             * 속도 변경 자체는 이미 성공했다.
             *
             * 동기화 실패로 그룹이 Paused 상태가 되었더라도
             * 속도 변경 전 Playing 상태였다면 재생 상태를 복원한다.
             */
            NvrResult stateRestoreResult =
                await RestoreStateAfterSpeedSynchronizationAsync(
                    groupSession,
                    previousState);

            if (stateRestoreResult == null
                || !stateRestoreResult.Success)
            {
                groupSession.SetState(
                    NvrPlaybackState.Faulted);

                groupSession.SetReady(
                    false,
                    "속도 변경 후 재생 상태를 복원하지 못했습니다.");

                groupSession.SetSynchronizationStatus(
                    false,
                    null,
                    "속도 변경 후 동기화 및 상태 복원에 실패했습니다.");

                return NvrResult.Fail(
                    stateRestoreResult == null
                        ? NvrResultStatus.Failed
                        : stateRestoreResult.Status,

                    stateRestoreResult == null
                        ? "속도 변경 후 재생 상태 복원 결과가 없습니다."
                        : stateRestoreResult.Message,

                    stateRestoreResult == null
                        ? CreateError(
                            "DAHUA_GROUP_SPEED_STATE_RESTORE_RESULT_EMPTY",
                            "속도 변경 후 재생 상태 복원 결과가 없습니다.",
                            "SetSpeed")
                        : stateRestoreResult.Error);
            }

            /*
             * 속도 변경은 성공했지만 동기화는 실패했다.
             *
             * 기존 재생 상태는 복원되었으므로 완전 실패가 아니라
             * PartialSuccess로 반환한다.
             */
            groupSession.SetSynchronizationStatus(
                false,
                null,
                "재생속도는 변경되었지만 "
                + "채널 동기화를 완료하지 못했습니다.");

            return new NvrResult
            {
                Success =
                    true,

                Status =
                    NvrResultStatus.PartialSuccess,

                Message =
                    "Dahua 재생 그룹의 속도는 "
                    + GetPlaybackSpeedText(speed)
                    + "으로 변경되었지만 채널 동기화에 실패했습니다. "
                    + (
                        synchronizeResult == null
                            ? "동기화 결과가 없습니다."
                            : synchronizeResult.Message
                    ),

                Error =
                    synchronizeResult == null
                        ? CreateError(
                            "DAHUA_GROUP_SPEED_SYNC_RESULT_EMPTY",
                            "속도 변경 후 채널 동기화 결과가 없습니다.",
                            "SetSpeed")
                        : synchronizeResult.Error
            };
        }

        /// <summary>
        /// Dahua 제조사 방식으로 재생 그룹의 채널 시간을 동기화한다.
        ///
        /// 실제 OSD 조회, 채널별 보정값 적용, 허용 오차 판단,
        /// 재시도 및 재생 상태 복원은 DahuaPlaybackSynchronizer가 처리한다.
        /// </summary>
        public Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            DahuaPlaybackGroupSession groupSession =
                session as DahuaPlaybackGroupSession;

            if (groupSession == null)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        CreateError(
                            "INVALID_DAHUA_GROUP_SESSION",
                            "Dahua 재생 그룹 세션이 아닙니다.",
                            "Synchronize")));
            }

            return _synchronizer.SynchronizeAsync(
                groupSession,
                cancellationToken);
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
        /// PauseAsync 처리 중 이미 일시정지된 Dahua 채널들을
        /// 다시 재생 상태로 복원한다.
        ///
        /// 일부 채널의 Resume 실패가 나머지 채널 복원을
        /// 막지 않도록 독립적으로 처리한다.
        /// </summary>
        private async Task ResumePausedChannelsAsync(IList<DahuaPlaybackGroupChannel> channels)
        {
            if (channels == null
                || channels.Count == 0)
            {
                return;
            }

            /*
             * Pause 실행 순서의 역순으로 Resume한다.
             */
            for (int index = channels.Count - 1;
                index >= 0;
                index--)
            {
                DahuaPlaybackGroupChannel channel =
                    channels[index];

                if (channel == null
                    || channel.Session == null)
                {
                    continue;
                }

                try
                {
                    await _provider.ResumeAsync(
                        channel.Session,
                        CancellationToken.None);
                }
                catch
                {
                    /*
                     * 롤백 중 발생한 예외가
                     * 원래 Pause 실패 결과를 덮어쓰지 않게 한다.
                     */
                }
            }
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

        /// <summary>
        /// 재생속도 변경 전에 기준 채널의 실제 OSD 시간을 읽어
        /// 그룹의 현재 공통 영상재생시간을 갱신한다.
        ///
        /// 왼쪽 화면 채널을 우선 사용하며,
        /// 왼쪽 채널이 없으면 첫 번째 채널을 사용한다.
        /// </summary>
        private async Task<NvrResult>
            CapturePlaybackTimeBeforeSpeedChangeAsync(
                DahuaPlaybackGroupSession groupSession,
                CancellationToken cancellationToken)
        {
            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            DahuaPlaybackGroupChannel referenceChannel =
                null;

            /*
             * 화면 왼쪽 채널을 기준 채널로 우선 선택한다.
             */
            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (channel != null
                    && channel.ScreenPosition == 0)
                {
                    referenceChannel =
                        channel;

                    break;
                }
            }

            /*
             * 왼쪽 채널이 없으면 첫 번째 유효 채널을 사용한다.
             */
            if (referenceChannel == null)
            {
                foreach (DahuaPlaybackGroupChannel channel
                    in channels)
                {
                    if (channel != null
                        && channel.Session != null)
                    {
                        referenceChannel =
                            channel;

                        break;
                    }
                }
            }

            if (referenceChannel == null
                || referenceChannel.Session == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "속도 변경 전 시간을 확인할 기준 채널이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_SPEED_REFERENCE_CHANNEL_EMPTY",
                        "속도 변경 전 시간을 확인할 기준 채널이 없습니다.",
                        "SetSpeed"));
            }

            NvrResult<DateTime> timeResult =
                await _provider.GetPlaybackTimeAsync(
                    referenceChannel.Session,
                    cancellationToken);

            if (timeResult == null
                || !timeResult.Success)
            {
                return NvrResult.Fail(
                    timeResult == null
                        ? NvrResultStatus.Failed
                        : timeResult.Status,

                    timeResult == null
                        ? "속도 변경 전 영상재생시간 조회 결과가 없습니다."
                        : timeResult.Message,

                    timeResult == null
                        ? CreateError(
                            "DAHUA_GROUP_SPEED_TIME_RESULT_EMPTY",
                            "속도 변경 전 영상재생시간 조회 결과가 없습니다.",
                            "SetSpeed")
                        : timeResult.Error);
            }

            /*
             * Dahua OSD 원본 시각에서 채널 보정값을 제거하여
             * CamViewer 공통 영상재생시간으로 변환한다.
             */
            DateTime commonPlaybackTime =
                referenceChannel.ToCommonTime(
                    timeResult.Data);

            groupSession.SetCurrentPlaybackTime(
                commonPlaybackTime);

            return NvrResult.Ok(
                "속도 변경 전 영상재생시간을 갱신했습니다.");
        }

        /// <summary>
        /// 일부 채널 속도 변경 실패 시
        /// 이미 변경된 채널들을 이전 속도로 복원한다.
        /// </summary>
        private async Task<bool> RestoreChannelSpeedsAsync(
            IList<DahuaPlaybackGroupChannel> channels,
            NvrPlaybackSpeed previousSpeed)
        {
            if (channels == null
                || channels.Count == 0)
            {
                return true;
            }

            bool allSucceeded =
                true;

            /*
             * 변경 순서의 역순으로 이전 속도를 복원한다.
             */
            for (int index = channels.Count - 1;
                index >= 0;
                index--)
            {
                DahuaPlaybackGroupChannel channel =
                    channels[index];

                if (channel == null
                    || channel.Session == null)
                {
                    allSucceeded =
                        false;

                    continue;
                }

                try
                {
                    NvrResult restoreResult =
                        await _provider.SetPlaybackSpeedAsync(
                            channel.Session,
                            previousSpeed,
                            CancellationToken.None);

                    if (restoreResult == null
                        || !restoreResult.Success)
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
        /// 채널별 속도가 서로 다른 상태로 남았을 가능성이 있을 때
        /// 그룹을 더 이상 안전하게 사용할 수 없는 상태로 변경한다.
        /// </summary>
        private static void SetSpeedRollbackFailedState(
            DahuaPlaybackGroupSession groupSession)
        {
            if (groupSession == null)
            {
                return;
            }

            groupSession.SetState(
                NvrPlaybackState.Faulted);

            groupSession.SetReady(
                false,
                "일부 Dahua 채널의 속도를 이전 상태로 복원하지 못했습니다.");

            groupSession.SetSynchronizationStatus(
                false,
                null,
                "채널별 재생속도가 서로 다를 수 있어 "
                + "재생 그룹을 다시 열어야 합니다.");
        }

        /// <summary>
        /// 공통 재생속도를 사용자 표시 문자열로 변환한다.
        /// </summary>
        private static string GetPlaybackSpeedText(
            NvrPlaybackSpeed speed)
        {
            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    return "0.5배속";

                case NvrPlaybackSpeed.Normal:
                    return "1배속";

                case NvrPlaybackSpeed.Double:
                    return "2배속";

                case NvrPlaybackSpeed.Quad:
                    return "4배속";

                case NvrPlaybackSpeed.Octuple:
                    return "8배속";

                default:
                    return speed.ToString();
            }
        }


        /// <summary>
        /// 속도 변경 후 자동 동기화가 실패했을 때
        /// 속도 변경 전 그룹 재생 상태를 복원한다.
        ///
        /// 속도 변경 자체는 완료된 상태이므로
        /// 이전 속도로 되돌리지는 않는다.
        ///
        /// 복원 정책:
        /// - 이전 상태가 Paused이면 Paused 유지
        /// - 이전 상태가 Playing이면 전체 채널 Resume
        /// - 이미 이전 상태라면 추가 SDK 명령을 호출하지 않음
        /// </summary>
        private async Task<NvrResult>
            RestoreStateAfterSpeedSynchronizationAsync(
                DahuaPlaybackGroupSession groupSession,
                NvrPlaybackState previousState)
        {
            if (groupSession == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "복원할 Dahua 재생 그룹 세션이 없습니다.",
                    CreateError(
                        "DAHUA_GROUP_REQUIRED_FOR_STATE_RESTORE",
                        "복원할 Dahua 재생 그룹 세션이 없습니다.",
                        "RestoreSpeedState"));
            }

            /*
             * 속도 변경 전부터 일시정지 상태였다면
             * 동기화 실패 후에도 일시정지 상태를 유지한다.
             */
            if (previousState
                == NvrPlaybackState.Paused)
            {
                groupSession.SetState(
                    NvrPlaybackState.Paused);

                groupSession.SetReady(
                    true,
                    "속도 변경 전 일시정지 상태를 유지합니다.");

                return NvrResult.Ok(
                    "Dahua 재생 그룹의 일시정지 상태를 유지했습니다.");
            }

            /*
             * 동기화 실패 과정에서 Pause가 실행되지 않았거나
             * Pause 롤백이 완료되어 이미 Playing 상태라면
             * 추가 Resume 명령이 필요하지 않다.
             */
            if (previousState == NvrPlaybackState.Playing
                && groupSession.State
                    == NvrPlaybackState.Playing)
            {
                groupSession.SetReady(
                    true,
                    "속도 변경 전 재생 상태를 유지합니다.");

                return NvrResult.Ok(
                    "Dahua 재생 그룹이 재생 상태를 유지하고 있습니다.");
            }

            /*
             * 속도 변경 전 재생 중이었으나
             * 동기화 실패 후 Paused 상태로 남았다면
             * 그룹 전체를 다시 재개한다.
             */
            if (previousState
                == NvrPlaybackState.Playing)
            {
                groupSession.SetState(
                    NvrPlaybackState.Paused);

                NvrResult resumeResult =
                    await ResumeAsync(
                        groupSession,
                        CancellationToken.None);

                if (resumeResult == null)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 상태 복원 결과가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_SPEED_RESUME_RESULT_EMPTY",
                            "Dahua 재생 상태 복원 결과가 없습니다.",
                            "RestoreSpeedState"));
                }

                return resumeResult;
            }

            /*
             * 역재생은 자동 동기화 대상에서 제외되므로
             * 일반적으로 이 구간에 진입하지 않는다.
             *
             * 방어적으로 기존 역재생 상태를 유지한다.
             */
            if (previousState
                == NvrPlaybackState.Rewinding)
            {
                groupSession.SetState(
                    NvrPlaybackState.Rewinding);

                groupSession.SetReady(
                    true,
                    "속도 변경 전 역재생 상태를 유지합니다.");

                return NvrResult.Ok(
                    "Dahua 재생 그룹의 역재생 상태를 유지했습니다.");
            }

            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "복원할 수 없는 Dahua 재생 상태입니다. "
                + "PreviousState="
                + previousState,
                CreateError(
                    "DAHUA_GROUP_SPEED_STATE_RESTORE_NOT_SUPPORTED",
                    "복원할 수 없는 Dahua 재생 상태입니다.",
                    "RestoreSpeedState"));
        }

    }
}