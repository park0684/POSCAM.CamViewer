using System.Collections.Generic;
using CamViewerClient.Models.Config;

namespace CamViewer.Helpers
{
    /// <summary>
    /// 캠뷰어 설정 모델의 복사본을 생성한다.
    ///
    /// 설정 화면에서 작업 중인 값이 원본 설정에 즉시 반영되지 않도록 사용한다.
    /// </summary>
    public static class ViewerConfigCloneHelper
    {
        /// <summary>
        /// ViewerConfig 전체 복사본을 생성한다.
        /// </summary>
        public static ViewerConfig Clone(ViewerConfig source)
        {
            if (source == null)
            {
                return new ViewerConfig();
            }

            var clone = new ViewerConfig
            {
                ConfigVersion = source.ConfigVersion,
                StoreCode = source.StoreCode,
                SyncStatus = source.SyncStatus,
                LastDownloadedAtUtc = source.LastDownloadedAtUtc,
                LastUploadedAtUtc = source.LastUploadedAtUtc,
                NextNvrNo = source.NextNvrNo,
                PlaybackOption = Clone(source.PlaybackOption)
            };

            if (source.NvrList != null)
            {
                foreach (NvrConfig nvr in source.NvrList)
                {
                    clone.NvrList.Add(Clone(nvr));
                }
            }

            if (source.CounterMapList != null)
            {
                foreach (CounterMap counterMap in source.CounterMapList)
                {
                    clone.CounterMapList.Add(Clone(counterMap));
                }
            }

            return clone;
        }

        /// <summary>
        /// NVR 설정 복사본을 생성한다.
        /// </summary>
        public static NvrConfig Clone(NvrConfig source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new NvrConfig
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

            if (source.ProviderSettings != null)
            {
                foreach (KeyValuePair<string, string> item in source.ProviderSettings)
                {
                    clone.ProviderSettings[item.Key] = item.Value;
                }
            }

            return clone;
        }

        /// <summary>
        /// 계산대 등록정보 복사본을 생성한다.
        /// </summary>
        public static CounterMap Clone(CounterMap source)
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
                ScreenPosition = source.ScreenPosition
            };
        }

        /// <summary>
        /// 재생 옵션 복사본을 생성한다.
        /// </summary>
        public static PlaybackOption Clone(PlaybackOption source)
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
    }
}