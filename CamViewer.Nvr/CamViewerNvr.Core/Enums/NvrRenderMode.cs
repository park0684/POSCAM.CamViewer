namespace CamViewer.Nvr.Core.Enums
{
    /// <summary>
    /// NVR Provider가 영상을 CamViewer에 전달하는 방식을 정의한다.
    /// </summary>
    public enum NvrRenderMode
    {
        /// <summary>
        /// Provider가 전달받은 Windows Handle에 직접 영상을 출력한다.
        /// </summary>
        DirectRender = 1,

        /// <summary>
        /// Provider가 재생 가능한 RTSP 주소를 반환한다.
        /// </summary>
        RtspUrl = 2,

        /// <summary>
        /// Provider가 영상 프레임 스트림을 반환한다.
        /// </summary>
        FrameStream = 3,

        /// <summary>
        /// Provider가 추출된 영상 파일 경로를 반환한다.
        /// </summary>
        FilePlayback = 4
    }
}