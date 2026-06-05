using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// NVR Provider의 식별 정보와 기본 특성을 나타낸다.
    /// </summary>
    public sealed class ProviderMetadata
    {
        /// <summary>
        /// Provider를 선택하기 위한 고유 키.
        /// 예: DAHUA_SDK, TPLINK_API, GENERIC_RTSP
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// 설정 화면 또는 로그에 표시할 Provider 이름.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// NVR 제조사명.
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// NVR 접속 방식.
        /// </summary>
        public NvrConnectionType ConnectionType { get; set; }

        /// <summary>
        /// Provider 버전.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Provider의 영상 출력 방식.
        /// </summary>
        public NvrRenderMode RenderMode { get; set; }

        /// <summary>
        /// Provider가 요구하는 프로세스 아키텍처.
        /// 초기 CamViewer는 x64를 사용한다.
        /// </summary>
        public string RequiredArchitecture { get; set; }
    }
}