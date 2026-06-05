namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 최초 로그인 응답 DTO이다.
    /// </summary>
    public sealed class ViewerLoginResponseDto
    {
        /// <summary>
        /// 로그인 성공 여부.
        /// </summary>
        public bool LoginSuccess { get; set; }

        /// <summary>
        /// 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 등록된 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 캠뷰어 실행 인증 토큰.
        /// </summary>
        public string Token { get; set; }
    }
}