using CamViewer.Models;
using CamViewerClient;
using CamViewerClient.Models;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// CamViewer 시작 시 인증 이후 필요한 설정 확인 흐름을 처리한다.
    /// 
    /// 이 서비스는 화면을 직접 제어하지 않는다.
    /// Presenter는 이 서비스의 결과를 받아
    /// 로그인 창, 설정 창, Player 화면 이동 같은 화면 흐름만 처리한다.
    /// </summary>
    public sealed class LandingStartupService : ILandingStartupService
    {
        private readonly CamViewerClientFacade _clientFacade;

        /// <summary>
        /// LandingStartupService를 초기화한다.
        /// </summary>
        /// <param name="clientFacade">인증/설정 저장/서버 API 호출 Facade.</param>
        public LandingStartupService(
            CamViewerClientFacade clientFacade)
        {
            _clientFacade = clientFacade;
        }

        /// <summary>
        /// 로컬 설정 파일을 사용할 수 있는지 확인한다.
        /// 
        /// 처리 기준:
        /// - 설정 파일이 없으면 false
        /// - 설정 파일은 있지만 불러오지 못하면 false
        /// - 정상적으로 로드되면 true
        /// </summary>
        /// <returns>로컬 설정 사용 가능 여부.</returns>
        public bool CanUseLocalConfig()
        {
            if (!_clientFacade.HasLocalConfig())
            {
                return false;
            }

            ClientResult<ViewerConfig> loadConfigResult =
                _clientFacade.LoadLocalConfig();

            if (!loadConfigResult.Success || loadConfigResult.Data == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// AuthServer에서 최신 설정을 다운로드하고 로컬 설정 파일로 저장한다.
        ///
        /// AuthServer ConfigLatestRequest 기준:
        /// - Token
        /// - Hwid
        /// - LocalConfigVersion
        /// - ProgramVersion
        ///
        /// StoreCode, DeviceCode는 요청 Body로 보내지 않는다.
        /// 서버는 Token payload에서 StoreCode, DeviceCode를 추출한다.
        ///
        /// PlaybackOption은 서버 관리 대상이 아닌 로컬 전용 설정이므로,
        /// 서버 설정 다운로드 시 기존 로컬 값을 유지한다.
        /// </summary>
        public async Task<StartupFlowResult> DownloadServerConfigAsync(
            ViewerAuthToken token,
            string hwid,
            string programVersion,
            CancellationToken cancellationToken)
        {
            if (token == null
                || string.IsNullOrWhiteSpace(token.Token))
            {
                return StartupFlowResult.RequireLogin(
                    "캠뷰어 인증 정보가 없습니다. 다시 로그인해 주세요.");
            }

            if (string.IsNullOrWhiteSpace(hwid))
            {
                return StartupFlowResult.Close(
                    "장비 식별값을 확인할 수 없습니다.");
            }

            string localConfigVersion =
                GetCurrentLocalConfigVersionText();

            ClientResult<ViewerConfig> downloadResult =
                await _clientFacade.DownloadServerConfigAsync(
                    token.Token,
                    hwid,
                    localConfigVersion,
                    programVersion,
                    cancellationToken);

            /*
             * 서버 인증 또는 설정 다운로드에 실패한 경우에는
             * 기존 로컬 설정을 수정하지 않는다.
             */
            if (!downloadResult.Success
                || downloadResult.Data == null)
            {
                if (_clientFacade.HasLocalConfig())
                {
                    ClientResult<ViewerConfig> localLoadResult =
                        _clientFacade.LoadLocalConfig();

                    if (localLoadResult.Success
                        && localLoadResult.Data != null)
                    {
                        return StartupFlowResult.ContinueToPlayer(
                            "서버 설정 다운로드에 실패했습니다. 로컬 설정으로 실행합니다."
                            + Environment.NewLine
                            + downloadResult.Message,
                            false);
                    }
                }

                return StartupFlowResult.RequireSettings(
                    "서버 설정을 다운로드할 수 없습니다."
                    + Environment.NewLine
                    + downloadResult.Message
                    + Environment.NewLine
                    + "설정 화면에서 캠뷰어 설정을 저장해 주세요.",
                    "서버 설정 다운로드 실패"
                    + Environment.NewLine
                    + "ErrorCode: "
                    + downloadResult.ErrorCode);
            }

            /*
             * 이 시점에는 서버 인증과 설정 다운로드가 성공했다.
             *
             * 로컬 설정을 덮어쓰기 직전에 기존 PC의 재생 옵션을 읽는다.
             * 기존 로컬 설정이 없다면 PlaybackOption 기본값인
             * BeforeSeconds=30, AfterCompleteSeconds=3을 사용한다.
             */
            PlaybackOption localPlaybackOption =
                LoadLocalPlaybackOptionOrDefault();

            ViewerConfig downloadedConfig =
                downloadResult.Data;

            /*
             * 서버 응답에는 PlaybackOption이 없으므로,
             * 서버 설정 변환 과정에서 생성된 기본값 대신
             * 기존 PC에 저장된 로컬 재생 옵션을 다시 적용한다.
             */
            downloadedConfig.PlaybackOption =
                localPlaybackOption;

            ClientResult saveResult =
                _clientFacade.SaveLocalConfig(
                    downloadedConfig);

            if (!saveResult.Success)
            {
                return StartupFlowResult.RequireSettings(
                    "서버 설정은 다운로드했지만 로컬 저장에 실패했습니다."
                    + Environment.NewLine
                    + saveResult.Message
                    + Environment.NewLine
                    + "설정 화면에서 다시 저장해 주세요.",
                    "서버 설정 다운로드 후 로컬 저장 실패");
            }

            return StartupFlowResult.ContinueToPlayer(
                "서버 설정을 다운로드하여 로컬 설정으로 저장했습니다.",
                false);
        }

        /// <summary>
        /// 온라인 인증 성공 후 로컬 설정과 서버 설정 상태를 확인한다.
        ///
        /// 처리 흐름:
        /// 1. 인증 토큰 확인
        /// 2. 로컬 설정이 없으면 서버 최신 설정 다운로드 시도
        /// 3. 로컬 설정이 있으면 서버 설정 버전 확인
        /// 4. 로컬 설정이 최신이면 PlayerView로 진행
        /// 5. 서버 설정이 더 최신이면 다운로드 확인 단계로 진행
        /// </summary>
        public async Task<StartupFlowResult> CheckConfigAfterOnlineAuthAsync(
            ViewerAuthToken token,
            string hwid,
            string programVersion,
            CancellationToken cancellationToken)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.Token))
            {
                return StartupFlowResult.RequireLogin(
                    "캠뷰어 인증 정보가 없습니다. 다시 로그인해 주세요.");
            }

            if (string.IsNullOrWhiteSpace(hwid))
            {
                return StartupFlowResult.Close(
                    "장비 식별값을 확인할 수 없습니다.");
            }

            if (!_clientFacade.HasLocalConfig())
            {
                return StartupFlowResult.RequireServerConfigDownloadConfirm(
                    "로컬 캠뷰어 설정 파일이 없습니다."
                    + Environment.NewLine
                    + "인증서버에서 설정을 다운로드하시겠습니까?",
                    "로컬 설정 파일이 없어 서버 설정 다운로드 확인이 필요합니다.",
                    false);
            }

            ClientResult<ViewerConfig> loadResult =
                _clientFacade.LoadLocalConfig();

            if (!loadResult.Success || loadResult.Data == null)
            {
                return StartupFlowResult.RequireServerConfigDownloadConfirm(
                    "로컬 캠뷰어 설정 정보를 불러올 수 없습니다."
                    + Environment.NewLine
                    + "인증서버에서 설정을 다시 다운로드하시겠습니까?",
                    "로컬 설정 로드 실패: "
                    + loadResult.Message,
                    false);
            }

            ViewerConfig localConfig =
                loadResult.Data;

            ClientResult<ConfigVersionResponseDto> versionResult =
                await _clientFacade.GetServerConfigVersionAsync(
                    token.Token,
                    hwid,
                    localConfig,
                    programVersion,
                    cancellationToken);

            if (!versionResult.Success || versionResult.Data == null)
            {
                return StartupFlowResult.ContinueToPlayer(
                    "서버 설정 버전 확인에 실패했습니다. 로컬 설정으로 실행합니다."
                    + Environment.NewLine
                    + versionResult.Message,
                    false);
            }

            if (versionResult.Data.IsLatest)
            {
                return StartupFlowResult.ContinueToPlayer(
                    "로컬 설정이 최신입니다. 로컬 설정으로 캠뷰어를 실행합니다.",
                    false);
            }

            return StartupFlowResult.RequireServerConfigDownloadConfirm(
                "서버 설정 버전이 로컬 설정 버전보다 높습니다."
                + Environment.NewLine
                + "서버에서 최신 설정을 다운로드하시겠습니까?",
                "서버 설정 버전: "
                + versionResult.Data.ConfigVersion
                + Environment.NewLine
                + "로컬 설정 버전: "
                + GetLocalConfigVersionText(localConfig));
        }

        /// <summary>
        /// 서버에 저장된 캠뷰어 설정 버전을 확인하고,
        /// 로컬 설정과 비교한 뒤 다음 시작 흐름을 결정한다.
        ///
        /// AuthServer /api/config/version 요청 기준:
        /// - Token
        /// - Hwid
        /// - LocalConfigVersion
        /// - ProgramVersion
        ///
        /// 주의:
        /// - StoreCode, DeviceCode는 요청 Body로 보내지 않는다.
        /// - AuthServer는 Token payload에서 StoreCode, DeviceCode를 추출한다.
        /// </summary>
        /// <param name="token">현재 인증된 캠뷰어 토큰 정보.</param>
        /// <param name="hwid">현재 PC의 HWID.</param>
        /// <param name="localConfig">현재 로컬 설정.</param>
        /// <param name="programVersion">캠뷰어 프로그램 버전.</param>
        /// <param name="cancellationToken">비동기 취소 토큰.</param>
        /// <returns>서버 설정 버전 확인 결과에 따른 시작 흐름 결과.</returns>
        private async Task<StartupFlowResult> CheckServerConfigVersionAsync(
            ViewerAuthToken token,
            string hwid,
            ViewerConfig localConfig,
            string programVersion,
            CancellationToken cancellationToken)
        {
            if (token == null || string.IsNullOrWhiteSpace(token.Token))
            {
                return StartupFlowResult.RequireLogin(
                    "캠뷰어 인증 정보가 없습니다. 다시 로그인해 주세요.");
            }

            if (string.IsNullOrWhiteSpace(hwid))
            {
                return StartupFlowResult.Close(
                    "장비 식별값을 확인할 수 없습니다.");
            }

            if (localConfig == null)
            {
                return await DownloadServerConfigAsync(
                    token,
                    hwid,
                    programVersion,
                    cancellationToken);
            }

            ClientResult<ConfigVersionResponseDto> versionResult =
                await _clientFacade.GetServerConfigVersionAsync(
                    token.Token,
                    hwid,
                    localConfig,
                    programVersion,
                    cancellationToken);

            /*
             * 서버 설정 버전 확인 실패:
             * 로컬 설정이 정상적으로 존재하는 상태라면
             * 서버 확인 실패만으로 프로그램 실행을 막지 않는다.
             */
            if (!versionResult.Success || versionResult.Data == null)
            {
                return StartupFlowResult.ContinueToPlayer(
                    "서버 설정 버전 확인에 실패했습니다. 로컬 설정으로 캠뷰어를 실행합니다."
                    + Environment.NewLine
                    + versionResult.Message,
                    false);
            }

            /*
             * 서버 기준으로 로컬 설정이 최신이면 그대로 실행한다.
             */
            if (versionResult.Data.IsLatest)
            {
                return StartupFlowResult.ContinueToPlayer(
                    "로컬 설정이 최신입니다. 로컬 설정으로 캠뷰어를 실행합니다.",
                    false);
            }

            /*
             * 서버 설정이 더 최신이면 사용자에게 다운로드 여부를 묻는다.
             * 실제 다운로드는 Presenter의 ConfirmServerConfigDownload 처리에서
             * LandingStartupService.DownloadServerConfigAsync()를 다시 호출한다.
             */
            return StartupFlowResult.RequireServerConfigDownloadConfirm(
                "서버 설정 버전이 로컬 설정 버전보다 높습니다."
                + Environment.NewLine
                + "서버에서 최신 설정을 다운로드하시겠습니까?",
                "서버 설정 버전: "
                + versionResult.Data.ConfigVersion
                + Environment.NewLine
                + "로컬 설정 버전: "
                + GetLocalConfigVersionText(localConfig));
        }

        /// <summary>
        /// 서버 접속 실패 시 오프라인 실행에 필요한 로컬 인증/설정 상태를 확인한다.
        /// 
        /// 처리 기준:
        /// - 오프라인 토큰이 없거나 기간이 만료되면 로그인 필요
        /// - 오프라인 토큰이 유효해도 로컬 설정이 없으면 설정 필요
        /// - 로컬 설정까지 유효하면 Player 화면으로 진행
        /// </summary>
        /// <returns>시작 흐름 처리 결과.</returns>
        public StartupFlowResult CheckOfflineStartup()
        {
            ClientResult<ViewerAuthToken> offlineResult =
                _clientFacade.CheckOfflineToken();

            if (!offlineResult.Success || offlineResult.Data == null)
            {
                return StartupFlowResult.RequireLogin(
                    "오프라인 실행 허용 기간이 만료되었거나 로컬 인증정보가 없습니다. "
                    + offlineResult.Message);
            }

            if (!CanUseLocalConfig())
            {
                return StartupFlowResult.RequireSettings(
                    "오프라인 인증은 유효하지만 로컬 설정 정보를 사용할 수 없습니다.");
            }

            return StartupFlowResult.ContinueToPlayer(
                "오프라인 실행 허용 기간 내입니다. 로컬 설정으로 실행합니다.",
                true);
        }


        /*내부*/

        /// <summary>
        /// 서버 설정 다운로드에 사용할 수 있는 등록 디바이스 토큰인지 확인한다.
        /// 
        /// CamViewer 설정은 단순 매장 정보가 아니라,
        /// 인증된 사용자 + 등록된 CamViewer 디바이스 권한을 기준으로 다운로드되어야 한다.
        /// 따라서 DeviceCode가 없으면 설정 다운로드를 시도하지 않고
        /// 로그인/디바이스 등록 흐름으로 보내야 한다.
        /// </summary>
        /// <param name="token">현재 인증 토큰 정보.</param>
        /// <returns>설정 다운로드에 사용할 수 있는 토큰이면 true.</returns>
        private bool HasValidDeviceToken(
            ViewerAuthToken token)
        {
            if (token == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(token.Token))
            {
                return false;
            }

            if (token.StoreCode <= 0)
            {
                return false;
            }

            if (token.DeviceCode <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 현재 로컬 설정 파일에서 설정 버전을 문자열로 가져온다.
        /// 로컬 설정이 없거나 읽을 수 없으면 빈 문자열을 반환한다.
        /// </summary>
        private string GetCurrentLocalConfigVersionText()
        {
            if (!_clientFacade.HasLocalConfig())
            {
                return string.Empty;
            }

            ClientResult<ViewerConfig> loadResult =
                _clientFacade.LoadLocalConfig();

            if (!loadResult.Success || loadResult.Data == null)
            {
                return string.Empty;
            }

            return GetLocalConfigVersionText(
                loadResult.Data);
        }

        /// <summary>
        /// ViewerConfig의 ConfigVersion 값을 AuthServer 요청용 문자열로 가져온다.
        /// </summary>
        /// <param name="config">로컬 캠뷰어 설정.</param>
        /// <returns>AuthServer 요청에 사용할 설정 버전 문자열.</returns>
        private static string GetLocalConfigVersionText(
            ViewerConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return config.ConfigVersion ?? string.Empty;
        }



        /// <summary>
        /// 현재 로컬 설정에 저장된 재생 옵션을 반환한다.
        ///
        /// 로컬 설정이 없거나 재생 옵션이 올바르지 않은 경우
        /// PlaybackOption의 기본값인 이전 30초, 이후 3초를 반환한다.
        /// </summary>
        private PlaybackOption LoadLocalPlaybackOptionOrDefault()
        {
            var defaultOption =
                new PlaybackOption();

            ClientResult<ViewerConfig> loadResult =
                _clientFacade.LoadLocalConfig();

            if (!loadResult.Success
                || loadResult.Data == null
                || loadResult.Data.PlaybackOption == null)
            {
                return defaultOption;
            }

            int beforeSeconds =
                loadResult.Data.PlaybackOption.BeforeSeconds;

            int afterCompleteSeconds =
                loadResult.Data.PlaybackOption.AfterCompleteSeconds;

            // 이전 조회 시간 검증
            if (beforeSeconds < 0
                || beforeSeconds > 300)
            {
                beforeSeconds =
                    defaultOption.BeforeSeconds;
            }

            // 거래완료 이후 보정 시간 검증
            if (afterCompleteSeconds < 0
                || afterCompleteSeconds > 10)
            {
                afterCompleteSeconds =
                    defaultOption.AfterCompleteSeconds;
            }

            return new PlaybackOption
            {
                BeforeSeconds = beforeSeconds,
                AfterCompleteSeconds = afterCompleteSeconds
            };
        }

    }
}