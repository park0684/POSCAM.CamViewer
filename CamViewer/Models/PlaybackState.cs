namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView의 현재 재생 상태를 나타낸다.
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>
        /// 재생 전 또는 정지 상태.
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// 일반 재생 중.
        /// </summary>
        Playing = 1,

        /// <summary>
        /// 일시정지 상태.
        /// </summary>
        Paused = 2,

        /// <summary>
        /// 역재생 중.
        /// </summary>
        Rewinding = 3
    }
}