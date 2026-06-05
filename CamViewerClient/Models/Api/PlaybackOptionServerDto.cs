namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버에 저장할 캠뷰어 재생 옵션 DTO이다.
    /// </summary>
    public sealed class PlaybackOptionServerDto
    {
        /// <summary>
        /// 영상검색일시 이전 조회 시간.
        /// </summary>
        public int BeforeSeconds { get; set; }

        /// <summary>
        /// 거래완료 시각 이후 보정 시간.
        /// </summary>
        public int AfterCompleteSeconds { get; set; }
    }
}
