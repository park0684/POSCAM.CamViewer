using CamViewer.Nvr.Core.Models;
using NetSDKCS;
using System;

namespace CamViewer.Nvr.Dahua.Models
{
    /// <summary>
    /// Dahua NVR 로그인 핸들과 장비 정보를 보관한다.
    /// </summary>
    internal sealed class DahuaLoginSession : IDisposable
    {
        private bool _disposed;

        internal DahuaLoginSession(
            IntPtr loginHandle,
            NvrConnectionInfo connectionInfo,
            NET_DEVICEINFO_Ex deviceInfo)
        {
            if (loginHandle == IntPtr.Zero)
            {
                throw new ArgumentException(
                    "Dahua 로그인 핸들이 올바르지 않습니다.",
                    "loginHandle");
            }

            if (connectionInfo == null)
            {
                throw new ArgumentNullException(
                    "connectionInfo");
            }

            LoginHandle =
                loginHandle;

            ConnectionInfo =
                connectionInfo;

            DeviceInfo =
                deviceInfo;
        }

        internal IntPtr LoginHandle
        {
            get;
            private set;
        }

        internal NvrConnectionInfo ConnectionInfo
        {
            get;
            private set;
        }

        internal NET_DEVICEINFO_Ex DeviceInfo
        {
            get;
            private set;
        }

        internal int ChannelCount
        {
            get
            {
                return DeviceInfo.nChanNum;
            }
        }

        internal bool IsValid
        {
            get
            {
                return !_disposed
                    && LoginHandle != IntPtr.Zero;
            }
        }

        /// <summary>
        /// 로그아웃을 수행할 호출자에게 핸들을 넘기고
        /// 현재 세션에서는 즉시 분리한다.
        /// </summary>
        internal IntPtr TakeLoginHandle()
        {
            IntPtr handle =
                LoginHandle;

            LoginHandle =
                IntPtr.Zero;

            return handle;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            IntPtr handle =
                TakeLoginHandle();

            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                NETClient.Logout(
                    handle);
            }
            catch
            {
                /*
                 * Dispose 경로에서는 네이티브 로그아웃 예외를 전파하지 않는다.
                 */
            }
        }
    }
}
