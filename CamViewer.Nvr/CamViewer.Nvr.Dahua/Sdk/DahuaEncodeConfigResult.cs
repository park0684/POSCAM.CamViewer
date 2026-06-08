namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua 채널 인코딩 설정 조회 결과이다.
    /// </summary>
    internal sealed class DahuaEncodeConfigResult
    {
        public bool Success { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string Message { get; set; }

        public string ErrorCode { get; set; }

        public string NativeErrorCode { get; set; }

        public static DahuaEncodeConfigResult Ok(
            int width,
            int height)
        {
            return new DahuaEncodeConfigResult
            {
                Success = true,
                Width = width,
                Height = height,
                Message = "Dahua 인코딩 설정을 조회했습니다."
            };
        }

        public static DahuaEncodeConfigResult Fail(
            string errorCode,
            string message,
            string nativeErrorCode)
        {
            return new DahuaEncodeConfigResult
            {
                Success = false,
                Width = 0,
                Height = 0,
                ErrorCode = errorCode,
                Message = message,
                NativeErrorCode = nativeErrorCode
            };
        }
    }
}