using System.Collections.Generic;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 설정 동기화 요청 DTO이다.
    ///
    /// AuthServer endpoint:
    /// POST api/config/sync
    /// </summary>
    public sealed class ConfigSyncRequestDto
    {
        /// <summary>
        /// 캠뷰어 인증 토큰.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// 현재 캠뷰어 장비 HWID.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// NVR 설정 정보.
        /// 현재 AuthServer는 단일 NVR 설정 기준이다.
        /// </summary>
        public NvrConfigDto NvrConfig { get; set; }

        /// <summary>
        /// 계산대별 채널 매핑 목록.
        /// </summary>
        public List<ChannelConfigDto> Channels { get; set; }

        /// <summary>
        /// 설정 버전.
        /// 비어 있으면 서버에서 새 버전을 생성한다.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 수정자.
        /// </summary>
        public string ModifiedBy { get; set; }

        /// <summary>
        /// 프로그램 버전.
        /// </summary>
        public string ProgramVersion { get; set; }

        /// <summary>
        /// 요청 DTO를 초기화한다.
        /// </summary>
        public ConfigSyncRequestDto()
        {
            Token = string.Empty;
            Hwid = string.Empty;
            NvrConfig = new NvrConfigDto();
            Channels = new List<ChannelConfigDto>();
            ConfigVersion = string.Empty;
            ModifiedBy = string.Empty;
            ProgramVersion = string.Empty;
        }
    }
}