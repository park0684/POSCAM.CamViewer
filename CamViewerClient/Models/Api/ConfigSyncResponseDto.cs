namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 동기화 응답 DTO이다.
    ///
    /// AuthServer의 ConfigSyncResponse와 속성명이 다르면
    /// 이 DTO를 서버 DTO에 맞게 조정한다.
    /// </summary>
    public sealed class ConfigSyncResponseDto
    {
        /// <summary>
        /// 동기화 후 서버 설정 버전.
        /// </summary>
        public long ConfigVersion { get; set; }

        /// <summary>
        /// 동기화 성공 여부.
        /// </summary>
        public bool Synced { get; set; }

        /// <summary>
        /// 응답 메시지.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 서버에서 최신 설정을 함께 반환하는 경우 사용한다.
        /// </summary>
        public ViewerConfigServerDto Config { get; set; }
    }
}
