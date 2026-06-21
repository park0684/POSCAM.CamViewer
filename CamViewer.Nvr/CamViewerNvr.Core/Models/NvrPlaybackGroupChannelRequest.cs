using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 다중채널 재생 그룹에 포함할 하나의 채널 정보.
    ///
    /// 이 모델은 재생 대상을 전달할 뿐이며,
    /// 실제 재생 또는 시간 보정은 제조사 엔진이 처리한다.
    /// </summary>
    public sealed class NvrPlaybackGroupChannelRequest
    {
        /// <summary>
        /// NVR 채널번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// 화면 위치.
        /// 0 = 좌측, 1 = 우측.
        /// </summary>
        public int ScreenPosition { get; set; }

        /// <summary>
        /// 영상을 출력할 Windows Handle.
        /// </summary>
        public IntPtr RenderTargetHandle { get; set; }

        /// <summary>
        /// 해당 채널에 설정된 영상시간 보정값.
        ///
        /// 공통 서비스는 이 값을 계산하거나 적용하지 않고,
        /// 제조사 재생 엔진에 그대로 전달한다.
        /// </summary>
        public int TimeOffsetSeconds { get; set; }
    }
}