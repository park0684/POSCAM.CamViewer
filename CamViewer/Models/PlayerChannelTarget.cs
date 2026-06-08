using System;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;

namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView에서 재생할 단일 화면 채널 정보를 나타낸다.
    ///
    /// 예:
    /// - 좌측 화면: NVR 1 / 채널 3 / pnlLeftVideo.Handle
    /// - 우측 화면: NVR 1 / 채널 4 / pnlRightVideo.Handle
    /// </summary>
    public sealed class PlayerChannelTarget
    {
        /// <summary>
        /// 화면 위치.
        /// </summary>
        public ScreenPosition ScreenPosition { get; set; }

        /// <summary>
        /// 연결된 NVR 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR 채널 번호.
        /// </summary>
        public int ChannelNo { get; set; }

        /// <summary>
        /// 영상을 출력할 WinForms Panel Handle.
        /// </summary>
        public IntPtr OutputHandle { get; set; }

        /// <summary>
        /// 해당 채널이 연결된 NVR 설정.
        /// 이후 NVR Provider 재생 요청을 만들 때 사용한다.
        /// </summary>
        public NvrConfig NvrConfig { get; set; }

        /// <summary>
        /// 해당 채널의 재생시간 보정 초.
        /// 
        /// CCTV와 POS 화면의 녹화 지연 차이를 보정할 때 사용한다.
        /// 현재 단계에서는 기본값 0으로 사용한다.
        /// </summary>
        public int TimeOffsetSeconds { get; set; }
    }
}