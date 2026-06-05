using System;
using System.IO;
using System.Text;
using CamViewerClient.Security;
using CamViewerClient.Validation;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;

namespace CamViewerClient.Config
{
    /// <summary>
    /// 캠뷰어 로컬 설정 파일의 저장, 불러오기, 존재 여부 확인을 담당한다.
    ///
    /// 저장 흐름:
    /// ViewerConfig
    /// → 유효성 검증
    /// → JSON 직렬화
    /// → 로컬 암호화
    /// → viewer_config.dat 파일 저장
    ///
    /// 불러오기 흐름:
    /// viewer_config.dat 파일 읽기
    /// → 로컬 복호화
    /// → JSON 역직렬화
    /// → ViewerConfig 반환
    /// </summary>
    public sealed class ViewerConfigStore
    {
        private readonly ViewerConfigPathProvider _pathProvider;
        private readonly ViewerConfigSerializer _serializer;
        private readonly LocalDataProtector _dataProtector;
        private readonly ViewerConfigValidator _validator;

        /// <summary>
        /// ViewerConfigStore를 초기화한다.
        /// </summary>
        public ViewerConfigStore()
            : this(
                  new ViewerConfigPathProvider(),
                  new ViewerConfigSerializer(),
                  new LocalDataProtector(),
                  new ViewerConfigValidator())
        {
        }

        /// <summary>
        /// ViewerConfigStore를 초기화한다.
        /// 테스트 또는 확장을 위해 의존 객체를 외부에서 전달받을 수 있다.
        /// </summary>
        public ViewerConfigStore(
            ViewerConfigPathProvider pathProvider,
            ViewerConfigSerializer serializer,
            LocalDataProtector dataProtector,
            ViewerConfigValidator validator)
        {
            if (pathProvider == null)
            {
                throw new ArgumentNullException("pathProvider");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (dataProtector == null)
            {
                throw new ArgumentNullException("dataProtector");
            }

            if (validator == null)
            {
                throw new ArgumentNullException("validator");
            }

            _pathProvider = pathProvider;
            _serializer = serializer;
            _dataProtector = dataProtector;
            _validator = validator;
        }

        /// <summary>
        /// 로컬 설정 파일이 존재하는지 확인한다.
        /// </summary>
        public bool Exists()
        {
            string configFilePath =
                _pathProvider.GetConfigFilePath();

            return File.Exists(configFilePath);
        }

        /// <summary>
        /// 로컬 설정 파일의 전체 경로를 반환한다.
        /// </summary>
        public string GetConfigFilePath()
        {
            return _pathProvider.GetConfigFilePath();
        }

        /// <summary>
        /// 캠뷰어 설정을 로컬 설정 파일에 저장한다.
        ///
        /// 기존 설정 파일이 있는 경우 임시 파일을 먼저 생성한 뒤 교체한다.
        /// 저장 실패 시 기존 설정 파일이 최대한 유지되도록 처리한다.
        /// </summary>
        /// <param name="config">저장할 캠뷰어 설정.</param>
        /// <returns>저장 처리 결과.</returns>
        public ClientResult Save(ViewerConfig config)
        {
            if (config == null)
            {
                return ClientResult.Fail(
                    "저장할 캠뷰어 설정정보가 없습니다.",
                    "CONFIG_REQUIRED");
            }

            ConfigValidationResult validationResult =
                _validator.Validate(config);

            if (!validationResult.IsValid)
            {
                return ClientResult.Fail(
                    BuildValidationMessage(validationResult),
                    "CONFIG_INVALID");
            }

            ClientResult<string> serializeResult =
                _serializer.Serialize(config);

            if (!serializeResult.Success)
            {
                return ClientResult.Fail(
                    serializeResult.Message,
                    serializeResult.ErrorCode);
            }

            ClientResult<string> protectResult =
                _dataProtector.Protect(serializeResult.Data);

            if (!protectResult.Success)
            {
                return ClientResult.Fail(
                    protectResult.Message,
                    protectResult.ErrorCode);
            }

            string configFilePath =
                _pathProvider.GetConfigFilePath();

            string tempFilePath =
                configFilePath + ".tmp";

            string backupFilePath =
                configFilePath + ".bak";

            try
            {
                string directoryPath =
                    Path.GetDirectoryName(configFilePath);

                if (!string.IsNullOrWhiteSpace(directoryPath)
                    && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(
                    tempFilePath,
                    protectResult.Data,
                    Encoding.UTF8);

                if (File.Exists(configFilePath))
                {
                    File.Replace(
                        tempFilePath,
                        configFilePath,
                        backupFilePath,
                        true);
                }
                else
                {
                    File.Move(
                        tempFilePath,
                        configFilePath);
                }

                return ClientResult.Ok(
                    "캠뷰어 설정정보를 저장했습니다.");
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempFilePath);

                return ClientResult.Fail(
                    "캠뷰어 설정정보 저장 중 오류가 발생했습니다. " + ex.Message,
                    "CONFIG_SAVE_FAILED");
            }
        }

        /// <summary>
        /// 로컬 설정 파일에서 캠뷰어 설정을 불러온다.
        /// </summary>
        /// <returns>캠뷰어 설정 불러오기 결과.</returns>
        public ClientResult<ViewerConfig> Load()
        {
            string configFilePath =
                _pathProvider.GetConfigFilePath();

            if (!File.Exists(configFilePath))
            {
                return ClientResult<ViewerConfig>.Fail(
                    "로컬 캠뷰어 설정 파일이 없습니다.",
                    "CONFIG_FILE_NOT_FOUND");
            }

            string protectedText;

            try
            {
                protectedText = File.ReadAllText(
                    configFilePath,
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "로컬 캠뷰어 설정 파일을 읽을 수 없습니다. " + ex.Message,
                    "CONFIG_FILE_READ_FAILED");
            }

            ClientResult<string> unprotectResult =
                _dataProtector.Unprotect(protectedText);

            if (!unprotectResult.Success)
            {
                return ClientResult<ViewerConfig>.Fail(
                    unprotectResult.Message,
                    unprotectResult.ErrorCode);
            }

            ClientResult<ViewerConfig> deserializeResult =
                _serializer.Deserialize(unprotectResult.Data);

            if (!deserializeResult.Success)
            {
                return ClientResult<ViewerConfig>.Fail(
                    deserializeResult.Message,
                    deserializeResult.ErrorCode);
            }

            ConfigValidationResult validationResult =
                _validator.Validate(deserializeResult.Data);

            if (!validationResult.IsValid)
            {
                return ClientResult<ViewerConfig>.Fail(
                    BuildValidationMessage(validationResult),
                    "CONFIG_LOAD_INVALID");
            }

            return ClientResult<ViewerConfig>.Ok(
                deserializeResult.Data,
                "캠뷰어 설정정보를 불러왔습니다.");
        }

        /// <summary>
        /// 검증 오류 목록을 사용자 표시용 문자열로 변환한다.
        /// </summary>
        private static string BuildValidationMessage(
            ConfigValidationResult validationResult)
        {
            if (validationResult == null
                || validationResult.Errors == null
                || validationResult.Errors.Count == 0)
            {
                return "캠뷰어 설정정보가 올바르지 않습니다.";
            }

            var builder = new StringBuilder();

            foreach (ConfigValidationError error in validationResult.Errors)
            {
                builder.AppendLine("- " + error.Message);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 저장 실패 시 남아 있을 수 있는 임시 파일을 삭제한다.
        /// </summary>
        private static void TryDeleteTempFile(string tempFilePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempFilePath)
                    && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
                // 임시 파일 삭제 실패는 저장 실패 원인을 덮지 않기 위해 무시한다.
            }
        }
    }
}
