namespace CamViewerClient.Api
{
    /// <summary>
    /// 인증서버 API 호출에 필요한 기본 설정이다.
    /// </summary>
    public sealed class ApiClientOptions
    {
        /// <summary>
        /// 인증서버 기본 주소.
        /// 예: https://accli.poscam.co.kr/
        /// </summary>
        public string BaseAddress { get; set; }

        /// <summary>
        /// 서버 설정 버전 확인 API 경로.
        /// AuthServer ConfigController: POST api/config/version
        /// </summary>
        public string ConfigVersionEndpoint { get; set; }

        /// <summary>
        /// 최신 설정 다운로드 API 경로.
        /// AuthServer ConfigController: POST api/config/latest
        /// </summary>
        public string ConfigLatestEndpoint { get; set; }

        /// <summary>
        /// 설정 동기화 API 경로.
        /// AuthServer ConfigController: POST api/config/sync
        /// </summary>
        public string ConfigSyncEndpoint { get; set; }

        /// <summary>
        /// API 호출 제한 시간. 단위는 초.
        /// </summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// 캠뷰어 최초 로그인 API 경로.
        /// </summary>
        public string ViewerLoginEndpoint { get; set; }

        /// <summary>
        /// 캠뷰어 토큰 검증 API 경로.
        /// </summary>
        public string ViewerVerifyTokenEndpoint { get; set; }

        /// <summary>
        /// 캠뷰어 등록 장비 목록 API 경로.
        /// </summary>
        public string ViewerDevicesEndpoint { get; set; }

        /// <summary>
        /// 캠뷰어 장비 등록해제 API 경로.
        /// </summary>
        public string ViewerDeviceReleaseEndpoint { get; set; }

        /// <summary>
        /// 기본 API 옵션을 생성한다.
        /// </summary>
        public static ApiClientOptions CreateDefault()
        {
            return new ApiClientOptions
            {
                BaseAddress = "https://accli.poscam.co.kr/",
                ConfigVersionEndpoint = "api/config/version",
                ConfigLatestEndpoint = "api/config/latest",
                ConfigSyncEndpoint = "api/config/sync",
                ViewerLoginEndpoint = "api/viewer/login",
                ViewerVerifyTokenEndpoint = "api/viewer/verify-token",
                ViewerDevicesEndpoint = "api/viewer/devices",
                ViewerDeviceReleaseEndpoint = "api/viewer/devices/release",
                TimeoutSeconds = 15
            };
        }
    }
}
