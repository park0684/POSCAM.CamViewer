using System;
using System.Windows.Forms;
using CamViewer.Interfaces;

namespace CamViewer.Views
{
    /// <summary>
    /// 캠뷰어 로그인 화면이다.
    ///
    /// 사용자는 매장코드와 비밀번호만 입력한다.
    /// 입력된 매장코드는 서버 요청에서 StoreId로 전달된다.
    /// </summary>
    public partial class LoginView : Form, ILoginView
    {
        /// <summary>
        /// 로그인 화면을 초기화한다.
        /// </summary>
        public LoginView()
        {
            InitializeComponent();

            InitializeControls();
            WireEvents();
        }

        /// <summary>
        /// 사용자가 입력한 매장코드.
        /// </summary>
        public string StoreId
        {
            get { return txtStoreId.Text.Trim(); }
        }

        /// <summary>
        /// 사용자가 입력한 매장 비밀번호.
        /// </summary>
        public string StorePassword
        {
            get { return txtStorePassword.Text; }
        }

        /// <summary>
        /// 사용자가 입력하거나 수정한 캠뷰어 장비명.
        /// </summary>
        public string DeviceName
        {
            get { return txtDeviceName.Text.Trim(); }
        }


        public event EventHandler LoadViewEvent;
        public event EventHandler LoginEvent;
        public event EventHandler ExitEvent;

        /// <summary>
        /// 로그인 진행 상태를 화면에 표시한다.
        /// </summary>
        public void SetBusy(bool isBusy, string message)
        {
            txtStoreId.Enabled = !isBusy;
            txtStorePassword.Enabled = !isBusy;
            btnLogin.Enabled = !isBusy;
            btnExit.Enabled = !isBusy;

            UseWaitCursor = isBusy;

            SetMessage(message);
        }

        /// <summary>
        /// 로그인 화면 메시지를 표시한다.
        /// </summary>
        public void SetMessage(string message)
        {
            lblMessage.Text = message ?? string.Empty;
        }

        /// <summary>
        /// 로그인 성공 상태로 화면을 닫는다.
        /// </summary>
        public void CloseWithSuccess()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 로그인 취소 또는 종료 상태로 화면을 닫는다.
        /// </summary>
        public void CloseWithCancel()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 로그인 화면을 일반 Form으로 표시한다.
        /// </summary>
        public void ShowView()
        {
            Show();
        }

        /// <summary>
        /// 로그인 화면을 모달로 표시하고 성공 여부를 반환한다.
        /// </summary>
        public bool ShowDialogView()
        {
            return ShowDialog() == DialogResult.OK;
        }

        /// <summary>
        /// 로그인 화면의 장비명 기본값을 설정한다.
        /// </summary>
        public void SetDeviceName(string deviceName)
        {
            txtDeviceName.Text = deviceName ?? string.Empty;
        }

        /// <summary>
        /// 화면 컨트롤의 기본 속성을 설정한다.
        /// </summary>
        private void InitializeControls()
        {
            Text = "캠뷰어 로그인";

            txtStorePassword.UseSystemPasswordChar = true;

            lblMessage.Text = string.Empty;

            AcceptButton = btnLogin;
            CancelButton = btnExit;
        }

        /// <summary>
        /// 화면 컨트롤 이벤트를 Presenter 이벤트와 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;
            btnLogin.Click += OnLoginButtonClick;
            btnExit.Click += OnExitButtonClick;
        }

        /// <summary>
        /// 화면이 최초 표시될 때 Presenter에 알린다.
        /// </summary>
        private void OnFormLoad(object sender, EventArgs e)
        {
            LoadViewEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 로그인 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnLoginButtonClick(object sender, EventArgs e)
        {
            LoginEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 종료 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnExitButtonClick(object sender, EventArgs e)
        {
            ExitEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}