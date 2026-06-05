using System;
using CamViewer.Nvr.Core.Enums;

namespace CamViewer.Nvr.Core.Events
{
    /// <summary>
    /// 재생 세션 상태가 변경되었을 때 전달하는 이벤트 데이터.
    /// </summary>
    public sealed class PlaybackStateChangedEventArgs : EventArgs
    {
        public PlaybackStateChangedEventArgs(NvrPlaybackState state)
        {
            State = state;
        }

        public NvrPlaybackState State { get; private set; }
    }
}