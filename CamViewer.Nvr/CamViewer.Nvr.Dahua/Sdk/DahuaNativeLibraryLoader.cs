using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Results;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// CamViewer 실행 폴더 아래의 native\Dahua 경로에서
    /// Dahua 네이티브 SDK를 명시적으로 로드한다.
    ///
    /// 배포 구조:
    /// - providers\Dahua : 관리 DLL
    /// - native\Dahua    : Dahua 네이티브 DLL
    ///
    /// .NET Framework의 일반 P/Invoke 검색은 실행 폴더의 하위 폴더를
    /// 자동으로 탐색하지 않으므로 NETClient 초기화 전에 이 로더가
    /// dhnetsdk.dll을 전체 경로로 먼저 로드해야 한다.
    /// </summary>
    internal static class DahuaNativeLibraryLoader
    {
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        private static readonly object SyncRoot =
            new object();

        private static IntPtr _mainModuleHandle;
        private static bool _loaded;
        private static string _nativeDirectory;

        /// <summary>
        /// 현재 확인된 Dahua 네이티브 SDK 폴더.
        /// </summary>
        internal static string NativeDirectory
        {
            get
            {
                lock (SyncRoot)
                {
                    return _nativeDirectory;
                }
            }
        }

        /// <summary>
        /// Dahua 메인 네이티브 모듈이 로드됐는지 여부.
        /// </summary>
        internal static bool IsLoaded
        {
            get
            {
                lock (SyncRoot)
                {
                    return _loaded
                        && _mainModuleHandle != IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// native\Dahua의 dhnetsdk.dll과 종속 DLL을 로드한다.
        ///
        /// LOAD_WITH_ALTERED_SEARCH_PATH를 사용하므로 dhnetsdk.dll의
        /// 직접 종속 DLL은 같은 native\Dahua 폴더에서 우선 검색한다.
        /// 또한 SDK가 실행 중 추가 모듈을 이름으로 로드할 수 있으므로
        /// 해당 폴더를 현재 프로세스 PATH에 한 번 추가한다.
        /// </summary>
        internal static NvrResult Load()
        {
            lock (SyncRoot)
            {
                if (IsLoaded)
                {
                    return NvrResult.Ok(
                        "Dahua 네이티브 SDK가 이미 로드되어 있습니다. "
                        + "Path="
                        + _nativeDirectory);
                }

                if (!Environment.Is64BitProcess)
                {
                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua x64 SDK는 64비트 프로세스에서만 로드할 수 있습니다.",
                        CreateError(
                            "DAHUA_PROCESS_ARCHITECTURE_MISMATCH",
                            "현재 CamViewer 프로세스가 64비트가 아닙니다.",
                            null,
                            "DahuaNativeLibraryLoader.Load"));
                }

                string baseDirectory =
                    AppDomain.CurrentDomain.BaseDirectory;

                string nativeDirectory =
                    Path.GetFullPath(
                        Path.Combine(
                            baseDirectory,
                            "native",
                            "Dahua"));

                _nativeDirectory =
                    nativeDirectory;

                if (!Directory.Exists(nativeDirectory))
                {
                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua 네이티브 SDK 폴더를 찾지 못했습니다."
                        + Environment.NewLine
                        + "경로: "
                        + nativeDirectory,
                        CreateError(
                            "DAHUA_NATIVE_DIRECTORY_NOT_FOUND",
                            "Dahua 네이티브 SDK 폴더가 없습니다.",
                            null,
                            "DahuaNativeLibraryLoader.Load"));
                }

                string mainSdkPath =
                    Path.Combine(
                        nativeDirectory,
                        "dhnetsdk.dll");

                if (!File.Exists(mainSdkPath))
                {
                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua SDK 메인 DLL을 찾지 못했습니다."
                        + Environment.NewLine
                        + "경로: "
                        + mainSdkPath,
                        CreateError(
                            "DAHUA_SDK_DLL_NOT_FOUND",
                            "dhnetsdk.dll 파일이 없습니다.",
                            null,
                            "DahuaNativeLibraryLoader.Load"));
                }

                try
                {
                    /*
                     * 여러 제조사의 네이티브 폴더를 동시에 유지할 수 있도록
                     * 기존 PATH를 교체하지 않고 Dahua 경로만 앞쪽에 추가한다.
                     */
                    AddDirectoryToProcessPath(
                        nativeDirectory);

                    /*
                     * 전체 경로로 메인 SDK를 먼저 로드한다.
                     * 이 호출이 성공한 뒤 NetSDKCS의
                     * [DllImport("dhnetsdk.dll")] 호출이 같은 모듈을 사용한다.
                     */
                    IntPtr moduleHandle =
                        LoadLibraryEx(
                            mainSdkPath,
                            IntPtr.Zero,
                            LoadWithAlteredSearchPath);

                    if (moduleHandle == IntPtr.Zero)
                    {
                        int nativeError =
                            Marshal.GetLastWin32Error();

                        string nativeMessage =
                            GetWin32ErrorMessage(
                                nativeError);

                        return NvrResult.Fail(
                            NvrResultStatus.SdkError,
                            "Dahua 네이티브 SDK를 로드하지 못했습니다."
                            + Environment.NewLine
                            + "파일: "
                            + mainSdkPath
                            + Environment.NewLine
                            + "Win32Error="
                            + nativeError
                            + " ("
                            + nativeMessage
                            + ")",
                            CreateError(
                                "DAHUA_NATIVE_DLL_LOAD_FAILED",
                                nativeMessage,
                                nativeError.ToString(),
                                "LoadLibraryEx"));
                    }

                    _mainModuleHandle =
                        moduleHandle;

                    _loaded =
                        true;

                    return NvrResult.Ok(
                        "Dahua 네이티브 SDK를 로드했습니다."
                        + Environment.NewLine
                        + "경로: "
                        + mainSdkPath);
                }
                catch (Exception ex)
                {
                    ResetState();

                    return NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua 네이티브 SDK 로드 중 오류가 발생했습니다.",
                        CreateError(
                            "DAHUA_NATIVE_LOAD_EXCEPTION",
                            ex.Message,
                            null,
                            "DahuaNativeLibraryLoader.Load"));
                }
            }
        }

        /// <summary>
        /// NETClient.Cleanup 이후 수동으로 확보한 메인 모듈 참조를 해제한다.
        ///
        /// 프로세스 PATH에 추가한 제조사 폴더는 제거하지 않는다.
        /// 다른 Dahua 객체 또는 네이티브 지연 로딩이 남아 있을 가능성을
        /// 고려하고, 프로세스 종료 시 운영체제가 함께 정리하도록 둔다.
        /// </summary>
        internal static void Unload()
        {
            lock (SyncRoot)
            {
                if (_mainModuleHandle != IntPtr.Zero)
                {
                    try
                    {
                        FreeLibrary(
                            _mainModuleHandle);
                    }
                    catch
                    {
                        /*
                         * 프로세스 종료 및 Provider Dispose 과정에서
                         * 네이티브 모듈 해제 오류를 외부로 전파하지 않는다.
                         */
                    }
                }

                ResetState();
            }
        }

        /// <summary>
        /// 지정한 폴더를 현재 프로세스 PATH 앞쪽에 중복 없이 추가한다.
        /// </summary>
        private static void AddDirectoryToProcessPath(
            string directory)
        {
            string normalizedDirectory =
                NormalizeDirectory(
                    directory);

            string currentPath =
                Environment.GetEnvironmentVariable(
                    "PATH",
                    EnvironmentVariableTarget.Process)
                ?? string.Empty;

            string[] pathItems =
                currentPath.Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string pathItem in pathItems)
            {
                if (string.Equals(
                        NormalizeDirectory(pathItem),
                        normalizedDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            string updatedPath =
                string.IsNullOrWhiteSpace(currentPath)
                    ? directory
                    : directory
                        + ";"
                        + currentPath;

            Environment.SetEnvironmentVariable(
                "PATH",
                updatedPath,
                EnvironmentVariableTarget.Process);
        }


        /// <summary>
        /// 폴더 비교용 경로를 생성한다.
        /// </summary>
        private static string NormalizeDirectory(
            string directory)
        {
            if (string.IsNullOrWhiteSpace(
                    directory))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(
                        directory.Trim())
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                /*
                 * 경로 정규화에 실패하더라도
                 * 문자열 앞뒤 공백과 마지막 경로 구분자는 제거한다.
                 *
                 * 역슬래시는 문자 리터럴에서 '\\'로 작성해야 한다.
                 */
                return directory.Trim()
                    .TrimEnd(
                        '\\',
                        '/');
            }
        }



        /// <summary>
        /// Win32 오류번호를 사람이 확인할 수 있는 메시지로 변환한다.
        /// </summary>
        private static string GetWin32ErrorMessage(
            int nativeError)
        {
            try
            {
                return new Win32Exception(
                    nativeError).Message;
            }
            catch
            {
                return "Win32 오류 메시지를 확인하지 못했습니다.";
            }
        }

        private static NvrErrorInfo CreateError(
            string errorCode,
            string errorMessage,
            string nativeErrorCode,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    errorMessage,

                NativeErrorCode =
                    nativeErrorCode,

                Operation =
                    operation
            };
        }

        private static void ResetState()
        {
            _mainModuleHandle =
                IntPtr.Zero;

            _loaded =
                false;
        }

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(
            string lpFileName,
            IntPtr hFile,
            uint dwFlags);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool FreeLibrary(
            IntPtr hModule);
    }
}
