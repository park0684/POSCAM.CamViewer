namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버 Config API에서 사용하는 계산대 채널 매핑 DTO이다.
    ///
    /// AuthServer에서는 PosNo, ChannelNo, Screen 값으로 채널 매핑을 저장한다.
    /// </summary>
    public sealed class ChannelConfigDto
    {
        /// <summary>
        /// 계산대번호.
        /// </summary>
        public int PosNo { get; set; }

        /// <summary>
        /// NVR 채널번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// 스크린위치.
        /// 0 = 좌측, 1 = 우측.
        /// </summary>
        public int Screen { get; set; }
    }
}