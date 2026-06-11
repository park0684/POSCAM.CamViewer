namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// AuthServer의 캠뷰어 설정 버전 확인 요청 DTO이다.
    ///
    /// AuthServer ConfigVersionRequest 기준:
    /// - Token
    /// - Hwid
    /// - LocalConfigVersion
    /// - ProgramVersion
    ///
    /// 주의:
    /// - StoreCode, DeviceCode는 요청하지 않는다.
    /// - 서버는 토큰 payload에서 매장과 장비 정보를 확인한다.
    /// </summary>
    public sealed class ConfigVersionRequestDto
    {
        /// <summary>
        /// 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 현재 캠뷰어 장비의 HWID.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// 현재 로컬 설정 버전.
        /// 로컬 설정이 없으면 빈 문자열로 전달한다.
        /// </summary>
        public string LocalConfigVersion { get; set; }

        /// <summary>
        /// 캠뷰어 프로그램 버전.
        /// </summary>
        public string ProgramVersion { get; set; }

        /// <summary>
        /// ConfigVersionRequestDto를 초기화한다.
        /// </summary>
        public ConfigVersionRequestDto()
        {
            Token = string.Empty;
            Hwid = string.Empty;
            LocalConfigVersion = string.Empty;
            ProgramVersion = string.Empty;
        }
    }
}