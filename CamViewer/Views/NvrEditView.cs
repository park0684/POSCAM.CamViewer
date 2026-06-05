using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;

namespace CamViewer.Views
{
    /// <summary>
    /// NVR 접속정보를 등록하거나 수정하는 팝업 화면이다.
    /// 
    /// 사용자는 제조사만 선택하며,
    /// 접속방식과 ProviderKey는 제조사에 따라 자동으로 결정된다.
    /// </summary>
    public partial class NvrEditView : Form, INvrEditView
    {
        private IDictionary<string, string> _providerSettings;
        /// <summary>
        /// NVR 등록/수정 화면을 초기화한다.
        /// </summary>
        public NvrEditView()
        {
            InitializeComponent();

            _providerSettings = new Dictionary<string, string>();

            InitializeControls();
            WireEvents();
        }

        /// <summary>
        /// 현재 선택된 제조사명을 반환하거나 선택한다.
        /// </summary>
        public string SelectedVendor
        {
            get
            {
                VendorOptionItem selectedItem =
                    cboVendor.SelectedItem as VendorOptionItem;

                if (selectedItem == null)
                {
                    return null;
                }

                return selectedItem.Vendor;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    cboVendor.SelectedIndex = -1;
                    return;
                }

                for (int index = 0; index < cboVendor.Items.Count; index++)
                {
                    VendorOptionItem item =
                        cboVendor.Items[index] as VendorOptionItem;

                    if (item == null)
                    {
                        continue;
                    }

                    if (string.Equals(
                        item.Vendor,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        cboVendor.SelectedIndex = index;
                        return;
                    }
                }

                cboVendor.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// 입력된 NVR IP 주소 또는 도메인을 반환하거나 설정한다.
        /// </summary>
        public string Host
        {
            get { return txtHost.Text.Trim(); }
            set { txtHost.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// 입력된 NVR 포트를 반환하거나 설정한다.
        /// </summary>
        public int? Port
        {
            get { return Convert.ToInt32(nudPort.Value); }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                nudPort.Value = Clamp(
                    value.Value,
                    nudPort.Minimum,
                    nudPort.Maximum);
            }
        }

        /// <summary>
        /// 입력된 NVR 채널 수를 반환하거나 설정한다.
        /// </summary>
        public int? ChannelCount
        {
            get { return Convert.ToInt32(nudChannelCount.Value); }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                nudChannelCount.Value = Clamp(
                    value.Value,
                    nudChannelCount.Minimum,
                    nudChannelCount.Maximum);
            }
        }

        /// <summary>
        /// 입력된 NVR 로그인 ID를 반환하거나 설정한다.
        /// </summary>
        public string UserId
        {
            get { return txtUserId.Text.Trim(); }
            set { txtUserId.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// 입력된 NVR 로그인 비밀번호를 반환하거나 설정한다.
        /// </summary>
        public string Password
        {
            get { return txtPassword.Text; }
            set { txtPassword.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Provider별 추가 설정값을 반환하거나 설정한다.
        /// </summary>
        public IDictionary<string, string> ProviderSettings
        {
            get
            {
                return new Dictionary<string, string>(_providerSettings);
            }
            set
            {
                _providerSettings = value == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(value);
            }
        }

        public event EventHandler LoadViewEvent;
        public event EventHandler VendorChangedEvent;
        public event EventHandler TestConnectionEvent;
        public event EventHandler SaveEvent;
        public event EventHandler CloseEvent;

        /// <summary>
        /// 팝업 화면의 제목을 설정한다.
        /// </summary>
        /// <param name="title">화면 제목.</param>
        public void SetTitle(string title)
        {
            Text = string.IsNullOrWhiteSpace(title)
                ? "NVR 설정"
                : title;
        }

        /// <summary>
        /// 신규 등록 또는 수정 대상 NVR번호를 화면에 표시한다.
        /// </summary>
        /// <param name="displayText">표시할 NVR번호 문자열.</param>
        public void SetNvrNoDisplay(string displayText)
        {
            lblNvrNoValue.Text = string.IsNullOrWhiteSpace(displayText)
                ? "-"
                : displayText;
        }

        /// <summary>
        /// 선택할 수 있는 제조사 목록을 설정한다.
        /// </summary>
        /// <param name="items">제조사 선택 목록.</param>
        public void SetVendorOptions(IEnumerable<VendorOptionItem> items)
        {
            List<VendorOptionItem> itemList = items == null
                ? new List<VendorOptionItem>()
                : items.ToList();

            var bindingList =
                new BindingList<VendorOptionItem>(itemList);

            cboVendor.DataSource = null;
            cboVendor.DisplayMember = "DisplayText";
            cboVendor.ValueMember = "Vendor";
            cboVendor.DataSource = bindingList;

            if (bindingList.Count == 0)
            {
                cboVendor.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// 선택된 제조사에 고정된 접속방식을 화면에 표시한다.
        /// </summary>
        /// <param name="connectionType">접속방식 문자열.</param>
        public void SetConnectionType(string connectionType)
        {
            txtConnectionType.Text = connectionType ?? string.Empty;
        }

        /// <summary>
        /// 연결 테스트 진행 상태를 화면에 표시한다.
        /// </summary>
        /// <param name="isRunning">연결 테스트 진행 여부.</param>
        /// <param name="statusText">화면에 표시할 상태 문자열.</param>
        public void SetConnectionTestState(
            bool isRunning,
            string statusText)
        {
            btnTestConnection.Enabled = !isRunning;
            btnSave.Enabled = !isRunning;
            cboVendor.Enabled = !isRunning;

            lblConnectionTestStatus.Text =
                statusText ?? string.Empty;

            UseWaitCursor = isRunning;
        }

        /// <summary>
        /// 사용자에게 안내 메시지를 표시한다.
        /// </summary>
        /// <param name="message">표시할 메시지.</param>
        public void ShowMessage(string message)
        {
            MessageBox.Show(
                this,
                message,
                "NVR 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// NVR 등록/수정 팝업을 모달로 표시한다.
        /// </summary>
        public void ShowView()
        {
            this.StartPosition = FormStartPosition.CenterParent;
            ShowDialog();
        }

        /// <summary>
        /// NVR 등록/수정 팝업을 닫는다.
        /// </summary>
        public void CloseView()
        {
            Close();
        }

        /// <summary>
        /// 화면 컨트롤의 기본 속성을 초기화한다.
        /// </summary>
        private void InitializeControls()
        {
            cboVendor.DropDownStyle = ComboBoxStyle.DropDownList;

            txtConnectionType.ReadOnly = true;
            txtConnectionType.TabStop = false;

            txtPassword.UseSystemPasswordChar = true;

            nudPort.Minimum = 1;
            nudPort.Maximum = 65535;
            nudPort.Value = 37777;

            nudChannelCount.Minimum = 1;
            nudChannelCount.Maximum = 999;
            nudChannelCount.Value = 4;

            lblNvrNoValue.Text = "자동 지정";
            lblConnectionTestStatus.Text = string.Empty;
        }

        /// <summary>
        /// 화면 컨트롤 이벤트를 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;
            cboVendor.SelectedIndexChanged += OnVendorSelectedIndexChanged;
            btnTestConnection.Click += OnTestConnectionButtonClick;
            btnSave.Click += OnSaveButtonClick;
            btnClose.Click += OnCloseButtonClick;
        }

        /// <summary>
        /// 화면이 최초 표시될 때 Presenter에 알린다.
        /// </summary>
        private void OnFormLoad(object sender, EventArgs e)
        {
            LoadViewEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 제조사 선택값 변경을 Presenter에 전달한다.
        /// </summary>
        private void OnVendorSelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            VendorChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 연결 테스트 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnTestConnectionButtonClick(
            object sender,
            EventArgs e)
        {
            TestConnectionEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 저장 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnSaveButtonClick(
            object sender,
            EventArgs e)
        {
            SaveEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 닫기 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnCloseButtonClick(
            object sender,
            EventArgs e)
        {
            CloseEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// NumericUpDown에 입력할 값을 허용 범위 안으로 제한한다.
        /// </summary>
        private static decimal Clamp(
            int value,
            decimal minimum,
            decimal maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }
    }
}
