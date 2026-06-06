using System;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Events;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Native;

namespace CamViewer.Nvr.Dahua.Sdk
{
    /// <summary>
    /// Dahua SDK 재생 세션을 나타낸다.
    ///
    /// 하나의 Dahua 재생 핸들은 하나의 NVR 채널과 하나의 출력 패널에 연결된다.
    /// 좌측/우측 동시 재생 시 각각 별도 세션이 생성된다.
    /// </summary>
    internal sealed class DahuaPlaybackSession : INvrPlaybackSession
    {
        private bool _disposed;

        /// <summary>
        /// Dahua 재생 세션을 초기화한다.
        /// </summary>
        public DahuaPlaybackSession(
            IntPtr playbackHandle,
            int nvrNo,
            int channelNo,
            int screenPosition,
            DateTime startTime,
            DateTime endTime)
        {
            SessionId = Guid.NewGuid();
            PlaybackHandle = playbackHandle;
            NvrNo = nvrNo;
            ChannelNo = channelNo;
            ScreenPosition = screenPosition;
            StartTime = startTime;
            EndTime = endTime;
            CurrentPlaybackTime = startTime;
            State = NvrPlaybackState.Playing;
        }

        /// <summary>
        /// Dahua SDK 재생 핸들.
        /// </summary>
        public IntPtr PlaybackHandle { get; private set; }

        /// <summary>
        /// 재생 세션 고유 식별값.
        /// </summary>
        public Guid SessionId { get; private set; }

        public int NvrNo { get; private set; }

        public int ChannelNo { get; private set; }

        public int ScreenPosition { get; private set; }

        public DateTime StartTime { get; private set; }

        public DateTime EndTime { get; private set; }

        public DateTime CurrentPlaybackTime { get; private set; }

        public NvrPlaybackState State { get; private set; }

        public event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;

        public event EventHandler<PlaybackStateChangedEventArgs> PlaybackStateChanged;

        public event EventHandler PlaybackEnded;

        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;

        /// <summary>
        /// 세션 상태를 변경한다.
        /// </summary>
        public void SetState(NvrPlaybackState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;

            EventHandler<PlaybackStateChangedEventArgs> handler =
                PlaybackStateChanged;

            if (handler != null)
            {
                handler(
                    this,
                    new PlaybackStateChangedEventArgs(state));
            }
        }

        /// <summary>
        /// 현재 재생 위치를 변경한다.
        /// </summary>
        public void SetCurrentPlaybackTime(DateTime playbackTime)
        {
            CurrentPlaybackTime = playbackTime;

            EventHandler<PlaybackPositionChangedEventArgs> handler =
                PlaybackPositionChanged;

            if (handler != null)
            {
                handler(
                    this,
                    new PlaybackPositionChangedEventArgs(playbackTime));
            }
        }

        /// <summary>
        /// 재생 종료 이벤트를 발생시킨다.
        /// </summary>
        public void RaisePlaybackEnded()
        {
            SetState(NvrPlaybackState.Completed);

            EventHandler handler = PlaybackEnded;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 재생 오류 이벤트를 발생시킨다.
        /// </summary>
        public void RaisePlaybackError(NvrErrorInfo error)
        {
            SetState(NvrPlaybackState.Faulted);

            EventHandler<PlaybackErrorEventArgs> handler =
                PlaybackError;

            if (handler != null)
            {
                handler(
                    this,
                    new PlaybackErrorEventArgs(error));
            }
        }

        /// <summary>
        /// 재생 세션을 해제한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (PlaybackHandle != IntPtr.Zero)
            {
                DahuaNative.CLIENT_StopPlayBack(PlaybackHandle);
                PlaybackHandle = IntPtr.Zero;
            }

            SetState(NvrPlaybackState.Stopped);

            _disposed = true;
        }
    }
}