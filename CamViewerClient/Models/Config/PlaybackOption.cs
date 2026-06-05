namespace CamViewerClient.Models.Config
{
    /// <summary>
    /// 영상검색일시를 기준으로 재생 구간을 계산하기 위한 옵션을 나타낸다.
    /// </summary>
    public sealed class PlaybackOption
    {
        /// <summary>
        /// 기본 재생 옵션을 초기화한다.
        /// </summary>
        public PlaybackOption()
        {
            BeforeSeconds = 30;
            AfterCompleteSeconds = 3;
        }

        /// <summary>
        /// 영상검색일시 이전에 추가로 조회할 시간.
        /// 기본값은 30초이다.
        /// </summary>
        public int BeforeSeconds { get; set; }

        /// <summary>
        /// 거래완료 시각 이후에 추가로 조회할 보정 시간.
        /// 기본값은 3초이다.
        /// </summary>
        public int AfterCompleteSeconds { get; set; }
    }
}