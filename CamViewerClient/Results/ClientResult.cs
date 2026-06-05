namespace CamViewerClient.Results
{
    /// <summary>
    /// CamViewerClient 내부 처리 결과를 표현하는 공통 결과 모델이다.
    ///
    /// 설정 저장, 설정 불러오기, 토큰 저장, API 호출 등의 결과를
    /// CamViewer 본체에 일관된 형식으로 전달하기 위해 사용한다.
    /// </summary>
    public class ClientResult
    {
        /// <summary>
        /// 처리 성공 여부.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 사용자 또는 로그에 표시할 메시지.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 오류 코드.
        /// 필요하지 않으면 비워둘 수 있다.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 성공 결과를 생성한다.
        /// </summary>
        public static ClientResult Ok(string message = null)
        {
            return new ClientResult
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// 실패 결과를 생성한다.
        /// </summary>
        public static ClientResult Fail(
            string message,
            string errorCode = null)
        {
            return new ClientResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// 반환 데이터를 포함하는 CamViewerClient 처리 결과 모델이다.
    /// </summary>
    public class ClientResult<T> : ClientResult
    {
        /// <summary>
        /// 처리 성공 시 반환할 데이터.
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 반환 데이터를 포함하는 성공 결과를 생성한다.
        /// </summary>
        public static ClientResult<T> Ok(
            T data,
            string message = null)
        {
            return new ClientResult<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 반환 데이터가 없는 실패 결과를 생성한다.
        /// </summary>
        public new static ClientResult<T> Fail(
            string message,
            string errorCode = null)
        {
            return new ClientResult<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Data = default(T)
            };
        }
    }
}