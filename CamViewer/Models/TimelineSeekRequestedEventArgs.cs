using System;

namespace CamViewer.Models
{
    /// <summary>
    /// 타임라인에서 특정 영상재생시간으로 이동 요청할 때 사용하는 이벤트 인자이다.
    /// </summary>
    public sealed class TimelineSeekRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// 이동할 영상재생시간.
        /// </summary>
        public DateTime TargetTime { get; private set; }

        /// <summary>
        /// 타임라인 이동 요청 이벤트 인자를 초기화한다.
        /// </summary>
        public TimelineSeekRequestedEventArgs(DateTime targetTime)
        {
            TargetTime = targetTime;
        }
    }
}