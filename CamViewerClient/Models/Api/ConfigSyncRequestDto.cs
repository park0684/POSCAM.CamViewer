namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 동기화 요청 DTO이다.
    ///
    /// AuthServer의 ConfigSyncRequest와 속성명이 다르면
    /// 이 DTO를 서버 DTO에 맞게 조정한다.
    /// </summary>
    public sealed class ConfigSyncRequestDto
    {
        /// <summary>
        /// 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 업로드 대상 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 클라이언트가 기준으로 삼은 서버 설정 버전.
        /// </summary>
        public long BaseConfigVersion { get; set; }

        /// <summary>
        /// 업로드할 설정 데이터.
        /// </summary>
        public ViewerConfigServerDto Config { get; set; }
    }
}
