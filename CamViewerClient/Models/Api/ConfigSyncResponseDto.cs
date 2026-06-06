namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 동기화 응답 DTO이다.
    /// </summary>
    public sealed class ConfigSyncResponseDto
    {
        /// <summary>
        /// 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// AuthServer는 문자열 버전을 반환한다.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 서버에 저장된 채널 매핑 수.
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// 동기화 성공 여부.
        /// </summary>
        public bool Synced { get; set; }
    }
}