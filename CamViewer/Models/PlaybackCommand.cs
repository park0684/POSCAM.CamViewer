namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView에서 사용자가 요청한 재생 제어 명령이다.
    /// </summary>
    public enum PlaybackCommand
    {
        /// <summary>
        /// 빠른 역재생.
        /// </summary>
        FastReverse = 1,

        /// <summary>
        /// 현재 재생 위치에서 10초 전으로 이동.
        /// </summary>
        SeekBackward10 = 2,

        /// <summary>
        /// 재생 또는 일시정지 전환.
        /// </summary>
        PlayPause = 3,

        /// <summary>
        /// 현재 재생 위치에서 10초 뒤로 이동.
        /// </summary>
        SeekForward10 = 4,

        /// <summary>
        /// 빠른 재생.
        /// </summary>
        FastForward = 5
    }
}