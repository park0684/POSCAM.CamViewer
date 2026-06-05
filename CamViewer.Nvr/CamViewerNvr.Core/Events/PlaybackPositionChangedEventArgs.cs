using System;

namespace CamViewer.Nvr.Core.Events
{
    /// <summary>
    /// 재생 중인 영상 시각이 변경되었을 때 전달하는 이벤트 데이터.
    /// </summary>
    public sealed class PlaybackPositionChangedEventArgs : EventArgs
    {
        public PlaybackPositionChangedEventArgs(DateTime playbackTime)
        {
            PlaybackTime = playbackTime;
        }

        public DateTime PlaybackTime { get; private set; }
    }
}