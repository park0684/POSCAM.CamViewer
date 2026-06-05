namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 토큰 실행 인증 요청 DTO이다.
    ///
    /// AuthServer endpoint:
    /// POST api/viewer/verify-token
    /// </summary>
    public sealed class ViewerTokenVerifyRequestDto
    {
        /// <summary>
        /// 로컬에 저장된 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 현재 PC의 HWID.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// 캠뷰어 프로그램 버전.
        /// </summary>
        public string ProgramVersion { get; set; }
    }
}