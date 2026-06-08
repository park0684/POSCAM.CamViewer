using System;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Native;

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
        ///
        /// Dahua SDK 데모 기준:
        /// - Normal: 일반속도 복귀
        /// - Fast: 호출할 때마다 2배씩 증가
        /// - Slow: 호출할 때마다 1/2배씩 감소
        ///
        /// 처리 방식:
        /// 1. 먼저 Normal로 초기화한다.
        /// 2. 원하는 속도에 따라 Fast 또는 Slow를 반복 호출한다.
        /// 3. 세션 상태는 변경하지 않고 속도값만 기록한다.
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
                    DahuaNative.DahuaPlaybackControlType.Normal,
                    "DAHUA_PLAYBACK_SPEED_NORMAL_FAILED",
                    "CLIENT_PlayBackControl Normal 호출에 실패했습니다.");

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
                        DahuaNative.DahuaPlaybackControlType.Fast,
                        "DAHUA_PLAYBACK_SPEED_FAST_FAILED",
                        "CLIENT_PlayBackControl Fast 호출에 실패했습니다.");

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
                        DahuaNative.DahuaPlaybackControlType.Slow,
                        "DAHUA_PLAYBACK_SPEED_SLOW_FAILED",
                        "CLIENT_PlayBackControl Slow 호출에 실패했습니다.");

                if (!slowResult.Success)
                {
                    return slowResult;
                }
            }

            // 여기서는 재생/일시정지 상태를 변경하지 않는다.
            // 상태 복원은 DahuaNvrProvider.SetPlaybackSpeedAsync에서 처리한다.
            playbackSession.SetPlaybackSpeed(speed);

            return NvrResult.Ok(
                "Dahua 재생속도를 변경했습니다. "
                + GetNvrPlaybackSpeedText(speed));
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
        /// Dahua 재생 제어 명령을 실행한다.
        /// </summary>
        private static NvrResult ApplyPlaybackControl(
            DahuaPlaybackSession playbackSession,
            DahuaNative.DahuaPlaybackControlType controlType,
            string errorCode,
            string errorMessage)
        {
            bool result =
                DahuaNative.CLIENT_PlayBackControl(
                    playbackSession.PlaybackHandle,
                    controlType,
                    0,
                    IntPtr.Zero);

            if (result)
            {
                return NvrResult.Ok();
            }

            uint nativeErrorCode =
                DahuaNative.CLIENT_GetLastError();

            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 제어에 실패했습니다.",
                new NvrErrorInfo
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    NativeErrorCode = nativeErrorCode.ToString(),
                    Operation = "CLIENT_PlayBackControl"
                });
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