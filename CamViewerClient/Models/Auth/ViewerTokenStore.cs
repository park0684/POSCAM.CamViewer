using System;
using System.IO;
using System.Text;
using CamViewerClient.Models.Auth;
using CamViewerClient.Security;
using Newtonsoft.Json;
using CamViewerClient.Results;

namespace CamViewerClient.Auth
{
    /// <summary>
    /// 캠뷰어 인증 토큰의 로컬 저장, 불러오기, 삭제를 담당한다.
    ///
    /// 저장 흐름:
    /// ViewerAuthToken
    /// → JSON 직렬화
    /// → LocalDataProtector 암호화
    /// → viewer_token.dat 저장
    ///
    /// 불러오기 흐름:
    /// viewer_token.dat 읽기
    /// → LocalDataProtector 복호화
    /// → ViewerAuthToken 역직렬화
    /// </summary>
    public sealed class ViewerTokenStore
    {
        private readonly ViewerTokenPathProvider _pathProvider;
        private readonly LocalDataProtector _dataProtector;
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>
        /// ViewerTokenStore를 초기화한다.
        /// </summary>
        public ViewerTokenStore()
            : this(
                  new ViewerTokenPathProvider(),
                  new LocalDataProtector())
        {
        }

        /// <summary>
        /// ViewerTokenStore를 초기화한다.
        /// </summary>
        public ViewerTokenStore(
            ViewerTokenPathProvider pathProvider,
            LocalDataProtector dataProtector)
        {
            if (pathProvider == null)
            {
                throw new ArgumentNullException("pathProvider");
            }

            if (dataProtector == null)
            {
                throw new ArgumentNullException("dataProtector");
            }

            _pathProvider = pathProvider;
            _dataProtector = dataProtector;

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
        }

        /// <summary>
        /// 로컬 토큰 파일이 존재하는지 확인한다.
        /// </summary>
        public bool Exists()
        {
            return File.Exists(
                _pathProvider.GetTokenFilePath());
        }

        /// <summary>
        /// 로컬 토큰 파일 전체 경로를 반환한다.
        /// </summary>
        public string GetTokenFilePath()
        {
            return _pathProvider.GetTokenFilePath();
        }

        /// <summary>
        /// 인증 토큰을 로컬 파일에 암호화 저장한다.
        /// </summary>
        public ClientResult Save(ViewerAuthToken token)
        {
            if (token == null)
            {
                return ClientResult.Fail(
                    "저장할 인증 토큰 정보가 없습니다.",
                    "TOKEN_REQUIRED");
            }

            if (string.IsNullOrWhiteSpace(token.Token))
            {
                return ClientResult.Fail(
                    "저장할 인증 토큰 값이 없습니다.",
                    "TOKEN_VALUE_REQUIRED");
            }

            try
            {
                string json = JsonConvert.SerializeObject(
                    token,
                    _jsonSettings);

                ClientResult<string> protectResult =
                    _dataProtector.Protect(json);

                if (!protectResult.Success)
                {
                    return ClientResult.Fail(
                        protectResult.Message,
                        protectResult.ErrorCode);
                }

                string tokenFilePath =
                    _pathProvider.GetTokenFilePath();

                string tempFilePath =
                    tokenFilePath + ".tmp";

                string backupFilePath =
                    tokenFilePath + ".bak";

                File.WriteAllText(
                    tempFilePath,
                    protectResult.Data,
                    Encoding.UTF8);

                if (File.Exists(tokenFilePath))
                {
                    File.Replace(
                        tempFilePath,
                        tokenFilePath,
                        backupFilePath,
                        true);
                }
                else
                {
                    File.Move(
                        tempFilePath,
                        tokenFilePath);
                }

                return ClientResult.Ok(
                    "캠뷰어 인증 토큰을 저장했습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult.Fail(
                    "캠뷰어 인증 토큰 저장 중 오류가 발생했습니다. " + ex.Message,
                    "TOKEN_SAVE_FAILED");
            }
        }

        /// <summary>
        /// 로컬 파일에서 인증 토큰을 불러온다.
        /// </summary>
        public ClientResult<ViewerAuthToken> Load()
        {
            string tokenFilePath =
                _pathProvider.GetTokenFilePath();

            if (!File.Exists(tokenFilePath))
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "로컬 인증 토큰 파일이 없습니다.",
                    "TOKEN_FILE_NOT_FOUND");
            }

            try
            {
                string protectedText = File.ReadAllText(
                    tokenFilePath,
                    Encoding.UTF8);

                ClientResult<string> unprotectResult =
                    _dataProtector.Unprotect(protectedText);

                if (!unprotectResult.Success)
                {
                    return ClientResult<ViewerAuthToken>.Fail(
                        unprotectResult.Message,
                        unprotectResult.ErrorCode);
                }

                ViewerAuthToken token =
                    JsonConvert.DeserializeObject<ViewerAuthToken>(
                        unprotectResult.Data,
                        _jsonSettings);

                if (token == null)
                {
                    return ClientResult<ViewerAuthToken>.Fail(
                        "로컬 인증 토큰 정보를 복원할 수 없습니다.",
                        "TOKEN_DESERIALIZE_NULL");
                }

                return ClientResult<ViewerAuthToken>.Ok(
                    token,
                    "캠뷰어 인증 토큰을 불러왔습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "캠뷰어 인증 토큰 불러오기 중 오류가 발생했습니다. " + ex.Message,
                    "TOKEN_LOAD_FAILED");
            }
        }

        /// <summary>
        /// 로컬 인증 토큰 파일을 삭제한다.
        /// </summary>
        public ClientResult Delete()
        {
            string tokenFilePath =
                _pathProvider.GetTokenFilePath();

            try
            {
                if (File.Exists(tokenFilePath))
                {
                    File.Delete(tokenFilePath);
                }

                return ClientResult.Ok(
                    "캠뷰어 인증 토큰을 삭제했습니다.");
            }
            catch (Exception ex)
            {
                return ClientResult.Fail(
                    "캠뷰어 인증 토큰 삭제 중 오류가 발생했습니다. " + ex.Message,
                    "TOKEN_DELETE_FAILED");
            }
        }
    }
}