using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 하나의 제조사 재생 그룹에 포함할 채널 요청이다.
    /// </summary>
    public sealed class NvrPlaybackGroupChannelRequest
    {
        public int ChannelNo { get; set; }

        public int ScreenPosition { get; set; }

        public IntPtr RenderTargetHandle { get; set; }

        /// <summary>
        /// 공통 영상시간을 이 채널의 NVR 원본시간으로 바꿀 때
        /// 더할 보정값이다.
        /// </summary>
        public int TimeOffsetSeconds { get; set; }
    }
}
