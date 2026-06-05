namespace CamViewer.Models.ViewModels
{
    /// <summary>
    /// 계산대 등록/수정 팝업의 NVR 선택 목록에 표시할 데이터 모델이다.
    /// </summary>
    public sealed class NvrOptionItem
    {
        /// <summary>
        /// NVR 식별 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR 전체 채널 수.
        /// 채널번호 입력 범위 검증에 사용한다.
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// 사용자에게 표시할 NVR 설명.
        /// 예: NVR 1 - Dahua / 192.168.0.100
        /// </summary>
        public string DisplayText { get; set; }
    }
}