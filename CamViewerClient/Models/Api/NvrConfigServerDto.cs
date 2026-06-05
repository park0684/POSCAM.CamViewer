using System.Collections.Generic;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버에 저장할 NVR 접속 설정 DTO이다.
    ///
    /// NVR 비밀번호는 HTTPS 요청 본문으로 전달되며,
    /// 서버 저장 시에는 서버 기준 암호화 정책으로 저장해야 한다.
    /// </summary>
    public sealed class NvrConfigServerDto
    {
        /// <summary>
        /// NVR번호.
        /// 클라이언트 설정 내에서 계산대 매핑과 연결되는 식별값이다.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// 제조사명.
        /// 예: Dahua, TP-Link
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// 접속방식.
        /// 예: Sdk, Api, Rtsp, Onvif
        /// </summary>
        public string ConnectionType { get; set; }

        /// <summary>
        /// Provider 식별 키.
        /// 예: DAHUA_SDK, TPLINK_API
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// NVR IP 또는 도메인.
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
        /// 클라이언트 로컬 파일에는 암호화 저장하고, 서버 저장 시에는 서버가 별도로 암호화해야 한다.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Provider별 추가 설정값.
        /// </summary>
        public IDictionary<string, string> ProviderSettings { get; set; }

        /// <summary>
        /// NVR 설정 DTO를 초기화한다.
        /// </summary>
        public NvrConfigServerDto()
        {
            ProviderSettings = new Dictionary<string, string>();
        }
    }
}
