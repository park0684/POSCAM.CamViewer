namespace CamViewerClient.Enums
{
    /// <summary>
    /// 계산대 채널을 표시할 스크린위치를 정의한다.
    /// 화면에는 좌측/우측으로 표시하고 내부값은 0/1로 저장한다.
    /// </summary>
    public enum ScreenPosition
    {
        /// <summary>
        /// 좌측 영상 영역.
        /// </summary>
        Left = 0,

        /// <summary>
        /// 우측 영상 영역.
        /// </summary>
        Right = 1
    }
}