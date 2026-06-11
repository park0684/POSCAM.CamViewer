using System;
using CamViewerClient.Enums;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Config;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// AuthServer 설정 응답 DTO와 로컬 ViewerConfig 모델을 변환한다.
    ///
    /// AuthServer 최신 설정 응답은 단일 NVR 설정 + 채널 목록 구조이고,
    /// CamViewer 로컬 설정은 NvrList + CounterMapList 구조이다.
    /// </summary>
    public static class ViewerConfigApiMapper
    {
        private const int DefaultNvrNo = 1;

        /*
         * 현재 AuthServer 응답에는 제조사, 연결 방식, ProviderKey가 포함되어 있지 않다.
         * 현재 CamViewer 테스트 기준은 Dahua SDK이므로 기본값으로 보정한다.
         *
         * 향후 제조사를 서버에서 관리하려면 AuthServer의 NvrConfigDto에
         * Vendor, ConnectionType, ProviderKey를 추가하는 것이 맞다.
         */
        private const string DefaultVendor = "Dahua";
        private const string DefaultConnectionType = "SDK";
        private const string DefaultProviderKey = "DAHUA_SDK";

        /// <summary>
        /// 로컬 ViewerConfig를 서버 응답 DTO 형태로 변환한다.
        ///
        /// 현재 최신 설정 다운로드 흐름에서는 사용하지 않지만,
        /// 기존 코드 호환을 위해 유지한다.
        /// 서버 업로드는 ViewerConfigSyncMapper를 사용하는 것이 기준이다.
        /// </summary>
        /// <param name="source">로컬 캠뷰어 설정.</param>
        /// <returns>서버 설정 DTO.</returns>
        public static ViewerConfigServerDto ToServerDto(
            ViewerConfig source)
        {
            if (source == null)
            {
                return null;
            }

            var target = new ViewerConfigServerDto
            {
                StoreCode = source.StoreCode,
                ConfigVersion = source.ConfigVersion ?? string.Empty,
                NvrConfig = new NvrConfigDto(),
                Channels = new System.Collections.Generic.List<ChannelConfigDto>()
            };

            NvrConfig firstNvr =
                GetFirstNvr(source);

            if (firstNvr != null)
            {
                target.NvrConfig =
                    ToApiNvrConfig(
                        firstNvr,
                        target.ConfigVersion);
            }

            if (source.CounterMapList != null)
            {
                foreach (CounterMap map in source.CounterMapList)
                {
                    if (map == null)
                    {
                        continue;
                    }

                    target.Channels.Add(
                        new ChannelConfigDto
                        {
                            PosNo = map.CounterNo,
                            ChannelNo = map.ChannelNo,
                            Screen = (int)map.ScreenPosition
                        });
                }
            }

            return target;
        }

        /// <summary>
        /// AuthServer 최신 설정 응답 DTO를 로컬 ViewerConfig로 변환한다.
        /// </summary>
        /// <param name="source">AuthServer 최신 설정 응답 DTO.</param>
        /// <returns>로컬 캠뷰어 설정.</returns>
        public static ViewerConfig ToLocalConfig(
            ViewerConfigServerDto source)
        {
            if (source == null)
            {
                return null;
            }

            var target = new ViewerConfig
            {
                StoreCode = source.StoreCode,
                ConfigVersion = source.ConfigVersion ?? string.Empty,
                SyncStatus = ViewerConfigSyncStatus.Synced,
                LastDownloadedAtUtc = DateTime.UtcNow,
                PlaybackOption = new PlaybackOption(),
                VideoRenderMode = VideoRenderMode.KeepAspectRatio
            };

            NvrConfig localNvr =
                ToLocalNvrConfig(
                    source.NvrConfig);

            if (localNvr != null)
            {
                target.NvrList.Add(
                    localNvr);
            }

            if (source.Channels != null)
            {
                foreach (ChannelConfigDto channel in source.Channels)
                {
                    if (channel == null)
                    {
                        continue;
                    }

                    target.CounterMapList.Add(
                        new CounterMap
                        {
                            CounterNo = channel.PosNo,
                            NvrNo = DefaultNvrNo,
                            ChannelNo = channel.ChannelNo,
                            ScreenPosition = ToScreenPosition(channel.Screen)
                        });
                }
            }

            NormalizeNextNvrNo(
                target);

            return target;
        }

        /// <summary>
        /// AuthServer NVR DTO를 로컬 NvrConfig로 변환한다.
        /// </summary>
        /// <param name="source">AuthServer NVR 설정 DTO.</param>
        /// <returns>로컬 NVR 설정.</returns>
        private static NvrConfig ToLocalNvrConfig(
            NvrConfigDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new NvrConfig
            {
                NvrNo = DefaultNvrNo,
                Vendor = DefaultVendor,
                ConnectionType = DefaultConnectionType,
                ProviderKey = DefaultProviderKey,
                Host = source.NvrIp ?? string.Empty,
                Port = source.NvrPort,
                ChannelCount = source.NvrChannels.GetValueOrDefault(),
                UserId = source.NvrId ?? string.Empty,
                Password = source.NvrPassword ?? string.Empty
            };
        }

        /// <summary>
        /// 로컬 NvrConfig를 AuthServer NVR DTO로 변환한다.
        /// </summary>
        /// <param name="source">로컬 NVR 설정.</param>
        /// <param name="configVersion">설정 버전.</param>
        /// <returns>AuthServer NVR DTO.</returns>
        private static NvrConfigDto ToApiNvrConfig(
            NvrConfig source,
            string configVersion)
        {
            if (source == null)
            {
                return new NvrConfigDto();
            }

            return new NvrConfigDto
            {
                NvrId = source.UserId ?? string.Empty,
                NvrPassword = source.Password ?? string.Empty,
                NvrIp = source.Host ?? string.Empty,
                NvrPort = source.Port,
                NvrChannels = source.ChannelCount,
                NvrVersion = configVersion ?? string.Empty
            };
        }

        /// <summary>
        /// 서버 screen 값을 로컬 ScreenPosition enum으로 변환한다.
        /// </summary>
        /// <param name="screen">서버 화면 위치 값.</param>
        /// <returns>로컬 화면 위치 enum.</returns>
        private static ScreenPosition ToScreenPosition(
            int screen)
        {
            if (screen == 1)
            {
                return (ScreenPosition)1;
            }

            return (ScreenPosition)0;
        }

        /// <summary>
        /// 로컬 설정에서 첫 번째 NVR 설정을 가져온다.
        /// </summary>
        /// <param name="config">로컬 캠뷰어 설정.</param>
        /// <returns>첫 번째 NVR 설정.</returns>
        private static NvrConfig GetFirstNvr(
            ViewerConfig config)
        {
            if (config == null || config.NvrList == null)
            {
                return null;
            }

            NvrConfig selected = null;

            foreach (NvrConfig nvr in config.NvrList)
            {
                if (nvr == null)
                {
                    continue;
                }

                if (selected == null || nvr.NvrNo < selected.NvrNo)
                {
                    selected = nvr;
                }
            }

            return selected;
        }

        /// <summary>
        /// 다음 NVR 번호를 현재 로컬 NVR 목록 기준으로 보정한다.
        /// </summary>
        /// <param name="config">로컬 캠뷰어 설정.</param>
        private static void NormalizeNextNvrNo(
            ViewerConfig config)
        {
            if (config == null)
            {
                return;
            }

            int maxNvrNo = 0;

            if (config.NvrList != null)
            {
                foreach (NvrConfig nvr in config.NvrList)
                {
                    if (nvr != null && nvr.NvrNo > maxNvrNo)
                    {
                        maxNvrNo = nvr.NvrNo;
                    }
                }
            }

            config.NextNvrNo = maxNvrNo + 1;

            if (config.NextNvrNo <= 0)
            {
                config.NextNvrNo = 1;
            }
        }
    }
}