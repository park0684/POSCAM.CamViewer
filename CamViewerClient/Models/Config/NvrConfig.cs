using System.Collections.Generic;

namespace CamViewerClient.Models.Config
{
    /// <summary>
    /// 캠뷰어에서 사용할 NVR 접속 설정 정보를 나타낸다.
    /// </summary>
    public sealed class NvrConfig
    {
        /// <summary>
        /// NVR 설정을 초기화한다.
        /// </summary>
        public NvrConfig()
        {
            ProviderSettings = new Dictionary<string, string>();
        }

        /// <summary>
        /// 설정 내 NVR 식별 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR 제조사 코드 또는 명칭.
        /// 예: Dahua, TP-Link
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// NVR 접속 방식 코드.
        /// 예: SDK, API, RTSP, ONVIF
        /// </summary>
        public string ConnectionType { get; set; }

        /// <summary>
        /// NVR Provider 선택에 사용하는 고유 키.
        /// 예: DAHUA_SDK, TPLINK_API, GENERIC_RTSP
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// NVR IP 주소 또는 도메인.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// NVR 접속 포트.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// NVR 전체 채널 수.
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// NVR 로그인 ID.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// NVR 로그인 비밀번호.
        /// 로컬 설정 저장 시 반드시 암호화된 파일 안에 보관해야 한다.
        /// 로그에는 기록하지 않는다.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Provider별 추가 설정값.
        /// 제조사별 추가 옵션이 필요한 경우 사용한다.
        /// </summary>
        public IDictionary<string, string> ProviderSettings { get; private set; }
    }
}