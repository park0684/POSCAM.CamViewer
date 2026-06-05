using System.Threading;
using System.Threading.Tasks;
using CamViewerClient.Api;
using CamViewerClient.Config;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using CamViewerClient.Auth;
using CamViewerClient.Models.Auth;
using System;
using System.Collections.Generic;
using CamViewerClient.Mappers;


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
        /// </summary>
        public Task<ClientResult<ConfigVersionResponseDto>> GetServerConfigVersionAsync(
            string token,
            ViewerConfig localConfig,
            int deviceCode,
            CancellationToken cancellationToken)
        {
            var request = new ConfigVersionRequestDto
            {
                Token = token,
                StoreCode = localConfig == null ? 0 : localConfig.StoreCode,
                DeviceCode = deviceCode,
                LocalConfigVersion = localConfig == null
                    ? 0
                    : localConfig.ConfigVersion
            };

            return _configApiClient.GetVersionAsync(
                request,
                cancellationToken);
        }

        /// <summary>
        /// AuthServer에서 최신 설정을 다운로드하고 로컬 ViewerConfig로 변환한다.
        /// </summary>
        public async Task<ClientResult<ViewerConfig>> DownloadServerConfigAsync(
            string token,
            int storeCode,
            int deviceCode,
            CancellationToken cancellationToken)
        {
            var request = new ConfigLatestRequestDto
            {
                Token = token,
                StoreCode = storeCode,
                DeviceCode = deviceCode
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

            ViewerConfig localConfig =
                ViewerConfigApiMapper.ToLocalConfig(
                    latestResult.Data);

            if (localConfig == null)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "서버 설정정보를 로컬 설정으로 변환할 수 없습니다.",
                    "SERVER_CONFIG_MAP_FAILED");
            }

            return ClientResult<ViewerConfig>.Ok(
                localConfig,
                "서버 캠뷰어 설정정보를 다운로드했습니다.");
        }

        /// <summary>
        /// 로컬 ViewerConfig를 AuthServer 설정 동기화 API로 업로드한다.
        /// </summary>
        public async Task<ClientResult<ViewerConfig>> SyncServerConfigAsync(
            string token,
            int deviceCode,
            ViewerConfig config,
            CancellationToken cancellationToken)
        {
            if (config == null)
            {
                return ClientResult<ViewerConfig>.Fail(
                    "업로드할 캠뷰어 설정정보가 없습니다.",
                    "CONFIG_REQUIRED");
            }

            var request = new ConfigSyncRequestDto
            {
                Token = token,
                StoreCode = config.StoreCode,
                DeviceCode = deviceCode,
                BaseConfigVersion = config.ConfigVersion,
                Config = ViewerConfigApiMapper.ToServerDto(config)
            };

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

            if (syncResult.Data != null && syncResult.Data.Config != null)
            {
                ViewerConfig syncedConfig =
                    ViewerConfigApiMapper.ToLocalConfig(
                        syncResult.Data.Config);

                return ClientResult<ViewerConfig>.Ok(
                    syncedConfig,
                    "캠뷰어 설정정보를 서버와 동기화했습니다.");
            }

            config.ConfigVersion = syncResult.Data == null
                ? config.ConfigVersion
                : syncResult.Data.ConfigVersion;

            return ClientResult<ViewerConfig>.Ok(
                config,
                "캠뷰어 설정정보를 서버와 동기화했습니다.");
        }

        /// <summary>
        /// 로컬 설정이 없거나 불러올 수 없을 때 사용할 기본 설정을 생성한다.
        /// </summary>
        public ViewerConfig CreateDefaultConfig()
        {
            return new ViewerConfig
            {
                StoreCode = 1,
                ConfigVersion = 0,
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
        /// 매장 로그인 정보로 캠뷰어 인증을 수행하고,
        /// 성공 시 로컬 인증 토큰을 저장한다.
        /// </summary>
        public async Task<ClientResult<ViewerAuthToken>> LoginViewerAsync(
            int storeCode,
            string storeId,
            string storePassword,
            string hwid,
            string deviceName,
            string programVersion,
            CancellationToken cancellationToken)
        {
            var request = new ViewerLoginRequestDto
            {
                StoreCode = storeCode,
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
            int storeCode,
            string storeId,
            string storePassword,
            CancellationToken cancellationToken)
        {
            var request = new ViewerDeviceListRequestDto
            {
                StoreCode = storeCode,
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
            int storeCode,
            string storeId,
            string storePassword,
            int deviceCode,
            string reason,
            CancellationToken cancellationToken)
        {
            var request = new ViewerDeviceReleaseRequestDto
            {
                StoreCode = storeCode,
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

            return new ViewerAuthToken
            {
                Token = response.Token,
                StoreCode = response.StoreCode,
                DeviceCode = response.DeviceCode,
                DeviceName = deviceName,
                IssuedAtUtc = nowUtc,
                ExpireAtUtc = null,
                LastVerifiedAtUtc = nowUtc,
                OfflineExpireAtUtc = nowUtc.AddDays(7)
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

            return new ViewerAuthToken
            {
                Token = response.Token,
                StoreCode = response.StoreCode,
                DeviceCode = response.DeviceCode,
                DeviceName = previousToken.DeviceName,
                IssuedAtUtc = previousToken.IssuedAtUtc,
                ExpireAtUtc = previousToken.ExpireAtUtc,
                LastVerifiedAtUtc = nowUtc,
                OfflineExpireAtUtc = nowUtc.AddDays(7)
            };
        }

    }
}
