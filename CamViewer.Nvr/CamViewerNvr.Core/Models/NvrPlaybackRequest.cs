using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 하나의 NVR 채널을 하나의 화면 영역에 재생하기 위한 요청 정보.
    /// 좌측과 우측 영상은 각각 별도의 요청으로 생성한다.
    /// </summary>
    public sealed class NvrPlaybackRequest
    {
        /// <summary>
        /// 조회 대상 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 조회 대상 NVR번호.
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

        /// <summary>
        /// 사용자가 입력하거나 외부 POS에서 전달한 영상검색일시.
        /// 거래완료 시각으로 본다.
        /// </summary>
        public DateTime SearchDateTime { get; set; }

        /// <summary>
        /// 실제 재생 시작 시각.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 실제 재생 종료 시각.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Direct Render 방식에서 영상을 출력할 Windows Handle.
        /// </summary>
        public IntPtr RenderTargetHandle { get; set; }

        /// <summary>
        /// 재생 준비 후 자동으로 재생을 시작할지 여부.
        /// </summary>
        public bool AutoPlay { get; set; }
    }
}