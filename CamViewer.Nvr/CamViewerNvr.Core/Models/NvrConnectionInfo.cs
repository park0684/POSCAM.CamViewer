using System.Collections.Generic;
using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// NVR 접속에 필요한 공통 설정 정보를 나타낸다.
    /// </summary>
    public sealed class NvrConnectionInfo
    {
        public NvrConnectionInfo()
        {
            ProviderSettings = new Dictionary<string, string>();
        }

        /// <summary>
        /// 설정 내 NVR 식별 번호.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// NVR Provider 선택에 사용하는 고유 키.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// NVR 제조사명.
        /// </summary>
        public string Vendor { get; set; }

        /// <summary>
        /// SDK, API, RTSP 등의 접속 방식.
        /// </summary>
        public NvrConnectionType ConnectionType { get; set; }

        /// <summary>
        /// NVR IP 주소 또는 도메인.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// NVR 접속 포트.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// NVR 로그인 계정.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// NVR 로그인 비밀번호.
        /// 로그에 기록하면 안 된다.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// NVR 전체 채널 수.
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// Provider별 추가 설정값.
        /// Core 모델 변경 없이 제조사별 설정을 추가하기 위해 사용한다.
        /// </summary>
        public IDictionary<string, string> ProviderSettings { get; private set; }
    }
}