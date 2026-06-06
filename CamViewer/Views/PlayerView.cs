using CamViewer.Interfaces;
using CamViewer.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;



namespace CamViewer.Views
{
    /// <summary>
    /// 캠뷰어 영상 재생 화면이다.
    ///
    /// 실제 NVR 재생은 Presenter와 NVR Provider에서 처리하며,
    /// 이 View는 사용자 입력과 영상 출력 패널 Handle을 제공한다.
    /// </summary>
    public partial class PlayerView : Form, IPlayerView
    {

        private Rectangle _normalBounds;
        private bool _isMaximized;
        private bool _normalBoundsCaptured;

        private const int ResizeBorderThickness = 3;

        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        private const int WmNclButtonDown = 0xA1;
        private const int HtCaption = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            int wParam,
            int lParam);

        /// <summary>
        /// PlayerView를 초기화한다.
        /// </summary>
        public PlayerView()
        {
            InitializeComponent();

            InitializeControls();
            WireEvents();
        }

        /// <summary>
        /// 선택된 계산대번호.
        /// </summary>
        public int? SelectedCounterNo
        {
            get
            {
                if (cboCounterNo.SelectedItem == null)
                {
                    return null;
                }

                int counterNo;

                if (int.TryParse(
                    cboCounterNo.SelectedItem.ToString(),
                    out counterNo))
                {
                    return counterNo;
                }

                return null;
            }
        }

        /// <summary>
        /// 사용자가 선택한 영상검색일시.
        /// </summary>
        public DateTime SearchDateTime
        {
            get
            {
                DateTime date = dtpSearchDate.Value.Date;

                int hour = ParseSelectedNumber(cboHour, 0);
                int minute = ParseSelectedNumber(cboMinute, 0);
                int second = ParseSelectedNumber(cboSecond, 0);

                string ampm = cboAmPm.SelectedItem == null
                    ? "오전"
                    : cboAmPm.SelectedItem.ToString();

                if (ampm == "오후" && hour < 12)
                {
                    hour += 12;
                }

                if (ampm == "오전" && hour == 12)
                {
                    hour = 0;
                }

                return date
                    .AddHours(hour)
                    .AddMinutes(minute)
                    .AddSeconds(second);
            }
        }

        /// <summary>
        /// 영상검색일시 기준 이전 검색 시간. 단위는 초.
        /// </summary>
        public int SearchAdjustSeconds
        {
            get
            {
                return Convert.ToInt32(
                    nudSearchAdjustSeconds.Value);
            }
        }

        /// <summary>
        /// 좌측 영상 출력 패널 Handle.
        /// </summary>
        public IntPtr LeftVideoHandle
        {
            get { return pnlLeftVideo.Handle; }
        }

        /// <summary>
        /// 우측 영상 출력 패널 Handle.
        /// </summary>
        public IntPtr RightVideoHandle
        {
            get { return pnlRightVideo.Handle; }
        }

        public event EventHandler LoadViewEvent;
        public event EventHandler CounterChangedEvent;
        public event EventHandler SearchEvent;
        public event EventHandler FastReverseEvent;
        public event EventHandler SeekBackward10Event;
        public event EventHandler PlayPauseEvent;
        public event EventHandler SeekForward10Event;
        public event EventHandler FastForwardEvent;
        public event EventHandler SettingsEvent;
        public event EventHandler CloseEvent;

        /// <summary>
        /// 계산대번호 목록을 설정한다.
        /// </summary>
        public void SetCounterNumbers(IEnumerable<int> counterNumbers)
        {
            cboCounterNo.Items.Clear();

            if (counterNumbers == null)
            {
                return;
            }

            foreach (int counterNo in counterNumbers
                .Distinct()
                .OrderBy(x => x))
            {
                cboCounterNo.Items.Add(counterNo.ToString());
            }

            if (cboCounterNo.Items.Count > 0)
            {
                cboCounterNo.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 계산대번호를 선택한다.
        /// </summary>
        public void SelectCounterNo(int counterNo)
        {
            string value = counterNo.ToString();

            for (int index = 0; index < cboCounterNo.Items.Count; index++)
            {
                if (cboCounterNo.Items[index].ToString() == value)
                {
                    cboCounterNo.SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>
        /// 영상검색일시를 설정한다.
        /// </summary>
        public void SetSearchDateTime(DateTime searchDateTime)
        {
            dtpSearchDate.Value = searchDateTime.Date;

            int hour24 = searchDateTime.Hour;
            int hour12 = hour24 % 12;

            if (hour12 == 0)
            {
                hour12 = 12;
            }

            cboAmPm.SelectedItem = hour24 >= 12 ? "오후" : "오전";
            cboHour.SelectedItem = hour12.ToString("00");
            cboMinute.SelectedItem = searchDateTime.Minute.ToString("00");
            cboSecond.SelectedItem = searchDateTime.Second.ToString("00");
        }

        /// <summary>
        /// 좌측 영상 제목 또는 안내 문구를 설정한다.
        /// </summary>
        public void SetLeftVideoTitle(string title)
        {
            lblLeftVideoEmpty.Text = string.IsNullOrWhiteSpace(title)
                ? "영상 없음"
                : title;
        }

        /// <summary>
        /// 우측 영상 제목 또는 안내 문구를 설정한다.
        /// </summary>
        public void SetRightVideoTitle(string title)
        {
            lblRightVideoEmpty.Text = string.IsNullOrWhiteSpace(title)
                ? "영상 없음"
                : title;
        }

        /// <summary>
        /// 현재 영상재생일시를 표시한다.
        /// </summary>
        public void SetPlaybackDateTime(DateTime? playbackDateTime)
        {
            lblPlaybackDateTime.Text = playbackDateTime.HasValue
                ? playbackDateTime.Value.ToString("yyyy-MM-dd tt hh:mm:ss")
                : "-";
        }

        /// <summary>
        /// 재생 상태에 맞게 버튼 표시를 변경한다.
        /// </summary>
        public void SetPlaybackState(PlaybackState state)
        {
            switch (state)
            {
                case PlaybackState.Playing:
                case PlaybackState.FastForward:
                case PlaybackState.FastReverse:
                    btnPlayPause.Image = Properties.Resources.Pause;
                    break;

                default:
                    btnPlayPause.Image = Properties.Resources.Paly;
                    break;
            }
        }

        /// <summary>
        /// 상태 메시지를 표시한다.
        /// </summary>
        public void SetStatus(string message)
        {
            Text = string.IsNullOrWhiteSpace(message)
                ? "POSCAM CamViewer"
                : "POSCAM CamViewer - " + message;
        }

        /// <summary>
        /// 사용자 안내 메시지를 표시한다.
        /// </summary>
        public void ShowMessage(string message)
        {
            MessageBox.Show(
                this,
                message,
                "POSCAM CamViewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// PlayerView를 표시한다.
        /// </summary>
        public void ShowView()
        {
            Show();
        }

        /// <summary>
        /// PlayerView를 닫는다.
        /// </summary>
        public void CloseView()
        {
            Close();
        }

        /// <summary>
        /// 화면 컨트롤의 기본값을 설정한다.
        /// </summary>
        private void InitializeControls()
        {
            Text = "POSCAM CamViewer";

            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            MinimumSize = new Size(1200, 650);

            // 테두리 없는 Form에서 사용자가 직접 크기 조절할 수 있도록
            // Form 가장자리에 리사이즈 감지 영역을 남긴다.
            Padding = new Padding(ResizeBorderThickness);

            InitializeBodyRows();
            InitializeTimeComboBoxes();

            nudSearchAdjustSeconds.Minimum = 0;
            nudSearchAdjustSeconds.Maximum = 300;
            nudSearchAdjustSeconds.Value = 30;


            SetPlaybackDateTime(null);
            SetPlaybackState(PlaybackState.Stopped);

            SetLeftVideoTitle("좌측 영상");
            SetRightVideoTitle("우측 영상");

            SetSearchDateTime(DateTime.Now);
        }

        /// <summary>
        /// 시간 선택 콤보박스 항목을 초기화한다.
        /// </summary>
        private void InitializeTimeComboBoxes()
        {
            cboAmPm.Items.Clear();
            cboAmPm.Items.Add("오전");
            cboAmPm.Items.Add("오후");

            cboHour.Items.Clear();
            for (int hour = 1; hour <= 12; hour++)
            {
                cboHour.Items.Add(hour.ToString("00"));
            }

            cboMinute.Items.Clear();
            cboSecond.Items.Clear();

            for (int value = 0; value <= 59; value++)
            {
                cboMinute.Items.Add(value.ToString("00"));
                cboSecond.Items.Add(value.ToString("00"));
            }

            cboAmPm.DropDownStyle = ComboBoxStyle.DropDownList;
            cboHour.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMinute.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSecond.DropDownStyle = ComboBoxStyle.DropDownList;

            cboAmPm.SelectedIndex = 0;
            cboHour.SelectedIndex = 0;
            cboMinute.SelectedIndex = 0;
            cboSecond.SelectedIndex = 0;
        }

        /// <summary>
        /// 본문 TableLayoutPanel의 Row 비율을 설정한다.
        ///
        /// Row 0: 영상 영역, 남은 공간 자동 사용
        /// Row 1: 하단 컨트롤 영역, 현재 높이 고정
        /// </summary>
        private void InitializeBodyRows()
        {
            int controlRowHeight = 170;

            int[] rowHeights = tlpBody.GetRowHeights();

            if (rowHeights != null && rowHeights.Length > 1 && rowHeights[1] > 0)
            {
                controlRowHeight = rowHeights[1];
            }

            tlpBody.Dock = DockStyle.Fill;
            tlpBody.RowCount = 2;
            tlpBody.RowStyles.Clear();

            tlpBody.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100F));

            tlpBody.RowStyles.Add(
                new RowStyle(SizeType.Absolute, controlRowHeight));
        }

        /// <summary>
        /// View 컨트롤 이벤트를 Presenter 이벤트로 연결한다.
        /// </summary>
        private void WireEvents()
        {
            Load += OnFormLoad;
            Shown += OnFormShown;
            Resize += OnFormResizeOrMove;
            Move += OnFormResizeOrMove;

            cboCounterNo.SelectedIndexChanged += OnCounterChanged;
            btnSearch.Click += OnSearchButtonClick;

            btnFastReverse.Click += OnFastReverseButtonClick;
            btnSeekBackward10.Click += OnSeekBackward10ButtonClick;
            btnPlayPause.Click += OnPlayPauseButtonClick;
            btnSeekForward10.Click += OnSeekForward10ButtonClick;
            btnFastForward.Click += OnFastForwardButtonClick;

            btnSettings.Click += OnSettingsButtonClick;
            btnMinimize.Click += OnMinimizeButtonClick;
            btnResize.Click += OnResizeButtonClick;
            btnClose.Click += OnCloseButtonClick;

            pnlTitleBar.MouseDown += OnTitleBarMouseDown;
            lblTitle.MouseDown += OnTitleBarMouseDown;
        }

        /// <summary>
        /// 화면이 처음 표시된 후 현재 크기를 복원 기준 크기로 저장한다.
        /// </summary>
        private void OnFormShown(object sender, EventArgs e)
        {
            if (_normalBoundsCaptured)
            {
                return;
            }

            _normalBounds = Bounds;
            _normalBoundsCaptured = true;
        }

        /// <summary>
        /// 일반 상태에서 사용자가 창 크기나 위치를 변경하면 마지막 Bounds를 기억한다.
        /// </summary>
        private void OnFormResizeOrMove(object sender, EventArgs e)
        {
            if (_isMaximized)
            {
                return;
            }

            if (!_normalBoundsCaptured)
            {
                return;
            }

            if (WindowState != FormWindowState.Normal)
            {
                return;
            }

            _normalBounds = Bounds;
        }


        /// <summary>
        /// 크기조절 버튼 클릭 시 최대화/복원을 전환한다.
        /// </summary>
        private void OnResizeButtonClick(object sender, EventArgs e)
        {
            ToggleWindowSize();
        }

        /// <summary>
        /// 현재 창 크기를 최대화하거나 마지막 일반 크기로 복원한다.
        /// </summary>
        private void ToggleWindowSize()
        {
            if (!_isMaximized)
            {
                if (WindowState == FormWindowState.Normal)
                {
                    _normalBounds = Bounds;
                    _normalBoundsCaptured = true;
                }

                Rectangle workingArea =
                    Screen.FromControl(this).WorkingArea;

                _isMaximized = true;

                // 최대화 상태에서는 리사이즈 여백 제거
                Padding = Padding.Empty;

                Bounds = workingArea;
                btnResize.Text = "❐";
                return;
            }

            _isMaximized = false;

            if (_normalBounds.Width <= 0 || _normalBounds.Height <= 0)
            {
                _normalBounds = new Rectangle(
                    100,
                    100,
                    1280,
                    720);
            }

            // 복원 상태에서는 다시 테두리 리사이즈 영역 확보
            Padding = new Padding(ResizeBorderThickness);

            Bounds = _normalBounds;
            btnResize.Text = "□";
        }

        /// <summary>
        /// FormBorderStyle.None 상태에서 창 테두리 드래그 크기 조절을 지원한다.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg != WmNcHitTest)
            {
                return;
            }

            if (_isMaximized)
            {
                return;
            }

            if ((int)m.Result != HtClient)
            {
                return;
            }

            Point cursor = PointToClient(
                new Point(
                    GetXFromLParam(m.LParam),
                    GetYFromLParam(m.LParam)));

            bool left = cursor.X <= ResizeBorderThickness;
            bool right = cursor.X >= ClientSize.Width - ResizeBorderThickness;
            bool top = cursor.Y <= ResizeBorderThickness;
            bool bottom = cursor.Y >= ClientSize.Height - ResizeBorderThickness;

            if (left && top)
            {
                m.Result = (IntPtr)HtTopLeft;
            }
            else if (right && top)
            {
                m.Result = (IntPtr)HtTopRight;
            }
            else if (left && bottom)
            {
                m.Result = (IntPtr)HtBottomLeft;
            }
            else if (right && bottom)
            {
                m.Result = (IntPtr)HtBottomRight;
            }
            else if (left)
            {
                m.Result = (IntPtr)HtLeft;
            }
            else if (right)
            {
                m.Result = (IntPtr)HtRight;
            }
            else if (top)
            {
                m.Result = (IntPtr)HtTop;
            }
            else if (bottom)
            {
                m.Result = (IntPtr)HtBottom;
            }
        }

        private static int GetXFromLParam(IntPtr lParam)
        {
            return unchecked((short)((long)lParam & 0xffff));
        }

        private static int GetYFromLParam(IntPtr lParam)
        {
            return unchecked((short)(((long)lParam >> 16) & 0xffff));
        }


        private void OnFormLoad(object sender, EventArgs e)
        {
            LoadViewEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnCounterChanged(object sender, EventArgs e)
        {
            CounterChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnSearchButtonClick(object sender, EventArgs e)
        {
            SearchEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnFastReverseButtonClick(object sender, EventArgs e)
        {
            FastReverseEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnSeekBackward10ButtonClick(object sender, EventArgs e)
        {
            SeekBackward10Event?.Invoke(this, EventArgs.Empty);
        }

        private void OnPlayPauseButtonClick(object sender, EventArgs e)
        {
            PlayPauseEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnSeekForward10ButtonClick(object sender, EventArgs e)
        {
            SeekForward10Event?.Invoke(this, EventArgs.Empty);
        }

        private void OnFastForwardButtonClick(object sender, EventArgs e)
        {
            FastForwardEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnSettingsButtonClick(object sender, EventArgs e)
        {
            SettingsEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnMinimizeButtonClick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void OnCloseButtonClick(object sender, EventArgs e)
        {
            CloseEvent?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// ComboBox 선택값을 숫자로 변환한다.
        /// </summary>
        private static int ParseSelectedNumber(
            ComboBox comboBox,
            int defaultValue)
        {
            if (comboBox == null || comboBox.SelectedItem == null)
            {
                return defaultValue;
            }

            int value;

            return int.TryParse(
                comboBox.SelectedItem.ToString(),
                out value)
                ? value
                : defaultValue;
        }

        /// <summary>
        /// 자체 타이틀바를 드래그하면 Form을 이동시킨다.
        /// </summary>
        private void OnTitleBarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            // 타이틀바 더블클릭시 최대화/복원처리
            if (e.Clicks == 2)
            {
                ToggleWindowSize();
                return;
            }

            // 일반 클릭 드레그는 창 이동 처리
            if (_isMaximized)
            {
                return;
            }

            ReleaseCapture();

            SendMessage(
                Handle,
                WmNclButtonDown,
                HtCaption,
                0);
        }
    }
}