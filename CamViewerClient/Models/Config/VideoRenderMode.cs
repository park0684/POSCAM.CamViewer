namespace CamViewerClient.Models.Config
{
    /// <summary>
    /// PlayerView 영상 표시 방식이다.
    /// </summary>
    public enum VideoRenderMode
    {
        /// <summary>
        /// 원본 비율을 무시하고 영상 영역 전체를 채운다.
        /// </summary>
        Fill = 1,

        /// <summary>
        /// 원본 비율을 유지한다.
        /// 영상 영역 비율과 맞지 않으면 검은 여백이 생긴다.
        /// </summary>
        KeepAspectRatio = 2
    }
}