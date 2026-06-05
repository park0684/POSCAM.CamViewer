using System;
using System.Threading;
using System.Threading.Tasks;
using CamViewer.Interfaces;
using CamViewer.Services;
using CamViewerClient;
using CamViewerClient.Models.Auth;
using CamViewerClient.Results;

namespace CamViewer.Presenters
{
    /// <summary>
    /// 캠뷰어 로그인 화면의 입력값 검증과 로그인 API 호출을 처리한다.
    ///
    /// 사용자는 매장코드와 비밀번호만 입력한다.
    /// HWID, 장비명, 프로그램 버전은 실행 환경에서 자동 생성한다.
    /// </summary>
    public sealed class LoginPresenter
    {
        private readonly ILoginView _view;
        private readonly CamViewerClientFacade _clientFacade;
        private readonly IClientEnvironmentProvider _environmentProvider;

        private bool _isLoginRunning;

        /// <summary>
        /// LoginPresenter를 초기화한다.
        /// </summary>
        public LoginPresenter(
            ILoginView view,
            CamViewerClientFacade clientFacade,
            IClientEnvironmentProvider environmentProvider)
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

            _view = view;
            _clientFacade = clientFacade;
            _environmentProvider = environmentProvider;

            _view.LoadViewEvent += OnLoadView;
            _view.LoginEvent += OnLogin;
            _view.ExitEvent += OnExit;
        }

        /// <summary>
        /// 로그인 화면을 모달로 표시하고 로그인 성공 여부를 반환한다.
        /// </summary>
        public bool ShowDialog()
        {
            return _view.ShowDialogView();
        }

        /// <summary>
        /// 로그인 화면을 일반 Form으로 표시한다.
        /// </summary>
        public void Show()
        {
            _view.ShowView();
        }

        /// <summary>
        /// 로그인 화면 초기 메시지를 설정한다.
        /// </summary>
        private void OnLoadView(object sender, EventArgs e)
        {
            _view.SetBusy(false, string.Empty);
            _view.SetMessage(string.Empty);

            string deviceName = _environmentProvider.GetDeviceName();
            _view.SetDeviceName(deviceName);
        }

        /// <summary>
        /// 로그인 버튼 클릭 시 입력값을 검증하고 서버 로그인을 시도한다.
        /// </summary>
        private async void OnLogin(object sender, EventArgs e)
        {
            if (_isLoginRunning)
            {
                return;
            }

            string validationMessage = ValidateInput();

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                _view.SetMessage(validationMessage);
                return;
            }

            await LoginAsync();
        }

        /// <summary>
        /// 종료 버튼 클릭 시 로그인 화면을 취소 상태로 닫는다.
        /// </summary>
        private void OnExit(object sender, EventArgs e)
        {
            if (_isLoginRunning)
            {
                return;
            }

            _view.CloseWithCancel();
        }

        /// <summary>
        /// 로그인 입력값을 검증한다.
        /// </summary>
        private string ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_view.StoreId))
            {
                return "매장코드를 입력해 주세요.";
            }

            if (string.IsNullOrWhiteSpace(_view.StorePassword))
            {
                return "비밀번호를 입력해 주세요.";
            }

            if (string.IsNullOrWhiteSpace(_view.DeviceName))
            {
                return "PC명을 입력해 주세요.";
            }

            return null;
        }

        /// <summary>
        /// 서버 로그인 API를 호출하고 성공 시 로컬 토큰을 저장한다.
        /// </summary>
        private async Task LoginAsync()
        {
            try
            {
                _isLoginRunning = true;

                _view.SetBusy(
                    true,
                    "캠뷰어 인증을 확인하고 있습니다.");

                string hwid = _environmentProvider.GetHwid();
                string deviceName = _environmentProvider.GetDeviceName();
                string programVersion =
                    _environmentProvider.GetProgramVersion();

                ClientResult<ViewerAuthToken> loginResult =
                    await _clientFacade.LoginViewerAsync(
                        _view.StoreId,
                        _view.StorePassword,
                        hwid,
                        deviceName,
                        programVersion,
                        CancellationToken.None);

                if (!loginResult.Success)
                {
                    _view.SetBusy(false, string.Empty);
                    _view.SetMessage(loginResult.Message);
                    return;
                }

                _view.SetMessage("로그인되었습니다.");
                _view.CloseWithSuccess();
            }
            catch (Exception ex)
            {
                _view.SetBusy(false, string.Empty);
                _view.SetMessage(
                    "로그인 처리 중 오류가 발생했습니다. " + ex.Message);
            }
            finally
            {
                _isLoginRunning = false;
            }
        }
    }
}