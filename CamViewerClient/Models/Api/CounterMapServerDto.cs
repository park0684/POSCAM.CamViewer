namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버에 저장할 계산대 채널 매핑 DTO이다.
    /// </summary>
    public sealed class CounterMapServerDto
    {
        /// <summary>
        /// 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 연결된 NVR번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR 채널번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// 스크린위치.
        /// 0 = 좌측, 1 = 우측.
        /// </summary>
        public int ScreenPosition { get; set; }
    }
}
