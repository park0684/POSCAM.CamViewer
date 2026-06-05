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
    }
}