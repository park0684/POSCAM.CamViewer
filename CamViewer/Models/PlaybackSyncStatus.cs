using System;
using System.Collections.Generic;
using System.Linq;

namespace CamViewer.Models
{
    /// <summary>
    /// 좌/우 재생 채널 간 시간 동기화 상태이다.
    /// </summary>
    public sealed class PlaybackSyncStatus
    {
        /// <summary>
        /// 채널별 재생시간 목록.
        /// </summary>
        public List<PlaybackChannelTimeStatus> Channels { get; private set; }

        /// <summary>
        /// 가장 빠른 채널과 가장 느린 채널의 시간 차이.
        /// </summary>
        public TimeSpan? MaxDifference { get; set; }

        /// <summary>
        /// Provider 실제 시간 기준으로 계산했는지 여부.
        /// </summary>
        public bool UsesProviderTime { get; set; }

        /// <summary>
        /// 동기화 상태 객체를 초기화한다.
        /// </summary>
        public PlaybackSyncStatus()
        {
            Channels = new List<PlaybackChannelTimeStatus>();
        }

        /// <summary>
        /// 상태 표시 문자열을 생성한다.
        /// </summary>
        public string ToDisplayText()
        {
            if (Channels.Count <= 1 || !MaxDifference.HasValue)
            {
                return "동기화: 비교할 채널이 부족합니다.";
            }

            string sourceText = UsesProviderTime
                ? "실제시간"
                : "추정시간";

            return "동기화("
                + sourceText
                + "): 최대 차이 "
                + MaxDifference.Value.TotalSeconds.ToString("0.0")
                + "초";
        }

        /// <summary>
        /// 채널 시간 목록으로 동기화 상태를 생성한다.
        /// </summary>
        public static PlaybackSyncStatus FromChannels(
            List<PlaybackChannelTimeStatus> channels)
        {
            var status = new PlaybackSyncStatus();

            if (channels != null)
            {
                status.Channels.AddRange(channels);
            }

            List<DateTime> validTimes =
                status.Channels
                    .Where(x => x.PlaybackTime.HasValue)
                    .Select(x => x.PlaybackTime.Value)
                    .ToList();

            if (validTimes.Count >= 2)
            {
                DateTime minTime = validTimes.Min();
                DateTime maxTime = validTimes.Max();

                status.MaxDifference = maxTime - minTime;
            }

            status.UsesProviderTime =
                status.Channels.Any(x => x.IsProviderTime);

            return status;
        }
    }
}