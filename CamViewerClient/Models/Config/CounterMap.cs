using CamViewerClient.Enums;

namespace CamViewerClient.Models.Config
{
    /// <summary>
    /// 계산대번호와 NVR 채널, 스크린위치의 연결 정보를 나타낸다.
    /// </summary>
    public sealed class CounterMap
    {
        /// <summary>
        /// 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 연결할 NVR번호.
        /// NvrConfig.NvrNo와 연결된다.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR의 실제 채널번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// 영상을 표시할 스크린위치.
        /// 내부값은 좌측 0, 우측 1이다.
        /// </summary>
        public ScreenPosition ScreenPosition { get; set; }

        public int VideoWidth { get; set; }
        public int VideoHeight { get; set; }
    }
}