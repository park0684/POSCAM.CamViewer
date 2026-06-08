namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// NVR 채널의 영상 원본 정보이다.
    /// 원본 비율 표시 모드에서 Width / Height를 사용한다.
    /// </summary>
    public sealed class NvrVideoSourceInfo
    {
        /// <summary>
        /// 영상 원본 너비.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 영상 원본 높이.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 원본 비율.
        /// Width / Height 값으로 계산한다.
        /// </summary>
        public double AspectRatio
        {
            get
            {
                if (Width <= 0 || Height <= 0)
                {
                    return 0;
                }

                return (double)Width / Height;
            }
        }
    }
}