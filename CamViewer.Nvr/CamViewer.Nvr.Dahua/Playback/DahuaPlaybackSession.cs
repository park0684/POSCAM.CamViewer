using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Events;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Sdk;
using System;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// 하나의 Dahua 녹화 재생 핸들과 논리 상태를 관리한다.
    /// </summary>
    internal sealed class DahuaPlaybackSession :
        INvrPlaybackSession
    {
        private bool _disposed;
        private IntPtr _playbackHandle;

        internal DahuaPlaybackSession(
            IntPtr playbackHandle,
            int counterNo,
            int nvrNo,
            int channelNo,
            int screenPosition,
            DateTime searchDateTime,
            DateTime startTime,
            DateTime endTime,
            IntPtr renderTargetHandle,
            NvrPlaybackDirection direction)
        {
            if (playbackHandle == IntPtr.Zero)
            {
                throw new ArgumentException(
                    "Dahua 재생 핸들이 올바르지 않습니다.",
                    "playbackHandle");
            }

            SessionId =
                Guid.NewGuid();

            _playbackHandle =
                playbackHandle;

            CounterNo =
                counterNo;

            NvrNo =
                nvrNo;

            ChannelNo =
                channelNo;

            ScreenPosition =
                screenPosition;

            SearchDateTime =
                searchDateTime;

            StartTime =
                startTime;

            EndTime =
                endTime;

            NativeStartTime =
                startTime;

            NativeEndTime =
                endTime;

            RenderTargetHandle =
                renderTargetHandle;

            Direction =
                direction;

            Speed =
                NvrPlaybackSpeed.Normal;

            CurrentPlaybackTime =
                direction == NvrPlaybackDirection.Reverse
                    ? endTime
                    : startTime;

            State =
                direction == NvrPlaybackDirection.Reverse
                    ? NvrPlaybackState.Rewinding
                    : NvrPlaybackState.Playing;
        }

        public Guid SessionId
        {
            get;
            private set;
        }

        internal int CounterNo
        {
            get;
            private set;
        }

        public int NvrNo
        {
            get;
            private set;
        }

        public int ChannelNo
        {
            get;
            private set;
        }

        public int ScreenPosition
        {
            get;
            private set;
        }

        internal DateTime SearchDateTime
        {
            get;
            private set;
        }

        public DateTime StartTime
        {
            get;
            private set;
        }

        public DateTime EndTime
        {
            get;
            private set;
        }

        /// <summary>
        /// 현재 네이티브 재생 핸들을 만들 때 전달한 시작시간.
        /// 그룹 재구성 과정에서는 공통 조회 시작시간과 다를 수 있다.
        /// </summary>
        internal DateTime NativeStartTime
        {
            get;
            private set;
        }

        /// <summary>
        /// 현재 네이티브 재생 핸들을 만들 때 전달한 종료시간.
        /// </summary>
        internal DateTime NativeEndTime
        {
            get;
            private set;
        }

        internal IntPtr RenderTargetHandle
        {
            get;
            private set;
        }

        public DateTime CurrentPlaybackTime
        {
            get;
            private set;
        }

        public NvrPlaybackState State
        {
            get;
            private set;
        }

        internal NvrPlaybackDirection Direction
        {
            get;
            private set;
        }

        internal NvrPlaybackSpeed Speed
        {
            get;
            private set;
        }

        internal IntPtr PlaybackHandle
        {
            get
            {
                return _playbackHandle;
            }
        }

        internal bool IsValid
        {
            get
            {
                return !_disposed
                    && _playbackHandle != IntPtr.Zero;
            }
        }

        public event EventHandler<PlaybackPositionChangedEventArgs>
            PlaybackPositionChanged;

        public event EventHandler<PlaybackStateChangedEventArgs>
            PlaybackStateChanged;

        public event EventHandler
            PlaybackEnded;

        public event EventHandler<PlaybackErrorEventArgs>
            PlaybackError;

        internal void SetCurrentPlaybackTime(
            DateTime playbackTime)
        {
            if (playbackTime < StartTime)
            {
                playbackTime =
                    StartTime;
            }

            if (playbackTime > EndTime)
            {
                playbackTime =
                    EndTime;
            }

            CurrentPlaybackTime =
                playbackTime;

            EventHandler<PlaybackPositionChangedEventArgs> handler =
                PlaybackPositionChanged;

            if (handler != null)
            {
                try
                {
                    handler(
                        this,
                        new PlaybackPositionChangedEventArgs(
                            playbackTime));
                }
                catch
                {
                }
            }
        }

        internal void SetState(
            NvrPlaybackState state)
        {
            State =
                state;

            EventHandler<PlaybackStateChangedEventArgs> handler =
                PlaybackStateChanged;

            if (handler != null)
            {
                try
                {
                    handler(
                        this,
                        new PlaybackStateChangedEventArgs(
                            state));
                }
                catch
                {
                }
            }

            if (state == NvrPlaybackState.Completed)
            {
                EventHandler endedHandler =
                    PlaybackEnded;

                if (endedHandler != null)
                {
                    try
                    {
                        endedHandler(
                            this,
                            EventArgs.Empty);
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal void SetDirection(
            NvrPlaybackDirection direction)
        {
            Direction =
                direction;
        }

        internal void SetSpeed(
            NvrPlaybackSpeed speed)
        {
            Speed =
                speed;
        }

        internal void SetNativeRange(
            DateTime nativeStartTime,
            DateTime nativeEndTime)
        {
            NativeStartTime =
                nativeStartTime;

            NativeEndTime =
                nativeEndTime;
        }

        internal void ReportError(
            NvrErrorInfo error)
        {
            if (error == null)
            {
                return;
            }

            SetState(
                NvrPlaybackState.Faulted);

            EventHandler<PlaybackErrorEventArgs> handler =
                PlaybackError;

            if (handler != null)
            {
                try
                {
                    handler(
                        this,
                        new PlaybackErrorEventArgs(
                            error));
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 네이티브 Stop을 수행할 호출자에게 핸들을 넘기고
        /// 세션에서는 즉시 분리한다.
        /// </summary>
        internal IntPtr TakePlaybackHandle()
        {
            IntPtr handle =
                _playbackHandle;

            _playbackHandle =
                IntPtr.Zero;

            return handle;
        }

        internal void AdoptPlaybackHandle(
            IntPtr playbackHandle,
            DateTime nativeStartTime,
            DateTime nativeEndTime)
        {
            if (playbackHandle == IntPtr.Zero)
            {
                throw new ArgumentException(
                    "Dahua 재생 핸들이 올바르지 않습니다.",
                    "playbackHandle");
            }

            _playbackHandle =
                playbackHandle;

            NativeStartTime =
                nativeStartTime;

            NativeEndTime =
                nativeEndTime;

            _disposed =
                false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            try
            {
                DahuaPlaybackClient.Stop(
                    this);
            }
            catch
            {
                _playbackHandle =
                    IntPtr.Zero;
            }

            SetState(
                NvrPlaybackState.Stopped);
        }
    }
}
