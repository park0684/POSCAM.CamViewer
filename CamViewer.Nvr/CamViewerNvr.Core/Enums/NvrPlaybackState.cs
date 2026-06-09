namespace CamViewer.Nvr.Core.Enums
{
    /// <summary>
    /// 개별 NVR 채널 재생 세션의 상태를 정의한다.
    /// </summary>
    public enum NvrPlaybackState
    {
        Created = 0,
        Playing = 1,
        Paused = 2,
        Stopped = 3,
        Completed = 4,
        Faulted = 5,
        Rewinding = 6

    }
}