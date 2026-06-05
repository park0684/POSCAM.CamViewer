using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;

namespace CamViewer.Views
{
    /// <summary>
    /// 계산대번호와 NVR 채널, 스크린위치를 등록하거나 수정하는 팝업 화면이다.
    /// </summary>
    public partial class CounterEditView : Form, ICounterEditView
    {
        /// <summary>
        /// 계산대 등록/수정 화면을 초기화한다.
        /// </summary>
        public CounterEditView()
        {
            InitializeComponent();

            InitializeControls();
            WireEvents();
        }

        /// <summary>
        /// 입력된 계산대번호를 반환하거나 설정한다.
        /// </summary>
        public int? CounterNo
        {
            get
            {
                return Convert.ToInt32(nudCounterNo.Value);
            }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                nudCounterNo.Value = Clamp(
                    value.Value,
                    nudCounterNo.Minimum,
                    nudCounterNo.Maximum);
            }
        }

        /// <summary>
        /// 선택된 NVR번호를 반환하거나 설정한다.
        /// </summary>
        public int? SelectedNvrNo
        {
            get
            {
                NvrOptionItem selectedItem =
                    cboNvr.SelectedItem as NvrOptionItem;

                if (selectedItem == null)
                {
                    return null;
                }

                return selectedItem.NvrNo;
            }
            set
            {
                if (!value.HasValue)
                {
                    cboNvr.SelectedIndex = -1;
                    return;
                }

                for (int index = 0; index < cboNvr.Items.Count; index++)
                {
                    NvrOptionItem item =
                        cboNvr.Items[index] as NvrOptionItem;

                    if (item != null && item.NvrNo == value.Value)
                    {
                        cboNvr.SelectedIndex = index;
                        return;
                    }
                }

                cboNvr.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// 입력된 채널번호를 반환하거나 설정한다.
        /// </summary>
        public int? ChannelNo
        {
            get
            {
                return Convert.ToInt32(nudChannelNo.Value);
            }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                nudChannelNo.Value = Clamp(
                    value.Value,
                    nudChannelNo.Minimum,
                    nudChannelNo.Maximum);
            }
        }

        /// <summary>
        /// 선택된 스크린위치를 반환하거나 설정한다.
        /// </summary>
        public ScreenPosition ScreenPosition
        {
            get
            {
                return cboScreenPosition.SelectedIndex == 1
                    ? ScreenPosition.Right
                    : ScreenPosition.Left;
            }
            set
            {
                cboScreenPosition.SelectedIndex =
                    value == ScreenPosition.Right
                        ? 1
                        : 0;
            }
        }

        public event EventHandler LoadViewEvent;
        public event EventHandler NvrChangedEvent;
        public event EventHandler SaveEvent;
        public event EventHandler CloseEvent;

        /// <summary>
        /// 선택할 수 있는 NVR 목록을 설정한다.
        /// </summary>
        public void SetNvrOptions(IEnumerable<NvrOptionItem> items)
        {
            List<NvrOptionItem> itemList = items == null
                ? new List<NvrOptionItem>()
                : items.ToList();

            var bindingList =
                new BindingList<NvrOptionItem>(itemList);

            cboNvr.DataSource = null;
            cboNvr.DisplayMember = "DisplayText";
            cboNvr.ValueMember = "NvrNo";
            cboNvr.DataSource = bindingList;

            if (bindingList.Count == 0)
            {
                cboNvr.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// 선택된 NVR의 채널번호 입력 가능 범위를 설정한다.
        /// </summary>
        public void SetChannelRange(int minimum, int maximum)
        {
            if (minimum < 1)
            {
                minimum = 1;
            }

            if (maximum < minimum)
            {
                maximum = minimum;
            }

            nudChannelNo.Minimum = minimum;
            nudChannelNo.Maximum = maximum;

            if (nudChannelNo.Value < nudChannelNo.Minimum)
            {
                nudChannelNo.Value = nudChannelNo.Minimum;
            }

            if (nudChannelNo.Value > nudChannelNo.Maximum)
            {
                nudChannelNo.Value = nudChannelNo.Maximum;
            }

            lblChannelRange.Text = string.Format(
                "입력 가능 채널: {0} ~ {1}",
                minimum,
                maximum);
        }

        /// <summary>
        /// 사용자에게 안내 메시지를 표시한다.
        /// </summary>
        public void ShowMessage(string message)
        {
            MessageBox.Show(
                this,
                message,
                "계산대 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 계산대 등록/수정 팝업을 모달로 표시한다.
        /// </summary>
        public void ShowView()
        {
            this.StartPosition = FormStartPosition.CenterParent;
            ShowDialog();
        }

        /// <summary>
        /// 계산대 등록/수정 팝업을 닫는다.
        /// </summary>
        public void CloseView()
        {
            Close();
        }

        /// <summary>
        /// 화면 컨트롤의 기본 속성과 초기값을 설정한다.
        /// </summary>
        private void InitializeControls()
        {
            nudCounterNo.Minimum = 1;
            nudCounterNo.Maximum = 9999;
            nudCounterNo.Value = 1;

            nudChannelNo.Minimum = 1;
            nudChannelNo.Maximum = 1;
            nudChannelNo.Value = 1;

            cboNvr.DropDownStyle =
                ComboBoxStyle.DropDownList;

            cboScreenPosition.DropDownStyle =
                ComboBoxStyle.DropDownList;

            cboScreenPosition.Items.Clear();
            cboScreenPosition.Items.Add("좌측");
            cboScreenPosition.Items.Add("우측");
            cboScreenPosition.SelectedIndex = 0;

            SetChannelRange(1, 1);
        }

        /// <summary>
        /// 화면 컨트롤 이벤트를 Presenter 이벤트와 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;
            cboNvr.SelectedIndexChanged += OnNvrSelectedIndexChanged;
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
        /// NVR 선택값 변경을 Presenter에 전달한다.
        /// </summary>
        private void OnNvrSelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            NvrChangedEvent?.Invoke(this, EventArgs.Empty);
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
