using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// NVR Provider가 지원하는 선택 기능을 나타낸다.
    /// CamViewer는 이 정보를 기준으로 재생 버튼의 활성화 여부를 결정한다.
    /// </summary>
    public sealed class ProviderCapabilities
    {
        /// <summary>
        /// 영상 출력 방식.
        /// </summary>
        public NvrRenderMode RenderMode { get; set; }

        public bool CanPause { get; set; }

        public bool CanResume { get; set; }

        public bool CanSeek { get; set; }

        public bool CanPlayByRange { get; set; }

        public bool CanSnapshot { get; set; }

        public bool CanTestConnection { get; set; }

        public bool CanQueryRecordExists { get; set; }

        /// <summary>
        /// Provider가 현재 재생 중인 영상 시각을 제공할 수 있는지 여부.
        /// </summary>
        public bool CanGetPlaybackPosition { get; set; }

        /// <summary>
        /// 재생속도 변경 지원 여부.
        /// 예: 0.5배속, 1배속, 2배속, 4배속, 8배속.
        /// </summary>
        public bool CanChangeSpeed { get; set; }

        /// <summary>
        /// 채널 영상 원본 정보 조회 지원 여부.
        /// 예: 영상 너비, 높이, 원본 비율 계산용 정보.
        /// </summary>
        public bool CanGetVideoSourceInfo { get; set; }

    }
}