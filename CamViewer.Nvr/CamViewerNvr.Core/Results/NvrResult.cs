using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Results
{
    /// <summary>
    /// NVR 처리 결과의 공통 형식.
    /// </summary>
    public class NvrResult
    {
        public bool Success { get; set; }

        public NvrResultStatus Status { get; set; }

        public string Message { get; set; }

        public NvrErrorInfo Error { get; set; }

        /// <summary>
        /// 성공 결과를 생성한다.
        /// </summary>
        public static NvrResult Ok(string message = null)
        {
            return new NvrResult
            {
                Success = true,
                Status = NvrResultStatus.Success,
                Message = message
            };
        }

        /// <summary>
        /// 실패 결과를 생성한다.
        /// </summary>
        public static NvrResult Fail(
            NvrResultStatus status,
            string message,
            NvrErrorInfo error = null)
        {
            return new NvrResult
            {
                Success = false,
                Status = status,
                Message = message,
                Error = error
            };
        }
    }
}