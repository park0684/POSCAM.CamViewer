namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 토큰 검증 응답 DTO이다.
    /// </summary>
    public sealed class ViewerTokenVerifyResponseDto
    {
        /// <summary>
        /// 토큰 유효 여부.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 매장 내부 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// 현재 서버 응답은 빈 문자열이 올 수 있으므로 string으로 받는다.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 갱신된 인증 토큰 정보.
        /// </summary>
        public AuthTokenDto Token { get; set; }
    }
}