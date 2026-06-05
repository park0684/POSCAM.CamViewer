using System;

namespace CamViewerClient.Models.Auth
{
    /// <summary>
    /// 캠뷰어 로컬 인증 토큰 정보를 나타낸다.
    ///
    /// 이 정보는 실행 파일과 동일한 위치의 viewer_token.dat 파일에
    /// 암호화되어 저장된다.
    /// </summary>
    public sealed class ViewerAuthToken
    {
        /// <summary>
        /// 인증서버에서 발급받은 캠뷰어 실행 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 토큰이 연결된 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 토큰이 연결된 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 캠뷰어 장비 이름 또는 PC 이름.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 토큰 발급 UTC 일시.
        /// </summary>
        public DateTime IssuedAtUtc { get; set; }

        /// <summary>
        /// 토큰 만료 UTC 일시.
        /// 서버 응답에 만료 시간이 없으면 null일 수 있다.
        /// </summary>
        public DateTime? ExpireAtUtc { get; set; }

        /// <summary>
        /// 마지막 정상 온라인 인증 UTC 일시.
        /// 오프라인 허용 기간 계산에 사용한다.
        /// </summary>
        public DateTime LastVerifiedAtUtc { get; set; }

        /// <summary>
        /// 오프라인 실행 허용 종료 UTC 일시.
        /// 현재 정책은 마지막 정상 인증 기준 7일이다.
        /// </summary>
        public DateTime OfflineExpireAtUtc { get; set; }
    }
}