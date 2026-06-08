using System;

namespace CamViewer.Models
{
    /// <summary>
    /// 하나의 재생 채널에 대한 현재 재생시간 상태이다.
    /// </summary>
    public sealed class PlaybackChannelTimeStatus
    {
        /// <summary>
        /// 화면 위치.
        /// 예: Left, Right
        /// </summary>
        public string ScreenPosition { get; set; }

        /// <summary>
        /// NVR 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// 채널 번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// Provider에서 실제 재생시간을 조회했는지 여부.
        /// false이면 추정 재생시간이다.
        /// </summary>
        public bool IsProviderTime { get; set; }

        /// <summary>
        /// 현재 재생시간.
        /// </summary>
        public DateTime? PlaybackTime { get; set; }

        /// <summary>
        /// 채널별 보정 초.
        /// 현재 단계에서는 기본 0초로 사용한다.
        /// </summary>
        public int TimeOffsetSeconds { get; set; }
    }
}