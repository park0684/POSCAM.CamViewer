using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CamViewer.Interfaces;
using CamViewer.Services;
using CamViewerClient;
using CamViewerClient.Models.Auth;
using CamViewerClient.Models.Config;
using CamViewerClient.Results;

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

        private bool _isRunning;

        /// <summary>
        /// LandingPresenter를 초기화한다.
        /// </summary>
        public LandingPresenter(
            ILandingView view,
            CamViewerClientFacade clientFacade,
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
                HandleOfflineFlow();
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

            await RunStartupFlowAsync();
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
        /// </summary>
        private void HandleOfflineFlow()
        {
            ClientResult<ViewerAuthToken> offlineResult =
                _clientFacade.CheckOfflineToken();

            if (offlineResult.Success)
            {
                _view.SetProgressVisible(false);
                _view.ShowMessage(
                    "인증서버에 연결할 수 없습니다."
                    + Environment.NewLine
                    + "오프라인 실행 허용 기간 내이므로 캠뷰어를 실행합니다.");

                ContinueToPlayer();
                return;
            }

            _view.SetProgressVisible(false);
            _view.ShowMessage(
                "인증서버에 연결할 수 없습니다."
                + Environment.NewLine
                + "오프라인 실행 허용 기간이 만료되었습니다."
                + Environment.NewLine
                + "서버 연결 후 다시 로그인해 주세요.");

            bool loginSuccess = ShowLoginView();

            if (!loginSuccess)
            {
                _view.CloseView();
            }
        }

        /// <summary>
        /// 설정 파일 상태를 확인하고 다음 화면으로 진행한다.
        /// </summary>
        private async Task CheckConfigAndContinueAsync(
            ViewerAuthToken token)
        {
            _view.SetStatus("캠뷰어 설정 정보를 확인하고 있습니다.");

            if (!_clientFacade.HasLocalConfig())
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "로컬 설정 정보가 없습니다."
                    + Environment.NewLine
                    + "설정 화면에서 캠뷰어 설정을 등록해 주세요.");

                bool loginSuccess = ShowLoginView();

                if (!loginSuccess)
                {
                    _view.CloseView();
                    return;
                }

                if (_openSettingsFunc == null)
                {
                    return;
                }

                bool saved = _openSettingsFunc();

                if (saved)
                {
                    await RunStartupFlowCoreAsync();
                }

                return;
            }

            ClientResult<ViewerConfig> loadConfigResult =
                _clientFacade.LoadLocalConfig();

            if (!loadConfigResult.Success)
            {
                _view.SetProgressVisible(false);

                _view.ShowMessage(
                    "로컬 설정 정보를 불러올 수 없습니다."
                    + Environment.NewLine
                    + loadConfigResult.Message
                    + Environment.NewLine
                    + "설정 화면에서 다시 저장해 주세요.");

                bool loginSuccess = ShowLoginView();

                if (!loginSuccess)
                {
                    _view.CloseView();
                    return;
                }

                if (_openSettingsFunc == null)
                {
                    return;
                }

                bool saved = _openSettingsFunc();

                if (saved)
                {
                    await RunStartupFlowCoreAsync();
                }

                return;
            }

            // 이후 단계에서 서버 설정 버전 확인 api/config/version을 연결한다.
            await Task.CompletedTask;

            ContinueToPlayer();
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
        /// 서버 접속 실패 또는 네트워크 오류 여부를 판단한다.
        /// </summary>
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
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}