using CamViewer.Interfaces;
using CamViewer.Models;
using CamViewer.Services;
using CamViewerClient;
using CamViewerClient.Models.Api;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;
using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CamViewer.Presenters
{
    /// <summary>
    /// 캠뷰어 랜딩페이지의 시작 흐름을 제어한다.
    ///
    /// 처리 흐름:
    /// 1. 로컬 인증 토큰 확인
    /// 2. 서버 토큰 검증
    /// 3. 서버 접속 실패 시 오프라인 실행 가능 여부 확인
    /// 4. 토큰 없음 또는 토큰 무효 시 로그인창 표시
    /// 5. 인증 성공 시 설정 확인 후 다음 화면으로 진행
    /// </summary>
    public sealed class LandingPresenter
    {
        private readonly ILandingView _view;
        private readonly CamViewerClientFacade _clientFacade;
        private readonly IClientEnvironmentProvider _environmentProvider;
        private readonly Func<ILoginView> _loginViewFactory;
        //private readonly Action _openSettingsAction; _openSettingsFunc로 대체
        private readonly Action _openPlayerAction;
        private bool _playerOpened;
        private readonly Func<bool> _openSettingsFunc;
        private readonly ILandingStartupService _startupService;
        private bool _isRunning;
        
        /// <summary>
        /// LandingPresenter를 초기화한다.
        /// </summary>
        public LandingPresenter(
            ILandingView view,
            CamViewerClientFacade clientFacade,
            ILandingStartupService startupService,
            IClientEnvironmentProvider environmentProvider,
            Func<ILoginView> loginViewFactory,
            Func<bool> opneSettingFunc,
            Action openPlayerAction)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (clientFacade == null)
            {
                throw new ArgumentNullException("clientFacade");
            }

            if (startupService == null)
            {
                throw new ArgumentNullException("startupService");
            }
                
            if (environmentProvider == null)
            {
                throw new ArgumentNullException("environmentProvider");
            }

            if (loginViewFactory == null)
            {
                throw new ArgumentNullException("loginViewFactory");
            }

            _view = view;
            _clientFacade = clientFacade;
            _environmentProvider = environmentProvider;
            _loginViewFactory = loginViewFactory;
            _openSettingsFunc = opneSettingFunc;
            _openPlayerAction = openPlayerAction;
            _startupService = startupService;
            _view.LoadViewEvent += OnLoadView;
            _view.SettingsEvent += OnSettings;
        }

        /// <summary>
        /// 랜딩페이지를 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// 랜딩페이지 표시 후 시작 인증 흐름을 실행한다.
        /// </summary>
        private async void OnLoadView(object sender, EventArgs e)
        {
            await RunStartupFlowAsync();
        }

        /// <summary>
        /// 설정 버튼 클릭 시 로그인창을 띄운 뒤 설정 화면을 연다.
        /// 설정 저장에 성공하면 시작 흐름을 다시 실행한다.
        /// </summary>
        private async void OnSettings(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                return;
            }

            bool loginSuccess = ShowLoginView();

            if (!loginSuccess)
            {
                return;
            }

            if (_openSettingsFunc == null)
            {
                return;
            }

            bool saved = _openSettingsFunc();

            if (saved)
            {
                await RunStartupFlowAsync();
            }
        }

        /// <summary>
        /// 로컬 토큰을 서버에 검증하고 다음 단계로 진행한다.
        /// </summary>
        private async Task VerifyTokenAndContinueAsync()
        {
            _view.SetProgressVisible(true);
            _view.SetStatus("서버 인증을 확인하고 있습니다.");

            string hwid = _environmentProvider.GetHwid();
            string programVersion = _environmentProvider.GetProgramVersion();

            ClientResult<ViewerAuthToken> verifyResult =
                await _clientFacade.VerifyLocalTokenAsync(
                    hwid,
                    programVersion,
                    CancellationToken.None);

            if (verifyResult.Success)
            {
                await CheckConfigAndContinueAsync(verifyResult.Data);
                return;
            }

            if (IsNetworkError(verifyResult.ErrorCode))
            {
                await HandleOfflineFlowAsync();
                return;
            }

            _view.SetProgressVisible(false);
            _view.ShowMessage(
                "저장된 로그인 정보가 유효하지 않습니다."
                + Environment.NewLine
                + "라이선스 상태 또는 캠뷰어 등록 상태를 확인해야 합니다."
                + Environment.NewLine
                + "다시 로그인해 주세요.");

            bool loginSuccess = ShowLoginView();

            if (!loginSuccess)
            {
                _view.CloseView();
                return;
            }

            await RunStartupFlowCoreAsync();
        }

        /// <summary>
        /// 프로그램 시작 시 인증 및 설정 확인 흐름을 실행한다.
        /// 중복 실행을 방지하는 외부 진입점이다.
        /// </summary>
        private async Task RunStartupFlowAsync()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _isRunning = true;
                _view.SetSettingsEnabled(false);

                await RunStartupFlowCoreAsync();
            }
            finally
            {
                _isRunning = false;
                _view.SetSettingsEnabled(true);
            }
        }

        /// <summary>
        /// 실제 인증 및 설정 확인 흐름을 처리한다.
        /// 내부에서 재실행이 필요할 때는 이 메서드를 호출한다.
        /// </summary>
        private async Task RunStartupFlowCoreAsync()
        {
            _view.SetProgressVisible(true);
            _view.SetStatus("캠뷰어 인증 정보를 확인하고 있습니다.");
            _view.SetDetailMessage(string.Empty);

            if (!_clientFacade.HasLocalToken())
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "캠뷰어 인증 정보가 없습니다."
                    + Environment.NewLine
                    + "캠뷰어를 사용하려면 로그인이 필요합니다.");

                bool loginSuccess = ShowLoginView();

                if (!loginSuccess)
                {
                    _view.CloseView();
                    return;
                }

                await RunStartupFlowCoreAsync();
                return;
            }

            await VerifyTokenAndContinueAsync();
        }


        /// <summary>
        /// 서버 접속 실패 시 오프라인 실행 가능 여부를 확인한다.
        /// 
        /// 오프라인 실행 조건:
        /// 1. 로컬 인증 토큰이 존재해야 한다.
        /// 2. 오프라인 허용 기간 내여야 한다.
        /// 3. 로컬 설정 파일이 존재해야 한다.
        /// 4. 로컬 설정 파일을 정상적으로 불러올 수 있어야 한다.
        /// 
        /// 주의:
        /// - 인증 정보가 없으면 로그인 창이 뜨는 것은 정상 흐름이다.
        /// - 서버 연결이 안 되는 상태에서는 서버 설정 다운로드를 할 수 없으므로,
        ///   로컬 설정이 없으면 Player 화면으로 진행하면 안 된다.
        /// </summary>
        private async Task HandleOfflineFlowAsync()
        {
            ClientResult<ViewerAuthToken> offlineResult =
                _clientFacade.CheckOfflineToken();

            if (!offlineResult.Success || offlineResult.Data == null)
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "인증서버에 연결할 수 없습니다."
                    + Environment.NewLine
                    + "오프라인 실행 허용 기간이 만료되었거나 로컬 인증정보가 없습니다."
                    + Environment.NewLine
                    + offlineResult.Message);

                bool loginSuccess =
                    ShowLoginView();

                if (!loginSuccess)
                {
                    _view.CloseView();
                }

                return;
            }

            if (!_clientFacade.HasLocalConfig())
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "오프라인 인증은 유효하지만 로컬 설정 정보가 없습니다."
                    + Environment.NewLine
                    + "서버 연결 후 설정을 다운로드하거나 설정 화면에서 저장해 주세요.");

                if (_openSettingsFunc == null)
                {
                    _view.CloseView();
                    return;
                }

                bool saved =
                    _openSettingsFunc();

                if (saved)
                {
                    await RunStartupFlowCoreAsync();
                }
                else
                {
                    _view.CloseView();
                }

                return;
            }

            ClientResult<ViewerConfig> loadConfigResult =
                _clientFacade.LoadLocalConfig();

            if (!loadConfigResult.Success || loadConfigResult.Data == null)
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "오프라인 인증은 유효하지만 로컬 설정 정보를 불러올 수 없습니다."
                    + Environment.NewLine
                    + loadConfigResult.Message
                    + Environment.NewLine
                    + "설정 화면에서 다시 저장해 주세요.");

                if (_openSettingsFunc == null)
                {
                    _view.CloseView();
                    return;
                }

                bool saved =
                    _openSettingsFunc();

                if (saved)
                {
                    await RunStartupFlowCoreAsync();
                }
                else
                {
                    _view.CloseView();
                }

                return;
            }

            _view.SetProgressVisible(false);

            _view.SetDetailMessage(
                "오프라인 실행 허용 기간 내입니다. 로컬 설정으로 캠뷰어를 실행합니다.");

            ContinueToPlayer();
        }

        /// <summary>
        /// 설정 파일 상태를 확인하고 다음 화면으로 진행한다.
        /// 
        /// 실제 설정 존재 여부, 서버 설정 다운로드,
        /// 서버 설정 버전 비교는 LandingStartupService에서 처리한다.
        /// Presenter는 결과에 따른 화면 흐름만 처리한다.
        /// </summary>
        /// <param name="token">현재 인증된 CamViewer 토큰 정보.</param>
        private async Task CheckConfigAndContinueAsync(
            ViewerAuthToken token)
        {
            _view.SetStatus(
                "캠뷰어 설정 정보를 확인하고 있습니다.");

            _view.SetDetailMessage(
                "로컬 설정 및 서버 설정 버전을 확인하고 있습니다.");

            string hwid =
                _environmentProvider.GetHwid();

            string programVersion =
                _environmentProvider.GetProgramVersion();

            StartupFlowResult result =
                await _startupService.CheckConfigAfterOnlineAuthAsync(
                    token,
                    hwid,
                    programVersion,
                    CancellationToken.None);

            await HandleOnlineStartupResultAsync(
                result,
                token);
        }

        /// <summary>
        /// 로그인창을 모달로 표시한다.
        /// </summary>
        private bool ShowLoginView()
        {
            ILoginView loginView =
                _loginViewFactory();

            var loginPresenter = new LoginPresenter(
                loginView,
                _clientFacade,
                _environmentProvider);

            return loginPresenter.ShowDialog();
        }

        /// <summary>
        /// PlayerView로 전환한다.
        ///
        /// LandingView는 Application.Run의 메인 Form이므로 닫지 않고 숨긴다.
        /// PlayerView가 종료되면 Program.cs에서 Application.Exit()을 호출한다.
        /// </summary>
        private void ContinueToPlayer()
        {
            if (_playerOpened)
            {
                return;
            }

            _view.SetProgressVisible(false);
            _view.SetStatus("캠뷰어 실행 준비가 완료되었습니다.");
            _view.SetDetailMessage("PlayerView를 실행합니다.");

            if (_openPlayerAction == null)
            {
                _view.ShowMessage("PlayerView 실행 구성이 없습니다.");
                return;
            }

            _playerOpened = true;

            _view.HideView();

            _openPlayerAction();
        }

        /// <summary>
        /// 인증서버 접속 실패 또는 통신 장애로 인해
        /// 오프라인 실행 판단을 해야 하는 오류인지 확인한다.
        /// 
        /// 주의:
        /// - 아이디/비밀번호 오류, 토큰 무효, 권한 없음 같은 인증 실패는
        ///   오프라인 실행 대상으로 보면 안 된다.
        /// - 서버에 도달하지 못했거나, 프록시/서버 장애로 정상 응답을 받지 못한 경우만
        ///   오프라인 실행 판단 대상으로 본다.
        /// </summary>
        /// <param name="errorCode">API 호출 결과의 오류 코드.</param>
        /// <returns>오프라인 실행 판단 대상으로 볼 수 있으면 true.</returns>
        private static bool IsNetworkError(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return false;
            }

            return string.Equals(
                    errorCode,
                    "API_REQUEST_CANCELLED",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    errorCode,
                    "API_REQUEST_FAILED",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    errorCode,
                    "API_RESPONSE_EMPTY",
                    StringComparison.OrdinalIgnoreCase)

                // 현재 API Client가 HTTP 상태코드를 세분화하지 못하고
                // 모든 HTTP 실패를 HTTP_REQUEST_FAILED로 반환하는 경우를 임시로 포함한다.
                // 단, 이후 단계에서 401/403 같은 인증 실패와
                // 502/503/504 같은 서버 장애는 분리하는 것이 더 안전하다.
                || string.Equals(
                    errorCode,
                    "HTTP_REQUEST_FAILED",
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 서버에 저장된 CamViewer 설정을 다운로드하여 로컬 설정 파일로 저장한다.
        /// 
        /// 사용 시점:
        /// - 로컬 설정 파일이 없는 상태에서 온라인 인증이 성공한 경우
        /// - 서버 설정 버전이 로컬 설정보다 높은 경우
        /// 
        /// 처리 순서:
        /// 1. 인증 토큰 정보 확인
        /// 2. 서버 최신 설정 다운로드
        /// 3. 다운로드된 설정을 로컬 설정 파일로 저장
        /// 4. 성공 여부 반환
        /// </summary>
        /// <param name="token">현재 인증된 CamViewer 토큰 정보.</param>
        /// <returns>서버 설정 다운로드 및 로컬 저장에 성공하면 true.</returns>
        //private async Task<bool> TryDownloadServerConfigAsync(
        //    ViewerAuthToken token)
        //{
        //    if (token == null)
        //    {
        //        return false;
        //    }

        //    ClientResult<ViewerConfig> downloadResult =
        //    await _startupService.DownloadServerConfigAsync(
        //        token,
        //        hwid,
        //        programVersion,
        //        CancellationToken.None);

        //    if (!downloadResult.Success || downloadResult.Data == null)
        //    {
        //        string errorCodeText =
        //            string.IsNullOrWhiteSpace(downloadResult.ErrorCode)
        //    ? "오류코드 없음"
        //    : downloadResult.ErrorCode;

        //        string messageText =
        //            string.IsNullOrWhiteSpace(downloadResult.Message)
        //                ? "오류 메시지 없음"
        //                : downloadResult.Message;

        //        _view.SetDetailMessage(
        //            "서버 설정 다운로드 실패"
        //            + Environment.NewLine
        //            + "ErrorCode: "
        //            + errorCodeText
        //            + Environment.NewLine
        //            + "Message: "
        //            + messageText
        //            + Environment.NewLine
        //            + "StoreCode: "
        //            + token.StoreCode
        //            + Environment.NewLine
        //            + "DeviceCode: "
        //            + token.DeviceCode);

        //        return false;
        //    }

        //    ClientResult saveResult =
        //        _clientFacade.SaveLocalConfig(
        //            downloadResult.Data);

        //    if (!saveResult.Success)
        //    {
        //        _view.ShowMessage(
        //            "서버 설정은 다운로드했지만 로컬 저장에 실패했습니다."
        //            + Environment.NewLine
        //            + saveResult.Message);

        //        return false;
        //    }

        //    _view.SetDetailMessage(
        //        "서버 설정을 다운로드하여 로컬 설정으로 저장했습니다.");

        //    return true;
        //}


        /// <summary>
        /// 서버 설정 버전과 로컬 설정 버전을 비교한다.
        /// 
        /// </summary>
        /// <param name="token">현재 인증 CamViewer의 토큰 정보</param>
        /// <param name="localConfig"> 현재 로컬에 저장된 설정</param>
        /// <returns></returns>
        //private async Task CheckServerConfigVersionAsync(ViewerAuthToken token, ViewerConfig localConfig)
        //{
        //    if (token == null || localConfig == null)
        //    {
        //        return;
        //    }

        //    _view.SetStatus("서버 설정 버전을 확인하고 있습니다.");
        //    ClientResult<ConfigVersionResponseDto> versionResult =
        //        await _clientFacade.GetServerConfigVersionAsync(
        //            token.Token,
        //            localConfig,
        //            token.DeviceCode,
        //            CancellationToken.None);

        //    // 서버의 설정 버전 확인에 실패한 경우, 로컬 설정으로 계속 진행한다.
        //    if (!versionResult.Success || versionResult.Data == null)
        //    {
        //        _view.SetDetailMessage(
        //            "서버 설정 버전 확인에 실패했습니다. 로컬설정으로 실행합니다."
        //            + versionResult.Message);
        //        return;
        //    }

        //    // 설정이 서버에 없는 경우, 로컬 설정으로 계속 진행한다.
        //    if (!versionResult.Data.HasConfig)
        //    {
        //        _view.SetDetailMessage(
        //            "서버에 설정 정보가 없습니다. 로컬설정으로 실행합니다.");
        //        return;
        //    }

        //    // 로컬 설정이 최신인 경우, 로컬 설정으로 계속 진행한다.
        //    if (versionResult.Data.IsLatest)
        //    {
        //        _view.SetDetailMessage(
        //            "로컬 설정이 최신입니다. 로컬설정으로 실행합니다.");
        //        return;
        //    }

        //    // 서버 설정이 최신인 경우, 사용자에게 다운로드 여부를 묻는다.
        //    bool downloadConfim = _view.Confirm(
        //        "서버 설정 버전이 로컬 설정 버전보다 높습니다."
        //        + Environment.NewLine
        //        + "서버에서 최신 설정을 다운로드하시겠습니까?");
        //    if (!downloadConfim)
        //    {
        //        _view.SetDetailMessage("다운로드를 취고 했습니다. 로컬 설정으로 실행합니다.");
        //        return;
        //    }

        //    // 서버 설정 다운로드 실패 또는 사용자가 다운로드를 거부한 경우, 로컬 설정으로 계속 진행한다.
        //    bool downloaded = await TryDownloadServerConfigAsync(token);
        //    if (!downloaded)
        //    {
        //        _view.SetDetailMessage(
        //            "서버 설정 다운로드에 실패했습니다. 로컬설정으로 실행합니다.");
        //        return;
        //    }

        //    if (!CanUseLocalConfig())
        //    {
        //        _view.ShowMessage("설정을 다운로드했지만 검증에 실패 했습니다.");
        //        return;
        //    }
        //    _view.SetDetailMessage("최신 설정을 다운로드하여 로컬 설정을 갱신했습니다.");
        //}

        /// <summary>
        /// 로컬 CamViewer 설정 파일이 존재하고 정상적으로 불러와지는지 확인한다.
        /// 
        /// 사용 시점:
        /// - 서버 설정 다운로드 후 Player 화면으로 이동하기 전
        /// - 오프라인 실행 전
        /// - 로컬 설정 손상 여부를 확인해야 할 때
        /// </summary>
        /// <returns>로컬 설정을 정상적으로 사용할 수 있으면 true.</returns>
        //private bool CanUseLocalConfig()
        //{
        //    if (!_clientFacade.HasLocalConfig())
        //    {
        //        _view.SetDetailMessage(
        //            "로컬 설정 파일이 존재하지 않습니다.");

        //        return false;
        //    }

        //    ClientResult<ViewerConfig> loadConfigResult =
        //        _clientFacade.LoadLocalConfig();

        //    if (!loadConfigResult.Success || loadConfigResult.Data == null)
        //    {
        //        _view.SetDetailMessage(
        //            "로컬 설정 파일을 불러올 수 없습니다. "
        //            + loadConfigResult.Message);

        //        return false;
        //    }

        //    return true;
        //}

        /// <summary>
        /// 온라인 인증 이후 설정 확인 결과에 따라 다음 화면 흐름을 처리한다.
        /// 
        /// 이 메서드는 실제 인증/설정 판단을 하지 않고,
        /// LandingStartupService가 반환한 결과에 따라
        /// 메시지 표시, 설정창 표시, 서버 설정 다운로드 확인,
        /// Player 화면 이동만 담당한다.
        /// </summary>
        /// <param name="result">시작 흐름 처리 결과.</param>
        /// <param name="token">현재 인증된 CamViewer 토큰 정보.</param>
        private async Task HandleOnlineStartupResultAsync(
            StartupFlowResult result,
            ViewerAuthToken token)
        {
            if (result == null)
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "시작 흐름 처리 결과가 없습니다.");

                _view.CloseView();
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.DetailMessage))
            {
                _view.SetDetailMessage(
                    result.DetailMessage);
            }

            if (result.NextAction == StartupNextAction.ContinueToPlayer)
            {
                _view.SetProgressVisible(false);

                ContinueToPlayer();
                return;
            }

            if (result.NextAction == StartupNextAction.OpenSettings)
            {
                _view.SetProgressVisible(false);

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    _view.ShowMessage(
                        result.Message);
                }

                bool loginSuccess =
                    ShowLoginView();

                if (!loginSuccess)
                {
                    _view.CloseView();
                    return;
                }

                if (_openSettingsFunc == null)
                {
                    _view.ShowMessage(
                        "설정 화면을 열 수 없습니다.");

                    return;
                }

                bool onlineSettingsSaved =
                    _openSettingsFunc();

                if (onlineSettingsSaved)
                {
                    await RunStartupFlowCoreAsync();
                }

                return;
            }

            if (result.NextAction == StartupNextAction.ConfirmServerConfigDownload)
            {
                _view.SetProgressVisible(false);

                bool downloadConfirm =
                    _view.Confirm(
                        result.Message);

                if (!downloadConfirm)
                {
                    if (result.CanContinueWithoutDownload)
                    {
                        _view.SetDetailMessage(
                            "사용자가 서버 설정 다운로드를 취소했습니다. 로컬 설정으로 실행합니다.");

                        ContinueToPlayer();
                        return;
                    }

                    _view.SetDetailMessage(
                        "서버 설정 다운로드가 취소되었습니다. 로컬 설정이 없어 PlayerView를 실행할 수 없습니다.");

                    _view.ShowMessage(
                        "로컬 설정이 없어 캠뷰어를 실행할 수 없습니다."
                        + Environment.NewLine
                        + "설정 화면에서 직접 저장하거나 서버 설정을 다운로드해야 합니다.");

                    if (_openSettingsFunc == null)
                    {
                        _view.CloseView();
                        return;
                    }

                    bool saved =
                        _openSettingsFunc();

                    if (saved)
                    {
                        await RunStartupFlowCoreAsync();
                    }
                    else
                    {
                        _view.CloseView();
                    }

                    return;
                }

                _view.SetProgressVisible(true);

                string hwid = _environmentProvider.GetHwid();

                string programVersion = _environmentProvider.GetProgramVersion();

                StartupFlowResult downloadResult =
                    await _startupService.DownloadServerConfigAsync(
                        token,
                        hwid,
                        programVersion,
                        CancellationToken.None);

                await HandleOnlineStartupResultAsync(
                    downloadResult,
                    token);

                return;
            }

            if (result.NextAction == StartupNextAction.ShowLogin)
            {
                _view.SetProgressVisible(false);

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    _view.ShowMessage(
                        result.Message);
                }

                bool loginSuccess =
                    ShowLoginView();

                if (loginSuccess)
                {
                    await RunStartupFlowCoreAsync();
                }
                else
                {
                    _view.CloseView();
                }

                return;
            }

            if (result.NextAction == StartupNextAction.Close)
            {
                _view.SetProgressVisible(false);

                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    _view.ShowMessage(
                        result.Message);
                }

                _view.CloseView();
                return;
            }

            _view.SetProgressVisible(false);

            _view.ShowMessage(
                "처리할 수 없는 시작 흐름 상태입니다."
                + Environment.NewLine
                + result.NextAction);

            _view.CloseView();
        }
    }
}