using CamViewer.Nvr.Core.Enums;
using System;
using System.Collections.Generic;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 하나의 NVR에 속한 여러 채널을 함께 준비하기 위한 요청이다.
    /// </summary>
    public sealed class NvrPlaybackGroupRequest
    {
        public NvrPlaybackGroupRequest()
        {
            Channels =
                new List<NvrPlaybackGroupChannelRequest>();
        }

        public int CounterNo { get; set; }

        public int NvrNo { get; set; }

        public string ProviderKey { get; set; }

        public DateTime SearchDateTime { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime InitialTime { get; set; }

        public NvrPlaybackDirection InitialDirection { get; set; }

        public NvrPlaybackSpeed InitialSpeed { get; set; }

        public IList<NvrPlaybackGroupChannelRequest> Channels
        {
            get;
            private set;
        }
    }
}
