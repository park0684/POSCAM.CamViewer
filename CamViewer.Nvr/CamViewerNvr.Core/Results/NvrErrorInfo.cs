namespace CamViewer.Nvr.Core.Results
{
    /// <summary>
    /// 제조사 SDK 또는 API 오류를 공통 형식으로 표현한다.
    /// </summary>
    public sealed class NvrErrorInfo
    {
        /// <summary>
        /// Provider 내부 또는 제조사 SDK의 오류 코드.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 오류 설명.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 제조사에서 반환한 원본 오류 코드.
        /// </summary>
        public string NativeErrorCode { get; set; }

        /// <summary>
        /// 오류가 발생한 기능명.
        /// </summary>
        public string Operation { get; set; }
    }
}