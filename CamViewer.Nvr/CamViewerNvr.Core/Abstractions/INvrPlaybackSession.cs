using System;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Events;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 하나의 NVR 채널 재생 세션을 나타낸다.
    /// 재생 제어 명령은 INvrProvider를 통해 수행한다.
    /// </summary>
    public interface INvrPlaybackSession : IDisposable
    {
        /// <summary>
        /// 재생 세션 고유 식별값.
        /// </summary>
        Guid SessionId { get; }

        int NvrNo { get; }

        int ChannelNo { get; }

        int ScreenPosition { get; }

        DateTime StartTime { get; }

        DateTime EndTime { get; }

        /// <summary>
        /// 현재 재생 중인 영상 시각.
        /// Provider가 지원하지 않으면 시작 시각 기준 추정값을 사용할 수 있다.
        /// </summary>
        DateTime CurrentPlaybackTime { get; }

        NvrPlaybackState State { get; }

        event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;

        event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;

        event EventHandler PlaybackEnded;

        event EventHandler<PlaybackErrorEventArgs> PlaybackError;
    }
}