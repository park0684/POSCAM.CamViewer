using Newtonsoft.Json;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// AuthServer의 캠뷰어 설정 버전 확인 응답 DTO이다.
    ///
    /// AuthServer ConfigVersionResponse 기준:
    /// - StoreCode
    /// - ConfigVersion
    /// - IsLatest
    /// </summary>
    public sealed class ConfigVersionResponseDto
    {
        /// <summary>
        /// 설정이 적용되는 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 서버에 저장된 설정 버전.
        /// </summary>
        public string ConfigVersion { get; set; }

        /// <summary>
        /// 로컬 설정이 서버 기준 최신인지 여부.
        /// </summary>
        public bool IsLatest { get; set; }

        /// <summary>
        /// 기존 코드 호환용 속성.
        ///
        /// AuthServer는 HasConfig를 내려주지 않는다.
        /// 버전 조회 API가 성공했다는 것은 서버에 NVR 설정이 존재한다는 의미이므로
        /// 기존 흐름에서 HasConfig를 참조하더라도 true로 처리한다.
        /// </summary>
        [JsonIgnore]
        public bool HasConfig
        {
            get { return true; }
        }

        /// <summary>
        /// 기존 코드 호환용 속성.
        ///
        /// 기존 CamViewer 코드에서 ServerConfigVersion 이름을 사용하던 구간을
        /// 단계적으로 정리하기 위한 임시 호환 속성이다.
        /// </summary>
        [JsonIgnore]
        public string ServerConfigVersion
        {
            get { return ConfigVersion; }
        }

        /// <summary>
        /// ConfigVersionResponseDto를 초기화한다.
        /// </summary>
        public ConfigVersionResponseDto()
        {
            ConfigVersion = string.Empty;
        }
    }
}