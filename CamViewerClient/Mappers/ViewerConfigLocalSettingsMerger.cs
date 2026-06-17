using CamViewerClient.Models.Config;
using System;
using System.Linq;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// 서버에서 다운로드한 설정에 기존 PC의 로컬 전용 설정을 병합한다.
    ///
    /// 서버 설정 다운로드 시 교체되는 값:
    /// - 매장코드
    /// - 서버 설정 버전
    /// - NVR 접속정보
    /// - 계산대 및 채널 매핑
    ///
    /// 기존 PC에서 유지하는 값:
    /// - 영상 재생 구간 옵션
    /// - 영상 표시 방식
    /// - Provider 선택 및 Provider별 추가 설정
    /// - 영상 원본 해상도 정보
    /// - 다음 NVR 번호
    /// - 마지막 업로드 일시
    /// </summary>
    public static class ViewerConfigLocalSettingsMerger
    {
        /// <summary>
        /// 서버 설정에 기존 로컬 전용 값을 병합한다.
        /// </summary>
        /// <param name="serverConfig">
        /// 서버에서 다운로드하여 변환한 설정.
        /// 서버 동기화 대상 값은 이 설정을 기준으로 사용한다.
        /// </param>
        /// <param name="existingLocalConfig">
        /// 다운로드 전에 PC에 저장되어 있던 기존 로컬 설정.
        /// 없으면 null을 허용한다.
        /// </param>
        /// <returns>
        /// 로컬 전용 설정이 병합된 서버 설정.
        /// </returns>
        public static ViewerConfig Merge(
            ViewerConfig serverConfig,
            ViewerConfig existingLocalConfig)
        {
            if (serverConfig == null)
            {
                throw new ArgumentNullException(
                    "serverConfig");
            }

            /*
             * 최초 설치처럼 기존 로컬 설정이 없다면
             * 서버 설정 변환 결과를 그대로 사용한다.
             */
            if (existingLocalConfig == null)
            {
                return serverConfig;
            }

            ApplyPlaybackOption(
                serverConfig,
                existingLocalConfig);

            serverConfig.VideoRenderMode =
                existingLocalConfig.VideoRenderMode;

            /*
             * 서버 다운로드 이력은 새 설정에 기록된 현재 값을 사용한다.
             * 마지막 업로드 이력은 이 PC의 로컬 이력이므로 유지한다.
             */
            serverConfig.LastUploadedAtUtc =
                existingLocalConfig.LastUploadedAtUtc;

            /*
             * 삭제된 NVR 번호를 다시 사용하지 않도록
             * 기존 NextNvrNo가 더 크면 해당 값을 유지한다.
             */
            if (existingLocalConfig.NextNvrNo
                > serverConfig.NextNvrNo)
            {
                serverConfig.NextNvrNo =
                    existingLocalConfig.NextNvrNo;
            }

            ApplyNvrLocalSettings(
                serverConfig,
                existingLocalConfig);

            ApplyCounterLocalSettings(
                serverConfig,
                existingLocalConfig);

            return serverConfig;
        }

        /// <summary>
        /// 영상 재생시간과 관련된 로컬 옵션을 복사한다.
        /// </summary>
        private static void ApplyPlaybackOption(
            ViewerConfig target,
            ViewerConfig source)
        {
            if (source.PlaybackOption == null)
            {
                return;
            }

            target.PlaybackOption =
                new PlaybackOption
                {
                    BeforeSeconds =
                        source.PlaybackOption.BeforeSeconds,

                    AfterCompleteSeconds =
                        source.PlaybackOption.AfterCompleteSeconds
                };
        }

        /// <summary>
        /// 서버가 제공하지 않는 NVR Provider 관련 값을 유지한다.
        ///
        /// 현재 서버는 단일 NVR 기준이므로 같은 NvrNo를 우선 찾고,
        /// 찾을 수 없으면 기존 로컬 설정의 첫 번째 NVR을 사용한다.
        /// 다중 NVR 정책은 CV-005에서 별도로 정리한다.
        /// </summary>
        private static void ApplyNvrLocalSettings(
            ViewerConfig target,
            ViewerConfig source)
        {
            if (target.NvrList == null
                || source.NvrList == null)
            {
                return;
            }

            foreach (NvrConfig targetNvr
                in target.NvrList.Where(x => x != null))
            {
                NvrConfig sourceNvr =
                    source.NvrList.FirstOrDefault(
                        x =>
                            x != null
                            && x.NvrNo == targetNvr.NvrNo);

                if (sourceNvr == null)
                {
                    sourceNvr =
                        source.NvrList
                            .Where(x => x != null)
                            .OrderBy(x => x.NvrNo)
                            .FirstOrDefault();
                }

                if (sourceNvr == null)
                {
                    continue;
                }

                /*
                 * 서버 DTO에 존재하지 않는 Provider 선택값은
                 * 이 PC의 로컬 설정을 유지한다.
                 */
                if (!string.IsNullOrWhiteSpace(
                    sourceNvr.Vendor))
                {
                    targetNvr.Vendor =
                        sourceNvr.Vendor;
                }

                if (!string.IsNullOrWhiteSpace(
                    sourceNvr.ConnectionType))
                {
                    targetNvr.ConnectionType =
                        sourceNvr.ConnectionType;
                }

                if (!string.IsNullOrWhiteSpace(
                    sourceNvr.ProviderKey))
                {
                    targetNvr.ProviderKey =
                        sourceNvr.ProviderKey;
                }

                /*
                 * Provider별 추가 설정값도 서버에서 관리하지 않으므로
                 * 기존 로컬 값을 복사한다.
                 */
                targetNvr.ProviderSettings.Clear();

                if (sourceNvr.ProviderSettings == null)
                {
                    continue;
                }

                foreach (var pair
                    in sourceNvr.ProviderSettings)
                {
                    targetNvr.ProviderSettings[pair.Key] =
                        pair.Value;
                }
            }
        }

        /// <summary>
        /// 서버 DTO에 존재하지 않는 계산대 영상 원본 크기를 유지한다.
        ///
        /// 계산대, 화면 위치, 채널번호가 모두 같은 경우에만
        /// 동일 영상 소스로 판단하여 크기를 복사한다.
        /// </summary>
        private static void ApplyCounterLocalSettings(
            ViewerConfig target,
            ViewerConfig source)
        {
            if (target.CounterMapList == null
                || source.CounterMapList == null)
            {
                return;
            }

            foreach (CounterMap targetMap
                in target.CounterMapList.Where(x => x != null))
            {
                CounterMap sourceMap =
                    source.CounterMapList.FirstOrDefault(
                        x =>
                            x != null
                            && x.CounterNo
                                == targetMap.CounterNo
                            && x.ScreenPosition
                                == targetMap.ScreenPosition
                            && x.ChannelNo
                                == targetMap.ChannelNo);

                if (sourceMap == null)
                {
                    continue;
                }

                targetMap.VideoWidth =
                    sourceMap.VideoWidth;

                targetMap.VideoHeight =
                    sourceMap.VideoHeight;
            }
        }
    }
}