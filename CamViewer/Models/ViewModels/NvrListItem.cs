namespace CamViewer.Models.ViewModels
{
    /// <summary>
    /// 설정 화면의 NVR 목록에 표시할 데이터 모델이다.
    ///
    /// NVR 비밀번호와 Provider별 추가 설정은 목록에 표시하지 않는다.
    /// </summary>
    public sealed class NvrListItem
    {
        /// <summary>
        /// 설정 내 NVR 식별 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR 제조사명.
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// NVR 접속방식.
        /// 예: SDK, API, RTSP
        /// </summary>
        public string ConnectionType { get; set; }

        /// <summary>
        /// NVR Provider 식별 키.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// NVR IP 주소 또는 도메인.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// NVR 접속 포트.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// NVR 전체 채널 수.
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// NVR 로그인 ID.
        /// </summary>
        public string UserId { get; set; }
    }
}