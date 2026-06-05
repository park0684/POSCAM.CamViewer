namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 서버 설정 버전 확인 응답 DTO이다.
    ///
    /// AuthServer의 ConfigVersionResponse와 속성명이 다르면
    /// 이 DTO를 서버 DTO에 맞게 조정한다.
    /// </summary>
    public sealed class ConfigVersionResponseDto
    {
        /// <summary>
        /// 서버 설정 버전.
        /// </summary>
        public long ServerConfigVersion { get; set; }

        /// <summary>
        /// 로컬 설정이 서버 기준 최신인지 여부.
        /// </summary>
        public bool IsLatest { get; set; }

        /// <summary>
        /// 서버에 설정이 존재하는지 여부.
        /// </summary>
        public bool HasConfig { get; set; }

        /// <summary>
        /// 응답 메시지.
        /// </summary>
        public string Message { get; set; }
    }
}
