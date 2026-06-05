namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 토큰 실행 인증 응답 DTO이다.
    /// </summary>
    public sealed class ViewerTokenVerifyResponseDto
    {
        /// <summary>
        /// 토큰 유효 여부.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 갱신된 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }
    }
}