namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 업로드 API 요청 DTO이다.
    /// </summary>
    public sealed class ViewerConfigUploadRequest
    {
        /// <summary>
        /// 업로드 대상 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 클라이언트가 기준으로 삼은 서버 설정 버전.
        /// 서버에서 충돌 검증이 필요할 때 사용한다.
        /// </summary>
        public long BaseConfigVersion { get; set; }

        /// <summary>
        /// 업로드할 설정 데이터.
        /// </summary>
        public ViewerConfigServerDto Config { get; set; }
    }
}
