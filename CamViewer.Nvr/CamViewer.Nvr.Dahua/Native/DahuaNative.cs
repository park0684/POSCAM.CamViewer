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

        /// <summary>
        /// Dahua SDK 재생 진행률 콜백.
        /// 현재 단계에서는 콜백을 사용하지 않으므로 함수 정의만 둔다.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void fDownLoadPosCallBack(
            IntPtr playHandle,
            uint totalSize,
            uint downloadSize,
            IntPtr userData);

        /// <summary>
        /// Dahua SDK 재생 데이터 콜백.
        /// 현재 단계에서는 SDK Direct Render를 사용하므로 데이터 콜백은 사용하지 않는다.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int fDataCallBack(
            IntPtr playHandle,
            uint dataType,
            IntPtr buffer,
            uint bufferSize,
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

        /// <summary>
        /// Dahua SDK에서 사용하는 시간 구조체이다.
        /// 
        /// Dahua NetSDK의 NET_TIME 구조와 매칭된다.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct NET_TIME
        {
            /// <summary>
            /// 연도.
            /// </summary>
            public uint dwYear;

            /// <summary>
            /// 월.
            /// </summary>
            public uint dwMonth;

            /// <summary>
            /// 일.
            /// </summary>
            public uint dwDay;

            /// <summary>
            /// 시.
            /// </summary>
            public uint dwHour;

            /// <summary>
            /// 분.
            /// </summary>
            public uint dwMinute;

            /// <summary>
            /// 초.
            /// </summary>
            public uint dwSecond;

            /// <summary>
            /// DateTime을 Dahua NET_TIME으로 변환한다.
            /// </summary>
            public static NET_TIME FromDateTime(DateTime value)
            {
                return new NET_TIME
                {
                    dwYear = Convert.ToUInt32(value.Year),
                    dwMonth = Convert.ToUInt32(value.Month),
                    dwDay = Convert.ToUInt32(value.Day),
                    dwHour = Convert.ToUInt32(value.Hour),
                    dwMinute = Convert.ToUInt32(value.Minute),
                    dwSecond = Convert.ToUInt32(value.Second)
                };
            }

            /// <summary>
            /// Dahua NET_TIME 값을 DateTime으로 변환한다.
            /// </summary>
            public DateTime ToDateTime()
            {
                if (dwYear < 1
                    || dwMonth < 1 || dwMonth > 12
                    || dwDay < 1 || dwDay > 31
                    || dwHour > 23
                    || dwMinute > 59
                    || dwSecond > 59)
                {
                    return DateTime.MinValue;
                }

                return new DateTime(
                    Convert.ToInt32(dwYear),
                    Convert.ToInt32(dwMonth),
                    Convert.ToInt32(dwDay),
                    Convert.ToInt32(dwHour),
                    Convert.ToInt32(dwMinute),
                    Convert.ToInt32(dwSecond));
            }
        }

        //private const int MAX_VIDEO_STREAM_NUM = 3;

        /// <summary>
        /// Dahua 영상 인코딩 옵션 중 해상도 확인에 필요한 최소 필드이다.
        /// 
        /// 주의:
        /// 실제 SDK의 CFG_VIDEOENC_OPT 전체 구조체와 레이아웃이 다르면 파싱 결과가 틀릴 수 있다.
        /// 반드시 사용 중인 Dahua SDK C# 예제의 CFG_VIDEOENC_OPT 정의와 비교해야 한다.
        /// 
        /// 다후아 SDKCS로 대체
        /// </summary>
        //[StructLayout(
        //    LayoutKind.Sequential,
        //    Pack = 1,
        //    CharSet = CharSet.Ansi)]
        //internal struct CFG_VIDEOENC_OPT
        //{
        //    /// <summary>
        //    /// 너비 값 유효 여부.
        //    /// </summary>
        //    [MarshalAs(UnmanagedType.Bool)]
        //    public bool abWidth;

        //    /// <summary>
        //    /// 영상 너비.
        //    /// </summary>
        //    public int nWidth;

        //    /// <summary>
        //    /// 높이 값 유효 여부.
        //    /// </summary>
        //    [MarshalAs(UnmanagedType.Bool)]
        //    public bool abHeight;

        //    /// <summary>
        //    /// 영상 높이.
        //    /// </summary>
        //    public int nHeight;
        //}

        /// <summary>
        /// Dahua 채널 인코딩 설정.
        /// 
        /// 주의:
        /// 이 구조체는 해상도 조회를 위한 최소 구조체 예시이다.
        /// SDK 실제 정의와 다르면 반드시 SDK 예제 기준으로 교체해야 한다.
        /// 
        /// 다후아 SDKCS로 대체
        /// </summary>
        //[StructLayout(
        //    LayoutKind.Sequential,
        //    Pack = 1,
        //    CharSet = CharSet.Ansi)]
        //internal struct CFG_ENCODE_INFO
        //{
        //    /// <summary>
        //    /// Main Stream 인코딩 설정 목록.
        //    /// 일반적으로 0번 요소를 기본 Main Stream으로 사용한다.
        //    /// </summary>
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_VIDEO_STREAM_NUM)]
        //    public CFG_VIDEOENC_OPT[] stuMainStream;

        //    /// <summary>
        //    /// Sub Stream 인코딩 설정 목록.
        //    /// </summary>
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_VIDEO_STREAM_NUM)]
        //    public CFG_VIDEOENC_OPT[] stuExtraStream;

        //    /// <summary>
        //    /// 구조체 배열을 초기화한다.
        //    /// </summary>
        //    public static CFG_ENCODE_INFO Create()
        //    {
        //        return new CFG_ENCODE_INFO
        //        {
        //            stuMainStream = new CFG_VIDEOENC_OPT[MAX_VIDEO_STREAM_NUM],
        //            stuExtraStream = new CFG_VIDEOENC_OPT[MAX_VIDEO_STREAM_NUM]
        //        };
        //    }
        //}

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

        #region Playback

        /// <summary>
        /// 지정 시간 구간의 녹화 영상을 재생한다.
        /// 
        /// hWnd에 WinForms Panel Handle을 전달하면 Dahua SDK가 해당 영역에 직접 렌더링한다.
        /// </summary>
        /// <param name="loginHandle">로그인 핸들.</param>
        /// <param name="channelId">채널 번호.</param>
        /// <param name="startTime">재생 시작 시각.</param>
        /// <param name="stopTime">재생 종료 시각.</param>
        /// <param name="renderHandle">영상 출력 윈도우 핸들.</param>
        /// <param name="positionCallback">재생 진행 콜백.</param>
        /// <param name="positionUserData">재생 진행 콜백 사용자 데이터.</param>
        /// <param name="dataCallback">재생 데이터 콜백.</param>
        /// <param name="dataUserData">재생 데이터 콜백 사용자 데이터.</param>
        /// <returns>재생 핸들. 실패 시 IntPtr.Zero.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern IntPtr CLIENT_PlayBackByTimeEx(
            IntPtr loginHandle,
            int channelId,
            ref NET_TIME startTime,
            ref NET_TIME stopTime,
            IntPtr renderHandle,
            fDownLoadPosCallBack positionCallback,
            IntPtr positionUserData,
            fDataCallBack dataCallback,
            IntPtr dataUserData);

        /// <summary>
        /// 재생을 중지한다.
        /// </summary>
        /// <param name="playHandle">재생 핸들.</param>
        /// <returns>성공 여부.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_StopPlayBack(
            IntPtr playHandle);

        /// <summary>
        /// 재생을 일시정지하거나 재개한다.
        /// </summary>
        /// <param name="playHandle">재생 핸들.</param>
        /// <param name="pause">true면 일시정지, false면 재개.</param>
        /// <returns>성공 여부.</returns>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_PausePlayBack(
            IntPtr playHandle,
            [MarshalAs(UnmanagedType.Bool)] bool pause);

        /// <summary>
        /// 재생 속도를 빠르게 한다.
        /// SDK 내부 기준으로 호출할 때마다 한 단계씩 빨라진다.
        /// </summary>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_FastPlayBack(
            IntPtr playHandle);

        /// <summary>
        /// 재생 속도를 느리게 한다.
        /// SDK 내부 기준으로 호출할 때마다 한 단계씩 느려진다.
        /// </summary>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_SlowPlayBack(
            IntPtr playHandle);

        /// <summary>
        /// 재생 속도를 일반 속도로 되돌린다.
        /// </summary>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_NormalPlayBack(
            IntPtr playHandle);

        /// <summary>
        /// Dahua 재생 핸들의 현재 OSD 재생 시간을 조회한다.
        /// </summary>
        [DllImport(
            DllName,
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CLIENT_GetPlayBackOsdTime(
            IntPtr playHandle,
            ref NET_TIME osdTime,
            ref NET_TIME startTime,
            ref NET_TIME endTime);

        /// <summary>
        /// Dahua SDK 재생 제어 타입.
        /// 
        /// 주의:
        /// 이 값은 Dahua SDK의 PlayBackType enum 값과 맞아야 한다.
        /// NetSDKCS의 PlayBackType 값과 다를 경우 반드시 SDK enum 값에 맞춰 수정해야 한다.
        /// 
        /// NetSDKCS로 대체 2026-06-08
        /// </summary>
        //internal enum DahuaPlaybackControlType
        //{
        //    Play = 0,
        //    Stop = 1,
        //    Pause = 2,
        //    Fast = 3,
        //    Slow = 4,
        //    Normal = 5
        //}

        /// <summary>
        /// Dahua SDK 재생 제어 함수.
        /// Fast, Slow, Normal, Pause, Play, Stop 등의 제어를 처리한다.
        /// 
        /// NetSDKCS로 대체 2026-06-08
        /// </summary>
        //[DllImport(
        //    DllName,
        //    CallingConvention = CallingConvention.StdCall,
        //    CharSet = CharSet.Ansi)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //internal static extern bool CLIENT_PlayBackControl(
        //    IntPtr playHandle,
        //    DahuaPlaybackControlType controlType,
        //    uint inValue,
        //    IntPtr outValue);

        #endregion

        #region Config

        /// <summary>
        /// Dahua New Config 인터페이스로 장비 설정을 조회한다.
        /// 예: Encode 설정.
        /// 
        /// 다후아 SDKCS로 대체 2026-06-08
        /// </summary>
        //[DllImport(
        //    DllName,
        //    CallingConvention = CallingConvention.StdCall,
        //    CharSet = CharSet.Ansi,
        //    EntryPoint = "CLIENT_GetNewDevConfig")]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //internal static extern bool CLIENT_GetNewDevConfig(
        //    IntPtr loginHandle,
        //    [MarshalAs(UnmanagedType.LPStr)] string command,
        //    int channelNo,
        //    IntPtr outBuffer,
        //    int outBufferSize,
        //    ref int error,
        //    int waitTime);

        /// <summary>
        /// Dahua New Config 조회 결과를 지정 구조체로 파싱한다.
        /// 
        /// 다후아 SdkCS로 대체 2026-06-08
        /// </summary>
        //[DllImport(
        //    DllName,
        //    CallingConvention = CallingConvention.StdCall,
        //    CharSet = CharSet.Ansi)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //internal static extern bool CLIENT_ParseData(
        //    [MarshalAs(UnmanagedType.LPStr)] string command,
        //    IntPtr inBuffer,
        //    ref CFG_ENCODE_INFO config,
        //    int configSize,
        //    IntPtr reserved);

        #endregion
    }

}