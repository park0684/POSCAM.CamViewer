using System.Collections.Generic;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// AuthServer의 캠뷰어 최신 설정 조회 응답 DTO이다.
    ///
    /// AuthServer ViewerConfigResponse 기준:
    /// - StoreCode
    /// - ConfigVersion
    /// - NvrConfig
    /// - Channels
    ///
    /// 주의:
    /// - 현재 AuthServer는 매장당 단일 NVR 설정 기준이다.
    /// - CamViewer 로컬 설정은 NvrList / CounterMapList 구조이므로
    ///   ViewerConfigApiMapper에서 로컬 구조로 변환한다.
    /// </summary>
    public sealed class ViewerConfigServerDto
    {
        /// <summary>
        /// 설정이 적용되는 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// AuthServer는 문자열 버전을 사용한다.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// NVR 접속 설정.
        /// </summary>
        public NvrConfigDto NvrConfig { get; set; }

        /// <summary>
        /// POS 번호와 NVR 채널 매핑 목록.
        /// </summary>
        public List<ChannelConfigDto> Channels { get; set; }

        /// <summary>
        /// ViewerConfigServerDto를 초기화한다.
        /// </summary>
        public ViewerConfigServerDto()
        {
            ConfigVersion = string.Empty;
            NvrConfig = new NvrConfigDto();
            Channels = new List<ChannelConfigDto>();
        }
    }
}