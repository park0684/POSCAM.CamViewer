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

            IntPtr playbackHandle =
                DahuaNative.CLIENT_PlayBackByTimeEx(
                    loginSession.LoginHandle,
                    request.ChannelNo,
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
                request.NvrNo,
                request.ChannelNo,
                request.ScreenPosition,
                request.StartTime,
                request.EndTime);

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

            if (request.ChannelNo < 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.InvalidChannel,
                    "NVR 채널번호가 올바르지 않습니다.");
            }

            if (loginSession.ChannelCount > 0
                && request.ChannelNo >= loginSession.ChannelCount)
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
        /// CamViewer에서 관리하는 1번 기준 채널번호를 Dahua SDK의 0번 기준 채널 인덱스로 변환한다.
        /// </summary>
        private static int ToDahuaChannelId(int channelNo)
        {
            return channelNo - 1;
        }
    }
}