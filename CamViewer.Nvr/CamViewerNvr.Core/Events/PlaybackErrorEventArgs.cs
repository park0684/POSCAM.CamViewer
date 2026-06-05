using System;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Events
{
    /// <summary>
    /// 재생 세션에서 오류가 발생했을 때 전달하는 이벤트 데이터.
    /// </summary>
    public sealed class PlaybackErrorEventArgs : EventArgs
    {
        public PlaybackErrorEventArgs(NvrErrorInfo error)
        {
            Error = error;
        }

        public NvrErrorInfo Error { get; private set; }
    }
}