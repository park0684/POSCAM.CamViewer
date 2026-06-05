using System;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 지정된 시간 구간에 녹화 영상이 존재하는지 조회하기 위한 요청 정보.
    /// </summary>
    public sealed class NvrRecordQueryRequest
    {
        public int NvrNo { get; set; }

        public int ChannelNo { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
    }
}