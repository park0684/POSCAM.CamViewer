namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 다운로드 API 응답 DTO이다.
    /// </summary>
    public sealed class ViewerConfigDownloadResponse
    {
        /// <summary>
        /// 다운로드 가능한 설정이 있는지 여부.
        /// </summary>
        public bool HasConfig { get; set; }

        /// <summary>
        /// 서버 설정 데이터.
        /// </summary>
        public ViewerConfigServerDto Config { get; set; }

        /// <summary>
        /// 응답 메시지.
        /// </summary>
        public string Message { get; set; }
    }
}
