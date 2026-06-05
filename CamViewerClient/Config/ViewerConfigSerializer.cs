using Newtonsoft.Json;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Xml;

namespace CamViewerClient.Config
{
    /// <summary>
    /// ViewerConfig 모델을 JSON 문자열로 직렬화하거나
    /// JSON 문자열에서 ViewerConfig 모델로 복원한다.
    ///
    /// 주의:
    /// - 이 클래스는 암호화를 수행하지 않는다.
    /// - NVR 비밀번호가 포함될 수 있으므로 반환된 JSON 문자열을 평문 파일로 저장하면 안 된다.
    /// - 실제 파일 저장은 ViewerConfigStore에서 LocalDataProtector를 통해 암호화 후 수행한다.
    /// </summary>
    public sealed class ViewerConfigSerializer
    {
        private readonly JsonSerializerSettings _settings;

        /// <summary>
        /// ViewerConfigSerializer를 초기화한다.
        /// </summary>
        public ViewerConfigSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ObjectCreationHandling = ObjectCreationHandling.Auto
            };
        }

        /// <summary>
        /// ViewerConfig 모델을 JSON 문자열로 변환한다.
        /// </summary>
        public ClientResult<string> Serialize(ViewerConfig config)
        {
            if (config == null)
            {
                return ClientResult<string>.Fail(
                    "직렬화할 캠뷰어 설정정보가 없습니다.",
                    "CONFIG_REQUIRED");
            }

            try
            {
                string json = JsonConvert.SerializeObject(
                    config,
                    _settings);

                return ClientResult<string>.Ok(
                    json,
                    "캠뷰어 설정정보를 JSON으로 변환했습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult<string>.Fail(
                    "캠뷰어 설정정보 변환 중 오류가 발생했습니다. " + ex.Message,
                    "CONFIG_SERIALIZE_FAILED");
            }
        }

        /// <summary>
        /// JSON 문자열을 ViewerConfig 모델로 복원한다.
        /// </summary>
        public ClientResult<ViewerConfig> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return ClientResult<ViewerConfig>.Fail(
                    "복원할 캠뷰어 설정 문자열이 없습니다.",
                    "CONFIG_JSON_REQUIRED");
            }

            try
            {
                ViewerConfig config = JsonConvert.DeserializeObject<ViewerConfig>(
                    json,
                    _settings);

                if (config == null)
                {
                    return ClientResult<ViewerConfig>.Fail(
                        "캠뷰어 설정정보를 복원할 수 없습니다.",
                        "CONFIG_DESERIALIZE_NULL");
                }

                Normalize(config);

                return ClientResult<ViewerConfig>.Ok(
                    config,
                    "캠뷰어 설정정보를 복원했습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "캠뷰어 설정정보 복원 중 오류가 발생했습니다. " + ex.Message,
                    "CONFIG_DESERIALIZE_FAILED");
            }
        }

        /// <summary>
        /// 역직렬화 후 필수 기본값을 보정한다.
        ///
        /// 오래된 설정 파일에 NextNvrNo가 없거나 잘못 저장된 경우에도
        /// 현재 NVR 목록의 최대 번호보다 큰 값으로 보정한다.
        /// </summary>
        private static void Normalize(ViewerConfig config)
        {
            if (config.PlaybackOption == null)
            {
                config.PlaybackOption = new PlaybackOption();
            }

            int maxNvrNo = 0;

            if (config.NvrList != null)
            {
                foreach (NvrConfig nvr in config.NvrList)
                {
                    if (nvr == null)
                    {
                        continue;
                    }

                    if (nvr.NvrNo > maxNvrNo)
                    {
                        maxNvrNo = nvr.NvrNo;
                    }
                }
            }

            int minimumNextNvrNo = maxNvrNo + 1;

            if (config.NextNvrNo < minimumNextNvrNo)
            {
                config.NextNvrNo = minimumNextNvrNo;
            }

            if (config.NextNvrNo <= 0)
            {
                config.NextNvrNo = 1;
            }
        }
    }
}
