namespace CamViewer.Models
{
    /// <summary>
    /// PlayerPlaybackService 처리 결과를 나타낸다.
    /// </summary>
    public sealed class PlayerPlaybackResult
    {
        /// <summary>
        /// 처리 성공 여부.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 사용자 표시 메시지.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 오류 코드.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 성공 결과를 생성한다.
        /// </summary>
        public static PlayerPlaybackResult Ok(string message)
        {
            return new PlayerPlaybackResult
            {
                Success = true,
                Message = message,
                ErrorCode = string.Empty
            };
        }

        /// <summary>
        /// 실패 결과를 생성한다.
        /// </summary>
        public static PlayerPlaybackResult Fail(
            string message,
            string errorCode)
        {
            return new PlayerPlaybackResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }
}