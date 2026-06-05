using System;
using System.Runtime.InteropServices;

namespace CamViewer.Nvr.Dahua.Native
{
    /// <summary>
    /// Dahua NetSDK 네이티브 함수와 공통 구조체를 정의한다.
    ///
    /// 주의사항:
    /// - 현재 프로젝트는 x64로 빌드하므로 반드시 Dahua x64 SDK DLL을 사용해야 한다.
    /// - SDK 버전에 따라 함수 또는 구조체 정의가 달라질 수 있으므로,
    ///   실제 배포 SDK의 dhnetsdk.h 또는 C# 예제와 반드시 비교해야 한다.
    /// - 사용자 ID, 비밀번호, 토큰 등의 민감정보는 로그에 기록하지 않는다.
    /// </summary>
    internal static class DahuaNative
    {
        /// <summary>
        /// Dahua NetSDK 기본 네이티브 DLL 이름.
        /// </summary>
        private const string DllName = "dhnetsdk.dll";

        #region Delegates

        /// <summary>
        /// NVR 연결이 끊겼을 때 SDK에서 호출하는 콜백.
        /// </summary>
        /// <param name="loginHandle">로그인 핸들.</param>
        /// <param name="dvrIp">연결이 끊긴 장비 IP.</param>
        /// <param name="dvrPort">연결이 끊긴 장비 포트.</param>
        /// <param name="userData">사용자 데이터 포인터.</param>
        [UnmanagedFunctionPointer(
            CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal delegate void fDisConnect(
            IntPtr loginHandle,
            [MarshalAs(UnmanagedType.LPStr)] string dvrIp,
            int dvrPort,
            IntPtr userData);

        /// <summary>
        /// NVR 연결이 복구되었을 때 SDK에서 호출하는 콜백.
        /// </summary>
        /// <param name="loginHandle">로그인 핸들.</param>
        /// <param name="dvrIp">연결이 복구된 장비 IP.</param>
        /// <param name="dvrPort">연결이 복구된 장비 포트.</param>
        /// <param name="userData">사용자 데이터 포인터.</param>
        [UnmanagedFunctionPointer(
            CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal delegate void fHaveReConnect(
            IntPtr loginHandle,
            [MarshalAs(UnmanagedType.LPStr)] string dvrIp,
            int dvrPort,
            IntPtr userData);

        #endregion

        #region Enums

        /// <summary>
        /// Dahua NVR 로그인 방식.
        ///
        /// 현재 단계에서는 TCP 로그인만 사용한다.
        /// </summary>
        internal enum EM_LOGIN_SPAC_CAP_TYPE
        {
            /// <summary>
            /// TCP 방식 로그인.
            /// </summary>
            TCP = 0
        }

        #endregion

        #region Structs

        /// <summary>
        /// Dahua NVR 로그인 후 반환되는 장비 정보.
        ///
        /// ByValArray 필드는 네이티브 함수 호출 전에 반드시 초기화해야 한다.
        /// </summary>
        [StructLayout(
            LayoutKind.Sequential,
            Pack = 1,
            CharSet = CharSet.Ansi)]
        internal struct NET_DEVICEINFO_Ex
        {
            /// <summary>
            /// 장비 시리얼 번호.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] sSerialNumber;

            /// <summary>
            /// 알람 입력 포트 수.
            /// </summary>
            public int nAlarmInPortNum;

            /// <summary>
            /// 알람 출력 포트 수.
            /// </summary>
            public int nAlarmOutPortNum;

            /// <summary>
            /// 디스크 수.
            /// </summary>
            public int nDiskNum;

            /// <summary>
            /// 장비 유형.
            /// </summary>
            public int nDVRType;

            /// <summary>
            /// 장비 채널 수.
            /// </summary>
            public int nChanNum;

            /// <summary>
            /// 로그인 시간 제한 여부 또는 제한 시간.
            /// </summary>
            public byte byLimitLoginTime;

            /// <summary>
            /// 비밀번호 오류 후 남은 로그인 횟수.
            /// </summary>
            public byte byLeftLogTimes;

            /// <summary>
            /// 바이트 정렬용 예약 영역.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byReserved;

            /// <summary>
            /// 계정 잠금 해제까지 남은 시간.
            /// </summary>
            public int nLockLeftTime;

            /// <summary>
            /// 예약 영역.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] byReserved2;

            /// <summary>
            /// 네이티브 함수 호출에 사용할 초기화된 구조체를 생성한다.
            /// </summary>
            public static NET_DEVICEINFO_Ex Create()
            {
                return new NET_DEVICEINFO_Ex
                {
                    sSerialNumber = new byte[64],
                    byReserved = new byte[2],
                    byReserved2 = new byte[24]
                };
            }
        }

        #endregion

        #region SDK Initialization

        /// <summary>
        /// Dahua NetSDK를 초기화한다.
        /// SDK의 다른 함수를 호출하기 전에 반드시 한 번 호출해야 한다.
        /// </summary>
        /// <param name="disconnectCallback">장비 연결 끊김 콜백.</param>
        /// <param name="userData">사용자 데이터 포인터.</param>
        /// <returns>초기화 성공 여부.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_Init(
            fDisConnect disconnectCallback,
            IntPtr userData);

        /// <summary>
        /// 장비 연결 복구 시 호출할 자동 재연결 콜백을 설정한다.
        /// </summary>
        /// <param name="reconnectCallback">장비 연결 복구 콜백.</param>
        /// <param name="userData">사용자 데이터 포인터.</param>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern void CLIENT_SetAutoReconnect(
            fHaveReConnect reconnectCallback,
            IntPtr userData);

        /// <summary>
        /// 로그인 접속 대기 시간과 재시도 횟수를 설정한다.
        /// </summary>
        /// <param name="waitTime">접속 대기 시간. 단위는 밀리초.</param>
        /// <param name="tryTimes">재시도 횟수.</param>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern void CLIENT_SetConnectTime(
            int waitTime,
            int tryTimes);

        /// <summary>
        /// Dahua NetSDK에서 사용하는 리소스를 정리한다.
        /// </summary>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern void CLIENT_Cleanup();

        #endregion

        #region Login

        /// <summary>
        /// Dahua NVR에 로그인한다.
        /// </summary>
        /// <param name="dvrIp">NVR IP 또는 도메인.</param>
        /// <param name="dvrPort">NVR 접속 포트.</param>
        /// <param name="userName">NVR 사용자 ID.</param>
        /// <param name="password">NVR 비밀번호.</param>
        /// <param name="loginType">로그인 방식.</param>
        /// <param name="capParam">추가 로그인 정보 포인터.</param>
        /// <param name="deviceInfo">로그인 성공 시 반환되는 장비 정보.</param>
        /// <param name="error">로그인 실패 시 반환되는 오류 코드.</param>
        /// <returns>로그인 핸들. 실패 시 IntPtr.Zero.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern IntPtr CLIENT_LoginEx2(
            [MarshalAs(UnmanagedType.LPStr)] string dvrIp,
            ushort dvrPort,
            [MarshalAs(UnmanagedType.LPStr)] string userName,
            [MarshalAs(UnmanagedType.LPStr)] string password,
            EM_LOGIN_SPAC_CAP_TYPE loginType,
            IntPtr capParam,
            ref NET_DEVICEINFO_Ex deviceInfo,
            ref int error);

        /// <summary>
        /// Dahua NVR에서 로그아웃한다.
        /// </summary>
        /// <param name="loginHandle">로그인 성공 시 반환된 핸들.</param>
        /// <returns>로그아웃 성공 여부.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_Logout(IntPtr loginHandle);

        /// <summary>
        /// Dahua NetSDK에서 마지막으로 발생한 오류 코드를 반환한다.
        /// </summary>
        /// <returns>Dahua NetSDK 오류 코드.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern uint CLIENT_GetLastError();

        #endregion
    }
}