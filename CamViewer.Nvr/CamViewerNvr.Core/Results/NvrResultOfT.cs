using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Results
{
    /// <summary>
    /// 반환 데이터를 포함하는 NVR 처리 결과.
    /// </summary>
    public class NvrResult<T> : NvrResult
    {
        public T Data { get; set; }

        /// <summary>
        /// 반환 데이터를 포함하는 성공 결과를 생성한다.
        /// </summary>
        public static NvrResult<T> Ok(T data, string message = null)
        {
            return new NvrResult<T>
            {
                Success = true,
                Status = NvrResultStatus.Success,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 반환 데이터를 포함하지 않는 실패 결과를 생성한다.
        /// </summary>
        public new static NvrResult<T> Fail(
            NvrResultStatus status,
            string message,
            NvrErrorInfo error = null)
        {
            return new NvrResult<T>
            {
                Success = false,
                Status = status,
                Message = message,
                Error = error,
                Data = default(T)
            };
        }
    }
}