using CamViewer.Nvr.Core.Enums;
using System;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 하나의 NVR에서 함께 재생되는 채널 그룹의 공통 상태다.
    /// 네이티브 핸들은 제조사 프로젝트 내부에서만 관리한다.
    /// </summary>
    public interface INvrPlaybackGroupSession
    {
        string SessionId { get; }

        string ProviderKey { get; }

        int CounterNo { get; }

        int NvrNo { get; }

        int ChannelCount { get; }

        DateTime SearchDateTime { get; }

        DateTime StartTime { get; }

        DateTime EndTime { get; }

        DateTime CurrentPlaybackTime { get; }

        NvrPlaybackState State { get; }

        NvrPlaybackDirection Direction { get; }

        NvrPlaybackSpeed Speed { get; }

        bool IsReady { get; }

        bool IsSynchronized { get; }

        double? MaximumDriftSeconds { get; }

        string StatusMessage { get; }
    }
}
