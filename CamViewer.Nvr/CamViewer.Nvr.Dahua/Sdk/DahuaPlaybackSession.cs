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
            int counterNo,
            int nvrNo,
            int channelNo,
            int screenPosition,
            DateTime searchDateTime,
            DateTime startTime,
            DateTime endTime,
            IntPtr renderTargetHandle,
            bool autoPlay)
        {
            SessionId = Guid.NewGuid();
            PlaybackHandle = playbackHandle;

            CounterNo = counterNo;
            NvrNo = nvrNo;
            ChannelNo = channelNo;
            ScreenPosition = screenPosition;
            SearchDateTime = searchDateTime;
            StartTime = startTime;
            EndTime = endTime;
            RenderTargetHandle = renderTargetHandle;
            AutoPlay = autoPlay;

            CurrentPlaybackTime = startTime;
            State = NvrPlaybackState.Playing;
        }

        /// <summary>
        /// Dahua SDK 재생 핸들.
        /// </summary>
        public IntPtr PlaybackHandle { get; private set; }

        /// <summary>
        /// 조회 대상 계산대번호.
        /// Seek 또는 재재생 시 요청 복원에 사용한다.
        /// </summary>
        public int CounterNo { get; private set; }

        /// <summary>
        /// 사용자가 입력하거나 외부 POS에서 전달한 영상검색일시.
        /// </summary>
        public DateTime SearchDateTime { get; private set; }

        /// <summary>
        /// 영상을 출력할 Windows Handle.
        /// </summary>
        public IntPtr RenderTargetHandle { get; private set; }

        /// <summary>
        /// 재생 준비 후 자동 재생 여부.
        /// </summary>
        public bool AutoPlay { get; private set; }

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
        /// Seek 또는 재재생 시 기존 재생 핸들을 새 핸들로 교체한다.
        ///
        /// 주의:
        /// StartTime과 EndTime은 사용자가 지정한 전체 조회 구간이므로 변경하지 않는다.
        /// Seek 후 현재 재생 위치만 변경한다.
        /// </summary>
        public void ReplacePlaybackHandle(
            IntPtr playbackHandle,
            DateTime currentPlaybackTime)
        {
            if (PlaybackHandle != IntPtr.Zero)
            {
                DahuaNative.CLIENT_StopPlayBack(PlaybackHandle);
            }

            PlaybackHandle = playbackHandle;

            SetState(NvrPlaybackState.Playing);
            SetCurrentPlaybackTime(currentPlaybackTime);
        }

        /// <summary>
        /// 현재 재생 핸들의 소유권을 외부로 넘긴다.
        /// 이후 이 세션을 Dispose해도 해당 핸들을 중지하지 않는다.
        /// </summary>
        public IntPtr DetachPlaybackHandle()
        {
            IntPtr handle = PlaybackHandle;
            PlaybackHandle = IntPtr.Zero;
            return handle;
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