using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Diagnostics;
using NetSDKCS;
using System;
using System.Runtime.InteropServices;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua NetSDK의 프로세스 전역 초기화와 해제를 관리한다.
    ///
    /// 첫 번째 Provider가 Acquire를 호출할 때 SDK를 초기화하고,
    /// 마지막 Provider가 Release를 호출할 때 SDK를 정리한다.
    /// </summary>
    internal static class DahuaSdkRuntime
    {
        private static readonly object SyncRoot =
            new object();

        /*
         * 네이티브 SDK가 콜백 함수 포인터를 계속 사용하므로
         * Delegate를 정적 필드에 보관한다.
         */
        private static readonly fDisConnectCallBack DisconnectCallback =
            OnDisconnected;

        private static readonly fHaveReConnectCallBack ReconnectCallback =
            OnReconnected;

        private static bool _initialized;
        private static int _referenceCount;

        internal static event Action<IntPtr, string, int>
            DeviceDisconnected;

        internal static event Action<IntPtr, string, int>
            DeviceReconnected;

        internal static bool IsInitialized
        {
            get
            {
                lock (SyncRoot)
                {
                    return _initialized;
                }
            }
        }

        internal static int ReferenceCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _referenceCount;
                }
            }
        }

        /// <summary>
        /// Dahua SDK 사용 참조를 획득한다.
        /// </summary>
        internal static NvrResult Acquire()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    checked
                    {
                        _referenceCount++;
                    }

                    return NvrResult.Ok(
                        "기존 Dahua SDK 초기화 상태를 재사용합니다. "
                        + "ReferenceCount="
                        + _referenceCount);
                }

                try
                {
                    /*
                     * NetSDKCS가 처음 네이티브 P/Invoke를 실행하기 전에
                     * CamViewer의 제조사별 native\Dahua 폴더에서
                     * dhnetsdk.dll을 명시적으로 로드한다.
                     */
                    NvrResult nativeLoadResult =
                        DahuaNativeLibraryLoader.Load();

                    if (nativeLoadResult == null
                        || !nativeLoadResult.Success)
                    {
                        ResetState();

                        return nativeLoadResult
                            ?? NvrResult.Fail(
                                NvrResultStatus.SdkError,
                                "Dahua 네이티브 SDK 로드 결과가 없습니다.",
                                CreateError(
                                    "DAHUA_NATIVE_LOAD_RESULT_EMPTY",
                                    "Dahua 네이티브 SDK 로드 결과가 없습니다.",
                                    "DahuaNativeLibraryLoader.Load"));
                    }

                    bool initialized =
                        NETClient.InitWithDefaultSetting(
                            DisconnectCallback,
                            ReconnectCallback,
                            IntPtr.Zero,
                            null);

                    if (!initialized)
                    {
                        DahuaNativeLibraryLoader.Unload();

                        ResetState();

                        return NvrResult.Fail(
                            NvrResultStatus.SdkError,
                            "Dahua SDK 초기화에 실패했습니다.",
                            CreateError(
                                "DAHUA_SDK_INITIALIZE_FAILED",
                                GetLastErrorSafe(),
                                "NETClient.InitWithDefaultSetting"));
                    }

                    _initialized =
                        true;

                    _referenceCount =
                        1;

                    DahuaLogWriter.Write(
                        "INFO",
                        "SDK.Initialize",
                        "Dahua SDK 초기화 성공");

                    return NvrResult.Ok(
                        "Dahua SDK를 초기화했습니다.");
                }
                catch (DllNotFoundException ex)
                {
                    DahuaNativeLibraryLoader.Unload();

                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK 네이티브 DLL을 찾지 못했습니다.",
                        CreateError(
                            "DAHUA_SDK_DLL_NOT_FOUND",
                            ex.Message,
                            "NETClient.InitWithDefaultSetting"));
                }
                catch (BadImageFormatException ex)
                {
                    DahuaNativeLibraryLoader.Unload();

                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK DLL과 CamViewer의 플랫폼이 일치하지 않습니다. "
                        + "모두 x64로 빌드해야 합니다.",
                        CreateError(
                            "DAHUA_SDK_PLATFORM_MISMATCH",
                            ex.Message,
                            "NETClient.InitWithDefaultSetting"));
                }
                catch (EntryPointNotFoundException ex)
                {
                    DahuaNativeLibraryLoader.Unload();

                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK DLL에서 필요한 초기화 함수를 찾지 못했습니다.",
                        CreateError(
                            "DAHUA_SDK_ENTRYPOINT_NOT_FOUND",
                            ex.Message,
                            "NETClient.InitWithDefaultSetting"));
                }
                catch (Exception ex)
                {
                    DahuaNativeLibraryLoader.Unload();

                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK 초기화 중 오류가 발생했습니다.",
                        CreateError(
                            "DAHUA_SDK_INITIALIZE_EXCEPTION",
                            ex.Message,
                            "NETClient.InitWithDefaultSetting"));
                }
            }
        }

        /// <summary>
        /// Dahua SDK 사용 참조를 반환한다.
        /// </summary>
        internal static NvrResult Release()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    ResetState();

                    return NvrResult.Ok(
                        "Dahua SDK가 이미 정리된 상태입니다.");
                }

                if (_referenceCount > 0)
                {
                    _referenceCount--;
                }

                if (_referenceCount > 0)
                {
                    return NvrResult.Ok(
                        "Dahua SDK 참조를 반환했습니다. "
                        + "ReferenceCount="
                        + _referenceCount);
                }

                try
                {
                    NETClient.Cleanup();

                    DahuaNativeLibraryLoader.Unload();

                    DahuaLogWriter.Write(
                        "INFO",
                        "SDK.Cleanup",
                        "Dahua SDK 정리 완료");

                    ResetState();

                    return NvrResult.Ok(
                        "Dahua SDK를 정리했습니다.");
                }
                catch (Exception ex)
                {
                    DahuaNativeLibraryLoader.Unload();

                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK 정리 중 오류가 발생했습니다.",
                        CreateError(
                            "DAHUA_SDK_CLEANUP_EXCEPTION",
                            ex.Message,
                            "NETClient.Cleanup"));
                }
            }
        }

        private static void OnDisconnected(
            IntPtr loginHandle,
            IntPtr deviceIpPointer,
            int devicePort,
            IntPtr userData)
        {
            string deviceIp =
                ConvertAnsiString(
                    deviceIpPointer);

            DahuaLogWriter.Write(
                "WARN",
                "SDK.Disconnected",
                "Handle="
                + loginHandle
                + ", Host="
                + deviceIp
                + ", Port="
                + devicePort);

            RaiseEvent(
                DeviceDisconnected,
                loginHandle,
                deviceIp,
                devicePort);
        }

        private static void OnReconnected(
            IntPtr loginHandle,
            IntPtr deviceIpPointer,
            int devicePort,
            IntPtr userData)
        {
            string deviceIp =
                ConvertAnsiString(
                    deviceIpPointer);

            DahuaLogWriter.Write(
                "INFO",
                "SDK.Reconnected",
                "Handle="
                + loginHandle
                + ", Host="
                + deviceIp
                + ", Port="
                + devicePort);

            RaiseEvent(
                DeviceReconnected,
                loginHandle,
                deviceIp,
                devicePort);
        }

        private static void RaiseEvent(
            Action<IntPtr, string, int> handler,
            IntPtr loginHandle,
            string deviceIp,
            int devicePort)
        {
            if (handler == null)
            {
                return;
            }

            Delegate[] subscribers =
                handler.GetInvocationList();

            foreach (Delegate subscriber
                in subscribers)
            {
                Action<IntPtr, string, int> callback =
                    subscriber
                        as Action<IntPtr, string, int>;

                if (callback == null)
                {
                    continue;
                }

                try
                {
                    callback(
                        loginHandle,
                        deviceIp,
                        devicePort);
                }
                catch
                {
                    /*
                     * 구독자 예외를 네이티브 콜백 경계 밖으로 보내지 않는다.
                     */
                }
            }
        }

        private static string ConvertAnsiString(
            IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringAnsi(
                    value)
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string GetLastErrorSafe()
        {
            try
            {
                string error =
                    NETClient.GetLastError();

                return string.IsNullOrWhiteSpace(
                        error)
                    ? "Dahua SDK 상세 오류가 없습니다."
                    : error;
            }
            catch (Exception ex)
            {
                return "Dahua SDK 오류 조회 실패: "
                    + ex.Message;
            }
        }

        internal static NvrErrorInfo CreateError(
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
                    GetLastErrorSafe(),

                Operation =
                    operation
            };
        }

        private static void ResetState()
        {
            _initialized =
                false;

            _referenceCount =
                0;
        }
    }
}
