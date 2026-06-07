namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView에서 선택 가능한 영상 재생 속도이다.
    /// </summary>
    public enum PlaybackSpeed
    {
        /// <summary>
        /// 0.5배속.
        /// </summary>
        Half = 50,

        /// <summary>
        /// 1배속.
        /// </summary>
        Normal = 100,

        /// <summary>
        /// 2배속.
        /// </summary>
        Double = 200,

        /// <summary>
        /// 4배속.
        /// </summary>
        Quad = 400,

        /// <summary>
        /// 8배속.
        /// </summary>
        Octuple = 800
    }
}