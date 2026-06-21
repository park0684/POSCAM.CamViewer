using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Native;
using NetSDKCS;
using System;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua NetSDK의 로그인 및 공통 SDK 호출을 담당한다.
    /// </summary>
    internal static class DahuaSdkClient
    {
        /// <summary>
        /// NVR 접속 정보로 Dahua NVR에 로그인한다.
        /// </summary>
        public static NvrResult<DahuaLoginSession> Login(
            NvrConnectionInfo connectionInfo)
        {
            NvrResult validationResult = ValidateConnectionInfo(connectionInfo);

            if (!validationResult.Success)
            {
                return NvrResult<DahuaLoginSession>.Fail(
                    validationResult.Status,
                    validationResult.Message,
                    validationResult.Error);
            }

            if (!DahuaSdkRuntime.IsInitialized)
            {
                return NvrResult<DahuaLoginSession>.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua SDK가 초기화되지 않았습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "SDK_NOT_INITIALIZED",
                        ErrorMessage = "Dahua SDK가 초기화되지 않았습니다.",
                        Operation = "Login"
                    });
            }

            DahuaNative.NET_DEVICEINFO_Ex deviceInfo =
                DahuaNative.NET_DEVICEINFO_Ex.Create();

            int loginError = 0;

            IntPtr loginHandle = DahuaNative.CLIENT_LoginEx2(
                connectionInfo.Host,
                Convert.ToUInt16(connectionInfo.Port),
                connectionInfo.UserId,
                connectionInfo.Password,
                DahuaNative.EM_LOGIN_SPAC_CAP_TYPE.TCP,
                IntPtr.Zero,
                ref deviceInfo,
                ref loginError);

            if (loginHandle == IntPtr.Zero)
            {
                uint nativeErrorCode = DahuaNative.CLIENT_GetLastError();

                return NvrResult<DahuaLoginSession>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR 로그인에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "NVR_LOGIN_FAILED",
                        ErrorMessage = "Dahua NVR 로그인에 실패했습니다.",
                        NativeErrorCode =
                            loginError + " / " + nativeErrorCode,
                        Operation = "CLIENT_LoginEx2"
                    });
            }

            var session = new DahuaLoginSession(
                loginHandle,
                connectionInfo,
                deviceInfo.nChanNum);

            return NvrResult<DahuaLoginSession>.Ok(
                session,
                "Dahua NVR 로그인에 성공했습니다.");
        }

        /// <summary>
        /// 지정 시간 구간의 녹화 영상을 Dahua SDK로 재생한다.
        /// </summary>
        public static NvrResult<DahuaPlaybackSession> PlayByTime(
            DahuaLoginSession loginSession,
            NvrPlaybackRequest request)
        {
            NvrResult validationResult =
                ValidatePlaybackRequest(
                    loginSession,
                    request);

            if (!validationResult.Success)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    validationResult.Status,
                    validationResult.Message,
                    validationResult.Error);
            }

            DahuaNative.NET_TIME startTime =
                DahuaNative.NET_TIME.FromDateTime(request.StartTime);

            DahuaNative.NET_TIME endTime =
                DahuaNative.NET_TIME.FromDateTime(request.EndTime);

            int dahuaChannelId =
                ToDahuaChannelId(request.ChannelNo);

            IntPtr playbackHandle =
                DahuaNative.CLIENT_PlayBackByTimeEx(
                    loginSession.LoginHandle,
                    dahuaChannelId,
                    ref startTime,
                    ref endTime,
                    request.RenderTargetHandle,
                    null,
                    IntPtr.Zero,
                    null,
                    IntPtr.Zero);

            if (playbackHandle == IntPtr.Zero)
            {
                uint nativeErrorCode =
                    DahuaNative.CLIENT_GetLastError();

                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua NVR 녹화영상 재생 요청에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_PLAYBACK_FAILED",
                        ErrorMessage = "CLIENT_PlayBackByTimeEx 호출에 실패했습니다.",
                        NativeErrorCode = nativeErrorCode.ToString(),
                        Operation = "CLIENT_PlayBackByTimeEx"
                    });
            }

            var session = new DahuaPlaybackSession(
                playbackHandle,
                request.CounterNo,
                request.NvrNo,
                request.ChannelNo,
                request.ScreenPosition,
                request.SearchDateTime,
                request.StartTime,
                request.EndTime,
                request.RenderTargetHandle,
                request.AutoPlay);

            return NvrResult<DahuaPlaybackSession>.Ok(
                session,
                "Dahua NVR 녹화영상 재생을 시작했습니다.");
        }

        /// <summary>
        /// Dahua 재생 세션을 중지한다.
        /// </summary>
        public static NvrResult StopPlayback(
            DahuaPlaybackSession playbackSession)
        {
            if (playbackSession == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "중지할 Dahua 재생 세션이 없습니다.");
            }

            playbackSession.Dispose();

            return NvrResult.Ok("Dahua 재생을 중지했습니다.");
        }

        /// <summary>
        /// Dahua 재생 세션을 일시정지한다.
        /// </summary>
        public static NvrResult PausePlayback(
            DahuaPlaybackSession playbackSession)
        {
            if (playbackSession == null
                || playbackSession.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "일시정지할 Dahua 재생 세션이 없습니다.");
            }

            bool result =
                DahuaNative.CLIENT_PausePlayBack(
                    playbackSession.PlaybackHandle,
                    true);

            if (!result)
            {
                uint nativeErrorCode =
                    DahuaNative.CLIENT_GetLastError();

                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 일시정지에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_PAUSE_FAILED",
                        ErrorMessage = "CLIENT_PausePlayBack 호출에 실패했습니다.",
                        NativeErrorCode = nativeErrorCode.ToString(),
                        Operation = "CLIENT_PausePlayBack"
                    });
            }

            playbackSession.SetState(NvrPlaybackState.Paused);

            return NvrResult.Ok("Dahua 재생을 일시정지했습니다.");
        }

        /// <summary>
        /// 기존 Dahua 재생 핸들을 유지한 상태로
        /// 지정된 절대 영상 시각으로 이동한다.
        ///
        /// CLIENT_SeekPlayBack은 절대 시각이 아니라
        /// CLIENT_PlayBackByTimeEx에 전달한 시작 시각으로부터의
        /// 상대 초를 받는다.
        /// </summary>
        public static NvrResult SeekPlayback(
            DahuaPlaybackSession playbackSession,
            DateTime targetTime)
        {
            if (playbackSession == null
                || playbackSession.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "이동할 Dahua 재생 세션이 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_SEEK_SESSION_INVALID",

                        ErrorMessage =
                            "이동할 Dahua 재생 세션이 없습니다.",

                        Operation =
                            "CLIENT_SeekPlayBack"
                    });
            }

            if (targetTime < playbackSession.StartTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "이동할 재생 시각은 재생 시작 시각보다 이전일 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_SEEK_BEFORE_START",

                        ErrorMessage =
                            "이동할 재생 시각은 재생 시작 시각보다 이전일 수 없습니다.",

                        Operation =
                            "CLIENT_SeekPlayBack"
                    });
            }

            if (targetTime >= playbackSession.EndTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "이동할 재생 시각은 재생 종료 시각보다 이전이어야 합니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_SEEK_AFTER_END",

                        ErrorMessage =
                            "이동할 재생 시각은 재생 종료 시각보다 이전이어야 합니다.",

                        Operation =
                            "CLIENT_SeekPlayBack"
                    });
            }

            double offsetSecondsValue =
                (
                    targetTime
                    - playbackSession.StartTime
                ).TotalSeconds;

            if (offsetSecondsValue < 0
                || offsetSecondsValue > uint.MaxValue)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 이동 상대시간이 허용 범위를 벗어났습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_SEEK_OFFSET_OUT_OF_RANGE",

                        ErrorMessage =
                            "Dahua 재생 이동 상대시간이 허용 범위를 벗어났습니다.",

                        Operation =
                            "CLIENT_SeekPlayBack"
                    });
            }

            /*
             * Dahua 재생 위치는 초 단위이므로
             * 소수점 이하는 버린다.
             */
            uint offsetSeconds =
                Convert.ToUInt32(
                    Math.Floor(
                        offsetSecondsValue));

            /*
             * 시간 기준 Seek에서는 offsetByte를 0으로 전달한다.
             */
            bool seekResult =
                DahuaNative.CLIENT_SeekPlayBack(
                    playbackSession.PlaybackHandle,
                    offsetSeconds,
                    0u);

            if (!seekResult)
            {
                uint nativeErrorCode =
                    DahuaNative.CLIENT_GetLastError();

                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 위치 이동에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_SEEK_FAILED",

                        ErrorMessage =
                            "CLIENT_SeekPlayBack 호출에 실패했습니다.",

                        NativeErrorCode =
                            nativeErrorCode.ToString(),

                        Operation =
                            "CLIENT_SeekPlayBack"
                    });
            }

            /*
             * 여기서는 실제 OSD 도착 여부가 아직 확인되지 않았다.
             * CurrentPlaybackTime과 State를 확정하지 않는다.
             *
             * 실제 시간 확정은 AlignPlaybackAsync에서 수행한다.
             */
            return NvrResult.Ok(
                "Dahua 재생 위치 이동 명령을 전달했습니다.");
        }

        /// <summary>
        /// Dahua 재생 세션을 재개한다.
        /// </summary>
        public static NvrResult ResumePlayback(
            DahuaPlaybackSession playbackSession)
        {
            if (playbackSession == null
                || playbackSession.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "재개할 Dahua 재생 세션이 없습니다.");
            }

            bool result =
                DahuaNative.CLIENT_PausePlayBack(
                    playbackSession.PlaybackHandle,
                    false);

            if (!result)
            {
                uint nativeErrorCode =
                    DahuaNative.CLIENT_GetLastError();

                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 재개에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_RESUME_FAILED",
                        ErrorMessage = "CLIENT_PausePlayBack 호출에 실패했습니다.",
                        NativeErrorCode = nativeErrorCode.ToString(),
                        Operation = "CLIENT_PausePlayBack"
                    });
            }

            playbackSession.SetState(NvrPlaybackState.Playing);

            return NvrResult.Ok("Dahua 재생을 재개했습니다.");
        }

        /// <summary>
        /// NVR 접속 정보의 필수값을 검증한다.
        /// </summary>
        private static NvrResult ValidateConnectionInfo(
            NvrConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 접속 정보가 없습니다.");
            }

            if (string.IsNullOrWhiteSpace(connectionInfo.Host))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR IP 또는 도메인을 입력해야 합니다.");
            }

            if (connectionInfo.Port < 1 || connectionInfo.Port > 65535)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 포트 번호가 올바르지 않습니다.");
            }

            if (string.IsNullOrWhiteSpace(connectionInfo.UserId))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 로그인 ID를 입력해야 합니다.");
            }

            if (string.IsNullOrWhiteSpace(connectionInfo.Password))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 로그인 비밀번호를 입력해야 합니다.");
            }

            return NvrResult.Ok();
        }

        /// <summary>
        /// Dahua 녹화 재생 요청의 필수값을 검증한다.
        /// </summary>
        private static NvrResult ValidatePlaybackRequest(
            DahuaLoginSession loginSession,
            NvrPlaybackRequest request)
        {
            if (loginSession == null || !loginSession.IsValid)
            {
                return NvrResult.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR 로그인 세션이 유효하지 않습니다.");
            }

            if (request == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 재생 요청 정보가 없습니다.");
            }

            if (request.ChannelNo <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.InvalidChannel,
                    "NVR 채널번호가 올바르지 않습니다. 채널번호는 1 이상이어야 합니다.");
            }

            if (loginSession.ChannelCount > 0
                && request.ChannelNo > loginSession.ChannelCount)
            {
                return NvrResult.Fail(
                    NvrResultStatus.InvalidChannel,
                    "NVR 채널번호가 장비 채널 수를 초과했습니다. "
                    + "요청 채널: "
                    + request.ChannelNo
                    + ", 장비 채널 수: "
                    + loginSession.ChannelCount);
            }

            if (request.RenderTargetHandle == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "영상 출력 대상 Handle이 없습니다.");
            }

            if (request.StartTime >= request.EndTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "재생 시작 시각은 종료 시각보다 이전이어야 합니다.");
            }

            return NvrResult.Ok();
        }

        /// <summary>
        /// Dahua 재생 세션의 재생속도를 변경한다.
        /// </summary>
        public static NvrResult SetPlaybackSpeed(
            DahuaPlaybackSession playbackSession,
            NvrPlaybackSpeed speed)
        {
            if (playbackSession == null
                || playbackSession.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "재생속도를 변경할 Dahua 재생 세션이 없습니다.");
            }

            NvrResult normalResult =
                ApplyPlaybackControl(
                    playbackSession,
                    PlayBackType.Normal,
                    "DAHUA_PLAYBACK_SPEED_NORMAL_FAILED",
                    "Dahua 재생속도를 1배속으로 초기화하지 못했습니다.");

            if (!normalResult.Success)
            {
                return normalResult;
            }

            int fastCount = 0;
            int slowCount = 0;

            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    slowCount = 1;
                    break;

                case NvrPlaybackSpeed.Double:
                    fastCount = 1;
                    break;

                case NvrPlaybackSpeed.Quad:
                    fastCount = 2;
                    break;

                case NvrPlaybackSpeed.Octuple:
                    fastCount = 3;
                    break;

                case NvrPlaybackSpeed.Normal:
                default:
                    break;
            }

            for (int index = 0; index < fastCount; index++)
            {
                NvrResult fastResult =
                    ApplyPlaybackControl(
                        playbackSession,
                        PlayBackType.Fast,
                        "DAHUA_PLAYBACK_SPEED_FAST_FAILED",
                        "Dahua 빠른 재생 제어에 실패했습니다.");

                if (!fastResult.Success)
                {
                    return fastResult;
                }
            }

            for (int index = 0; index < slowCount; index++)
            {
                NvrResult slowResult =
                    ApplyPlaybackControl(
                        playbackSession,
                        PlayBackType.Slow,
                        "DAHUA_PLAYBACK_SPEED_SLOW_FAILED",
                        "Dahua 느린 재생 제어에 실패했습니다.");

                if (!slowResult.Success)
                {
                    return slowResult;
                }
            }

            playbackSession.SetPlaybackSpeed(speed);

            return NvrResult.Ok(
                "Dahua 재생속도를 변경했습니다. "
                + GetNvrPlaybackSpeedText(speed));
        }

        /// <summary>
        /// Dahua SDKCS 래퍼의 재생 제어 명령을 실행한다.
        /// </summary>
        private static NvrResult ApplyPlaybackControl(
            DahuaPlaybackSession playbackSession,
            PlayBackType controlType,
            string errorCode,
            string errorMessage)
        {
            bool result =
                NETClient.PlayBackControl(
                    playbackSession.PlaybackHandle,
                    controlType);

            if (result)
            {
                return NvrResult.Ok();
            }

            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 제어에 실패했습니다.",
                new NvrErrorInfo
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    NativeErrorCode = NETClient.GetLastError(),
                    Operation = "NETClient.PlayBackControl"
                });
        }

        
        /// <summary>
        /// 지정 시간 구간의 Dahua 녹화 영상을 역방향으로 재생한다.
        /// 
        /// Dahua SDKCS의 NET_IN_PLAY_BACK_BY_TIME_INFO.nPlayDirection 값을 사용한다.
        /// 일반 재생은 0, 역방향 재생은 1로 처리한다.
        /// </summary>
        public static NvrResult<DahuaPlaybackSession> PlayReverseByTime(
            DahuaLoginSession loginSession,
            NvrPlaybackRequest request,
            DateTime reverseStartTime)
        {
            NvrResult validationResult =
                ValidatePlaybackRequest(
                    loginSession,
                    request);

            if (!validationResult.Success)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    validationResult.Status,
                    validationResult.Message,
                    validationResult.Error);
            }

            if (reverseStartTime <= request.StartTime)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "역재생 시작 시각은 조회 시작 시각보다 이후여야 합니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "INVALID_REVERSE_START_TIME",
                        ErrorMessage = "역재생 시작 시각이 조회 시작 시각보다 빠르거나 같습니다.",
                        Operation = "PlayReverseByTime"
                    });
            }

            if (reverseStartTime > request.EndTime)
            {
                reverseStartTime = request.EndTime;
            }

            int dahuaChannelId =
                ToDahuaChannelId(request.ChannelNo);

            NET_IN_PLAY_BACK_BY_TIME_INFO input =
                new NET_IN_PLAY_BACK_BY_TIME_INFO();

            NET_OUT_PLAY_BACK_BY_TIME_INFO output =
                new NET_OUT_PLAY_BACK_BY_TIME_INFO();

            input.stStartTime =
                NET_TIME.FromDateTime(request.StartTime);

            // 역재생은 현재 시각 또는 선택 시각까지의 구간을 열고,
            // nPlayDirection = 1로 역방향 재생을 요청한다.
            input.stStopTime =
                NET_TIME.FromDateTime(reverseStartTime);

            input.hWnd =
                request.RenderTargetHandle;

            input.cbDownLoadPos =
                null;

            input.dwPosUser =
                IntPtr.Zero;

            input.fDownLoadDataCallBack =
                null;

            input.dwDataUser =
                IntPtr.Zero;

            input.nPlayDirection =
                1;

            input.nWaittime =
                5000;

            IntPtr playbackHandle =
                NETClient.PlayBackByTime(
                    loginSession.LoginHandle,
                    dahuaChannelId,
                    input,
                    ref output);

            if (playbackHandle == IntPtr.Zero)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua NVR 역재생 요청에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_REVERSE_PLAYBACK_FAILED",
                        ErrorMessage = NETClient.GetLastError(),
                        Operation = "NETClient.PlayBackByTime"
                    });
            }

            var session =
                new DahuaPlaybackSession(
                    playbackHandle,
                    request.CounterNo,
                    request.NvrNo,
                    request.ChannelNo,
                    request.ScreenPosition,
                    request.SearchDateTime,
                    request.StartTime,
                    reverseStartTime,
                    request.RenderTargetHandle,
                    request.AutoPlay);

            session.SetState(NvrPlaybackState.Rewinding);
            session.SetCurrentPlaybackTime(reverseStartTime);

            return NvrResult<DahuaPlaybackSession>.Ok(
                session,
                "Dahua NVR 역재생을 시작했습니다.");
        }

        /// <summary>
        /// CamViewer에서 관리하는 1번 기준 채널번호를 Dahua SDK의 0번 기준 채널 인덱스로 변환한다.
        /// </summary>
        private static int ToDahuaChannelId(int channelNo)
        {
            return channelNo - 1;
        }

        /// <summary>
        /// Dahua 재생속도 변경 실패 결과를 생성한다.
        /// </summary>
        private static NvrResult CreatePlaybackSpeedFailResult(
            string errorCode,
            string errorMessage,
            string operation)
        {
            uint nativeErrorCode =
                DahuaNative.CLIENT_GetLastError();

            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua 재생속도 변경에 실패했습니다.",
                new NvrErrorInfo
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    NativeErrorCode = nativeErrorCode.ToString(),
                    Operation = operation
                });
        }

        /// <summary>
        /// Dahua 재생 세션의 실제 OSD 재생시간을 조회한다.
        /// </summary>
        public static NvrResult<DateTime> GetPlaybackOsdTime(
            DahuaPlaybackSession playbackSession)
        {
            if (playbackSession == null
                || playbackSession.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "재생시간을 조회할 Dahua 재생 세션이 없습니다.");
            }

            DahuaNative.NET_TIME osdTime =
                new DahuaNative.NET_TIME();

            DahuaNative.NET_TIME startTime =
                new DahuaNative.NET_TIME();

            DahuaNative.NET_TIME endTime =
                new DahuaNative.NET_TIME();

            bool result =
                DahuaNative.CLIENT_GetPlayBackOsdTime(
                    playbackSession.PlaybackHandle,
                    ref osdTime,
                    ref startTime,
                    ref endTime);

            if (!result)
            {
                uint nativeErrorCode =
                    DahuaNative.CLIENT_GetLastError();

                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생시간 조회에 실패했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_GET_PLAYBACK_OSD_TIME_FAILED",
                        ErrorMessage = "CLIENT_GetPlayBackOsdTime 호출에 실패했습니다.",
                        NativeErrorCode = nativeErrorCode.ToString(),
                        Operation = "CLIENT_GetPlayBackOsdTime"
                    });
            }

            DateTime playbackTime =
                osdTime.ToDateTime();

            if (playbackTime == DateTime.MinValue)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생시간 값이 올바르지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_INVALID_PLAYBACK_OSD_TIME",
                        ErrorMessage = "OSD 시간이 DateTime으로 변환될 수 없습니다.",
                        Operation = "CLIENT_GetPlayBackOsdTime"
                    });
            }

            playbackSession.SetCurrentPlaybackTime(playbackTime);

            return NvrResult<DateTime>.Ok(
                playbackTime,
                "Dahua 재생시간을 조회했습니다.");
        }

        /// <summary>
        /// NVR 재생속도 표시 문자열을 반환한다.
        /// </summary>
        private static string GetNvrPlaybackSpeedText(
            NvrPlaybackSpeed speed)
        {
            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    return "0.5배속";

                case NvrPlaybackSpeed.Double:
                    return "2배속";

                case NvrPlaybackSpeed.Quad:
                    return "4배속";

                case NvrPlaybackSpeed.Octuple:
                    return "8배속";

                default:
                    return "1배속";
            }
        }


        /// <summary>
        /// Dahua NVR 채널의 영상 원본 정보를 조회한다.
        /// 
        /// PlayerView의 원본 비율 표시 모드에서 사용할
        /// 채널 영상 Width / Height를 반환한다.
        /// 
        /// 주의:
        /// Dahua SDK Native 설정 조회 과정에서 예외가 발생할 수 있으므로,
        /// 반드시 여기서 예외를 NvrResult 실패 결과로 변환한다.
        /// </summary>
        public static NvrResult<NvrVideoSourceInfo> GetVideoSourceInfo(
            DahuaLoginSession loginSession,
            int channelNo)
        {
            if (loginSession == null || !loginSession.IsValid)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua 로그인 세션이 유효하지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "INVALID_DAHUA_LOGIN_SESSION",
                        ErrorMessage = "Dahua 로그인 세션이 유효하지 않습니다.",
                        Operation = "GetVideoSourceInfo"
                    });
            }

            if (channelNo < 0)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "채널 번호가 올바르지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "INVALID_CHANNEL_NO",
                        ErrorMessage = "채널 번호가 올바르지 않습니다.",
                        Operation = "GetVideoSourceInfo"
                    });
            }

            DahuaEncodeConfigResult configResult;

            try
            {
                configResult =
                    DahuaEncodeConfigReader.ReadMainStreamEncodeConfig(
                        loginSession.LoginHandle,
                        channelNo);
            }
            catch (EntryPointNotFoundException ex)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "현재 Dahua SDK DLL에서 인코딩 설정 조회 함수를 찾을 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_CONFIG_ENTRYPOINT_NOT_FOUND",
                        ErrorMessage = ex.Message,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }
            catch (DllNotFoundException ex)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua SDK DLL을 찾을 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_SDK_DLL_NOT_FOUND",
                        ErrorMessage = ex.Message,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }
            catch (BadImageFormatException ex)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua SDK DLL의 32/64bit 구성이 현재 CamViewer와 맞지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_SDK_BITNESS_MISMATCH",
                        ErrorMessage = ex.Message,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }
            catch (AccessViolationException ex)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 인코딩 설정 조회 중 메모리 접근 오류가 발생했습니다. Native 함수 선언 또는 구조체 정의가 SDK와 맞지 않을 가능성이 높습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_CONFIG_ACCESS_VIOLATION",
                        ErrorMessage = ex.Message,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }
            catch (Exception ex)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 인코딩 설정 조회 중 예외가 발생했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_CONFIG_READ_EXCEPTION",
                        ErrorMessage = ex.Message,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }

            if (configResult == null)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 인코딩 설정 조회 결과가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_CONFIG_RESULT_NULL",
                        ErrorMessage = "DahuaEncodeConfigResult가 null입니다.",
                        Operation = "GetVideoSourceInfo"
                    });
            }

            if (!configResult.Success)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    configResult.Message,
                    new NvrErrorInfo
                    {
                        ErrorCode = configResult.ErrorCode,
                        ErrorMessage = configResult.Message,
                        NativeErrorCode = configResult.NativeErrorCode,
                        Operation = "ReadMainStreamEncodeConfig"
                    });
            }

            if (configResult.Width <= 0 || configResult.Height <= 0)
            {
                return NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 채널 해상도 정보가 올바르지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "INVALID_DAHUA_VIDEO_SIZE",
                        ErrorMessage =
                            "Width="
                            + configResult.Width
                            + ", Height="
                            + configResult.Height,
                        Operation = "GetVideoSourceInfo"
                    });
            }

            return NvrResult<NvrVideoSourceInfo>.Ok(
                new NvrVideoSourceInfo
                {
                    Width = configResult.Width,
                    Height = configResult.Height
                },
                "Dahua 채널 영상 원본 정보를 조회했습니다.");
        }
    }
}