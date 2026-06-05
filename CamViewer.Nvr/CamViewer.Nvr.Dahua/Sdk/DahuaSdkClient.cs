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
    }
}