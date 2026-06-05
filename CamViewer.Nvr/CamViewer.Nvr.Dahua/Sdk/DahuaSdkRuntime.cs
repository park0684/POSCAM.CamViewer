using System;
using System.IO;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Native;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua NetSDK의 프로세스 단위 초기화와 정리를 관리한다.
    ///
    /// 여러 NVR Provider 인스턴스가 동시에 사용되더라도 SDK 초기화는 한 번만 수행하고,
    /// 마지막 Provider가 해제될 때 SDK를 정리한다.
    /// </summary>
    internal static class DahuaSdkRuntime
    {
        private static readonly object SyncRoot = new object();

        private static int _referenceCount;
        private static bool _initialized;

        // 콜백이 GC에 의해 정리되지 않도록 정적 필드로 유지한다.
        private static DahuaNative.fDisConnect _disconnectCallback;
        private static DahuaNative.fHaveReConnect _reconnectCallback;

        /// <summary>
        /// Dahua SDK를 초기화하고 사용 참조 수를 증가시킨다.
        /// </summary>
        public static NvrResult Acquire()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    _referenceCount++;

                    return NvrResult.Ok("Dahua SDK가 이미 초기화되어 있습니다.");
                }

                var nativePathResult = ConfigureNativeLibraryPath();

                if (!nativePathResult.Success)
                {
                    return nativePathResult;
                }

                //_disconnectCallback = OnDisconnected;
                //_reconnectCallback = OnReconnected;

                bool initResult = DahuaNative.CLIENT_Init(
                    _disconnectCallback,
                    IntPtr.Zero);

                if (!initResult)
                {
                    return CreateSdkFailure(
                        "SDK_INIT_FAILED",
                        "Dahua SDK 초기화에 실패했습니다.",
                        "CLIENT_Init");
                }

                // 네트워크가 일시적으로 끊긴 경우 자동 재연결을 사용한다.
                DahuaNative.CLIENT_SetAutoReconnect(
                    _reconnectCallback,
                    IntPtr.Zero);

                // 접속 대기 시간과 재시도 횟수.
                DahuaNative.CLIENT_SetConnectTime(5000, 3);

                _initialized = true;
                _referenceCount = 1;

                return NvrResult.Ok("Dahua SDK 초기화가 완료되었습니다.");
            }
        }

        /// <summary>
        /// Dahua SDK 사용 참조 수를 감소시키고,
        /// 마지막 참조가 해제되면 SDK 리소스를 정리한다.
        /// </summary>
        public static void Release()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    return;
                }

                if (_referenceCount > 0)
                {
                    _referenceCount--;
                }

                if (_referenceCount > 0)
                {
                    return;
                }

                DahuaNative.CLIENT_Cleanup();

                _disconnectCallback = null;
                _reconnectCallback = null;
                _initialized = false;
                _referenceCount = 0;
            }
        }

        /// <summary>
        /// Dahua SDK 초기화 여부를 반환한다.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (SyncRoot)
                {
                    return _initialized;
                }
            }
        }

        /// <summary>
        /// Provider DLL 하위의 native 폴더를 네이티브 DLL 검색 경로로 설정한다.
        /// </summary>
        private static NvrResult ConfigureNativeLibraryPath()
        {
            string providerAssemblyPath =
                typeof(DahuaSdkRuntime).Assembly.Location;

            string providerDirectory =
                Path.GetDirectoryName(providerAssemblyPath);

            string nativeDirectory =
                Path.Combine(providerDirectory, "native");

            if (!Directory.Exists(nativeDirectory))
            {
                return NvrResult.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua SDK native 폴더를 찾을 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "NATIVE_DIRECTORY_NOT_FOUND",
                        ErrorMessage = nativeDirectory,
                        Operation = "ConfigureNativeLibraryPath"
                    });
            }

            bool pathResult = Kernel32Native.SetDllDirectory(nativeDirectory);

            if (!pathResult)
            {
                return NvrResult.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua SDK DLL 검색 경로를 설정할 수 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "SET_DLL_DIRECTORY_FAILED",
                        ErrorMessage = nativeDirectory,
                        Operation = "SetDllDirectory"
                    });
            }

            return NvrResult.Ok();
        }

        /// <summary>
        /// NVR 연결이 끊겼을 때 Dahua SDK에서 호출하는 콜백.
        /// 향후 로그 기록 기능을 연결한다.
        /// </summary>
        private static void OnDisconnected(
            long loginId,
            IntPtr dvrIp,
            int dvrPort,
            IntPtr userData)
        {
            // SDK 콜백 스레드에서 UI를 직접 조작하면 안 된다.
            // 향후 로그 또는 Provider 이벤트로 전달한다.
        }

        /// <summary>
        /// NVR 연결이 복구되었을 때 Dahua SDK에서 호출하는 콜백.
        /// 향후 로그 기록 기능을 연결한다.
        /// </summary>
        private static void OnReconnected(
            long loginId,
            IntPtr dvrIp,
            int dvrPort,
            IntPtr userData)
        {
            // SDK 콜백 스레드에서 UI를 직접 조작하면 안 된다.
            // 향후 로그 또는 Provider 이벤트로 전달한다.
        }

        /// <summary>
        /// Dahua SDK 오류 결과를 생성한다.
        /// </summary>
        private static NvrResult CreateSdkFailure(
            string errorCode,
            string message,
            string operation)
        {
            uint nativeErrorCode = DahuaNative.CLIENT_GetLastError();

            return NvrResult.Fail(
                NvrResultStatus.SdkError,
                message,
                new NvrErrorInfo
                {
                    ErrorCode = errorCode,
                    ErrorMessage = message,
                    NativeErrorCode = nativeErrorCode.ToString(),
                    Operation = operation
                });
        }
    }
}