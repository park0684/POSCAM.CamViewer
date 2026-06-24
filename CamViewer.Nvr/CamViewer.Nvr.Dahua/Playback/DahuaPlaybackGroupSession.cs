using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// 하나의 Dahua NVR에서 함께 재생되는 채널 그룹과
    /// PlayGroup 네이티브 상태를 관리한다.
    /// </summary>
    internal sealed class DahuaPlaybackGroupSession :
        INvrPlaybackGroupSession
    {
        private readonly List<NvrPlaybackGroupChannelRequest>
            _channelRequests;

        private readonly List<DahuaPlaybackGroupChannel>
            _channels;

        private IntPtr _playGroupHandle;
        private int? _baseChannelNo;
        private int? _baseScreenPosition;
        private DateTime _clockBaseTime;
        private DateTime? _clockStartedUtc;

        internal DahuaPlaybackGroupSession(
            NvrPlaybackGroupRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            SessionId =
                Guid.NewGuid().ToString("N");

            ProviderKey =
                request.ProviderKey;

            CounterNo =
                request.CounterNo;

            NvrNo =
                request.NvrNo;

            SearchDateTime =
                request.SearchDateTime;

            StartTime =
                request.StartTime;

            EndTime =
                request.EndTime;

            Direction =
                request.InitialDirection;

            Speed =
                request.InitialSpeed;

            State =
                NvrPlaybackState.Created;

            IsReady =
                false;

            IsSynchronized =
                false;

            MaximumDriftSeconds =
                null;

            StatusMessage =
                "Dahua 재생 그룹이 생성되었습니다.";

            _clockBaseTime =
                ClampTime(
                    request.InitialTime);

            _clockStartedUtc =
                null;

            _channelRequests =
                request.Channels
                    .Where(
                        item => item != null)
                    .Select(
                        CloneChannelRequest)
                    .ToList();

            _channels =
                new List<DahuaPlaybackGroupChannel>();
        }

        public string SessionId
        {
            get;
            private set;
        }

        public string ProviderKey
        {
            get;
            private set;
        }

        public int CounterNo
        {
            get;
            private set;
        }

        public int NvrNo
        {
            get;
            private set;
        }

        public int ChannelCount
        {
            get
            {
                return _channels.Count;
            }
        }

        public DateTime SearchDateTime
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

        public DateTime CurrentPlaybackTime
        {
            get
            {
                return GetEstimatedCurrentTime();
            }
        }

        public NvrPlaybackState State
        {
            get;
            private set;
        }

        public NvrPlaybackDirection Direction
        {
            get;
            private set;
        }

        public NvrPlaybackSpeed Speed
        {
            get;
            private set;
        }

        public bool IsReady
        {
            get;
            private set;
        }

        public bool IsSynchronized
        {
            get;
            private set;
        }

        public double? MaximumDriftSeconds
        {
            get;
            private set;
        }

        public string StatusMessage
        {
            get;
            private set;
        }

        internal IntPtr PlayGroupHandle
        {
            get
            {
                return _playGroupHandle;
            }
        }

        internal bool UsesPlayGroup
        {
            get
            {
                return ChannelCount > 1;
            }
        }

        internal bool IsPlayGroupReady
        {
            get
            {
                return UsesPlayGroup
                    && _playGroupHandle != IntPtr.Zero
                    && _baseChannelNo.HasValue;
            }
        }

        internal int? BaseChannelNo
        {
            get
            {
                return _baseChannelNo;
            }
        }

        internal int? BaseScreenPosition
        {
            get
            {
                return _baseScreenPosition;
            }
        }

        internal IList<NvrPlaybackGroupChannelRequest>
            GetChannelRequests()
        {
            return _channelRequests
                .Select(
                    CloneChannelRequest)
                .ToList()
                .AsReadOnly();
        }

        internal IList<DahuaPlaybackGroupChannel>
            GetChannels()
        {
            return _channels
                .ToList()
                .AsReadOnly();
        }

        internal DahuaPlaybackGroupChannel
            GetBaseChannel()
        {
            if (_baseChannelNo.HasValue)
            {
                DahuaPlaybackGroupChannel byChannel =
                    _channels.FirstOrDefault(
                        item =>
                            item.ChannelNo
                                == _baseChannelNo.Value);

                if (byChannel != null)
                {
                    return byChannel;
                }
            }

            DahuaPlaybackGroupChannel left =
                _channels.FirstOrDefault(
                    item =>
                        item.ScreenPosition == 0);

            return left
                ?? _channels.FirstOrDefault();
        }

        internal void ReplaceChannels(
            IList<DahuaPlaybackGroupChannel> channels)
        {
            _channels.Clear();

            if (channels != null)
            {
                foreach (DahuaPlaybackGroupChannel channel
                    in channels)
                {
                    if (channel != null)
                    {
                        _channels.Add(
                            channel);
                    }
                }
            }
        }

        internal void ClearChannels()
        {
            _channels.Clear();
        }

        internal void SetPlayGroup(
            IntPtr groupHandle,
            DahuaPlaybackGroupChannel baseChannel)
        {
            if (groupHandle == IntPtr.Zero)
            {
                throw new ArgumentException(
                    "Dahua PlayGroup 핸들이 올바르지 않습니다.",
                    "groupHandle");
            }

            if (baseChannel == null)
            {
                throw new ArgumentNullException(
                    "baseChannel");
            }

            _playGroupHandle =
                groupHandle;

            _baseChannelNo =
                baseChannel.ChannelNo;

            _baseScreenPosition =
                baseChannel.ScreenPosition;
        }

        internal IntPtr TakePlayGroupHandle()
        {
            IntPtr handle =
                _playGroupHandle;

            _playGroupHandle =
                IntPtr.Zero;

            _baseChannelNo =
                null;

            _baseScreenPosition =
                null;

            return handle;
        }

        internal void SetCurrentPlaybackTime(
            DateTime playbackTime)
        {
            _clockBaseTime =
                ClampTime(
                    playbackTime);

            _clockStartedUtc =
                State == NvrPlaybackState.Playing
                || State == NvrPlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;
        }

        internal void SetState(
            NvrPlaybackState state)
        {
            DateTime currentTime =
                GetEstimatedCurrentTime();

            State =
                state;

            _clockBaseTime =
                currentTime;

            _clockStartedUtc =
                state == NvrPlaybackState.Playing
                || state == NvrPlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;
        }

        internal void SetDirection(
            NvrPlaybackDirection direction)
        {
            DateTime currentTime =
                GetEstimatedCurrentTime();

            Direction =
                direction;

            _clockBaseTime =
                currentTime;

            _clockStartedUtc =
                State == NvrPlaybackState.Playing
                || State == NvrPlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;
        }

        internal void SetSpeed(
            NvrPlaybackSpeed speed)
        {
            DateTime currentTime =
                GetEstimatedCurrentTime();

            Speed =
                speed;

            _clockBaseTime =
                currentTime;

            _clockStartedUtc =
                State == NvrPlaybackState.Playing
                || State == NvrPlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;
        }

        internal void SetReady(
            bool ready,
            string message)
        {
            IsReady =
                ready;

            if (!string.IsNullOrWhiteSpace(
                    message))
            {
                StatusMessage =
                    message;
            }
        }

        internal void SetSynchronizationStatus(
            bool synchronized,
            double? maximumDriftSeconds,
            string message)
        {
            IsSynchronized =
                synchronized;

            MaximumDriftSeconds =
                maximumDriftSeconds;

            if (!string.IsNullOrWhiteSpace(
                    message))
            {
                StatusMessage =
                    message;
            }
        }

        internal DateTime ClampTime(
            DateTime playbackTime)
        {
            if (playbackTime < StartTime)
            {
                return StartTime;
            }

            if (playbackTime >= EndTime)
            {
                DateTime last =
                    EndTime.AddTicks(
                        -1);

                return last < StartTime
                    ? StartTime
                    : last;
            }

            return playbackTime;
        }

        private DateTime GetEstimatedCurrentTime()
        {
            DateTime result =
                _clockBaseTime;

            if (!_clockStartedUtc.HasValue
                || (
                    State != NvrPlaybackState.Playing
                    && State != NvrPlaybackState.Rewinding
                ))
            {
                return ClampTime(
                    result);
            }

            double seconds =
                (
                    DateTime.UtcNow
                    - _clockStartedUtc.Value
                ).TotalSeconds
                * GetSpeedMultiplier(
                    Speed);

            if (State == NvrPlaybackState.Rewinding
                || Direction == NvrPlaybackDirection.Reverse)
            {
                result =
                    result.AddSeconds(
                        -seconds);
            }
            else
            {
                result =
                    result.AddSeconds(
                        seconds);
            }

            return ClampTime(
                result);
        }

        private static double GetSpeedMultiplier(
            NvrPlaybackSpeed speed)
        {
            switch (speed)
            {
                case NvrPlaybackSpeed.Half:
                    return 0.5d;

                case NvrPlaybackSpeed.Double:
                    return 2d;

                case NvrPlaybackSpeed.Quad:
                    return 4d;

                case NvrPlaybackSpeed.Octuple:
                    return 8d;

                default:
                    return 1d;
            }
        }

        private static NvrPlaybackGroupChannelRequest
            CloneChannelRequest(
                NvrPlaybackGroupChannelRequest source)
        {
            return new NvrPlaybackGroupChannelRequest
            {
                ChannelNo =
                    source.ChannelNo,

                ScreenPosition =
                    source.ScreenPosition,

                RenderTargetHandle =
                    source.RenderTargetHandle,

                TimeOffsetSeconds =
                    source.TimeOffsetSeconds
            };
        }
    }
}
