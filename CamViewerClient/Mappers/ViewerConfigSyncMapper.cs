using System;
using System.Globalization;
using System.Linq;
using CamViewerClient.Enums;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Config;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// 로컬 ViewerConfig와 AuthServer Config API DTO를 변환한다.
    ///
    /// 현재 AuthServer는 단일 NVR 설정 기준이므로,
    /// 로컬 NvrList 중 첫 번째 NVR만 서버 동기화 대상으로 사용한다.
    /// </summary>
    public static class ViewerConfigSyncMapper
    {
        /// <summary>
        /// 로컬 ViewerConfig를 AuthServer 설정 동기화 요청 DTO로 변환한다.
        /// </summary>
        public static ConfigSyncRequestDto ToSyncRequest(
            ViewerConfig config,
            string token,
            string hwid,
            string modifiedBy,
            string programVersion)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            NvrConfig nvr = config.NvrList
                .Where(x => x != null)
                .OrderBy(x => x.NvrNo)
                .FirstOrDefault();

            if (nvr == null)
            {
                throw new InvalidOperationException(
                    "서버에 동기화할 NVR 설정이 없습니다.");
            }

            var request = new ConfigSyncRequestDto
            {
                Token = token ?? string.Empty,
                Hwid = hwid ?? string.Empty,
                ModifiedBy = modifiedBy ?? string.Empty,
                ProgramVersion = programVersion ?? string.Empty,
                ConfigVersion = config.ConfigVersion <= 0
                    ? string.Empty
                    : config.ConfigVersion.ToString(CultureInfo.InvariantCulture),

                NvrConfig = new NvrConfigDto
                {
                    NvrId = nvr.UserId ?? string.Empty,
                    NvrPassword = nvr.Password ?? string.Empty,
                    NvrIp = nvr.Host ?? string.Empty,
                    NvrPort = nvr.Port,
                    NvrChannels = nvr.ChannelCount,
                    NvrVersion = config.ConfigVersion <= 0
                        ? string.Empty
                        : config.ConfigVersion.ToString(CultureInfo.InvariantCulture)
                }
            };

            if (config.CounterMapList != null)
            {
                foreach (CounterMap map in config.CounterMapList)
                {
                    if (map == null)
                    {
                        continue;
                    }

                    request.Channels.Add(
                        new ChannelConfigDto
                        {
                            PosNo = map.CounterNo,
                            ChannelNo = map.ChannelNo,
                            Screen = (int)map.ScreenPosition
                        });
                }
            }

            return request;
        }

        /// <summary>
        /// 서버 동기화 응답의 설정 버전을 로컬 ViewerConfig에 반영한다.
        /// </summary>
        public static void ApplySyncResponse(
            ViewerConfig config,
            ConfigSyncResponseDto response)
        {
            if (config == null || response == null)
            {
                return;
            }

            long version;

            if (long.TryParse(
                response.ConfigVersion,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out version))
            {
                config.ConfigVersion = version;
            }

            config.StoreCode = response.StoreCode;
            config.SyncStatus = ViewerConfigSyncStatus.Synced;
            config.LastUploadedAtUtc = DateTime.UtcNow;
        }
    }
}