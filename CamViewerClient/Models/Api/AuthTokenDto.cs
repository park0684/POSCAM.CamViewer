using System;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버에서 발급하는 캠뷰어 인증 토큰 DTO이다.
    /// </summary>
    public sealed class AuthTokenDto
    {
        /// <summary>
        /// 실제 인증 토큰 문자열.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 토큰 발급 시각.
        /// </summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>
        /// 토큰 만료 시각.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 오프라인 실행 허용 만료 시각.
        /// </summary>
        public DateTime OfflineUntil { get; set; }

        /// <summary>
        /// 영구 토큰 여부.
        /// </summary>
        public bool IsPermanent { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// 현재 서버 응답은 빈 문자열이 올 수 있으므로 string으로 받는다.
        /// </summary>
        public string ConfigVersion { get; set; }
    }
}