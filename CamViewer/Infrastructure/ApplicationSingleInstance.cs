using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CamViewer.Infrastructure
{
    /// <summary>
    /// CamViewer 프로그램의 단일 실행 상태를 관리한다.
    ///
    /// 역할:
    /// - 이름이 지정된 Mutex를 사용하여 최초 실행 여부 확인
    /// - 이미 실행 중인 CamViewer 창 검색
    /// - 최소화된 기존 창 복원
    /// - 기존 창을 사용자 화면 앞으로 활성화
    ///
    /// 현재 단계에서는 중복 직접 실행을 차단하는 용도로 사용한다.
    /// 외부 영상 조회 요청 전달은 이후 Named Pipe 기능에서 확장한다.
    /// </summary>
    public sealed class ApplicationSingleInstance : IDisposable
    {
        /// <summary>
        /// PC 전체에서 CamViewer 실행 여부를 식별할 Mutex 이름.
        /// </summary>
        private const string MutexName =
            @"Global\POSCAM.CamViewer.SingleInstance";

        private const int SwShow = 5;
        private const int SwRestore = 9;

        private Mutex _mutex;
        private bool _ownsMutex;
        private bool _disposed;

        /// <summary>
        /// Windows 최상위 창을 순회할 때 사용하는 콜백 형식.
        /// </summary>
        private delegate bool EnumWindowsCallback(
            IntPtr windowHandle,
            IntPtr parameter);

        /// <summary>
        /// 현재 프로세스가 CamViewer 단일 실행 권한을 획득한다.
        ///
        /// 반환값:
        /// - true: 현재 프로세스가 최초 실행 프로세스
        /// - false: 다른 CamViewer 프로세스가 이미 실행 중
        /// </summary>
        public bool TryAcquire()
        {
            ThrowIfDisposed();

            if (_mutex != null)
            {
                return _ownsMutex;
            }

            _mutex =
                new Mutex(
                    false,
                    MutexName);

            try
            {
                /*
                 * 즉시 Mutex 획득을 시도한다.
                 *
                 * 다른 프로세스가 이미 소유하고 있으면
                 * 대기하지 않고 false를 반환한다.
                 */
                _ownsMutex =
                    _mutex.WaitOne(
                        0,
                        false);
            }
            catch (AbandonedMutexException)
            {
                /*
                 * 이전 CamViewer 프로세스가 비정상 종료되어
                 * Mutex가 버려진 경우 현재 프로세스가 소유권을 넘겨받는다.
                 */
                _ownsMutex = true;
            }

            return _ownsMutex;
        }

        /// <summary>
        /// 현재 프로세스를 제외한 기존 CamViewer 프로세스의
        /// 화면을 찾아 복원하고 활성화한다.
        /// </summary>
        /// <returns>
        /// 기존 창을 찾아 활성화했으면 true.
        /// 창을 찾지 못했으면 false.
        /// </returns>
        public bool TryActivateExistingInstance()
        {
            ThrowIfDisposed();

            using (Process currentProcess =
                Process.GetCurrentProcess())
            {
                Process[] processes =
                    Process.GetProcessesByName(
                        currentProcess.ProcessName);

                try
                {
                    foreach (Process process in processes)
                    {
                        if (process == null
                            || process.Id == currentProcess.Id)
                        {
                            continue;
                        }

                        /*
                         * 기존 프로세스가 시작 중일 수 있으므로
                         * 최대 약 2초 동안 화면 Handle을 검색한다.
                         */
                        for (int retry = 0;
                            retry < 20;
                            retry++)
                        {
                            IntPtr windowHandle =
                                FindVisibleWindow(
                                    process.Id);

                            if (windowHandle != IntPtr.Zero)
                            {
                                ActivateWindow(
                                    windowHandle);

                                return true;
                            }

                            Thread.Sleep(100);
                        }
                    }
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        if (process != null)
                        {
                            process.Dispose();
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 지정된 프로세스가 소유한 표시 가능한 최상위 창을 찾는다.
        ///
        /// LandingView, LoginView, PlayerView 또는 설정창 중
        /// 현재 화면에 표시된 창을 대상으로 한다.
        /// </summary>
        private static IntPtr FindVisibleWindow(
            int processId)
        {
            IntPtr selectedWindow =
                IntPtr.Zero;

            EnumWindows(
                delegate (
                    IntPtr windowHandle,
                    IntPtr parameter)
                {
                    uint windowProcessId;

                    GetWindowThreadProcessId(
                        windowHandle,
                        out windowProcessId);

                    if (windowProcessId
                        != (uint)processId)
                    {
                        return true;
                    }

                    if (!IsWindowVisible(
                        windowHandle))
                    {
                        return true;
                    }

                    selectedWindow =
                        windowHandle;

                    // 대상 창을 찾았으므로 열거를 중지한다.
                    return false;
                },
                IntPtr.Zero);

            return selectedWindow;
        }

        /// <summary>
        /// 기존 CamViewer 창을 복원하고 앞으로 가져온다.
        /// </summary>
        private static void ActivateWindow(
            IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            if (IsIconic(windowHandle))
            {
                ShowWindowAsync(
                    windowHandle,
                    SwRestore);
            }
            else
            {
                ShowWindowAsync(
                    windowHandle,
                    SwShow);
            }

            SetForegroundWindow(
                windowHandle);
        }

        /// <summary>
        /// 객체가 이미 해제되었는지 확인한다.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    "ApplicationSingleInstance");
            }
        }

        /// <summary>
        /// 현재 프로세스가 소유한 Mutex를 해제한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_mutex == null)
            {
                return;
            }

            if (_ownsMutex)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    /*
                     * 이미 소유권이 해제된 예외 상황에서는
                     * 프로그램 종료를 막지 않는다.
                     */
                }

                _ownsMutex = false;
            }

            _mutex.Dispose();
            _mutex = null;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr windowHandle,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(
            IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(
            IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(
            IntPtr windowHandle,
            int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(
            IntPtr windowHandle);
    }
}