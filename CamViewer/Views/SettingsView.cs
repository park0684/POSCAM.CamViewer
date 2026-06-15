using CamViewer.Interfaces;
using CamViewer.Models.ViewModels;
using CamViewerClient.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace CamViewer.Views
{
    public partial class SettingsView : Form, ISettingsView
    {
        private BindingList<NvrListItem> _nvrItems;
        private BindingList<CounterMapListItem> _counterMapItems;

        /// <summary>
        /// 설정 화면을 초기화한다.
        /// </summary>
        public SettingsView()
        {
            InitializeComponent();

            InitializeNvrGrid();
            InitializeCounterMapGrid();
            WireEvents();
        }

        /// <summary>
        /// 현재 선택된 NVR번호를 반환한다.
        /// 선택된 행이 없으면 null을 반환한다.
        /// </summary>
        public int? SelectedNvrNo
        {
            get
            {
                NvrListItem selectedItem = GetSelectedNvrItem();

                if (selectedItem == null)
                {
                    return null;
                }

                return selectedItem.NvrNo;
            }
        }

        /// <summary>
        /// 현재 선택된 계산대 등록정보의 식별 키를 반환한다.
        /// 선택된 행이 없으면 null을 반환한다.
        /// </summary>
        public CounterMapKey SelectedCounterMapKey
        {
            get
            {
                CounterMapListItem selectedItem = GetSelectedCounterMapItem();

                if (selectedItem == null)
                {
                    return null;
                }

                return new CounterMapKey
                {
                    CounterNo = selectedItem.CounterNo,
                    ScreenPosition = (ScreenPosition)selectedItem.ScreenPosition
                };
            }
        }

        /// <summary>
        /// 영상검색 기준 시각 이전에 조회할 시간(초).
        /// </summary>
        public int? AdjustSecond
        {
            get
            {
                int seconds;

                if (!int.TryParse(
                    txtAdjust.Text.Trim(),
                    out seconds))
                {
                    return null;
                }

                return seconds;
            }
            set
            {
                txtAdjust.Text =
                    value.HasValue
                        ? value.Value.ToString()
                        : string.Empty;
            }
        }

        public event EventHandler LoadViewEvent;
        public event EventHandler AddNvrEvent;
        public event EventHandler EditNvrEvent;
        public event EventHandler DeleteNvrEvent;
        public event EventHandler AddCounterMapEvent;
        public event EventHandler EditCounterMapEvent;
        public event EventHandler DeleteCounterMapEvent;
        public event EventHandler SaveEvent;
        public event EventHandler CloseEvent;

        /// <summary>
        /// NVR 목록을 화면에 표시한다.
        /// </summary>
        /// <param name="items">화면에 표시할 NVR 목록.</param>
        public void SetNvrList(IEnumerable<NvrListItem> items)
        {
            List<NvrListItem> itemList = items == null
                ? new List<NvrListItem>()
                : items.ToList();

            _nvrItems = new BindingList<NvrListItem>(itemList);

            dgvNvr.DataSource = null;
            dgvNvr.DataSource = _nvrItems;
        }

        /// <summary>
        /// 계산대 등록 목록을 화면에 표시한다.
        /// </summary>
        /// <param name="items">화면에 표시할 계산대 등록 목록.</param>
        public void SetCounterMapList(IEnumerable<CounterMapListItem> items)
        {
            List<CounterMapListItem> itemList = items == null
                ? new List<CounterMapListItem>()
                : items.ToList();

            _counterMapItems = new BindingList<CounterMapListItem>(itemList);

            dgvCounterMap.DataSource = null;
            dgvCounterMap.DataSource = _counterMapItems;
        }

        /// <summary>
        /// 현재 설정 버전과 동기화 상태를 화면에 표시한다.
        /// </summary>
        /// <param name="configVersion">설정 버전.</param>
        /// <param name="syncStatus">설정 동기화 상태.</param>
        /// <param name="lastDownloadedAtUtc">마지막 다운로드 UTC 일시.</param>
        /// <param name="lastUploadedAtUtc">마지막 업로드 UTC 일시.</param>
        public void SetConfigStatus(
            string configVersion,
            ViewerConfigSyncStatus syncStatus,
            DateTime? lastDownloadedAtUtc,
            DateTime? lastUploadedAtUtc)
        {
            string versionText =
                string.IsNullOrWhiteSpace(configVersion)
                    ? "-"
                    : configVersion;

            string downloadedText =
                lastDownloadedAtUtc.HasValue
                    ? lastDownloadedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-";

            string uploadedText =
                lastUploadedAtUtc.HasValue
                    ? lastUploadedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-";

            lblConfigStatus.Text = string.Format(
                "설정 버전: {0} 동기화 상태: {1} 다운로드: {2} 업로드: {3}",
                versionText,
                syncStatus,
                downloadedText,
                uploadedText);
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
                "캠뷰어 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 사용자에게 예/아니오 확인 메시지를 표시한다.
        /// </summary>
        /// <param name="message">확인할 메시지.</param>
        /// <returns>사용자가 예를 선택하면 true.</returns>
        public bool Confirm(string message)
        {
            DialogResult result = MessageBox.Show(
                this,
                message,
                "캠뷰어 설정",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        /// <summary>
        /// 설정 화면을 모달로 표시한다.
        /// </summary>
        public void ShowView()
        {
            ShowDialog();
        }

        /// <summary>
        /// 설정 화면을 닫는다.
        /// </summary>
        public void CloseView()
        {
            Close();
        }

        /// <summary>
        /// NVR 목록 DataGridView의 기본 속성과 컬럼을 초기화한다.
        /// </summary>
        private void InitializeNvrGrid()
        {
            dgvNvr.AutoGenerateColumns = false;
            dgvNvr.AllowUserToAddRows = false;
            dgvNvr.AllowUserToDeleteRows = false;
            dgvNvr.AllowUserToResizeRows = false;
            dgvNvr.MultiSelect = false;
            dgvNvr.ReadOnly = true;
            dgvNvr.RowHeadersVisible = false;
            dgvNvr.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvNvr.RowTemplate.Height = 30;
            dgvNvr.RowTemplate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvNvr.RowTemplate.DefaultCellStyle.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
            dgvNvr.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvNvr.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 12, FontStyle.Regular);

            dgvNvr.Columns.Clear();

            dgvNvr.Columns.Add(CreateTextColumn(
                "NvrNo",
                "NVR번호",
                100));

            dgvNvr.Columns.Add(CreateTextColumn(
                "Vendor",
                "제조사",
                110));

            dgvNvr.Columns.Add(CreateTextColumn(
                "ConnectionType",
                "접속방식",
                100));

            dgvNvr.Columns.Add(CreateTextColumn(
                "Host",
                "IP",
                200));

            dgvNvr.Columns.Add(CreateTextColumn(
                "Port",
                "PORT",
                70));

            dgvNvr.Columns.Add(CreateTextColumn(
                "ChannelCount",
                "채널수",
                70));

            dgvNvr.Columns.Add(CreateTextColumn(
                "UserId",
                "ID",
                120));

            dgvNvr.Columns.Add(CreateButtonColumn(
                "colNvrEdit",
                "수정"));

            dgvNvr.Columns.Add(CreateButtonColumn(
                "colNvrDelete",
                "삭제"));
        }

        /// <summary>
        /// 계산대 등록 목록 DataGridView의 기본 속성과 컬럼을 초기화한다.
        /// </summary>
        private void InitializeCounterMapGrid()
        {
            dgvCounterMap.AutoGenerateColumns = false;
            dgvCounterMap.AllowUserToAddRows = false;
            dgvCounterMap.AllowUserToDeleteRows = false;
            dgvCounterMap.AllowUserToResizeRows = false;
            dgvCounterMap.MultiSelect = false;
            dgvCounterMap.ReadOnly = true;
            dgvCounterMap.RowHeadersVisible = false;
            dgvCounterMap.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvCounterMap.RowTemplate.Height = 30;
            dgvCounterMap.RowTemplate.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvCounterMap.RowTemplate.DefaultCellStyle.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
            dgvCounterMap.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvCounterMap.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 12, FontStyle.Regular);

            dgvCounterMap.Columns.Clear();

            dgvCounterMap.Columns.Add(CreateTextColumn(
                "CounterNo",
                "계산대번호",
                190));

            dgvCounterMap.Columns.Add(CreateTextColumn(
                "NvrNo",
                "NVR번호",
                190));

            dgvCounterMap.Columns.Add(CreateTextColumn(
                "ChannelNo",
                "채널번호",
                190));

            dgvCounterMap.Columns.Add(CreateTextColumn(
                "ScreenPositionText",
                "스크린위치",
                190));

            dgvCounterMap.Columns.Add(CreateButtonColumn(
                "colCounterEdit",
                "수정"));

            dgvCounterMap.Columns.Add(CreateButtonColumn(
                "colCounterDelete",
                "삭제"));
        }

        /// <summary>
        /// 화면 컨트롤 이벤트를 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;

            btnAddNvr.Click += OnAddNvrButtonClick;
            btnAddCounterMap.Click += OnAddCounterMapButtonClick;
            btnSave.Click += OnSaveButtonClick;
            btnClose.Click += OnCloseButtonClick;

            dgvNvr.CellContentClick += OnNvrGridCellContentClick;
            dgvCounterMap.CellContentClick += OnCounterMapGridCellContentClick;
        }

        /// <summary>
        /// 설정 화면이 최초 표시될 때 Presenter에 알린다.
        /// </summary>
        private void OnFormLoad(object sender, EventArgs e)
        {
            LoadViewEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// NVR 추가 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnAddNvrButtonClick(object sender, EventArgs e)
        {
            AddNvrEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 계산대 추가 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnAddCounterMapButtonClick(object sender, EventArgs e)
        {
            AddCounterMapEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 설정 저장 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnSaveButtonClick(object sender, EventArgs e)
        {
            SaveEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 설정 화면 닫기 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnCloseButtonClick(object sender, EventArgs e)
        {
            CloseEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// NVR 목록의 수정 또는 삭제 버튼 클릭을 처리한다.
        /// </summary>
        private void OnNvrGridCellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            dgvNvr.CurrentCell = dgvNvr.Rows[e.RowIndex].Cells[e.ColumnIndex];

            string columnName = dgvNvr.Columns[e.ColumnIndex].Name;

            if (columnName == "colNvrEdit")
            {
                EditNvrEvent?.Invoke(this, EventArgs.Empty);
            }
            else if (columnName == "colNvrDelete")
            {
                DeleteNvrEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 계산대 등록 목록의 수정 또는 삭제 버튼 클릭을 처리한다.
        /// </summary>
        private void OnCounterMapGridCellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            dgvCounterMap.CurrentCell =
                dgvCounterMap.Rows[e.RowIndex].Cells[e.ColumnIndex];

            string columnName = dgvCounterMap.Columns[e.ColumnIndex].Name;

            if (columnName == "colCounterEdit")
            {
                EditCounterMapEvent?.Invoke(this, EventArgs.Empty);
            }
            else if (columnName == "colCounterDelete")
            {
                DeleteCounterMapEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 현재 선택된 NVR 행의 데이터를 반환한다.
        /// </summary>
        private NvrListItem GetSelectedNvrItem()
        {
            if (dgvNvr.CurrentRow == null)
            {
                return null;
            }

            return dgvNvr.CurrentRow.DataBoundItem as NvrListItem;
        }

        /// <summary>
        /// 현재 선택된 계산대 등록 행의 데이터를 반환한다.
        /// </summary>
        private CounterMapListItem GetSelectedCounterMapItem()
        {
            if (dgvCounterMap.CurrentRow == null)
            {
                return null;
            }

            return dgvCounterMap.CurrentRow.DataBoundItem as CounterMapListItem;
        }

        /// <summary>
        /// DataGridView 텍스트 컬럼을 생성한다.
        /// </summary>
        private static DataGridViewTextBoxColumn CreateTextColumn(
            string propertyName,
            string headerText,
            int width)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                Name = "col" + propertyName,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        /// <summary>
        /// DataGridView 버튼 컬럼을 생성한다.
        /// </summary>
        private static DataGridViewButtonColumn CreateButtonColumn(
            string name,
            string text)
        {
            return new DataGridViewButtonColumn
            {
                Name = name,
                HeaderText = string.Empty,
                Text = text,
                UseColumnTextForButtonValue = true,
                Width = 80
            };
        }
    }
}
