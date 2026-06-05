namespace CamViewer.Models.ViewModels
{
    /// <summary>
    /// 설정 화면의 계산대 등록 목록에 표시할 데이터 모델이다.
    /// </summary>
    public sealed class CounterMapListItem
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
        /// 스크린위치 내부값.
        /// 0 = 좌측, 1 = 우측.
        /// </summary>
        public int ScreenPosition { get; set; }

        /// <summary>
        /// 사용자에게 표시할 스크린위치 문자열.
        /// </summary>
        public string ScreenPositionText { get; set; }
    }
}