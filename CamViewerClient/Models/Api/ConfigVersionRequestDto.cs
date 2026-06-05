namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 서버 설정 버전 확인 요청 DTO이다.
    ///
    /// AuthServer의 ConfigVersionRequest와 속성명이 다르면
    /// 이 DTO를 서버 DTO에 맞게 조정한다.
    /// </summary>
    public sealed class ConfigVersionRequestDto
    {
        /// <summary>
        /// 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 로컬 설정 버전.
        /// 서버가 최신 여부를 판단할 때 사용한다.
        /// </summary>
        public long LocalConfigVersion { get; set; }

        /// <summary>
        /// 매장 코드.
        /// 토큰 기반 검증이 기본이지만, 서버 DTO가 요구하면 사용한다.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 캠뷰어 장비 코드.
        /// 서버 DTO가 요구하면 사용한다.
        /// </summary>
        public int DeviceCode { get; set; }
    }
}
