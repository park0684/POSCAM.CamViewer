namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// AuthServer의 최신 캠뷰어 설정 다운로드 요청 DTO이다.
    ///
    /// AuthServer ConfigLatestRequest 기준:
    /// - Token: 캠뷰어 로그인 또는 토큰 검증으로 발급받은 토큰
    /// - Hwid: 현재 캠뷰어 장비의 HWID
    /// - LocalConfigVersion: 현재 로컬 설정 버전
    /// - ProgramVersion: 캠뷰어 프로그램 버전
    ///
    /// 주의:
    /// - StoreCode, DeviceCode는 요청 Body로 보내지 않는다.
    /// - AuthServer는 Token payload에서 StoreCode, DeviceCode를 추출한다.
    /// </summary>
    public sealed class ConfigLatestRequestDto
    {
        /// <summary>
        /// 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 현재 캠뷰어 장비의 HWID.
        /// 토큰에 포함된 HWID와 일치해야 한다.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// 현재 로컬 설정 버전.
        /// 로컬 설정이 없으면 빈 문자열로 전달한다.
        /// </summary>
        public string LocalConfigVersion { get; set; }

        /// <summary>
        /// 캠뷰어 프로그램 버전.
        /// 서버 로그 기록 및 버전 추적에 사용한다.
        /// </summary>
        public string ProgramVersion { get; set; }

        /// <summary>
        /// ConfigLatestRequestDto를 초기화한다.
        /// </summary>
        public ConfigLatestRequestDto()
        {
            Token = string.Empty;
            Hwid = string.Empty;
            LocalConfigVersion = string.Empty;
            ProgramVersion = string.Empty;
        }
    }
}