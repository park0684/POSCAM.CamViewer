namespace CamViewer.Nvr.Core.Enums
{
    /// <summary>
    /// NVR과 통신하는 방식의 종류를 정의한다.
    /// </summary>
    public enum NvrConnectionType
    {
        /// <summary>
        /// 제조사 전용 SDK를 사용한다.
        /// </summary>
        Sdk = 1,

        /// <summary>
        /// HTTP 또는 제조사 전용 API를 사용한다.
        /// </summary>
        Api = 2,

        /// <summary>
        /// RTSP 재생 주소를 사용한다.
        /// </summary>
        Rtsp = 3,

        /// <summary>
        /// ONVIF 규격을 사용한다.
        /// </summary>
        Onvif = 4
    }
}