namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버 Config API에서 사용하는 NVR 설정 DTO이다.
    ///
    /// AuthServer의 NvrConfigDto와 속성명을 맞춘다.
    /// 현재 서버 구조는 매장당 단일 NVR 설정 기준이다.
    /// </summary>
    public sealed class NvrConfigDto
    {
        /// <summary>
        /// NVR 접속 ID.
        /// </summary>
        public string NvrId { get; set; }

        /// <summary>
        /// NVR 접속 비밀번호.
        /// </summary>
        public string NvrPassword { get; set; }

        /// <summary>
        /// NVR IP 또는 도메인.
        /// </summary>
        public string NvrIp { get; set; }

        /// <summary>
        /// NVR 접속 포트.
        /// </summary>
        public int NvrPort { get; set; }

        /// <summary>
        /// NVR 채널 수.
        /// </summary>
        public int? NvrChannels { get; set; }

        /// <summary>
        /// NVR 설정 버전.
        /// </summary>
        public string NvrVersion { get; set; }
    }
}