namespace CamViewer.Models.ViewModels
{
    /// <summary>
    /// NVR 등록/수정 화면의 제조사 선택 목록에 표시할 모델이다.
    ///
    /// 하나의 제조사에는 하나의 접속방식과 하나의 ProviderKey가 연결된다.
    /// </summary>
    public sealed class VendorOptionItem
    {
        /// <summary>
        /// 사용자에게 표시할 제조사명.
        /// 예: Dahua, TP-Link, Generic RTSP
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// 제조사에 고정된 접속방식.
        /// 예: SDK, API, RTSP
        /// </summary>
        public string ConnectionType { get; set; }

        /// <summary>
        /// 제조사에 고정된 NVR Provider 식별 키.
        /// 예: DAHUA_SDK, TPLINK_API, GENERIC_RTSP
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// 제조사 선택 ComboBox에 표시할 문자열.
        /// </summary>
        public string DisplayText { get; set; }
    }
}