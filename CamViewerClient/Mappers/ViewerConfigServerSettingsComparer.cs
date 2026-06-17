using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// 두 ViewerConfig 사이에서 서버 동기화 대상 설정이
    /// 변경되었는지 확인한다.
    ///
    /// 현재 AuthServer 동기화 범위:
    /// - 가장 작은 NvrNo를 가진 첫 번째 NVR의 접속정보
    /// - 계산대번호
    /// - 채널번호
    /// - 화면 위치
    ///
    /// 비교하지 않는 로컬 전용 설정:
    /// - PlaybackOption
    /// - VideoRenderMode
    /// - Vendor
    /// - ConnectionType
    /// - ProviderKey
    /// - ProviderSettings
    /// - VideoWidth / VideoHeight
    /// - NextNvrNo
    /// </summary>
    public static class ViewerConfigServerSettingsComparer
    {
        /// <summary>
        /// 이전 설정과 현재 설정의 서버 동기화 대상 값이
        /// 서로 다른지 확인한다.
        /// </summary>
        /// <param name="previousConfig">
        /// 저장 전 기존 로컬 설정.
        /// 기존 설정이 없으면 null을 허용한다.
        /// </param>
        /// <param name="currentConfig">
        /// 설정 화면에서 저장하려는 현재 설정.
        /// </param>
        /// <returns>
        /// 서버 동기화 대상 값이 변경되었으면 true.
        /// </returns>
        public static bool HasServerManagedChanges(
            ViewerConfig previousConfig,
            ViewerConfig currentConfig)
        {
            if (currentConfig == null)
            {
                throw new ArgumentNullException(
                    "currentConfig");
            }

            /*
             * 기존 로컬 설정이 없는 최초 저장은
             * 서버에 등록할 설정 전체가 새로 만들어진 것으로 판단한다.
             */
            if (previousConfig == null)
            {
                return true;
            }

            NvrConfig previousNvr =
                GetServerTargetNvr(
                    previousConfig);

            NvrConfig currentNvr =
                GetServerTargetNvr(
                    currentConfig);

            if (!AreServerNvrSettingsEqual(
                previousNvr,
                currentNvr))
            {
                return true;
            }

            if (!AreServerChannelSettingsEqual(
                previousConfig,
                currentConfig))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 서버 업로드 Mapper와 동일하게
        /// 가장 작은 NvrNo의 NVR을 동기화 대상으로 선택한다.
        /// </summary>
        private static NvrConfig GetServerTargetNvr(
            ViewerConfig config)
        {
            if (config == null
                || config.NvrList == null)
            {
                return null;
            }

            return config.NvrList
                .Where(x => x != null)
                .OrderBy(x => x.NvrNo)
                .FirstOrDefault();
        }

        /// <summary>
        /// 서버에 전송되는 NVR 접속정보만 비교한다.
        ///
        /// 제조사와 Provider 관련 값은 로컬 전용이므로
        /// 비교 대상에 포함하지 않는다.
        /// </summary>
        private static bool AreServerNvrSettingsEqual(
            NvrConfig previous,
            NvrConfig current)
        {
            if (ReferenceEquals(
                previous,
                current))
            {
                return true;
            }

            if (previous == null
                || current == null)
            {
                return false;
            }

            return string.Equals(
                       previous.Host ?? string.Empty,
                       current.Host ?? string.Empty,
                       StringComparison.Ordinal)
                && previous.Port
                    == current.Port
                && previous.ChannelCount
                    == current.ChannelCount
                && string.Equals(
                       previous.UserId ?? string.Empty,
                       current.UserId ?? string.Empty,
                       StringComparison.Ordinal)
                && string.Equals(
                       previous.Password ?? string.Empty,
                       current.Password ?? string.Empty,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// 서버에 전송되는 계산대·채널·화면 위치 목록을 비교한다.
        ///
        /// 현재 서버 DTO에는 NvrNo가 없으므로
        /// NvrNo는 비교하지 않는다.
        /// 다중 NVR 서버 정책은 CV-005에서 별도로 처리한다.
        /// </summary>
        private static bool AreServerChannelSettingsEqual(
            ViewerConfig previousConfig,
            ViewerConfig currentConfig)
        {
            IList<CounterMap> previousMaps =
                GetOrderedServerChannelMaps(
                    previousConfig);

            IList<CounterMap> currentMaps =
                GetOrderedServerChannelMaps(
                    currentConfig);

            if (previousMaps.Count
                != currentMaps.Count)
            {
                return false;
            }

            for (int index = 0;
                index < previousMaps.Count;
                index++)
            {
                CounterMap previous =
                    previousMaps[index];

                CounterMap current =
                    currentMaps[index];

                if (previous.CounterNo
                        != current.CounterNo
                    || previous.ChannelNo
                        != current.ChannelNo
                    || previous.ScreenPosition
                        != current.ScreenPosition)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 안정적인 비교를 위해 서버 전송 대상 채널 목록을
        /// 동일한 순서로 정렬하여 반환한다.
        /// </summary>
        private static IList<CounterMap>
            GetOrderedServerChannelMaps(
                ViewerConfig config)
        {
            if (config == null
                || config.CounterMapList == null)
            {
                return new List<CounterMap>();
            }

            return config.CounterMapList
                .Where(x => x != null)
                .OrderBy(x => x.CounterNo)
                .ThenBy(x => (int)x.ScreenPosition)
                .ThenBy(x => x.ChannelNo)
                .ToList();
        }
    }
}