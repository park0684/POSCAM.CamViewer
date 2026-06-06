using System;
using System.Windows.Forms;
using CamViewer.Interfaces;

namespace CamViewer.Views
{
    /// <summary>
    /// 캠뷰어 프로그램 시작 시 표시되는 랜딩페이지이다.
    ///
    /// 랜딩페이지는 로그인 입력을 직접 받지 않는다.
    /// 인증 상태, 설정 확인 상태, 실행 준비 상태를 표시하고,
    /// 하단 설정 버튼 클릭을 Presenter에 전달한다.
    /// </summary>
    public partial class LandingView : Form, ILandingView
    {
        /// <summary>
        /// 랜딩페이지를 초기화한다.
        /// </summary>
        public LandingView()
        {
            InitializeComponent();

            InitializeControls();
            WireEvents();
        }

        public event EventHandler LoadViewEvent;
        public event EventHandler SettingsEvent;

        /// <summary>
        /// 현재 상태 메시지를 표시한다.
        /// </summary>
        public void SetStatus(string status)
        {
            lblStatus.Text = status ?? string.Empty;
        }

        /// <summary>
        /// 상세 안내 메시지를 표시한다.
        /// </summary>
        public void SetDetailMessage(string message)
        {
            lblDetailMessage.Text = message ?? string.Empty;
        }

        /// <summary>
        /// 진행 표시 여부를 설정한다.
        /// </summary>
        public void SetProgressVisible(bool visible)
        {
            progressBar.Visible = visible;
        }

        /// <summary>
        /// 설정 버튼 활성화 여부를 설정한다.
        /// </summary>
        public void SetSettingsEnabled(bool enabled)
        {
            btnSettings.Enabled = enabled;
        }

        /// <summary>
        /// 사용자에게 안내 메시지를 표시한다.
        /// </summary>
        public void ShowMessage(string message)
        {
            MessageBox.Show(
                this,
                message,
                "POSCAM 캠뷰어",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 사용자에게 예/아니오 확인 메시지를 표시한다.
        /// </summary>
        public bool Confirm(string message)
        {
            DialogResult result = MessageBox.Show(
                this,
                message,
                "POSCAM 캠뷰어",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        /// <summary>
        /// 랜딩페이지를 표시한다.
        /// </summary>
        public void ShowView()
        {
            Application.Run(this);
        }


        /// <summary>
        /// 랜딩페이지를 숨긴다.
        /// </summary>
        public void HideView()
        {
            Hide();
        }


        /// <summary>
        /// 랜딩페이지를 닫는다.
        /// </summary>
        public void CloseView()
        {
            Close();
        }

        /// <summary>
        /// 화면 컨트롤의 초기 상태를 설정한다.
        /// </summary>
        private void InitializeControls()
        {
            Text = "POSCAM 캠뷰어";

            lblTitle.Text = "POSCAM 캠뷰어";

            lblStatus.Text = "캠뷰어 실행 준비 중입니다.";
            lblDetailMessage.Text = string.Empty;

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;

            btnSettings.Text = "설정";
            btnSettings.Enabled = true;
        }

        /// <summary>
        /// 화면 컨트롤 이벤트를 Presenter 이벤트와 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;
            btnSettings.Click += OnSettingsButtonClick;
        }

        /// <summary>
        /// 화면이 최초 표시될 때 Presenter에 알린다.
        /// </summary>
        private void OnFormLoad(object sender, EventArgs e)
        {
            LoadViewEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 설정 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnSettingsButtonClick(object sender, EventArgs e)
        {
            SettingsEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}