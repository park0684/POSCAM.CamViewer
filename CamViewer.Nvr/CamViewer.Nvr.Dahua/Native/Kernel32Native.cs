using System.Runtime.InteropServices;

namespace CamViewer.Nvr.Dahua.Native
{
    /// <summary>
    /// Windows 네이티브 DLL 검색 경로를 설정하기 위한 함수 모음.
    /// </summary>
    internal static class Kernel32Native
    {
        /// <summary>
        /// 프로세스의 네이티브 DLL 검색 경로를 설정한다.
        /// Dahua SDK 함수를 최초 호출하기 전에 실행해야 한다.
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetDllDirectory(string lpPathName);
    }
}