namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 최초 로그인 응답 DTO이다.
    /// </summary>
    public sealed class ViewerLoginResponseDto
    {
        /// <summary>
        /// 매장 내부 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 등록된 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 로그인 성공 여부.
        /// </summary>
        public bool LoginSuccess { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// 현재 서버 응답은 빈 문자열이 올 수 있으므로 string으로 받는다.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 인증서버에서 발급한 토큰 정보.
        /// </summary>
        public AuthTokenDto Token { get; set; }
    }
}