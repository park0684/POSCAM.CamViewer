using CamViewer.Nvr.Core.Enums;
using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 제조사 재생 엔진이 반환하는 그룹 상태다.
    /// </summary>
    public sealed class NvrPlaybackGroupStatus
    {
        public DateTime? CurrentPlaybackTime { get; set; }

        public NvrPlaybackState State { get; set; }

        public NvrPlaybackDirection Direction { get; set; }

        public NvrPlaybackSpeed Speed { get; set; }

        public bool IsReady { get; set; }

        public bool SynchronizationAvailable { get; set; }

        public bool IsSynchronized { get; set; }

        public double? MaximumDriftSeconds { get; set; }

        public string Message { get; set; }
    }
}
