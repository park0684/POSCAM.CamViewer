using System.Collections.Generic;
using CamViewerClient.Enums;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Config;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// 로컬 ViewerConfig 모델과 서버 API DTO를 변환한다.
    ///
    /// 로컬 저장 파일은 DPAPI로 암호화하지만,
    /// 서버 API 전송은 DTO로 변환하여 HTTPS 요청 본문으로 전송한다.
    /// </summary>
    public static class ViewerConfigApiMapper
    {
        /// <summary>
        /// 로컬 ViewerConfig를 서버 DTO로 변환한다.
        /// </summary>
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
                ConfigVersion = source.ConfigVersion,
                PlaybackOption = ToServerDto(source.PlaybackOption)
            };

            if (source.NvrList != null)
            {
                foreach (NvrConfig nvr in source.NvrList)
                {
                    NvrConfigServerDto dto = ToServerDto(nvr);

                    if (dto != null)
                    {
                        target.NvrList.Add(dto);
                    }
                }
            }

            if (source.CounterMapList != null)
            {
                foreach (CounterMap counterMap in source.CounterMapList)
                {
                    CounterMapServerDto dto = ToServerDto(counterMap);

                    if (dto != null)
                    {
                        target.CounterMapList.Add(dto);
                    }
                }
            }

            return target;
        }

        /// <summary>
        /// 서버 DTO를 로컬 ViewerConfig로 변환한다.
        /// </summary>
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
                ConfigVersion = source.ConfigVersion,
                PlaybackOption = ToLocalConfig(source.PlaybackOption)
            };

            if (source.NvrList != null)
            {
                foreach (NvrConfigServerDto nvr in source.NvrList)
                {
                    NvrConfig local = ToLocalConfig(nvr);

                    if (local != null)
                    {
                        target.NvrList.Add(local);
                    }
                }
            }

            if (source.CounterMapList != null)
            {
                foreach (CounterMapServerDto counterMap in source.CounterMapList)
                {
                    CounterMap local = ToLocalConfig(counterMap);

                    if (local != null)
                    {
                        target.CounterMapList.Add(local);
                    }
                }
            }

            NormalizeNextNvrNo(target);

            return target;
        }

        private static NvrConfigServerDto ToServerDto(
            NvrConfig source)
        {
            if (source == null)
            {
                return null;
            }

            var target = new NvrConfigServerDto
            {
                NvrNo = source.NvrNo,
                Vendor = source.Vendor,
                ConnectionType = source.ConnectionType,
                ProviderKey = source.ProviderKey,
                Host = source.Host,
                Port = source.Port,
                ChannelCount = source.ChannelCount,
                UserId = source.UserId,
                Password = source.Password
            };

            CopyDictionary(
                source.ProviderSettings,
                target.ProviderSettings);

            return target;
        }

        private static NvrConfig ToLocalConfig(
            NvrConfigServerDto source)
        {
            if (source == null)
            {
                return null;
            }

            var target = new NvrConfig
            {
                NvrNo = source.NvrNo,
                Vendor = source.Vendor,
                ConnectionType = source.ConnectionType,
                ProviderKey = source.ProviderKey,
                Host = source.Host,
                Port = source.Port,
                ChannelCount = source.ChannelCount,
                UserId = source.UserId,
                Password = source.Password
            };

            CopyDictionary(
                source.ProviderSettings,
                target.ProviderSettings);

            return target;
        }

        private static CounterMapServerDto ToServerDto(
            CounterMap source)
        {
            if (source == null)
            {
                return null;
            }

            return new CounterMapServerDto
            {
                CounterNo = source.CounterNo,
                NvrNo = source.NvrNo,
                ChannelNo = source.ChannelNo,
                ScreenPosition = (int)source.ScreenPosition
            };
        }

        private static CounterMap ToLocalConfig(
            CounterMapServerDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new CounterMap
            {
                CounterNo = source.CounterNo,
                NvrNo = source.NvrNo,
                ChannelNo = source.ChannelNo,
                ScreenPosition = (ScreenPosition)source.ScreenPosition
            };
        }

        private static PlaybackOptionServerDto ToServerDto(
            PlaybackOption source)
        {
            if (source == null)
            {
                return new PlaybackOptionServerDto
                {
                    BeforeSeconds = 30,
                    AfterCompleteSeconds = 3
                };
            }

            return new PlaybackOptionServerDto
            {
                BeforeSeconds = source.BeforeSeconds,
                AfterCompleteSeconds = source.AfterCompleteSeconds
            };
        }

        private static PlaybackOption ToLocalConfig(
            PlaybackOptionServerDto source)
        {
            if (source == null)
            {
                return new PlaybackOption();
            }

            return new PlaybackOption
            {
                BeforeSeconds = source.BeforeSeconds,
                AfterCompleteSeconds = source.AfterCompleteSeconds
            };
        }

        private static void CopyDictionary(
            IDictionary<string, string> source,
            IDictionary<string, string> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> item in source)
            {
                target[item.Key] = item.Value;
            }
        }

        private static void NormalizeNextNvrNo(
            ViewerConfig config)
        {
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
