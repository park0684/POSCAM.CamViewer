namespace CamViewer.Models
{
    /// <summary>
    /// CamViewer 프로그램 실행 요청의 유형을 나타낸다.
    /// </summary>
    public enum ApplicationLaunchRequestType
    {
        /// <summary>
        /// 사용자가 CamViewer 실행 파일을 직접 실행한 요청.
        /// </summary>
        DirectLaunch = 0,

        /// <summary>
        /// 외부 프로그램이 특정 영상 조회 시간을 전달한 요청.
        /// </summary>
        ExternalPlayback = 1
    }
}