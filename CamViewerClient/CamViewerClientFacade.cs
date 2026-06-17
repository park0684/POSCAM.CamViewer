using CamViewerClient.Api;
using CamViewerClient.Auth;
using CamViewerClient.Config;
using CamViewerClient.Mappers;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace CamViewerClient
{
    /// <summary>
    /// CamViewer 본체가 CamViewerClient 기능을 사용할 때 접근하는 단일 진입점이다.
    ///
    /// 현재 제공 기능:
    /// - 로컬 설정 저장/불러오기
    /// - AuthServer 설정 버전 확인
    /// - AuthServer 최신 설정 다운로드
    /// - AuthServer 설정 동기화
    /// </summary>
    public sealed class CamViewerClientFacade
    {
        private readonly ViewerConfigStore _configStore;
        private readonly ViewerConfigApiClient _configApiClient;
        private readonly ViewerAuthApiClient _authApiClient;
        private readonly ViewerTokenStore _tokenStore;

        /// <summary>
        /// CamViewerClientFacade를 초기화한다.
        /// </summary>
        public CamViewerClientFacade()
            : this(
                  new ViewerConfigStore(),
                  new ViewerConfigApiClient(),
                  new ViewerAuthApiClient(),
                  new ViewerTokenStore())
        {
        }

        /// <summary>
        /// CamViewerClientFacade를 초기화한다.
        /// </summary>
        public CamViewerClientFacade(
            ViewerConfigStore configStore,
            ViewerConfigApiClient configApiClient,
            ViewerAuthApiClient authApiClient,
            ViewerTokenStore tokenStore)
        {
            if (configStore == null)
            {
                throw new ArgumentNullException("configStore");
            }

            if (configApiClient == null)
            {
                throw new ArgumentNullException("configApiClient");
            }

            if (authApiClient == null)
            {
                throw new ArgumentNullException("authApiClient");
            }

            if (tokenStore == null)
            {
                throw new ArgumentNullException("tokenStore");
            }

            _configStore = configStore;
            _configApiClient = configApiClient;
            _authApiClient = authApiClient;
            _tokenStore = tokenStore;
        }

        /// <summary>
        /// 로컬 설정 파일이 존재하는지 확인한다.
        /// </summary>
        public bool HasLocalConfig()
        {
            return _configStore.Exists();
        }

        /// <summary>
        /// 로컬 설정 파일의 전체 경로를 반환한다.
        /// </summary>
        public string GetLocalConfigFilePath()
        {
            return _configStore.GetConfigFilePath();
        }

        /// <summary>
        /// 로컬 설정 파일에서 캠뷰어 설정을 불러온다.
        /// </summary>
        public ClientResult<ViewerConfig> LoadLocalConfig()
        {
            return _configStore.Load();
        }

        /// <summary>
        /// 캠뷰어 설정을 로컬 설정 파일에 저장한다.
        /// </summary>
        public ClientResult SaveLocalConfig(ViewerConfig config)
        {
            return _configStore.Save(config);
        }

        /// <summary>
        /// AuthServer 기준 서버 설정 버전을 확인한다.
        ///
        /// AuthServer ConfigVersionRequest 기준:
        /// - Token
        /// - Hwid
        /// - LocalConfigVersion
        /// - ProgramVersion
        ///
        /// 주의:
        /// - StoreCode, DeviceCode는 요청 Body로 보내지 않는다.
        /// - AuthServer는 Token payload에서 StoreCode, DeviceCode를 추출한다.
        /// </summary>
        /// <param name="token">캠뷰어 인증 토큰.</param>
        /// <param name="hwid">현재 PC의 HWID.</param>
        /// <param name="localConfig">현재 로컬 설정. 없으면 null 가능.</param>
        /// <param name="programVersion">캠뷰어 프로그램 버전.</param>
        /// <param name="cancellationToken">비동기 취소 토큰.</param>
        /// <returns>서버 설정 버전 확인 결과.</returns>
        public Task<ClientResult<ConfigVersionResponseDto>> GetServerConfigVersionAsync(
            string token,
            string hwid,
            ViewerConfig localConfig,
            string programVersion,
            CancellationToken cancellationToken)
        {
            var request = new ConfigVersionRequestDto
            {
                Token = token ?? string.Empty,
                Hwid = hwid ?? string.Empty,
                LocalConfigVersion = GetLocalConfigVersionText(localConfig),
                ProgramVersion = programVersion ?? string.Empty
            };

            return _configApiClient.GetVersionAsync(
                request,
                cancellationToken);
        }

        /// <summary>
        /// AuthServer에서 최신 설정을 다운로드하고 로컬 ViewerConfig로 변환한다.
        ///
        /// AuthServer ConfigLatestRequest 기준:
        /// - Token
        /// - Hwid
        /// - LocalConfigVersion
        /// - ProgramVersion
        ///
        /// 주의:
        /// - StoreCode, DeviceCode는 요청 Body로 보내지 않는다.
        /// - AuthServer는 Token payload에서 StoreCode, DeviceCode를 추출한다.
        /// </summary>
        /// <param name="token">캠뷰어 인증 토큰.</param>
        /// <param name="hwid">현재 PC의 HWID.</param>
        /// <param name="localConfigVersion">현재 로컬 설정 버전. 로컬 설정이 없으면 빈 문자열.</param>
        /// <param name="programVersion">캠뷰어 프로그램 버전.</param>
        /// <param name="cancellationToken">비동기 취소 토큰.</param>
        /// <returns>서버에서 다운로드한 로컬 ViewerConfig.</returns>
        public async Task<ClientResult<ViewerConfig>> DownloadServerConfigAsync(
            string token,
            string hwid,
            string localConfigVersion,
            string programVersion,
            CancellationToken cancellationToken)
        {
            var request = new ConfigLatestRequestDto
            {
                Token = token ?? string.Empty,
                Hwid = hwid ?? string.Empty,
                LocalConfigVersion = localConfigVersion ?? string.Empty,
                ProgramVersion = programVersion ?? string.Empty
            };

            ClientResult<ViewerConfigServerDto> latestResult =
                await _configApiClient.GetLatestConfigAsync(
                    request,
                    cancellationToken);

            if (!latestResult.Success)
            {
                return ClientResult<ViewerConfig>.Fail(
                    latestResult.Message,
                    latestResult.ErrorCode);
            }

            /*
             * 서버 응답을 CamViewer에서 사용하는 ViewerConfig로 변환한다.
             *
             * 이 시점의 설정에는 서버가 관리하는 NVR 접속정보와
             * 계산대·채널 매핑이 반영되어 있다.
             */
            ViewerConfig downloadedConfig =
                ViewerConfigApiMapper.ToLocalConfig(
                    latestResult.Data);

            if (downloadedConfig == null)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "서버 설정정보를 로컬 설정으로 변환할 수 없습니다.",
                    "SERVER_CONFIG_MAP_FAILED");
            }

            /*
             * 서버 설정을 적용하기 전에 기존 로컬 설정을 불러온다.
             *
             * 기존 로컬 설정에는 다음과 같은 PC별 설정이 들어 있을 수 있다.
             *
             * - 영상 재생 이전/이후 시간
             * - 영상 표시 비율
             * - NVR Provider 선택값
             * - Provider별 추가 설정
             * - 영상 원본 해상도
             */
            ViewerConfig existingLocalConfig =
                null;

            if (_configStore.Exists())
            {
                ClientResult<ViewerConfig> localLoadResult =
                    _configStore.Load();

                /*
                 * 기존 로컬 설정을 정상적으로 읽은 경우에만 병합한다.
                 *
                 * 로컬 파일이 손상되었더라도 서버 설정 다운로드 자체를
                 * 실패 처리하지 않고 서버 설정만 사용한다.
                 */
                if (localLoadResult.Success
                    && localLoadResult.Data != null)
                {
                    existingLocalConfig =
                        localLoadResult.Data;
                }
            }

            /*
             * 서버 설정을 기준으로 유지하되,
             * 서버에서 관리하지 않는 PC별 로컬 설정을 다시 병합한다.
             */
            ViewerConfig mergedConfig =
                ViewerConfigLocalSettingsMerger.Merge(
                    downloadedConfig,
                    existingLocalConfig);

            return ClientResult<ViewerConfig>.Ok(
                mergedConfig,
                "서버 캠뷰어 설정정보를 다운로드하고 "
                + "기존 로컬 전용 설정을 유지했습니다.");
        }

        /// <summary>
        /// 로컬 ViewerConfig를 AuthServer 설정 동기화 API로 업로드한다.
        /// </summary>
        public async Task<ClientResult<ViewerConfig>> SyncServerConfigAsync(
            string token,
            string hwid,
            string modifiedBy,
            string programVersion,
            ViewerConfig config,
            CancellationToken cancellationToken)
        {
            if (config == null)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "업로드할 캠뷰어 설정정보가 없습니다.",
                    "CONFIG_REQUIRED");
            }

            ConfigSyncRequestDto request;

            try
            {
                request = ViewerConfigSyncMapper.ToSyncRequest(
                    config,
                    token,
                    hwid,
                    modifiedBy,
                    programVersion);
            }
            catch (Exception ex)
            {
                return ClientResult<ViewerConfig>.Fail(
                    ex.Message,
                    "CONFIG_SYNC_MAP_FAILED");
            }

            ClientResult<ConfigSyncResponseDto> syncResult =
                await _configApiClient.SyncConfigAsync(
                    request,
                    cancellationToken);

            if (!syncResult.Success)
            {
                return ClientResult<ViewerConfig>.Fail(
                    syncResult.Message,
                    syncResult.ErrorCode);
            }

            ViewerConfigSyncMapper.ApplySyncResponse(
                config,
                syncResult.Data);

            return ClientResult<ViewerConfig>.Ok(
                config,
                "캠뷰어 설정정보를 서버와 동기화했습니다.");
        }

        /// <summary>
        /// 로컬 설정이 없거나 불러올 수 없을 때 사용할 기본 설정을 생성한다.
        /// 
        /// 주의:
        /// - StoreCode는 서버 설정 다운로드 후 실제 값으로 갱신된다.
        /// - ConfigVersion은 AuthServer 기준 문자열 버전을 사용한다.
        /// </summary>
        /// <returns>기본 캠뷰어 설정.</returns>
        public ViewerConfig CreateDefaultConfig()
        {
            return new ViewerConfig
            {
                StoreCode = 0,
                ConfigVersion = string.Empty,
                NextNvrNo = 1
            };
        }

        /// <summary>
        /// 로컬 인증 토큰 파일이 존재하는지 확인한다.
        /// </summary>
        public bool HasLocalToken()
        {
            return _tokenStore.Exists();
        }

        /// <summary>
        /// 로컬 인증 토큰 파일의 전체 경로를 반환한다.
        /// </summary>
        public string GetLocalTokenFilePath()
        {
            return _tokenStore.GetTokenFilePath();
        }

        /// <summary>
        /// 로컬 인증 토큰을 불러온다.
        /// </summary>
        public ClientResult<ViewerAuthToken> LoadLocalToken()
        {
            return _tokenStore.Load();
        }

        /// <summary>
        /// 로컬 인증 토큰을 저장한다.
        /// </summary>
        public ClientResult SaveLocalToken(ViewerAuthToken token)
        {
            return _tokenStore.Save(token);
        }

        /// <summary>
        /// 로컬 인증 토큰을 삭제한다.
        /// </summary>
        public ClientResult DeleteLocalToken()
        {
            return _tokenStore.Delete();
        }

        /// <summary>
        /// 매장코드와 비밀번호로 캠뷰어 인증을 수행하고,
        /// 성공 시 로컬 인증 토큰을 저장한다.
        /// </summary>
        public async Task<ClientResult<ViewerAuthToken>> LoginViewerAsync(
            string storeId,
            string storePassword,
            string hwid,
            string deviceName,
            string programVersion,
            CancellationToken cancellationToken)
        {
            var request = new ViewerLoginRequestDto
            {
                StoreId = storeId,
                StorePassword = storePassword,
                Hwid = hwid,
                DeviceName = deviceName,
                ProgramVersion = programVersion
            };

            ClientResult<ViewerLoginResponseDto> loginResult =
                await _authApiClient.LoginAsync(
                    request,
                    cancellationToken);

            if (!loginResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    loginResult.Message,
                    loginResult.ErrorCode);
            }

            if (loginResult.Data == null || !loginResult.Data.LoginSuccess)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "캠뷰어 로그인에 실패했습니다.",
                    "VIEWER_LOGIN_FAILED");
            }

            ViewerAuthToken token =
                CreateTokenFromLoginResponse(
                    loginResult.Data,
                    deviceName);

            ClientResult saveResult =
                _tokenStore.Save(token);

            if (!saveResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    saveResult.Message,
                    saveResult.ErrorCode);
            }

            return ClientResult<ViewerAuthToken>.Ok(
                token,
                "캠뷰어 인증이 완료되었습니다.");
        }

        /// <summary>
        /// 로컬 인증 토큰을 서버에 검증하고,
        /// 성공 시 갱신된 토큰 정보를 로컬에 저장한다.
        /// </summary>
        public async Task<ClientResult<ViewerAuthToken>> VerifyLocalTokenAsync(
            string hwid,
            string programVersion,
            CancellationToken cancellationToken)
        {
            ClientResult<ViewerAuthToken> loadResult =
                _tokenStore.Load();

            if (!loadResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    loadResult.Message,
                    loadResult.ErrorCode);
            }

            ViewerAuthToken localToken = loadResult.Data;

            if (localToken == null || string.IsNullOrWhiteSpace(localToken.Token))
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "로컬 인증 토큰이 없습니다.",
                    "LOCAL_TOKEN_EMPTY");
            }

            var request = new ViewerTokenVerifyRequestDto
            {
                Token = localToken.Token,
                Hwid = hwid,
                ProgramVersion = programVersion
            };

            ClientResult<ViewerTokenVerifyResponseDto> verifyResult =
                await _authApiClient.VerifyTokenAsync(
                    request,
                    cancellationToken);

            if (!verifyResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    verifyResult.Message,
                    verifyResult.ErrorCode);
            }

            if (verifyResult.Data == null || !verifyResult.Data.IsValid)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "캠뷰어 인증 토큰이 유효하지 않습니다.",
                    "VIEWER_TOKEN_INVALID");
            }

            ViewerAuthToken verifiedToken =
                CreateTokenFromVerifyResponse(
                    verifyResult.Data,
                    localToken);

            ClientResult saveResult =
                _tokenStore.Save(verifiedToken);

            if (!saveResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    saveResult.Message,
                    saveResult.ErrorCode);
            }

            return ClientResult<ViewerAuthToken>.Ok(
                verifiedToken,
                "캠뷰어 인증 토큰이 확인되었습니다.");
        }

        /// <summary>
        /// 로컬 토큰 기준으로 오프라인 실행 가능 여부를 확인한다.
        /// 서버 접속 실패 시 마지막 정상 인증 기준 7일 정책에 사용한다.
        /// </summary>
        public ClientResult<ViewerAuthToken> CheckOfflineToken()
        {
            ClientResult<ViewerAuthToken> loadResult =
                _tokenStore.Load();

            if (!loadResult.Success)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    loadResult.Message,
                    loadResult.ErrorCode);
            }

            ViewerAuthToken token = loadResult.Data;

            if (token == null || string.IsNullOrWhiteSpace(token.Token))
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "로컬 인증 토큰이 없습니다.",
                    "LOCAL_TOKEN_EMPTY");
            }

            DateTime nowUtc = DateTime.UtcNow;

            if (token.OfflineExpireAtUtc < nowUtc)
            {
                return ClientResult<ViewerAuthToken>.Fail(
                    "오프라인 실행 허용 기간이 만료되었습니다.",
                    "OFFLINE_TOKEN_EXPIRED");
            }

            return ClientResult<ViewerAuthToken>.Ok(
                token,
                "오프라인 실행이 허용됩니다.");
        }

        /// <summary>
        /// 매장에 등록된 캠뷰어 장비 목록을 조회한다.
        /// 슬롯 부족 시 등록해제 화면에서 사용한다.
        /// </summary>
        public Task<ClientResult<IList<ViewerDeviceSummaryDto>>> GetViewerDevicesAsync(
            string storeId,
            string storePassword,
            CancellationToken cancellationToken)
        {
            var request = new ViewerDeviceListRequestDto
            {
                StoreId = storeId,
                StorePassword = storePassword
            };

            return _authApiClient.GetDevicesAsync(
                request,
                cancellationToken);
        }

        /// <summary>
        /// 선택한 캠뷰어 장비 등록을 해제한다.
        /// </summary>
        public Task<ClientResult<ViewerDeviceReleaseResponseDto>> ReleaseViewerDeviceAsync(
            string storeId,
            string storePassword,
            int deviceCode,
            string reason,
            CancellationToken cancellationToken)
        {
            var request = new ViewerDeviceReleaseRequestDto
            {
                StoreId = storeId,
                StorePassword = storePassword,
                DeviceCode = deviceCode,
                Reason = reason
            };

            return _authApiClient.ReleaseDeviceAsync(
                request,
                cancellationToken);
        }

        /// <summary>
        /// 로그인 응답으로 로컬 인증 토큰을 생성한다.
        /// </summary>
        private static ViewerAuthToken CreateTokenFromLoginResponse(
            ViewerLoginResponseDto response,
            string deviceName)
        {
            DateTime nowUtc = DateTime.UtcNow;

            AuthTokenDto serverToken = response.Token;

            return new ViewerAuthToken
            {
                Token = serverToken == null ? string.Empty : serverToken.Token,
                StoreCode = response.StoreCode,
                DeviceCode = response.DeviceCode,
                DeviceName = deviceName,

                IssuedAtUtc = serverToken == null
                    ? nowUtc
                    : serverToken.IssuedAt.ToUniversalTime(),

                ExpireAtUtc = serverToken == null
                    ? (DateTime?)null
                    : serverToken.ExpiresAt.ToUniversalTime(),

                LastVerifiedAtUtc = nowUtc,

                OfflineExpireAtUtc = serverToken == null
                    ? nowUtc.AddDays(7)
                    : serverToken.OfflineUntil.ToUniversalTime()
            };
        }

        /// <summary>
        /// 토큰 검증 응답으로 로컬 인증 토큰을 갱신한다.
        /// </summary>
        private static ViewerAuthToken CreateTokenFromVerifyResponse(
            ViewerTokenVerifyResponseDto response,
            ViewerAuthToken previousToken)
        {
            DateTime nowUtc = DateTime.UtcNow;

            AuthTokenDto serverToken = response.Token;

            return new ViewerAuthToken
            {
                Token = serverToken == null ? string.Empty : serverToken.Token,
                StoreCode = response.StoreCode,
                DeviceCode = response.DeviceCode,
                DeviceName = previousToken.DeviceName,

                IssuedAtUtc = previousToken.IssuedAtUtc,

                ExpireAtUtc = serverToken == null
                    ? previousToken.ExpireAtUtc
                    : serverToken.ExpiresAt.ToUniversalTime(),

                LastVerifiedAtUtc = nowUtc,

                OfflineExpireAtUtc = serverToken == null
                    ? nowUtc.AddDays(7)
                    : serverToken.OfflineUntil.ToUniversalTime()
            };
        }

        /// <summary>
        /// 로컬 설정 버전을 AuthServer 요청 DTO에 전달할 문자열로 변환한다.
        ///
        /// AuthServer는 ConfigVersion을 문자열로 받는다.
        /// 현재 CamViewer 로컬 설정은 long 타입을 사용하므로,
        /// 0 이하이거나 로컬 설정이 없으면 빈 문자열로 전달한다.
        /// </summary>
        /// <param name="localConfig">현재 로컬 설정.</param>
        /// <returns>AuthServer에 전달할 로컬 설정 버전 문자열.</returns>
        private static string GetLocalConfigVersionText(
            ViewerConfig localConfig)
        {
            if (localConfig == null)
            {
                return string.Empty;
            }

            return localConfig.ConfigVersion ?? string.Empty;
        }
    }
}
