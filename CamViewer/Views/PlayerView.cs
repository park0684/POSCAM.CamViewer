using CamViewer.Interfaces;
using CamViewer.Models;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;



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

        private readonly System.Windows.Forms.Timer _playbackTimer;

        private DateTime? _timelineStartTime;
        private DateTime? _timelineEndTime;
        private DateTime? _timelinePlaybackTime;

        /// <summary>
        /// 좌측 영상 실제 렌더링 대상 패널.
        /// </summary>
        private Panel _leftRenderTarget;

        /// <summary>
        /// 우측 영상 실제 렌더링 대상 패널.
        /// </summary>
        private Panel _rightRenderTarget;

        /// <summary>
        /// 현재 영상 표시 방식.
        /// 기본값은 원본 비율 유지이다.
        /// </summary>
        private VideoRenderMode _videoRenderMode = VideoRenderMode.KeepAspectRatio;

        /// <summary>
        /// 좌측 영상 원본 비율.
        /// 값이 없으면 원본 비율을 알 수 없는 상태로 본다.
        /// </summary>
        private double? _leftVideoAspectRatio;

        /// <summary>
        /// 우측 영상 원본 비율.
        /// 값이 없으면 원본 비율을 알 수 없는 상태로 본다.
        /// </summary>
        private double? _rightVideoAspectRatio;

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
            InitializeVideoRenderTargets();
            _playbackTimer = new System.Windows.Forms.Timer();
            _playbackTimer.Interval = 1000;
            _playbackTimer.Tick += OnPlaybackTimerTick;

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
                DateTime date = dtpSearchStartDate.Value.Date;

                int hour = ParseSelectedNumber(cboStartHour, 0);
                int minute = ParseSelectedNumber(cboStartMinute, 0);
                int second = ParseSelectedNumber(cboStartSecond, 0);

                //string ampm = cboStartAmPm.SelectedItem == null
                //    ? "오전"
                //    : cboStartAmPm.SelectedItem.ToString();

                //if (ampm == "오후" && hour < 12)
                //{
                //    hour += 12;
                //}

                //if (ampm == "오전" && hour == 12)
                //{
                //    hour = 0;
                //}

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
                    nudTiemAdjustSeconds.Value);
            }
        }

        public int PlayAdjustSeconds
        {
            get
            {
                return 30;
            }
        }

        /// <summary>
        /// 조회 시작시간.
        /// </summary>
        public DateTime SearchStartTime
        {
            get
            {
                return BuildDateTime(
                    dtpSearchStartDate,
                    cboStartHour,
                    cboStartMinute,
                    cboStartSecond);
            }
        }

        /// <summary>
        /// 조회 종료시간.
        /// </summary>
        public DateTime SearchEndTime
        {
            get
            {
                return BuildDateTime(
                    dtpSearchEndDate,
                    cboEndHour,
                    cboEndMinute,
                    cboEndSecond);
            }
        }

        /// <summary>
        /// 좌측 영상 출력 대상 Handle.
        /// SDK에는 Host 패널이 아니라 실제 RenderTarget 패널 Handle을 전달한다.
        /// </summary>
        public IntPtr LeftVideoHandle
        {
            get
            {
                return _leftRenderTarget == null
                    ? pnlLeftVideo.Handle
                    : _leftRenderTarget.Handle;
            }
        }

        /// <summary>
        /// 우측 영상 출력 대상 Handle.
        /// SDK에는 Host 패널이 아니라 실제 RenderTarget 패널 Handle을 전달한다.
        /// </summary>
        public IntPtr RightVideoHandle
        {
            get
            {
                return _rightRenderTarget == null
                    ? pnlRightVideo.Handle
                    : _rightRenderTarget.Handle;
            }
        }

        /// <summary>
        /// 사용자 확인 메시지를 표시한다.
        /// </summary>
        public bool Confirm(string message)
        {
            return MessageBox.Show(
                this,
                message,
                "POSCAM CamViewer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public PlaybackSpeed SelectedPlaybackSpeed
        {
            get
            {
                PlaybackSpeedItem item =
                    cboPlaybackSpeed.SelectedItem as PlaybackSpeedItem;

                return item == null
                    ? PlaybackSpeed.Normal
                    : item.Speed;
            }
        }

        /// <summary>
        /// 현재 선택된 영상 표시 방식.
        /// </summary>
        public VideoRenderMode SelectedVideoRenderMode
        {
            get
            {
                VideoRenderModeItem item =
                    cmbRenderSelect.SelectedItem as VideoRenderModeItem;

                return item == null
                    ? VideoRenderMode.KeepAspectRatio
                    : item.RenderMode;
            }
        }


        /* evetns */

        public event EventHandler LoadViewEvent;
        public event EventHandler CounterChangedEvent;
        public event EventHandler SearchEvent;
        public event EventHandler SeekBackward10Event;
        public event EventHandler PlayPauseEvent;
        public event EventHandler SeekForward10Event;
        public event EventHandler RewindEvent;
        public event EventHandler StopEvent;
        public event EventHandler SettingsEvent;
        public event EventHandler CloseEvent;
        public event EventHandler MinimizeEvent;
        public event EventHandler PlaybackTimerTickEvent;
        public event EventHandler PlaybackSpeedChangedEvent;
        public event EventHandler SyncEvent;
        public event EventHandler<TimelineSeekRequestedEventArgs> TimelineSeekRequestedEvent;

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
            dtpSearchStartDate.Value = searchDateTime.Date;

            cboStartHour.SelectedItem =
                searchDateTime.Hour.ToString("00");

            cboStartMinute.SelectedItem =
                searchDateTime.Minute.ToString("00");

            cboStartSecond.SelectedItem =
                searchDateTime.Second.ToString("00");
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
                //case PlaybackState.FastForward:
                //case PlaybackState.FastReverse:
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
        /// 재생 시간 갱신 타이머를 시작한다.
        /// </summary>
        public void StartPlaybackTimer()
        {
            _playbackTimer.Start();
        }

        /// <summary>
        /// 재생 시간 갱신 타이머를 중지한다.
        /// </summary>
        public void StopPlaybackTimer()
        {
            _playbackTimer.Stop();
        }

        /// <summary>
        /// 현재 영상재생시간을 표시한다.
        /// </summary>
        public void SetPlaybackTime(DateTime? playbackTime)
        {
            lblPlaybackDateTime.Text = playbackTime.HasValue
                ? playbackTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "-";
        }

        /// <summary>
        /// 좌/우 영상 동기화 상태를 표시한다.
        /// </summary>
        public void SetPlaybackSyncStatus(string statusText)
        {
            lblPlaybackSyncStatus.Text =
                string.IsNullOrWhiteSpace(statusText)
                    ? ""
                    : statusText;
        }

        /// <summary>
        /// 10초 전/뒤 이동 버튼에서 사용할 이동 간격 초.
        /// </summary>
        public int TimeAdjustSeconds
        {
            get
            {
                int value = Convert.ToInt32(nudTiemAdjustSeconds.Value);

                if (value < 1)
                {
                    return 1;
                }

                if (value > 60)
                {
                    return 60;
                }

                return value;
            }
        }

        /// <summary>
        /// 타임라인의 전체 조회 구간을 설정한다.
        /// </summary>
        public void SetTimelineRange(
            DateTime? startTime,
            DateTime? endTime)
        {
            _timelineStartTime = startTime;
            _timelineEndTime = endTime;

            pnlTimeLIne.Invalidate();
        }

        /// <summary>
        /// 타임라인의 현재 재생 위치를 설정한다.
        /// </summary>
        public void SetTimelinePlaybackTime(DateTime? playbackTime)
        {
            _timelinePlaybackTime = playbackTime;

            pnlTimeLIne.Invalidate();
        }

        /// <summary>
        /// 좌측/우측 영상의 원본 크기를 설정한다.
        /// width/height가 올바르지 않으면 해당 화면의 원본 비율을 알 수 없는 상태로 둔다.
        /// </summary>
        public void SetVideoSourceSize(
            ScreenPosition screenPosition,
            int width,
            int height)
        {
            double? aspectRatio = null;

            if (width > 0 && height > 0)
            {
                aspectRatio = (double)width / height;
            }

            if (screenPosition == ScreenPosition.Left)
            {
                _leftVideoAspectRatio = aspectRatio;
            }
            else if (screenPosition == ScreenPosition.Right)
            {
                _rightVideoAspectRatio = aspectRatio;
            }

            UpdateVideoRenderTargetLayout();
        }



        /*내부 메서드*/



        /// <summary>
        /// 재생 시간 갱신 타이머 Tick을 Presenter에 전달한다.
        /// </summary>
        private void OnPlaybackTimerTick(object sender, EventArgs e)
        {
            PlaybackTimerTickEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 설정 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnSettingsButtonClick(object sender, EventArgs e)
        {
            SettingsEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 최소화 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnMinimizeButtonClick(object sender, EventArgs e)
        {
            MinimizeEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 종료 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnCloseButtonClick(object sender, EventArgs e)
        {
            CloseEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// PlayerView를 최소화한다.
        /// </summary>
        public void MinimizeView()
        {
            WindowState = FormWindowState.Minimized;
        }

        public void SetPlaybackSpeedOptions()
        {
            cboPlaybackSpeed.Items.Clear();

            cboPlaybackSpeed.Items.Add(new PlaybackSpeedItem("0.5배속", PlaybackSpeed.Half));
            cboPlaybackSpeed.Items.Add(new PlaybackSpeedItem("1배속", PlaybackSpeed.Normal));
            cboPlaybackSpeed.Items.Add(new PlaybackSpeedItem("2배속", PlaybackSpeed.Double));
            cboPlaybackSpeed.Items.Add(new PlaybackSpeedItem("4배속", PlaybackSpeed.Quad));
            cboPlaybackSpeed.Items.Add(new PlaybackSpeedItem("8배속", PlaybackSpeed.Octuple));

            cboPlaybackSpeed.DropDownStyle = ComboBoxStyle.DropDownList;
            SelectPlaybackSpeed(PlaybackSpeed.Normal);
        }

        public void SelectPlaybackSpeed(PlaybackSpeed speed)
        {
            for (int index = 0; index < cboPlaybackSpeed.Items.Count; index++)
            {
                PlaybackSpeedItem item = cboPlaybackSpeed.Items[index] as PlaybackSpeedItem;
                if (item != null && item.Speed == speed)
                {
                    cboPlaybackSpeed.SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>
        /// 영상 렌더링 대상 패널의 크기와 위치를 현재 View 크기에 맞게 갱신한다.
        /// Presenter에서 재생 요청을 만들기 전에 호출한다.
        /// </summary>
        public void UpdateVideoLayout()
        {
            UpdateVideoRenderTargetLayout();
        }

        /// <summary>
        /// 영상 표시 방식을 선택한다.
        /// </summary>
        public void SelectVideoRenderMode(VideoRenderMode renderMode)
        {
            for (int index = 0; index < cmbRenderSelect.Items.Count; index++)
            {
                VideoRenderModeItem item =
                    cmbRenderSelect.Items[index] as VideoRenderModeItem;

                if (item != null && item.RenderMode == renderMode)
                {
                    cmbRenderSelect.SelectedIndex = index;
                    _videoRenderMode = renderMode;
                    UpdateVideoRenderTargetLayout();
                    return;
                }
            }
        }


        /*내부 메서드*/

        /// <summary>
        /// NVR SDK가 실제로 영상을 렌더링할 전용 패널을 생성한다.
        /// 
        /// 기존 pnlLeftVideo / pnlRightVideo는 영상 영역의 Host로 사용하고,
        /// SDK에는 내부 RenderTarget 패널의 Handle을 전달한다.
        /// </summary>
        private void InitializeVideoRenderTargets()
        {
            pnlLeftVideo.BackColor = Color.Black;
            pnlRightVideo.BackColor = Color.Black;

            _leftRenderTarget = CreateVideoRenderTargetPanel();
            _rightRenderTarget = CreateVideoRenderTargetPanel();

            pnlLeftVideo.Controls.Add(_leftRenderTarget);
            pnlRightVideo.Controls.Add(_rightRenderTarget);

            _leftRenderTarget.BringToFront();
            _rightRenderTarget.BringToFront();

            pnlLeftVideo.Resize += OnVideoHostResize;
            pnlRightVideo.Resize += OnVideoHostResize;

            // Handle을 미리 생성해 둔다.
            _leftRenderTarget.CreateControl();
            _rightRenderTarget.CreateControl();

            UpdateVideoRenderTargetLayout();

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

            nudTiemAdjustSeconds.Minimum = 1;
            nudTiemAdjustSeconds.Maximum = 60;
            nudTiemAdjustSeconds.Value = 10;


            //SetPlaybackDateTime(null);
            SetPlaybackState(PlaybackState.Stopped);

            SetLeftVideoTitle("좌측 영상");
            SetRightVideoTitle("우측 영상");

 

            SetSearchDateTime(DateTime.Now);
            SetPlaybackSpeedOptions();
            InitializeRenderSelectComboBox();
        }

        /// <summary>
        /// 시작/종료 시간 선택 콤보박스를 24시간 기준으로 초기화한다.
        /// </summary>
        private void InitializeTimeComboBoxes()
        {
            InitializeHourComboBox(cboStartHour);
            InitializeHourComboBox(cboEndHour);

            InitializeMinuteSecondComboBox(cboStartMinute);
            InitializeMinuteSecondComboBox(cboStartSecond);
            InitializeMinuteSecondComboBox(cboEndMinute);
            InitializeMinuteSecondComboBox(cboEndSecond);
        }


        /// <summary>
        /// 시간 콤보박스를 00~23 기준으로 초기화한다.
        /// </summary>
        private static void InitializeHourComboBox(
            ComboBox comboBox)
        {
            comboBox.Items.Clear();

            for (int hour = 0; hour <= 23; hour++)
            {
                comboBox.Items.Add(hour.ToString("00"));
            }

            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 분/초 콤보박스를 00~59 기준으로 초기화한다.
        /// </summary>
        private static void InitializeMinuteSecondComboBox(
            ComboBox comboBox)
        {
            comboBox.Items.Clear();

            for (int value = 0; value <= 59; value++)
            {
                comboBox.Items.Add(value.ToString("00"));
            }

            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 본문 TableLayoutPanel의 Row 비율을 설정한다.
        ///
        /// Row 0: 영상 영역, 남은 공간 자동 사용
        /// Row 1: 하단 컨트롤 영역, 현재 높이 고정
        /// </summary>
        private void InitializeBodyRows()
        {
            tlpBody.Dock = DockStyle.Fill;
            tlpBody.RowCount = 1;
            tlpBody.RowStyles.Clear();

            tlpBody.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));
        }

        /// <summary>
        /// 영상 표시 방식 콤보박스를 초기화한다.
        /// </summary>
        private void InitializeRenderSelectComboBox()
        {
            cmbRenderSelect.Items.Clear();

            cmbRenderSelect.Items.Add(
                new VideoRenderModeItem(
                    "채우기",
                    VideoRenderMode.Fill));

            cmbRenderSelect.Items.Add(
                new VideoRenderModeItem(
                    "원본 비율",
                    VideoRenderMode.KeepAspectRatio));

            cmbRenderSelect.DropDownStyle =
                ComboBoxStyle.DropDownList;

            SelectVideoRenderMode(
                VideoRenderMode.KeepAspectRatio);
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

            btnRewind.Click += OnRewindButtonClick;
            btnSeekBackward10.Click += OnSeekBackward10ButtonClick;
            btnPlayPause.Click += OnPlayPauseButtonClick;
            btnSeekForward10.Click += OnSeekForward10ButtonClick;
            btnStop.Click += OnStopButtonClick;

            btnSettings.Click += OnSettingsButtonClick;
            btnMinimize.Click += OnMinimizeButtonClick;
            btnResize.Click += OnResizeButtonClick;
            btnClose.Click += OnCloseButtonClick;

            pnlTitleBar.MouseDown += OnTitleBarMouseDown;
            lblTitle.MouseDown += OnTitleBarMouseDown;
            cboPlaybackSpeed.SelectedIndexChanged += OnPlaybackSpeedChanged;

            btnSync.Click += OnSyncButtonClick;
            pnlTimeLIne.Paint += OnTimelinePaint;
            pnlTimeLIne.MouseDown += OnTimelineMouseDown;

            cmbRenderSelect.SelectedIndexChanged += OnRenderSelectChanged;
        }

        /// <summary>
        /// 실제 영상 렌더링 대상 패널을 생성한다.
        /// </summary>
        private static Panel CreateVideoRenderTargetPanel()
        {
            return new Panel
            {
                BackColor = Color.Black,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
        }

        /// <summary>
        /// 영상 Host 패널 크기가 변경되면 RenderTarget 위치와 크기를 다시 계산한다.
        /// </summary>
        private void OnVideoHostResize(object sender, EventArgs e)
        {
            UpdateVideoRenderTargetLayout();
        }

        /// <summary>
        /// 영상 표시 방식 선택 변경을 처리한다.
        /// </summary>
        private void OnRenderSelectChanged(object sender, EventArgs e)
        {
            VideoRenderModeItem item =
                cmbRenderSelect.SelectedItem as VideoRenderModeItem;

            if (item == null)
            {
                return;
            }

            _videoRenderMode = item.RenderMode;

            UpdateVideoRenderTargetLayout();
        }



        /// <summary>
        /// 하나의 영상 렌더링 패널을 Host 영역 안에 맞춘다.
        /// </summary>
        private void UpdateSingleRenderTargetLayout(
            Panel hostPanel,
            Panel renderTarget,
            double? aspectRatio)
        {
            if (hostPanel == null || renderTarget == null)
            {
                return;
            }

            int hostWidth = hostPanel.ClientSize.Width;
            int hostHeight = hostPanel.ClientSize.Height;

            if (hostWidth <= 0 || hostHeight <= 0)
            {
                return;
            }

            // 채우기 또는 원본 비율을 모르는 경우:
            // 임의로 16:9를 적용하지 않고 전체를 채운다.
            if (_videoRenderMode == VideoRenderMode.Fill
                || !aspectRatio.HasValue
                || aspectRatio.Value <= 0)
            {
                renderTarget.Bounds = hostPanel.ClientRectangle;
                renderTarget.Invalidate();
                return;
            }

            double videoRatio = aspectRatio.Value;
            double hostRatio = (double)hostWidth / hostHeight;

            int targetWidth;
            int targetHeight;

            if (hostRatio > videoRatio)
            {
                targetHeight = hostHeight;
                targetWidth = Convert.ToInt32(targetHeight * videoRatio);
            }
            else
            {
                targetWidth = hostWidth;
                targetHeight = Convert.ToInt32(targetWidth / videoRatio);
            }

            if (targetWidth < 1)
            {
                targetWidth = 1;
            }

            if (targetHeight < 1)
            {
                targetHeight = 1;
            }

            int targetX = (hostWidth - targetWidth) / 2;
            int targetY = (hostHeight - targetHeight) / 2;

            renderTarget.Bounds =
                new Rectangle(
                    targetX,
                    targetY,
                    targetWidth,
                    targetHeight);


            renderTarget.Invalidate();
        }

        /// <summary>
        /// 영상 표시 방식 ComboBox 항목이다.
        /// </summary>
        private sealed class VideoRenderModeItem
        {
            public VideoRenderModeItem(
                string text,
                VideoRenderMode renderMode)
            {
                Text = text;
                RenderMode = renderMode;
            }

            public string Text { get; private set; }

            public VideoRenderMode RenderMode { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>
        /// 좌/우 영상 렌더링 패널을 Host 패널 크기에 맞게 재배치한다.
        /// </summary>
        private void UpdateVideoRenderTargetLayout()
        {
            UpdateSingleRenderTargetLayout(
                pnlLeftVideo,
                _leftRenderTarget,
                _leftVideoAspectRatio);

            UpdateSingleRenderTargetLayout(
                pnlRightVideo,
                _rightRenderTarget,
                _rightVideoAspectRatio);
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
            if (!_isMaximized
                && _normalBoundsCaptured
                && WindowState == FormWindowState.Normal)
            {
                _normalBounds = Bounds;
            }

            UpdateVideoRenderTargetLayout();
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
                UpdateVideoRenderTargetLayout();
                btnResize.Image = Properties.Resources.Minimum;
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
            UpdateVideoRenderTargetLayout();
            btnResize.Image = Properties.Resources.Maximum;
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

        /// <summary>
        /// 역재생 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnRewindButtonClick(object sender, EventArgs e)
        {
            RewindEvent?.Invoke(this, EventArgs.Empty);
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

        /// <summary>
        /// 정지 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnStopButtonClick(object sender, EventArgs e)
        {
            StopEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 재생속도 선택 변경을 Presenter에 전달한다.
        /// </summary>
        private void OnPlaybackSpeedChanged(object sender, EventArgs e)
        {
            PlaybackSpeedChangedEvent?.Invoke(this, EventArgs.Empty);
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

        /// <summary>
        /// 영상 동기화 버튼 클릭을 Presenter에 전달한다.
        /// </summary>
        private void OnSyncButtonClick(object sender, EventArgs e)
        {
            SyncEvent?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// 날짜 선택 컨트롤과 24시간 콤보박스 값을 DateTime으로 조합한다.
        /// </summary>
        private static DateTime BuildDateTime(
            DateTimePicker datePicker,
            ComboBox hourComboBox,
            ComboBox minuteComboBox,
            ComboBox secondComboBox)
        {
            DateTime date = datePicker.Value.Date;

            int hour = ParseSelectedNumber(hourComboBox, 0);
            int minute = ParseSelectedNumber(minuteComboBox, 0);
            int second = ParseSelectedNumber(secondComboBox, 0);

            return date
                .AddHours(hour)
                .AddMinutes(minute)
                .AddSeconds(second);
        }

        /// <summary>
        /// 조회 시작시간과 종료시간을 설정한다.
        /// </summary>
        public void SetSearchRange(
            DateTime startTime,
            DateTime endTime)
        {
            SetDateTimeToControls(
                dtpSearchStartDate,
                cboStartHour,
                cboStartMinute,
                cboStartSecond,
                startTime);

            SetDateTimeToControls(
                dtpSearchEndDate,
                cboEndHour,
                cboEndMinute,
                cboEndSecond,
                endTime);
        }

        /// <summary>
        /// DateTime 값을 날짜/시/분/초 컨트롤에 반영한다.
        /// </summary>
        private static void SetDateTimeToControls(
            DateTimePicker datePicker,
            ComboBox hourComboBox,
            ComboBox minuteComboBox,
            ComboBox secondComboBox,
            DateTime value)
        {
            datePicker.Value = value.Date;

            SelectComboValue(
                hourComboBox,
                value.Hour.ToString("00"));

            SelectComboValue(
                minuteComboBox,
                value.Minute.ToString("00"));

            SelectComboValue(
                secondComboBox,
                value.Second.ToString("00"));
        }

        /// <summary>
        /// ComboBox에서 지정한 값을 선택한다.
        /// </summary>
        private static void SelectComboValue(
            ComboBox comboBox,
            string value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index].ToString() == value)
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>
        /// 재생속도 ComboBox 표시 항목이다.
        /// </summary>
        private sealed class PlaybackSpeedItem
        {
            public PlaybackSpeedItem(
                string text,
                PlaybackSpeed speed)
            {
                Text = text;
                Speed = speed;
            }

            public string Text { get; private set; }

            public PlaybackSpeed Speed { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>
        /// 타임라인을 그린다.
        /// </summary>
        private void OnTimelinePaint(object sender, PaintEventArgs e)
        {
            Rectangle bounds = pnlTimeLIne.ClientRectangle;

            e.Graphics.Clear(Color.FromArgb(28, 28, 28));

            if (!_timelineStartTime.HasValue
                || !_timelineEndTime.HasValue
                || _timelineStartTime.Value >= _timelineEndTime.Value)
            {
                DrawTimelineText(
                    e.Graphics,
                    bounds,
                    "조회 구간 없음");

                return;
            }

            int marginLeft = 40;
            int marginRight = 40;
            int centerY = bounds.Height / 2;

            int lineX = marginLeft;
            int lineWidth = Math.Max(
                1,
                bounds.Width - marginLeft - marginRight);

            using (Pen basePen = new Pen(Color.FromArgb(90, 90, 90), 6))
            {
                e.Graphics.DrawLine(
                    basePen,
                    lineX,
                    centerY,
                    lineX + lineWidth,
                    centerY);
            }

            if (_timelinePlaybackTime.HasValue)
            {
                double ratio =
                    GetTimelineRatio(_timelinePlaybackTime.Value);

                int currentX =
                    lineX + Convert.ToInt32(lineWidth * ratio);

                using (Pen progressPen = new Pen(Color.FromArgb(109, 53, 209), 6))
                {
                    e.Graphics.DrawLine(
                        progressPen,
                        lineX,
                        centerY,
                        currentX,
                        centerY);
                }

                using (Brush markerBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(
                        markerBrush,
                        currentX - 6,
                        centerY - 6,
                        12,
                        12);
                }
            }

            using (Brush textBrush = new SolidBrush(Color.White))
            {
                e.Graphics.DrawString(
                    _timelineStartTime.Value.ToString("HH:mm:ss"),
                    Font,
                    textBrush,
                    marginLeft,
                    centerY + 15);

                string endText =
                    _timelineEndTime.Value.ToString("HH:mm:ss");

                SizeF endSize =
                    e.Graphics.MeasureString(
                        endText,
                        Font);

                e.Graphics.DrawString(
                    endText,
                    Font,
                    textBrush,
                    bounds.Width - marginRight - endSize.Width,
                    centerY + 15);
            }
        }

        /// <summary>
        /// 타임라인 안내 문구를 표시한다.
        /// </summary>
        private void DrawTimelineText(
            Graphics graphics,
            Rectangle bounds,
            string text)
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
            {
                SizeF size =
                    graphics.MeasureString(
                        text,
                        Font);

                graphics.DrawString(
                    text,
                    Font,
                    brush,
                    (bounds.Width - size.Width) / 2,
                    (bounds.Height - size.Height) / 2);
            }
        }

        /// <summary>
        /// 타임라인 클릭 위치를 영상재생시간으로 변환해 Presenter에 전달한다.
        /// </summary>
        private void OnTimelineMouseDown(object sender, MouseEventArgs e)
        {
            if (!_timelineStartTime.HasValue
                || !_timelineEndTime.HasValue
                || _timelineStartTime.Value >= _timelineEndTime.Value)
            {
                return;
            }

            int marginLeft = 40;
            int marginRight = 40;

            int lineX = marginLeft;
            int lineWidth = Math.Max(
                1,
                pnlTimeLIne.Width - marginLeft - marginRight);

            int clickedX = e.X;

            if (clickedX < lineX)
            {
                clickedX = lineX;
            }

            if (clickedX > lineX + lineWidth)
            {
                clickedX = lineX + lineWidth;
            }

            double ratio =
                (double)(clickedX - lineX) / lineWidth;

            TimeSpan totalRange =
                _timelineEndTime.Value - _timelineStartTime.Value;

            DateTime targetTime =
                _timelineStartTime.Value.AddSeconds(
                    totalRange.TotalSeconds * ratio);

            TimelineSeekRequestedEvent?.Invoke(
                this,
                new TimelineSeekRequestedEventArgs(targetTime));
        }

        /// <summary>
        /// 타임라인 구간 내 현재 위치 비율을 계산한다.
        /// </summary>
        private double GetTimelineRatio(DateTime playbackTime)
        {
            if (!_timelineStartTime.HasValue
                || !_timelineEndTime.HasValue
                || _timelineStartTime.Value >= _timelineEndTime.Value)
            {
                return 0;
            }

            if (playbackTime <= _timelineStartTime.Value)
            {
                return 0;
            }

            if (playbackTime >= _timelineEndTime.Value)
            {
                return 1;
            }

            TimeSpan total =
                _timelineEndTime.Value - _timelineStartTime.Value;

            TimeSpan current =
                playbackTime - _timelineStartTime.Value;

            return current.TotalSeconds / total.TotalSeconds;
        }
    }

}