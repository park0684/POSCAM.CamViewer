using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Models;
using NetSDKCS;
using System;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua 장비 로그인, 로그아웃 및 녹화 존재 조회를 담당한다.
    /// </summary>
    internal static class DahuaDeviceClient
    {
        internal static NvrResult<DahuaLoginSession> Login(
            NvrConnectionInfo connectionInfo)
        {
            NvrResult validationResult =
                ValidateConnectionInfo(
                    connectionInfo);

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
                    CreateError(
                        "DAHUA_SDK_NOT_INITIALIZED",
                        "Dahua SDK가 초기화되지 않았습니다.",
                        "Login"));
            }

            try
            {
                NET_DEVICEINFO_Ex deviceInfo =
                    new NET_DEVICEINFO_Ex
                    {
                        bReserved =
                            new byte[2],

                        Reserved =
                            new byte[24]
                    };

                IntPtr loginHandle =
                    NETClient.LoginWithHighLevelSecurity(
                        connectionInfo.Host,
                        Convert.ToUInt16(
                            connectionInfo.Port),
                        connectionInfo.UserId,
                        connectionInfo.Password,
                        EM_LOGIN_SPAC_CAP_TYPE.TCP,
                        IntPtr.Zero,
                        ref deviceInfo);

                if (loginHandle == IntPtr.Zero)
                {
                    return NvrResult<DahuaLoginSession>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR 로그인에 실패했습니다.",
                        CreateError(
                            "DAHUA_LOGIN_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.LoginWithHighLevelSecurity"));
                }

                var session =
                    new DahuaLoginSession(
                        loginHandle,
                        connectionInfo,
                        deviceInfo);

                return NvrResult<DahuaLoginSession>.Ok(
                    session,
                    "Dahua NVR 로그인에 성공했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<DahuaLoginSession>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR 로그인 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_LOGIN_EXCEPTION",
                        ex.Message,
                        "NETClient.LoginWithHighLevelSecurity"));
            }
        }

        internal static NvrResult Logout(
            DahuaLoginSession session)
        {
            if (session == null)
            {
                return NvrResult.Ok(
                    "Dahua 로그인 세션이 없습니다.");
            }

            IntPtr loginHandle =
                session.TakeLoginHandle();

            if (loginHandle == IntPtr.Zero)
            {
                session.Dispose();

                return NvrResult.Ok(
                    "Dahua NVR에서 이미 로그아웃했습니다.");
            }

            try
            {
                bool success =
                    NETClient.Logout(
                        loginHandle);

                session.Dispose();

                if (!success)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua NVR 로그아웃에 실패했습니다.",
                        CreateError(
                            "DAHUA_LOGOUT_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.Logout"));
                }

                return NvrResult.Ok(
                    "Dahua NVR에서 로그아웃했습니다.");
            }
            catch (Exception ex)
            {
                session.Dispose();

                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "Dahua NVR 로그아웃 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_LOGOUT_EXCEPTION",
                        ex.Message,
                        "NETClient.Logout"));
            }
        }

        internal static NvrResult TestConnection(
            NvrConnectionInfo connectionInfo)
        {
            NvrResult<DahuaLoginSession> loginResult =
                Login(
                    connectionInfo);

            if (!loginResult.Success
                || loginResult.Data == null)
            {
                return NvrResult.Fail(
                    loginResult.Status,
                    loginResult.Message,
                    loginResult.Error);
            }

            NvrResult logoutResult =
                Logout(
                    loginResult.Data);

            if (!logoutResult.Success)
            {
                return NvrResult.Fail(
                    logoutResult.Status,
                    "Dahua NVR 연결 확인은 성공했지만 "
                    + "임시 로그인 세션 정리에 실패했습니다.",
                    logoutResult.Error);
            }

            return NvrResult.Ok(
                "Dahua NVR 연결 확인에 성공했습니다.");
        }

        internal static NvrResult<bool> QueryRecordExists(
            DahuaLoginSession loginSession,
            NvrRecordQueryRequest request)
        {
            if (loginSession == null
                || !loginSession.IsValid)
            {
                return NvrResult<bool>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR 로그인 세션이 유효하지 않습니다.",
                    CreateError(
                        "DAHUA_LOGIN_SESSION_INVALID",
                        "Dahua NVR 로그인 세션이 유효하지 않습니다.",
                        "QueryRecordExists"));
            }

            if (request == null)
            {
                return NvrResult<bool>.Fail(
                    NvrResultStatus.Failed,
                    "녹화 조회 요청 정보가 없습니다.",
                    CreateError(
                        "DAHUA_RECORD_QUERY_REQUIRED",
                        "녹화 조회 요청 정보가 없습니다.",
                        "QueryRecordExists"));
            }

            if (request.ChannelNo <= 0)
            {
                return NvrResult<bool>.Fail(
                    NvrResultStatus.InvalidChannel,
                    "NVR 채널번호는 1 이상이어야 합니다.",
                    CreateError(
                        "DAHUA_RECORD_QUERY_CHANNEL_INVALID",
                        "NVR 채널번호는 1 이상이어야 합니다.",
                        "QueryRecordExists"));
            }

            if (request.StartTime
                >= request.EndTime)
            {
                return NvrResult<bool>.Fail(
                    NvrResultStatus.Failed,
                    "녹화 조회 시작시간은 종료시간보다 이전이어야 합니다.",
                    CreateError(
                        "DAHUA_RECORD_QUERY_RANGE_INVALID",
                        "녹화 조회 시작시간은 종료시간보다 이전이어야 합니다.",
                        "QueryRecordExists"));
            }

            try
            {
                NET_RECORDFILE_INFO[] records =
                    new NET_RECORDFILE_INFO[1];

                int fileCount =
                    0;

                bool success =
                    NETClient.QueryRecordFile(
                        loginSession.LoginHandle,
                        ToDahuaChannelId(
                            request.ChannelNo),
                        EM_QUERY_RECORD_TYPE.ALL,
                        request.StartTime,
                        request.EndTime,
                        null,
                        ref records,
                        ref fileCount,
                        5000,
                        false);

                if (!success)
                {
                    return NvrResult<bool>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 녹화 영상 존재 여부 조회에 실패했습니다.",
                        CreateError(
                            "DAHUA_RECORD_QUERY_FAILED",
                            DahuaSdkRuntime.GetLastErrorSafe(),
                            "NETClient.QueryRecordFile"));
                }

                return NvrResult<bool>.Ok(
                    fileCount > 0,
                    fileCount > 0
                        ? "지정한 구간에 녹화 영상이 있습니다."
                        : "지정한 구간에 녹화 영상이 없습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<bool>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 녹화 영상 조회 중 오류가 발생했습니다.",
                    CreateError(
                        "DAHUA_RECORD_QUERY_EXCEPTION",
                        ex.Message,
                        "NETClient.QueryRecordFile"));
            }
        }

        internal static NvrResult ValidateConnectionInfo(
            NvrConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 접속 정보가 없습니다.",
                    CreateError(
                        "DAHUA_CONNECTION_INFO_REQUIRED",
                        "NVR 접속 정보가 없습니다.",
                        "ValidateConnectionInfo"));
            }

            if (string.IsNullOrWhiteSpace(
                    connectionInfo.Host))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR IP 또는 도메인이 없습니다.",
                    CreateError(
                        "DAHUA_HOST_REQUIRED",
                        "NVR IP 또는 도메인이 없습니다.",
                        "ValidateConnectionInfo"));
            }

            if (connectionInfo.Port <= 0
                || connectionInfo.Port > 65535)
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 포트가 올바르지 않습니다.",
                    CreateError(
                        "DAHUA_PORT_INVALID",
                        "NVR 포트가 올바르지 않습니다.",
                        "ValidateConnectionInfo"));
            }

            if (string.IsNullOrWhiteSpace(
                    connectionInfo.UserId))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 로그인 ID가 없습니다.",
                    CreateError(
                        "DAHUA_USER_REQUIRED",
                        "NVR 로그인 ID가 없습니다.",
                        "ValidateConnectionInfo"));
            }

            if (string.IsNullOrWhiteSpace(
                    connectionInfo.Password))
            {
                return NvrResult.Fail(
                    NvrResultStatus.Failed,
                    "NVR 로그인 비밀번호가 없습니다.",
                    CreateError(
                        "DAHUA_PASSWORD_REQUIRED",
                        "NVR 로그인 비밀번호가 없습니다.",
                        "ValidateConnectionInfo"));
            }

            return NvrResult.Ok();
        }

        internal static int ToDahuaChannelId(
            int channelNo)
        {
            return channelNo - 1;
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
