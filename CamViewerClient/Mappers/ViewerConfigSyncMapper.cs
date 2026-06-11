using System;
using System.Linq;
using CamViewerClient.Enums;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Config;

namespace CamViewerClient.Mappers
{
    /// <summary>
    /// 로컬 ViewerConfig와 AuthServer Config API DTO를 변환한다.
    ///
    /// AuthServer는 현재 단일 NVR 설정 기준이다.
    /// 따라서 로컬 NvrList 중 가장 작은 NvrNo를 가진 NVR을 서버 동기화 대상으로 사용한다.
    /// </summary>
    public static class ViewerConfigSyncMapper
    {
        /// <summary>
        /// 로컬 ViewerConfig를 AuthServer 설정 동기화 요청 DTO로 변환한다.
        ///
        /// AuthServer ConfigSyncRequest 기준:
        /// - Token
        /// - Hwid
        /// - NvrConfig
        /// - Channels
        /// - ConfigVersion
        /// - ModifiedBy
        /// - ProgramVersion
        /// </summary>
        /// <param name="config">로컬 캠뷰어 설정.</param>
        /// <param name="token">캠뷰어 인증 토큰.</param>
        /// <param name="hwid">현재 장비 HWID.</param>
        /// <param name="modifiedBy">수정자.</param>
        /// <param name="programVersion">캠뷰어 프로그램 버전.</param>
        /// <returns>AuthServer 설정 동기화 요청 DTO.</returns>
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

            NvrConfig nvr =
                config.NvrList
                    .Where(x => x != null)
                    .OrderBy(x => x.NvrNo)
                    .FirstOrDefault();

            if (nvr == null)
            {
                throw new InvalidOperationException(
                    "서버에 동기화할 NVR 설정이 없습니다.");
            }

            /*
             * 서버 버전 생성 정책:
             * - LocalModified 상태이면 빈 문자열 전송
             * - AuthServer가 새 ConfigVersion을 생성
             */
            string requestConfigVersion =
                config.SyncStatus == ViewerConfigSyncStatus.LocalModified
                    ? string.Empty
                    : config.ConfigVersion ?? string.Empty;

            var request = new ConfigSyncRequestDto
            {
                Token = token ?? string.Empty,
                Hwid = hwid ?? string.Empty,
                ModifiedBy = modifiedBy ?? string.Empty,
                ProgramVersion = programVersion ?? string.Empty,

                // 중요: 수정 업로드 시 빈 문자열이어야 서버가 새 버전을 생성한다.
                ConfigVersion = requestConfigVersion,

                NvrConfig = new NvrConfigDto
                {
                    NvrId = nvr.UserId ?? string.Empty,
                    NvrPassword = nvr.Password ?? string.Empty,
                    NvrIp = nvr.Host ?? string.Empty,
                    NvrPort = nvr.Port,
                    NvrChannels = nvr.ChannelCount,

                    // 서버에서는 request.ConfigVersion 기준으로 저장하므로
                    // 여기 값은 보조값이다. 동일하게 맞춰둔다.
                    NvrVersion = requestConfigVersion
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
        ///
        /// AuthServer는 ConfigVersion을 문자열로 반환하므로
        /// 숫자 변환 없이 그대로 저장한다.
        /// </summary>
        /// <param name="config">로컬 캠뷰어 설정.</param>
        /// <param name="response">AuthServer 설정 동기화 응답.</param>
        public static void ApplySyncResponse(
            ViewerConfig config,
            ConfigSyncResponseDto response)
        {
            if (config == null || response == null)
            {
                return;
            }

            config.ConfigVersion =
                response.ConfigVersion ?? string.Empty;

            config.StoreCode =
                response.StoreCode;

            config.SyncStatus =
                ViewerConfigSyncStatus.Synced;

            config.LastUploadedAtUtc =
                DateTime.UtcNow;
        }
    }
}