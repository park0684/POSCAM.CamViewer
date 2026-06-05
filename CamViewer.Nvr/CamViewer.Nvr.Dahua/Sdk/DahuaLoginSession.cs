using System;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Dahua.Native;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua NVR 로그인 세션을 나타낸다.
    /// 로그인 핸들과 접속 정보를 보관하며, 해제 시 자동으로 로그아웃한다.
    /// </summary>
    internal sealed class DahuaLoginSession : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 로그인 세션을 초기화한다.
        /// </summary>
        public DahuaLoginSession(
            IntPtr loginHandle,
            NvrConnectionInfo connectionInfo,
            int channelCount)
        {
            LoginHandle = loginHandle;
            ConnectionInfo = connectionInfo;
            ChannelCount = channelCount;
        }

        /// <summary>
        /// Dahua SDK 로그인 핸들.
        /// </summary>
        public IntPtr LoginHandle { get; private set; }

        /// <summary>
        /// 로그인에 사용한 NVR 접속 정보.
        /// </summary>
        public NvrConnectionInfo ConnectionInfo { get; private set; }

        /// <summary>
        /// NVR에서 반환한 채널 수.
        /// </summary>
        public int ChannelCount { get; private set; }

        /// <summary>
        /// 로그인 세션이 유효한지 여부.
        /// </summary>
        public bool IsValid
        {
            get { return !_disposed && LoginHandle != IntPtr.Zero; }
        }

        /// <summary>
        /// 로그인 세션을 해제하고 NVR에서 로그아웃한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (LoginHandle != IntPtr.Zero)
            {
                DahuaNative.CLIENT_Logout(LoginHandle);
                LoginHandle = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}