using CamViewer.Nvr.Core.Enums;
using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 하나의 재생 세션을 지정된 영상 시각과 상태로
    /// 정렬하기 위한 요청 정보.
    /// </summary>
    public sealed class NvrPlaybackAlignmentRequest
    {
        /// <summary>
        /// Provider에 적용할 목표 재생 시각.
        ///
        /// 채널별 시간 보정값이 존재하는 경우
        /// CamViewer 서비스에서 보정한 최종 시각을 전달한다.
        /// </summary>
        public DateTime TargetTime { get; set; }

        /// <summary>
        /// 정렬 후 유지할 재생 방향.
        /// </summary>
        public NvrPlaybackDirection Direction { get; set; }

        /// <summary>
        /// 정렬 후 적용할 재생속도.
        /// </summary>
        public NvrPlaybackSpeed Speed { get; set; }

        /// <summary>
        /// 정렬 완료 후 일시정지 상태를 유지할지 여부.
        ///
        /// true인 경우 AlignPlaybackAsync가 성공하여 반환될 때
        /// 해당 세션의 영상 시간이 계속 진행해서는 안 된다.
        /// </summary>
        public bool RemainPaused { get; set; }
    }
}