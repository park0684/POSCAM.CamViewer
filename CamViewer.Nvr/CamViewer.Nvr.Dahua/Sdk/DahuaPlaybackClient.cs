using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Models;
using CamViewer.Nvr.Dahua.Playback;
using NetSDKCS;
using System;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// 개별 Dahua PlaybackHandle 생성과 단일채널 제어를 담당한다.
    ///
    /// 다중채널 PlayGroup에 등록된 이후에는
    /// Pause, Resume, Speed, Direction을 이 클래스로 제어하지 않는다.
    /// </summary>
    internal static class DahuaPlaybackClient
    {
        private const int PlayInputReservedSizeX64 =
            940 - (2 * 8);

        internal static NvrResult<DahuaPlaybackSession> Open(
            DahuaLoginSession loginSession,
            NvrPlaybackRequest request,
            NvrPlaybackDirection direction)
        {
            NvrResult validationResult =
                ValidateOpenRequest(
                    loginSession,
                    request);

            if (!validationResult.Success)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    validationResult.Status,
                    validationResult.Message,
                    validationResult.Error);
            }

            try
            {
                var input =
                    new NET_IN_PLAY_BACK_BY_TIME_INFO
                    {
                        stStartTime =
                            NET_TIME.FromDateTime(
                                request.StartTime),

                        stStopTime =
                            NET_TIME.FromDateTime(
                                request.EndTime),

                        hWnd =
                            request.RenderTargetHandle,

                        cbDownLoadPos =
                            null,

                        dwPosUser =
                            IntPtr.Zero,

                        fDownLoadDataCallBack =
                            null,

                        dwDataUser =
                            IntPtr.Zero,

                        nPlayDirection =
                            direction == NvrPlaybackDirection.Reverse
                                ? 1
                                : 0,

                        nWaittime =
                            5000,

                        pstuEventInfo =
                            IntPtr.Zero,

                        nEventInfoCount =
                            0,

                        emSubClass =
                            EM_SUBCLASSID_TYPE.EM_SUBCLASSID_UNKNOWN,

                        pVKInfoCallBack =
                            null,

                        dwVKInfoUser =
                            IntPtr.Zero,

                        pOriDataCallBack =
                            null,

                        dwOriDataUser =
                            IntPtr.Zero,

                        bOnlySupportRealUTC =
                            false,

                        bReserved =
                            new byte[PlayInputReservedSizeX64]
                    };

                var output =
                    new NET_OUT_PLAY_BACK_BY_TIME_INFO
                    {
                        bReserved =
                            new byte[1024]
                    };

                IntPtr playbackHandle =
                    NETClient.PlayBackByTime(
                        loginSession.LoginHandle,
                        DahuaDeviceClient.ToDahuaChannelId(
                            request.ChannelNo),
                        input,
                        ref output);

                if (playbackHandle == IntPtr.Zero)
                {
                    return NvrResult<DahuaPlaybackSession>.Fail(
                        NvrResultStatus.Failed,
                        direction == NvrPlaybackDirection.Reverse
                            ? "Dahua NVR 역재생 핸들 생성에 실패했습니다."
                            : "Dahua NVR 재생 핸들 생성에 실패했습니다.",
                        CreateError(
                            direction == NvrPlaybackDirection.Reverse
                                ? "DAHUA_REVERSE_PLAYBACK_OPEN_FAILED"
                                : "DAHUA_PLAYBACK_OPEN_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.PlayBackByTime"));
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
                        request.EndTime,
                        request.RenderTargetHandle,
                        direction);

                return NvrResult<DahuaPlaybackSession>.Ok(
                    session,
                    direction == NvrPlaybackDirection.Reverse
                        ? "Dahua 역재생 핸들을 생성했습니다."
                        : "Dahua 재생 핸들을 생성했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 녹화영상 재생 핸들 생성 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_OPEN_EXCEPTION",
                        ex.Message,
                        "NETClient.PlayBackByTime"));
            }
        }

        internal static NvrResult Pause(
            DahuaPlaybackSession session)
        {
            return ApplyControl(
                session,
                PlayBackType.Pause,
                NvrPlaybackState.Paused,
                "DAHUA_PLAYBACK_PAUSE_FAILED",
                "Dahua 재생 일시정지에 실패했습니다.");
        }

        internal static NvrResult Resume(
            DahuaPlaybackSession session)
        {
            if (session == null
                || !session.IsValid)
            {
                return InvalidSession(
                    "Resume");
            }

            NvrPlaybackState state =
                session.Direction == NvrPlaybackDirection.Reverse
                    ? NvrPlaybackState.Rewinding
                    : NvrPlaybackState.Playing;

            return ApplyControl(
                session,
                PlayBackType.Play,
                state,
                "DAHUA_PLAYBACK_RESUME_FAILED",
                "Dahua 재생 재개에 실패했습니다.");
        }

        internal static NvrResult Stop(
            DahuaPlaybackSession session)
        {
            if (session == null)
            {
                return NvrResult.Ok(
                    "정리할 Dahua 재생 세션이 없습니다.");
            }

            IntPtr playbackHandle =
                session.TakePlaybackHandle();

            if (playbackHandle == IntPtr.Zero)
            {
                session.SetState(
                    NvrPlaybackState.Stopped);

                return NvrResult.Ok(
                    "Dahua 재생 세션이 이미 중지되었습니다.");
            }

            try
            {
                bool success =
                    NETClient.PlayBackControl(
                        playbackHandle,
                        PlayBackType.Stop);

                session.SetState(
                    NvrPlaybackState.Stopped);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 중지에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYBACK_STOP_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.PlayBackControl.Stop"));
                }

                return NvrResult.Ok(
                    "Dahua 재생을 중지했습니다.");
            }
            catch (Exception ex)
            {
                session.SetState(
                    NvrPlaybackState.Stopped);

                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 중지 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_STOP_EXCEPTION",
                        ex.Message,
                        "NETClient.PlayBackControl.Stop"));
            }
        }

        internal static NvrResult<DateTime> QueryTime(
            DahuaPlaybackSession session)
        {
            if (session == null
                || !session.IsValid)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "재생시간을 조회할 Dahua 세션이 없습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_SESSION_INVALID",
                        "재생시간을 조회할 Dahua 세션이 없습니다.",
                        "GetPlayBackOsdTime"));
            }

            try
            {
                NET_TIME osdTime =
                    new NET_TIME();

                NET_TIME startTime =
                    new NET_TIME();

                NET_TIME endTime =
                    new NET_TIME();

                bool success =
                    NETClient.GetPlayBackOsdTime(
                        session.PlaybackHandle,
                        ref osdTime,
                        ref startTime,
                        ref endTime);

                if (!success)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생시간 조회에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYBACK_TIME_QUERY_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.GetPlayBackOsdTime"));
                }

                DateTime playbackTime =
                    osdTime.ToDateTime();

                if (playbackTime == DateTime.MinValue)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생시간 값이 올바르지 않습니다.",
                        CreateError(
                            "DAHUA_PLAYBACK_TIME_INVALID",
                            "OSD 시간을 DateTime으로 변환하지 못했습니다.",
                            "NETClient.GetPlayBackOsdTime"));
                }

                session.SetCurrentPlaybackTime(
                    playbackTime);

                return NvrResult<DateTime>.Ok(
                    playbackTime,
                    "Dahua 재생시간을 조회했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생시간 조회 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_TIME_QUERY_EXCEPTION",
                        ex.Message,
                        "NETClient.GetPlayBackOsdTime"));
            }
        }

        internal static NvrResult SetSpeed(
            DahuaPlaybackSession session,
            NvrPlaybackSpeed speed)
        {
            if (session == null
                || !session.IsValid)
            {
                return InvalidSession(
                    "SetSpeed");
            }

            NvrResult normalResult =
                ApplySpeedControl(
                    session,
                    PlayBackType.Normal);

            if (!normalResult.Success)
            {
                return normalResult;
            }

            int fastCount =
                0;

            int slowCount =
                0;

            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    slowCount =
                        1;
                    break;

                case NvrPlaybackSpeed.Double:
                    fastCount =
                        1;
                    break;

                case NvrPlaybackSpeed.Quad:
                    fastCount =
                        2;
                    break;

                case NvrPlaybackSpeed.Octuple:
                    fastCount =
                        3;
                    break;

                case NvrPlaybackSpeed.Normal:
                    break;

                default:
                    return NvrResult.Fail(
                        NvrResultStatus.NotSupported,
                        "지원하지 않는 Dahua 재생속도입니다. "
                        + "Speed="
                        + speed,
                        CreateError(
                            "DAHUA_PLAYBACK_SPEED_NOT_SUPPORTED",
                            "지원하지 않는 재생속도입니다.",
                            "SetSpeed"));
            }

            for (int index = 0;
                index < fastCount;
                index++)
            {
                NvrResult fastResult =
                    ApplySpeedControl(
                        session,
                        PlayBackType.Fast);

                if (!fastResult.Success)
                {
                    return fastResult;
                }
            }

            for (int index = 0;
                index < slowCount;
                index++)
            {
                NvrResult slowResult =
                    ApplySpeedControl(
                        session,
                        PlayBackType.Slow);

                if (!slowResult.Success)
                {
                    return slowResult;
                }
            }

            session.SetSpeed(
                speed);

            return NvrResult.Ok(
                "Dahua 단일채널 재생속도를 변경했습니다.");
        }

        private static NvrResult ApplyControl(
            DahuaPlaybackSession session,
            PlayBackType controlType,
            NvrPlaybackState successState,
            string errorCode,
            string failureMessage)
        {
            if (session == null
                || !session.IsValid)
            {
                return InvalidSession(
                    controlType.ToString());
            }

            try
            {
                bool success =
                    NETClient.PlayBackControl(
                        session.PlaybackHandle,
                        controlType);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        failureMessage,
                        CreateError(
                            errorCode,
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.PlayBackControl."
                            + controlType));
                }

                session.SetState(
                    successState);

                return NvrResult.Ok();
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    failureMessage,
                    CreateError(
                        errorCode
                        + "_EXCEPTION",
                        ex.Message,
                        "NETClient.PlayBackControl."
                        + controlType));
            }
        }

        private static NvrResult ApplySpeedControl(
            DahuaPlaybackSession session,
            PlayBackType controlType)
        {
            try
            {
                bool success =
                    NETClient.PlayBackControl(
                        session.PlaybackHandle,
                        controlType);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 단일채널 재생속도 제어에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYBACK_SPEED_CONTROL_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.PlayBackControl."
                            + controlType));
                }

                return NvrResult.Ok();
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 단일채널 재생속도 제어 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_SPEED_CONTROL_EXCEPTION",
                        ex.Message,
                        "NETClient.PlayBackControl."
                        + controlType));
            }
        }

        private static NvrResult ValidateOpenRequest(
            DahuaLoginSession loginSession,
            NvrPlaybackRequest request)
        {
            if (loginSession == null
                || !loginSession.IsValid)
            {
                return NvrResult.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR 로그인 세션이 유효하지 않습니다.",
                    CreateError(
                        "DAHUA_LOGIN_SESSION_INVALID",
                        "Dahua NVR 로그인 세션이 유효하지 않습니다.",
                        "PlayBackByTime"));
            }

            if (request == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 요청 정보가 없습니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_REQUEST_REQUIRED",
                        "Dahua 재생 요청 정보가 없습니다.",
                        "PlayBackByTime"));
            }

            if (request.ChannelNo <= 0)
            {
                return NvrResult.Fail(
                    NvrResultStatus.InvalidChannel,
                    "Dahua 채널번호는 1 이상이어야 합니다.",
                    CreateError(
                        "DAHUA_CHANNEL_INVALID",
                        "Dahua 채널번호는 1 이상이어야 합니다.",
                        "PlayBackByTime"));
            }

            if (loginSession.ChannelCount > 0
                && request.ChannelNo
                    > loginSession.ChannelCount)
            {
                return NvrResult.Fail(
                    NvrResultStatus.InvalidChannel,
                    "요청한 Dahua 채널번호가 장비 채널 수를 초과했습니다. "
                    + "ChannelNo="
                    + request.ChannelNo
                    + ", DeviceChannelCount="
                    + loginSession.ChannelCount,
                    CreateError(
                        "DAHUA_CHANNEL_OUT_OF_RANGE",
                        "요청 채널번호가 장비 채널 수를 초과했습니다.",
                        "PlayBackByTime"));
            }

            if (request.RenderTargetHandle
                == IntPtr.Zero)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 영상 출력 대상 Handle이 없습니다.",
                    CreateError(
                        "DAHUA_RENDER_HANDLE_REQUIRED",
                        "Dahua 영상 출력 대상 Handle이 없습니다.",
                        "PlayBackByTime"));
            }

            if (request.StartTime
                >= request.EndTime)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 시작시간은 종료시간보다 이전이어야 합니다.",
                    CreateError(
                        "DAHUA_PLAYBACK_RANGE_INVALID",
                        "Dahua 재생 시작시간은 종료시간보다 이전이어야 합니다.",
                        "PlayBackByTime"));
            }

            return NvrResult.Ok();
        }

        private static NvrResult InvalidSession(
            string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 세션이 유효하지 않습니다.",
                CreateError(
                    "DAHUA_PLAYBACK_SESSION_INVALID",
                    "Dahua 재생 세션이 유효하지 않습니다.",
                    operation));
        }

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

                NativeErrorCode =
                    DahuaSdkRuntime.GetLastErrorSafe(),

                Operation =
                    operation
            };
        }
    }
}
