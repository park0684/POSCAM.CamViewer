using CamViewer.Nvr.Core.Enums;
using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 제조사 재생 엔진이 반환하는
    /// 다중채널 재생 그룹의 현재 상태.
    /// </summary>
    public sealed class NvrPlaybackGroupStatus
    {
        /// <summary>
        /// 현재 공통 영상재생시간.
        /// 확인할 수 없으면 null.
        /// </summary>
        public DateTime? CurrentPlaybackTime { get; set; }

        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        public NvrPlaybackState State { get; set; }

        /// <summary>
        /// 현재 재생 방향.
        /// </summary>
        public NvrPlaybackDirection Direction { get; set; }

        /// <summary>
        /// 현재 재생속도.
        /// </summary>
        public NvrPlaybackSpeed Speed { get; set; }

        /// <summary>
        /// 모든 채널이 재생 명령을 받을 수 있는 준비 상태인지 여부.
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// 제조사 엔진이 채널 간 동기화 상태를
        /// 확인할 수 있는지 여부.
        /// </summary>
        public bool SynchronizationAvailable { get; set; }

        /// <summary>
        /// 제조사 기준으로 채널들이 동기화된 상태인지 여부.
        /// </summary>
        public bool IsSynchronized { get; set; }

        /// <summary>
        /// 제조사 엔진에서 측정한 최대 채널 시간차.
        /// 확인할 수 없으면 null.
        /// </summary>
        public double? MaximumDriftSeconds { get; set; }

        /// <summary>
        /// 제조사별 상태 설명.
        /// </summary>
        public string Message { get; set; }
    }
}