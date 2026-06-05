namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버 ApiResponse&lt;T&gt;에 대응하는 공통 응답 DTO이다.
    ///
    /// AuthServer의 ApiResponse 속성명이 다르면 이 DTO의 속성명을 서버와 맞춰야 한다.
    /// </summary>
    public sealed class ApiResponseDto<T>
    {
        /// <summary>
        /// API 처리 성공 여부.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 응답 메시지.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 오류 코드.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 응답 데이터.
        /// </summary>
        public T Data { get; set; }
    }
}
