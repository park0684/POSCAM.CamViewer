using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// 하나의 Dahua NVR에서 동시에 재생되는
    /// 여러 채널을 하나의 논리적 재생 그룹으로 관리한다.
    ///
    /// 실제 재생 핸들, 채널별 OSD 시간,
    /// 시간 보정값 및 Dahua 전용 상태는
    /// 이 제조사 프로젝트 내부에서만 관리한다.
    /// </summary>
    internal sealed class DahuaPlaybackGroupSession :
        INvrPlaybackGroupSession
    {
        private const string DahuaProviderKey =
            "DAHUA_SDK";

        private readonly List<DahuaPlaybackGroupChannel>
            _channels;

        /// <summary>
        /// Dahua 다중채널 재생 그룹을 초기화한다.
        /// </summary>
        public DahuaPlaybackGroupSession(
            NvrPlaybackGroupRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            if (request.StartTime
                >= request.EndTime)
            {
                throw new ArgumentException(
                    "재생 시작시간은 종료시간보다 이전이어야 합니다.",
                    "request");
            }

            SessionId =
                Guid.NewGuid().ToString("N");

            ProviderKey =
                DahuaProviderKey;

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

            CurrentPlaybackTime =
                request.InitialTime;

            Direction =
                request.InitialDirection;

            Speed =
                request.InitialSpeed;

            State =
                NvrPlaybackState.Stopped;

            IsReady =
                false;

            IsSynchronized =
                false;

            MaximumDriftSeconds =
                null;

            StatusMessage =
                "Dahua 재생 그룹이 생성되었습니다.";

            _channels =
                new List<DahuaPlaybackGroupChannel>();
        }

        /// <summary>
        /// 재생 그룹 고유 식별값.
        /// </summary>
        public string SessionId { get; private set; }

        /// <summary>
        /// Dahua ProviderKey.
        /// </summary>
        public string ProviderKey { get; private set; }

        /// <summary>
        /// 조회 대상 계산대번호.
        /// </summary>
        public int CounterNo { get; private set; }

        /// <summary>
        /// 조회 대상 NVR번호.
        /// </summary>
        public int NvrNo { get; private set; }

        /// <summary>
        /// 그룹에 포함된 채널 수.
        /// </summary>
        public int ChannelCount
        {
            get
            {
                return _channels.Count;
            }
        }

        /// <summary>
        /// 외부 POS 또는 사용자가 입력한 기준 시각.
        /// </summary>
        public DateTime SearchDateTime { get; private set; }

        /// <summary>
        /// 그룹 전체의 공통 조회 시작시간.
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// 그룹 전체의 공통 조회 종료시간.
        /// </summary>
        public DateTime EndTime { get; private set; }

        /// <summary>
        /// Dahua 엔진이 관리하는 현재 공통 영상재생시간.
        /// </summary>
        public DateTime CurrentPlaybackTime { get; private set; }

        /// <summary>
        /// 현재 그룹 재생 상태.
        /// </summary>
        public NvrPlaybackState State { get; private set; }

        /// <summary>
        /// 현재 그룹 재생 방향.
        /// </summary>
        public NvrPlaybackDirection Direction { get; private set; }

        /// <summary>
        /// 현재 그룹 재생속도.
        /// </summary>
        public NvrPlaybackSpeed Speed { get; private set; }

        /// <summary>
        /// 모든 채널이 명령을 받을 준비가 되었는지 여부.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Dahua 기준으로 채널들이 동기화된 상태인지 여부.
        /// </summary>
        public bool IsSynchronized { get; private set; }

        /// <summary>
        /// Dahua 엔진에서 확인한 최대 채널 시간차.
        /// </summary>
        public double? MaximumDriftSeconds { get; private set; }

        /// <summary>
        /// 제조사별 현재 상태 설명.
        /// </summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// 그룹에 Dahua 재생 채널을 추가한다.
        /// </summary>
        public void AddChannel(
            DahuaPlaybackGroupChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(
                    "channel");
            }

            bool duplicateScreen =
                _channels.Any(
                    item =>
                        item.ScreenPosition
                            == channel.ScreenPosition);

            if (duplicateScreen)
            {
                throw new InvalidOperationException(
                    "같은 화면 위치의 Dahua 채널이 이미 등록되어 있습니다. "
                    + "ScreenPosition="
                    + channel.ScreenPosition);
            }

            bool duplicateChannel =
                _channels.Any(
                    item =>
                        item.ChannelNo
                            == channel.ChannelNo);

            if (duplicateChannel)
            {
                throw new InvalidOperationException(
                    "같은 Dahua 채널번호가 이미 등록되어 있습니다. "
                    + "ChannelNo="
                    + channel.ChannelNo);
            }

            _channels.Add(
                channel);
        }

        /// <summary>
        /// 현재 그룹 채널 목록의 복사본을 반환한다.
        ///
        /// 외부에서 내부 List를 직접 수정하지 못하게 한다.
        /// </summary>
        public IList<DahuaPlaybackGroupChannel>
            GetChannels()
        {
            return _channels
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 화면 위치를 기준으로 그룹 채널을 찾는다.
        /// </summary>
        public DahuaPlaybackGroupChannel
            FindChannelByScreenPosition(
                int screenPosition)
        {
            return _channels.FirstOrDefault(
                item =>
                    item.ScreenPosition
                        == screenPosition);
        }

        /// <summary>
        /// 공통 영상재생시간을 변경한다.
        /// </summary>
        public void SetCurrentPlaybackTime(
            DateTime playbackTime)
        {
            if (playbackTime < StartTime)
            {
                playbackTime =
                    StartTime;
            }

            if (playbackTime >= EndTime)
            {
                playbackTime =
                    EndTime.AddSeconds(-1);
            }

            CurrentPlaybackTime =
                playbackTime;
        }

        /// <summary>
        /// 현재 그룹 재생 상태를 변경한다.
        /// </summary>
        public void SetState(
            NvrPlaybackState state)
        {
            State =
                state;
        }

        /// <summary>
        /// 현재 그룹 재생 방향을 변경한다.
        /// </summary>
        public void SetDirection(
            NvrPlaybackDirection direction)
        {
            Direction =
                direction;
        }

        /// <summary>
        /// 현재 그룹 재생속도를 변경한다.
        /// </summary>
        public void SetSpeed(
            NvrPlaybackSpeed speed)
        {
            Speed =
                speed;
        }

        /// <summary>
        /// 그룹 준비 상태를 변경한다.
        /// </summary>
        public void SetReady(
            bool isReady,
            string message)
        {
            IsReady =
                isReady;

            if (!string.IsNullOrWhiteSpace(
                message))
            {
                StatusMessage =
                    message;
            }
        }

        /// <summary>
        /// 채널 동기화 상태를 변경한다.
        /// </summary>
        public void SetSynchronizationStatus(
            bool isSynchronized,
            double? maximumDriftSeconds,
            string message)
        {
            IsSynchronized =
                isSynchronized;

            MaximumDriftSeconds =
                maximumDriftSeconds;

            if (!string.IsNullOrWhiteSpace(
                message))
            {
                StatusMessage =
                    message;
            }
        }

        /// <summary>
        /// 그룹에 등록된 채널을 제거한다.
        ///
        /// 실제 Dahua 세션의 Stop과 Dispose는
        /// DahuaPlaybackEngine이 먼저 수행해야 한다.
        /// </summary>
        public void ClearChannels()
        {
            _channels.Clear();

            IsReady =
                false;

            IsSynchronized =
                false;

            MaximumDriftSeconds =
                null;
        }
    }
}