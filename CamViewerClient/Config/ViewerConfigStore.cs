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
        /// 로컬 설정 파일 또는 복구 가능한 백업 파일이
        /// 존재하는지 확인한다.
        /// </summary>
        public bool Exists()
        {
            string configFilePath =
                _pathProvider.GetConfigFilePath();

            string backupFilePath =
                configFilePath + ".bak";

            /*
             * 기본 설정이 없어도 백업 설정이 존재하면
             * Load()에서 자동 복구를 시도할 수 있으므로
             * 설정이 존재하는 것으로 판단한다.
             */
            return File.Exists(configFilePath)
                || File.Exists(backupFilePath);
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
        ///
        /// 기본 설정 파일을 먼저 검증하고,
        /// 불러오기에 실패하면 백업 설정을 검증하여
        /// 기본 설정 파일을 자동 복구한다.
        /// </summary>
        /// <returns>캠뷰어 설정 불러오기 결과.</returns>
        public ClientResult<ViewerConfig> Load()
        {
            string configFilePath =
                _pathProvider.GetConfigFilePath();

            string backupFilePath =
                configFilePath + ".bak";

            /*
             * 1. 기본 설정 파일을 먼저 불러온다.
             *
             * 파일 읽기뿐 아니라 다음 단계까지 모두 성공해야
             * 정상 설정으로 판단한다.
             *
             * - 암호화 해제
             * - JSON 역직렬화
             * - 설정 유효성 검증
             */
            ClientResult<ViewerConfig> primaryResult =
                LoadFromFile(
                    configFilePath,
                    "기본 설정");

            if (primaryResult.Success
                && primaryResult.Data != null)
            {
                return primaryResult;
            }

            /*
             * 2. 기본 설정이 없거나 손상된 경우
             * 백업 설정 파일을 불러온다.
             */
            ClientResult<ViewerConfig> backupResult =
                LoadFromFile(
                    backupFilePath,
                    "백업 설정");

            if (!backupResult.Success
                || backupResult.Data == null)
            {
                /*
                 * 기본 설정과 백업 설정이 모두 실패한 경우
                 * 두 오류 내용을 함께 반환한다.
                 */
                return ClientResult<ViewerConfig>.Fail(
                    "기본 설정과 백업 설정을 모두 불러오지 못했습니다."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "[기본 설정]"
                    + Environment.NewLine
                    + primaryResult.Message
                    + Environment.NewLine
                    + Environment.NewLine
                    + "[백업 설정]"
                    + Environment.NewLine
                    + backupResult.Message,
                    "CONFIG_AND_BACKUP_LOAD_FAILED");
            }

            /*
             * 3. 백업 설정이 정상이라면
             * 백업 파일을 기본 설정 파일로 복구한다.
             *
             * 복구 중에도 유효한 .bak 파일을 유지하기 위해
             * File.Replace()는 사용하지 않는다.
             */
            string restoreErrorMessage;

            bool restored =
                TryRestorePrimaryFromBackup(
                    backupFilePath,
                    configFilePath,
                    out restoreErrorMessage);

            if (restored)
            {
                return ClientResult<ViewerConfig>.Ok(
                    backupResult.Data,
                    "기본 설정 파일이 손상되어 "
                    + "백업 설정으로 자동 복구했습니다.");
            }

            /*
             * 백업 설정 자체는 정상이나
             * 파일 복구만 실패한 경우에는
             * 백업 데이터를 이용해 프로그램 실행은 허용한다.
             *
             * 백업 파일은 삭제하지 않으므로
             * 다음 실행에서도 다시 복구를 시도할 수 있다.
             */
            return ClientResult<ViewerConfig>.Ok(
                backupResult.Data,
                "백업 설정을 정상적으로 불러왔지만 "
                + "기본 설정 파일 복구에는 실패했습니다."
                + Environment.NewLine
                + restoreErrorMessage);
        }

        /// <summary>
        /// 지정된 설정 파일을 읽고 복호화, 역직렬화,
        /// 유효성 검증까지 수행한다.
        /// </summary>
        /// <param name="filePath">읽을 설정 파일 경로.</param>
        /// <param name="sourceName">
        /// 오류 메시지에 표시할 설정 파일 구분명.
        /// </param>
        /// <returns>검증된 ViewerConfig 또는 실패 결과.</returns>
        private ClientResult<ViewerConfig> LoadFromFile(
            string filePath,
            string sourceName)
        {
            if (string.IsNullOrWhiteSpace(filePath)
                || !File.Exists(filePath))
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName + " 파일이 없습니다.",
                    "CONFIG_FILE_NOT_FOUND");
            }

            string protectedText;

            try
            {
                protectedText =
                    File.ReadAllText(
                        filePath,
                        Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName
                    + " 파일을 읽을 수 없습니다. "
                    + ex.Message,
                    "CONFIG_FILE_READ_FAILED");
            }

            if (string.IsNullOrWhiteSpace(protectedText))
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName + " 파일의 내용이 비어 있습니다.",
                    "CONFIG_FILE_EMPTY");
            }

            ClientResult<string> unprotectResult =
                _dataProtector.Unprotect(
                    protectedText);

            if (!unprotectResult.Success)
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName
                    + " 복호화에 실패했습니다."
                    + Environment.NewLine
                    + unprotectResult.Message,
                    unprotectResult.ErrorCode);
            }

            ClientResult<ViewerConfig> deserializeResult =
                _serializer.Deserialize(
                    unprotectResult.Data);

            if (!deserializeResult.Success
                || deserializeResult.Data == null)
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName
                    + " 역직렬화에 실패했습니다."
                    + Environment.NewLine
                    + deserializeResult.Message,
                    deserializeResult.ErrorCode);
            }

            ConfigValidationResult validationResult =
                _validator.Validate(
                    deserializeResult.Data);

            if (!validationResult.IsValid)
            {
                return ClientResult<ViewerConfig>.Fail(
                    sourceName
                    + " 내용이 올바르지 않습니다."
                    + Environment.NewLine
                    + BuildValidationMessage(
                        validationResult),
                    "CONFIG_LOAD_INVALID");
            }

            return ClientResult<ViewerConfig>.Ok(
                deserializeResult.Data,
                sourceName + "을 불러왔습니다.");
        }

        /// <summary>
        /// 정상적으로 검증된 백업 설정 파일을
        /// 기본 설정 파일로 복구한다.
        ///
        /// 유효한 백업 파일을 보존하기 위해
        /// 백업 파일 자체를 이동하지 않고 임시 파일로 복사한 뒤
        /// 기본 설정 파일을 교체한다.
        /// </summary>
        /// <param name="backupFilePath">백업 설정 파일 경로.</param>
        /// <param name="configFilePath">기본 설정 파일 경로.</param>
        /// <param name="errorMessage">복구 실패 오류 메시지.</param>
        /// <returns>복구 성공 여부.</returns>
        private static bool TryRestorePrimaryFromBackup(
            string backupFilePath,
            string configFilePath,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;

            string restoreTempFilePath =
                configFilePath + ".restore.tmp";

            try
            {
                /*
                 * 이전 복구 작업에서 임시 파일이 남았다면 제거한다.
                 */
                TryDeleteTempFile(
                    restoreTempFilePath);

                /*
                 * 정상 검증된 백업 파일을 임시 파일로 복사한다.
                 * 원본 .bak 파일은 그대로 유지한다.
                 */
                File.Copy(
                    backupFilePath,
                    restoreTempFilePath,
                    true);

                /*
                 * 손상된 기본 설정 파일이 존재하면 제거한다.
                 *
                 * 여기에서 File.Replace()를 사용하면
                 * 손상된 기본 파일이 정상 백업 파일을 덮어쓸 수 있으므로
                 * 사용하지 않는다.
                 */
                if (File.Exists(configFilePath))
                {
                    File.Delete(configFilePath);
                }

                File.Move(
                    restoreTempFilePath,
                    configFilePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMessage =
                    ex.Message;

                TryDeleteTempFile(
                    restoreTempFilePath);

                return false;
            }
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
