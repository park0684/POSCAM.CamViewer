using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Playback;
using NetSDKCS;
using System;
using System.Runtime.InteropServices;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua 공식 PlayGroup API 호출을 캡슐화한다.
    ///
    /// 다중채널 그룹에 등록된 PlaybackHandle은
    /// 이 클래스의 그룹 명령으로만 Pause, Resume, Speed,
    /// Direction을 제어한다.
    /// </summary>
    internal static class DahuaPlayGroupClient
    {
        internal static NvrResult<IntPtr> Open()
        {
            try
            {
                IntPtr handle =
                    NETClient.OpenPlayGroup();

                if (handle == IntPtr.Zero)
                {
                    return NvrResult<IntPtr>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 생성에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_OPEN_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.OpenPlayGroup"));
                }

                return NvrResult<IntPtr>.Ok(
                    handle,
                    "Dahua PlayGroup을 생성했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<IntPtr>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 생성 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_OPEN_EXCEPTION",
                        ex.Message,
                        "NETClient.OpenPlayGroup"));
            }
        }

        internal static NvrResult Add(
            IntPtr groupHandle,
            DahuaPlaybackSession session)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return InvalidGroup(
                    "Add");
            }

            if (session == null
                || !session.IsValid)
            {
                return InvalidSession(
                    "Add");
            }

            try
            {
                var input =
                    new NET_IN_ADD_PLAYHANDLE_TO_PLAYGROUP
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_ADD_PLAYHANDLE_TO_PLAYGROUP))),

                        byReserved =
                            new byte[4],

                        lPlayGroupHandle =
                            groupHandle,

                        lPlayHandle =
                            session.PlaybackHandle
                    };

                var output =
                    new NET_OUT_ADD_PLAYHANDLE_TO_PLAYGROUP
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_ADD_PLAYHANDLE_TO_PLAYGROUP)))
                    };

                bool success =
                    NETClient.AddPlayHandleToPlayGroup(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 핸들을 PlayGroup에 추가하지 못했습니다. "
                        + "ChannelNo="
                        + session.ChannelNo,
                        CreateError(
                            "DAHUA_PLAYGROUP_ADD_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.AddPlayHandleToPlayGroup"));
                }

                return NvrResult.Ok(
                    "Dahua 재생 핸들을 PlayGroup에 추가했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 핸들을 PlayGroup에 추가하는 중 "
                    + "오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_ADD_EXCEPTION",
                        ex.Message,
                        "NETClient.AddPlayHandleToPlayGroup"));
            }
        }

        internal static NvrResult SetBaseChannel(
            IntPtr groupHandle,
            DahuaPlaybackSession session)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return InvalidGroup(
                    "SetBaseChannel");
            }

            if (session == null
                || !session.IsValid)
            {
                return InvalidSession(
                    "SetBaseChannel");
            }

            try
            {
                var input =
                    new NET_IN_SET_PLAYGROUP_BASECHANNEL
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_SET_PLAYGROUP_BASECHANNEL))),

                        byReserved =
                            new byte[4],

                        lPlayGroupHandle =
                            groupHandle,

                        lPlayHandle =
                            session.PlaybackHandle
                    };

                var output =
                    new NET_OUT_SET_PLAYGROUP_BASECHANNEL
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_SET_PLAYGROUP_BASECHANNEL)))
                    };

                bool success =
                    NETClient.SetPlayGroupBaseChannel(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 기준 채널 설정에 실패했습니다. "
                        + "ChannelNo="
                        + session.ChannelNo,
                        CreateError(
                            "DAHUA_PLAYGROUP_BASE_CHANNEL_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.SetPlayGroupBaseChannel"));
                }

                return NvrResult.Ok(
                    "Dahua PlayGroup 기준 채널을 설정했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 기준 채널 설정 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_BASE_CHANNEL_EXCEPTION",
                        ex.Message,
                        "NETClient.SetPlayGroupBaseChannel"));
            }
        }

        internal static NvrResult Pause(
            IntPtr groupHandle,
            bool pause)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return InvalidGroup(
                    pause
                        ? "Pause"
                        : "Resume");
            }

            try
            {
                bool success =
                    NETClient.PausePlayGroup(
                        groupHandle,
                        pause);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        pause
                            ? "Dahua PlayGroup 일시정지에 실패했습니다."
                            : "Dahua PlayGroup 재개에 실패했습니다.",
                        CreateError(
                            pause
                                ? "DAHUA_PLAYGROUP_PAUSE_FAILED"
                                : "DAHUA_PLAYGROUP_RESUME_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.PausePlayGroup"));
                }

                return NvrResult.Ok(
                    pause
                        ? "Dahua PlayGroup을 일시정지했습니다."
                        : "Dahua PlayGroup을 재개했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    pause
                        ? "Dahua PlayGroup 일시정지 중 오류가 발생했습니다."
                        : "Dahua PlayGroup 재개 중 오류가 발생했습니다.",
                    CreateError(
                        pause
                            ? "DAHUA_PLAYGROUP_PAUSE_EXCEPTION"
                            : "DAHUA_PLAYGROUP_RESUME_EXCEPTION",
                        ex.Message,
                        "NETClient.PausePlayGroup"));
            }
        }

        internal static NvrResult<DateTime> QueryTime(
            IntPtr groupHandle)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "시간을 조회할 Dahua PlayGroup이 없습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_HANDLE_REQUIRED",
                        "시간을 조회할 Dahua PlayGroup이 없습니다.",
                        "QueryPlayGroupTime"));
            }

            try
            {
                var input =
                    new NET_IN_QUERY_PLAYGROUP_TIME
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_QUERY_PLAYGROUP_TIME))),

                        byReserved =
                            new byte[4],

                        lPlayGroupHandle =
                            groupHandle
                    };

                var output =
                    new NET_OUT_QUERY_PLAYGROUP_TIME
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_QUERY_PLAYGROUP_TIME))),

                        stuTime =
                            new NET_TIME_EX
                            {
                                dwReserved =
                                    new uint[1]
                            }
                    };

                bool success =
                    NETClient.QueryPlayGroupTime(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 시간 조회에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_TIME_QUERY_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.QueryPlayGroupTime"));
                }

                DateTime time =
                    output.stuTime.ToDateTime();

                if (time == DateTime.MinValue)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 시간이 올바르지 않습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_TIME_INVALID",
                            "NET_TIME_EX 값을 DateTime으로 변환하지 못했습니다.",
                            "NETClient.QueryPlayGroupTime"));
                }

                return NvrResult<DateTime>.Ok(
                    time,
                    "Dahua PlayGroup 시간을 조회했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<DateTime>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 시간 조회 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_TIME_QUERY_EXCEPTION",
                        ex.Message,
                        "NETClient.QueryPlayGroupTime"));
            }
        }

        internal static NvrResult SetDirection(
            IntPtr groupHandle,
            NvrPlaybackDirection direction)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return InvalidGroup(
                    "SetDirection");
            }

            try
            {
                var input =
                    new NET_IN_SET_PLAYGROUP_DIRECTION
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_SET_PLAYGROUP_DIRECTION))),

                        nPlayDirection =
                            direction == NvrPlaybackDirection.Reverse
                                ? 1
                                : 0,

                        lPlayGroupHandle =
                            groupHandle
                    };

                var output =
                    new NET_OUT_SET_PLAYGROUP_DIRECTION
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_SET_PLAYGROUP_DIRECTION)))
                    };

                bool success =
                    NETClient.SetPlayGroupDirection(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 재생방향 변경에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_DIRECTION_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.SetPlayGroupDirection"));
                }

                return NvrResult.Ok(
                    direction == NvrPlaybackDirection.Reverse
                        ? "Dahua PlayGroup을 역재생 방향으로 변경했습니다."
                        : "Dahua PlayGroup을 정방향으로 변경했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 재생방향 변경 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_DIRECTION_EXCEPTION",
                        ex.Message,
                        "NETClient.SetPlayGroupDirection"));
            }
        }

        internal static NvrResult SetSpeed(
            IntPtr groupHandle,
            NvrPlaybackSpeed speed)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return InvalidGroup(
                    "SetSpeed");
            }

            EM_PLAY_BACK_SPEED nativeSpeed;

            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    nativeSpeed =
                        EM_PLAY_BACK_SPEED.SLOW_2;
                    break;

                case NvrPlaybackSpeed.Normal:
                    nativeSpeed =
                        EM_PLAY_BACK_SPEED.NORMAL;
                    break;

                case NvrPlaybackSpeed.Double:
                    nativeSpeed =
                        EM_PLAY_BACK_SPEED.FAST_2;
                    break;

                case NvrPlaybackSpeed.Quad:
                    nativeSpeed =
                        EM_PLAY_BACK_SPEED.FAST_4;
                    break;

                case NvrPlaybackSpeed.Octuple:
                    nativeSpeed =
                        EM_PLAY_BACK_SPEED.FAST_8;
                    break;

                default:
                    return NvrResult.Fail(
                        NvrResultStatus.NotSupported,
                        "지원하지 않는 Dahua PlayGroup 재생속도입니다. "
                        + "Speed="
                        + speed,
                        CreateError(
                            "DAHUA_PLAYGROUP_SPEED_NOT_SUPPORTED",
                            "지원하지 않는 PlayGroup 재생속도입니다.",
                            "SetPlayGroupSpeed"));
            }

            try
            {
                var input =
                    new NET_IN_SET_PLAYGROUP_SPEED
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_SET_PLAYGROUP_SPEED))),

                        emSpeed =
                            nativeSpeed,

                        lPlayGroupHandle =
                            groupHandle
                    };

                var output =
                    new NET_OUT_SET_PLAYGROUP_SPEED
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_SET_PLAYGROUP_SPEED)))
                    };

                bool success =
                    NETClient.SetPlayGroupSpeed(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 재생속도 변경에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_SPEED_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.SetPlayGroupSpeed"));
                }

                return NvrResult.Ok(
                    "Dahua PlayGroup 재생속도를 변경했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 재생속도 변경 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_SPEED_EXCEPTION",
                        ex.Message,
                        "NETClient.SetPlayGroupSpeed"));
            }
        }

        internal static NvrResult Remove(
            IntPtr groupHandle,
            DahuaPlaybackSession session)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return NvrResult.Ok(
                    "정리할 Dahua PlayGroup이 없습니다.");
            }

            if (session == null
                || session.PlaybackHandle == IntPtr.Zero)
            {
                return NvrResult.Ok(
                    "PlayGroup에서 제거할 Dahua 재생 핸들이 없습니다.");
            }

            try
            {
                var input =
                    new NET_IN_DELETE_FROM_PLAYGROUP
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_IN_DELETE_FROM_PLAYGROUP))),

                        byReserved =
                            new byte[4],

                        lPlayGroupHandle =
                            groupHandle,

                        lPlayHandle =
                            session.PlaybackHandle
                    };

                var output =
                    new NET_OUT_DELETE_FROM_PLAYGROUP
                    {
                        dwSize =
                            Convert.ToUInt32(
                                Marshal.SizeOf(
                                    typeof(
                                        NET_OUT_DELETE_FROM_PLAYGROUP)))
                    };

                bool success =
                    NETClient.DeletePlayHandleFromPlayGroup(
                        ref input,
                        ref output);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 핸들을 PlayGroup에서 제거하지 못했습니다. "
                        + "ChannelNo="
                        + session.ChannelNo,
                        CreateError(
                            "DAHUA_PLAYGROUP_REMOVE_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.DeletePlayHandleFromPlayGroup"));
                }

                return NvrResult.Ok(
                    "Dahua 재생 핸들을 PlayGroup에서 제거했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 핸들 제거 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_REMOVE_EXCEPTION",
                        ex.Message,
                        "NETClient.DeletePlayHandleFromPlayGroup"));
            }
        }

        internal static NvrResult Close(
            IntPtr groupHandle)
        {
            if (groupHandle == IntPtr.Zero)
            {
                return NvrResult.Ok(
                    "Dahua PlayGroup이 이미 정리되었습니다.");
            }

            try
            {
                bool success =
                    NETClient.ClosePlayGroup(
                        groupHandle);

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua PlayGroup 닫기에 실패했습니다.",
                        CreateError(
                            "DAHUA_PLAYGROUP_CLOSE_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.ClosePlayGroup"));
                }

                return NvrResult.Ok(
                    "Dahua PlayGroup을 닫았습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua PlayGroup 닫기 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_PLAYGROUP_CLOSE_EXCEPTION",
                        ex.Message,
                        "NETClient.ClosePlayGroup"));
            }
        }

        private static NvrResult InvalidGroup(
            string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua PlayGroup 핸들이 없습니다.",
                CreateError(
                    "DAHUA_PLAYGROUP_HANDLE_REQUIRED",
                    "Dahua PlayGroup 핸들이 없습니다.",
                    operation));
        }

        private static NvrResult InvalidSession(
            string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua PlaybackHandle이 유효하지 않습니다.",
                CreateError(
                    "DAHUA_PLAYBACK_HANDLE_INVALID",
                    "Dahua PlaybackHandle이 유효하지 않습니다.",
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
