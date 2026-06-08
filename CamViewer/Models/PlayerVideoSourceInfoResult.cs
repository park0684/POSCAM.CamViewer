using CamViewerClient.Enums;

namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView에 전달할 채널 영상 원본 정보 조회 결과이다.
    /// </summary>
    public sealed class PlayerVideoSourceInfoResult
    {
        public bool Success { get; set; }

        public ScreenPosition ScreenPosition { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string Message { get; set; }

        public static PlayerVideoSourceInfoResult Ok(
            ScreenPosition screenPosition,
            int width,
            int height)
        {
            return new PlayerVideoSourceInfoResult
            {
                Success = true,
                ScreenPosition = screenPosition,
                Width = width,
                Height = height,
                Message = "영상 원본 정보를 조회했습니다."
            };
        }

        public static PlayerVideoSourceInfoResult Fail(
            ScreenPosition screenPosition,
            string message)
        {
            return new PlayerVideoSourceInfoResult
            {
                Success = false,
                ScreenPosition = screenPosition,
                Width = 0,
                Height = 0,
                Message = message
            };
        }
    }
}