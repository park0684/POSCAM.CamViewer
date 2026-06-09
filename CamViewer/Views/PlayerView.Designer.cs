namespace CamViewer.Views
{
    partial class PlayerView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tlpBody = new System.Windows.Forms.TableLayoutPanel();
            this.pnlRightVideo = new System.Windows.Forms.Panel();
            this.lblRightVideoEmpty = new System.Windows.Forms.Label();
            this.pnlLeftVideo = new System.Windows.Forms.Panel();
            this.lblLeftVideoEmpty = new System.Windows.Forms.Label();
            this.pnlSearchArea = new System.Windows.Forms.Panel();
            this.cmbRenderSelect = new System.Windows.Forms.ComboBox();
            this.lblRender = new System.Windows.Forms.Label();
            this.btnSearch = new System.Windows.Forms.Button();
            this.cboEndSecond = new System.Windows.Forms.ComboBox();
            this.cboStartSecond = new System.Windows.Forms.ComboBox();
            this.cboEndMinute = new System.Windows.Forms.ComboBox();
            this.cboStartMinute = new System.Windows.Forms.ComboBox();
            this.cboEndHour = new System.Windows.Forms.ComboBox();
            this.cboStartHour = new System.Windows.Forms.ComboBox();
            this.dtpSearchEndDate = new System.Windows.Forms.DateTimePicker();
            this.dtpSearchStartDate = new System.Windows.Forms.DateTimePicker();
            this.label3 = new System.Windows.Forms.Label();
            this.lblSearchStartDate = new System.Windows.Forms.Label();
            this.cboCounterNo = new System.Windows.Forms.ComboBox();
            this.lblCounterNo = new System.Windows.Forms.Label();
            this.lblSeconds = new System.Windows.Forms.Label();
            this.nudTiemAdjustSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblTimehAdjust = new System.Windows.Forms.Label();
            this.pnlTitleBar = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResize = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.pnlTimeLIne = new System.Windows.Forms.Panel();
            this.tplMenuArea = new System.Windows.Forms.TableLayoutPanel();
            this.pnlPalyerMenu = new System.Windows.Forms.Panel();
            this.btnSync = new System.Windows.Forms.Button();
            this.lblPlaybackSyncStatus = new System.Windows.Forms.Label();
            this.cboPlaybackSpeed = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnSeekForward10 = new System.Windows.Forms.Button();
            this.btnPlayPause = new System.Windows.Forms.Button();
            this.btnSeekBackward10 = new System.Windows.Forms.Button();
            this.btnRewind = new System.Windows.Forms.Button();
            this.lblPlaybackDateTime = new System.Windows.Forms.Label();
            this.lblPlaybackDateTimeTitle = new System.Windows.Forms.Label();
            this.tlpBody.SuspendLayout();
            this.pnlRightVideo.SuspendLayout();
            this.pnlLeftVideo.SuspendLayout();
            this.pnlSearchArea.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudTiemAdjustSeconds)).BeginInit();
            this.pnlTitleBar.SuspendLayout();
            this.tplMenuArea.SuspendLayout();
            this.pnlPalyerMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpBody
            // 
            this.tlpBody.ColumnCount = 2;
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBody.Controls.Add(this.pnlRightVideo, 1, 0);
            this.tlpBody.Controls.Add(this.pnlLeftVideo, 0, 0);
            this.tlpBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpBody.Location = new System.Drawing.Point(0, 42);
            this.tlpBody.Name = "tlpBody";
            this.tlpBody.RowCount = 1;
            this.tlpBody.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 341F));
            this.tlpBody.Size = new System.Drawing.Size(1280, 338);
            this.tlpBody.TabIndex = 0;
            // 
            // pnlRightVideo
            // 
            this.pnlRightVideo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.pnlRightVideo.Controls.Add(this.lblRightVideoEmpty);
            this.pnlRightVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRightVideo.Location = new System.Drawing.Point(643, 3);
            this.pnlRightVideo.Name = "pnlRightVideo";
            this.pnlRightVideo.Size = new System.Drawing.Size(634, 335);
            this.pnlRightVideo.TabIndex = 0;
            // 
            // lblRightVideoEmpty
            // 
            this.lblRightVideoEmpty.AutoSize = true;
            this.lblRightVideoEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRightVideoEmpty.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.lblRightVideoEmpty.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblRightVideoEmpty.Location = new System.Drawing.Point(0, 0);
            this.lblRightVideoEmpty.Name = "lblRightVideoEmpty";
            this.lblRightVideoEmpty.Size = new System.Drawing.Size(95, 25);
            this.lblRightVideoEmpty.TabIndex = 1;
            this.lblRightVideoEmpty.Text = "영상 없음";
            this.lblRightVideoEmpty.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlLeftVideo
            // 
            this.pnlLeftVideo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.pnlLeftVideo.Controls.Add(this.lblLeftVideoEmpty);
            this.pnlLeftVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeftVideo.Location = new System.Drawing.Point(3, 3);
            this.pnlLeftVideo.Name = "pnlLeftVideo";
            this.pnlLeftVideo.Size = new System.Drawing.Size(634, 335);
            this.pnlLeftVideo.TabIndex = 0;
            // 
            // lblLeftVideoEmpty
            // 
            this.lblLeftVideoEmpty.AutoSize = true;
            this.lblLeftVideoEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLeftVideoEmpty.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.lblLeftVideoEmpty.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblLeftVideoEmpty.Location = new System.Drawing.Point(0, 0);
            this.lblLeftVideoEmpty.Name = "lblLeftVideoEmpty";
            this.lblLeftVideoEmpty.Size = new System.Drawing.Size(95, 25);
            this.lblLeftVideoEmpty.TabIndex = 0;
            this.lblLeftVideoEmpty.Text = "영상 없음";
            this.lblLeftVideoEmpty.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlSearchArea
            // 
            this.pnlSearchArea.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.pnlSearchArea.Controls.Add(this.cmbRenderSelect);
            this.pnlSearchArea.Controls.Add(this.lblRender);
            this.pnlSearchArea.Controls.Add(this.btnSearch);
            this.pnlSearchArea.Controls.Add(this.cboEndSecond);
            this.pnlSearchArea.Controls.Add(this.cboStartSecond);
            this.pnlSearchArea.Controls.Add(this.cboEndMinute);
            this.pnlSearchArea.Controls.Add(this.cboStartMinute);
            this.pnlSearchArea.Controls.Add(this.cboEndHour);
            this.pnlSearchArea.Controls.Add(this.cboStartHour);
            this.pnlSearchArea.Controls.Add(this.dtpSearchEndDate);
            this.pnlSearchArea.Controls.Add(this.dtpSearchStartDate);
            this.pnlSearchArea.Controls.Add(this.label3);
            this.pnlSearchArea.Controls.Add(this.lblSearchStartDate);
            this.pnlSearchArea.Controls.Add(this.cboCounterNo);
            this.pnlSearchArea.Controls.Add(this.lblCounterNo);
            this.pnlSearchArea.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSearchArea.Location = new System.Drawing.Point(3, 3);
            this.pnlSearchArea.Name = "pnlSearchArea";
            this.pnlSearchArea.Size = new System.Drawing.Size(634, 254);
            this.pnlSearchArea.TabIndex = 0;
            // 
            // cmbRenderSelect
            // 
            this.cmbRenderSelect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRenderSelect.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cmbRenderSelect.FormattingEnabled = true;
            this.cmbRenderSelect.Location = new System.Drawing.Point(165, 171);
            this.cmbRenderSelect.Name = "cmbRenderSelect";
            this.cmbRenderSelect.Size = new System.Drawing.Size(125, 25);
            this.cmbRenderSelect.TabIndex = 10;
            // 
            // lblRender
            // 
            this.lblRender.AutoSize = true;
            this.lblRender.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblRender.ForeColor = System.Drawing.Color.White;
            this.lblRender.Location = new System.Drawing.Point(45, 174);
            this.lblRender.Name = "lblRender";
            this.lblRender.Size = new System.Drawing.Size(98, 19);
            this.lblRender.TabIndex = 9;
            this.lblRender.Text = "영상 원본비율";
            // 
            // btnSearch
            // 
            this.btnSearch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSearch.FlatAppearance.BorderSize = 0;
            this.btnSearch.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSearch.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSearch.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(55)))), ((int)(((byte)(210)))));
            this.btnSearch.Image = global::CamViewer.Properties.Resources.Search1;
            this.btnSearch.Location = new System.Drawing.Point(499, 77);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(80, 80);
            this.btnSearch.TabIndex = 8;
            this.btnSearch.UseVisualStyleBackColor = false;
            // 
            // cboEndSecond
            // 
            this.cboEndSecond.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEndSecond.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboEndSecond.FormattingEnabled = true;
            this.cboEndSecond.Location = new System.Drawing.Point(426, 127);
            this.cboEndSecond.Name = "cboEndSecond";
            this.cboEndSecond.Size = new System.Drawing.Size(58, 25);
            this.cboEndSecond.TabIndex = 7;
            // 
            // cboStartSecond
            // 
            this.cboStartSecond.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartSecond.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartSecond.FormattingEnabled = true;
            this.cboStartSecond.Location = new System.Drawing.Point(426, 80);
            this.cboStartSecond.Name = "cboStartSecond";
            this.cboStartSecond.Size = new System.Drawing.Size(58, 25);
            this.cboStartSecond.TabIndex = 7;
            // 
            // cboEndMinute
            // 
            this.cboEndMinute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEndMinute.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboEndMinute.FormattingEnabled = true;
            this.cboEndMinute.Location = new System.Drawing.Point(361, 127);
            this.cboEndMinute.Name = "cboEndMinute";
            this.cboEndMinute.Size = new System.Drawing.Size(58, 25);
            this.cboEndMinute.TabIndex = 6;
            // 
            // cboStartMinute
            // 
            this.cboStartMinute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartMinute.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartMinute.FormattingEnabled = true;
            this.cboStartMinute.Location = new System.Drawing.Point(361, 80);
            this.cboStartMinute.Name = "cboStartMinute";
            this.cboStartMinute.Size = new System.Drawing.Size(58, 25);
            this.cboStartMinute.TabIndex = 6;
            // 
            // cboEndHour
            // 
            this.cboEndHour.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEndHour.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboEndHour.FormattingEnabled = true;
            this.cboEndHour.Location = new System.Drawing.Point(296, 127);
            this.cboEndHour.Name = "cboEndHour";
            this.cboEndHour.Size = new System.Drawing.Size(58, 25);
            this.cboEndHour.TabIndex = 5;
            // 
            // cboStartHour
            // 
            this.cboStartHour.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartHour.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartHour.FormattingEnabled = true;
            this.cboStartHour.Location = new System.Drawing.Point(296, 80);
            this.cboStartHour.Name = "cboStartHour";
            this.cboStartHour.Size = new System.Drawing.Size(58, 25);
            this.cboStartHour.TabIndex = 5;
            // 
            // dtpSearchEndDate
            // 
            this.dtpSearchEndDate.CalendarFont = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchEndDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchEndDate.Location = new System.Drawing.Point(165, 128);
            this.dtpSearchEndDate.Name = "dtpSearchEndDate";
            this.dtpSearchEndDate.Size = new System.Drawing.Size(125, 25);
            this.dtpSearchEndDate.TabIndex = 3;
            // 
            // dtpSearchStartDate
            // 
            this.dtpSearchStartDate.CalendarFont = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchStartDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchStartDate.Location = new System.Drawing.Point(165, 81);
            this.dtpSearchStartDate.Name = "dtpSearchStartDate";
            this.dtpSearchStartDate.Size = new System.Drawing.Size(125, 25);
            this.dtpSearchStartDate.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(45, 133);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(98, 19);
            this.label3.TabIndex = 0;
            this.label3.Text = "영상 종료시간";
            // 
            // lblSearchStartDate
            // 
            this.lblSearchStartDate.AutoSize = true;
            this.lblSearchStartDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSearchStartDate.ForeColor = System.Drawing.Color.White;
            this.lblSearchStartDate.Location = new System.Drawing.Point(45, 88);
            this.lblSearchStartDate.Name = "lblSearchStartDate";
            this.lblSearchStartDate.Size = new System.Drawing.Size(98, 19);
            this.lblSearchStartDate.TabIndex = 0;
            this.lblSearchStartDate.Text = "영상 시작시간";
            // 
            // cboCounterNo
            // 
            this.cboCounterNo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboCounterNo.FormattingEnabled = true;
            this.cboCounterNo.Location = new System.Drawing.Point(165, 38);
            this.cboCounterNo.Name = "cboCounterNo";
            this.cboCounterNo.Size = new System.Drawing.Size(70, 25);
            this.cboCounterNo.TabIndex = 1;
            // 
            // lblCounterNo
            // 
            this.lblCounterNo.AutoSize = true;
            this.lblCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblCounterNo.ForeColor = System.Drawing.Color.White;
            this.lblCounterNo.Location = new System.Drawing.Point(50, 43);
            this.lblCounterNo.Name = "lblCounterNo";
            this.lblCounterNo.Size = new System.Drawing.Size(79, 19);
            this.lblCounterNo.TabIndex = 0;
            this.lblCounterNo.Text = "계산대번호";
            // 
            // lblSeconds
            // 
            this.lblSeconds.AutoSize = true;
            this.lblSeconds.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSeconds.ForeColor = System.Drawing.Color.White;
            this.lblSeconds.Location = new System.Drawing.Point(503, 177);
            this.lblSeconds.Name = "lblSeconds";
            this.lblSeconds.Size = new System.Drawing.Size(23, 19);
            this.lblSeconds.TabIndex = 0;
            this.lblSeconds.Text = "초";
            // 
            // nudTiemAdjustSeconds
            // 
            this.nudTiemAdjustSeconds.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.nudTiemAdjustSeconds.Location = new System.Drawing.Point(437, 175);
            this.nudTiemAdjustSeconds.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.nudTiemAdjustSeconds.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudTiemAdjustSeconds.Name = "nudTiemAdjustSeconds";
            this.nudTiemAdjustSeconds.Size = new System.Drawing.Size(60, 25);
            this.nudTiemAdjustSeconds.TabIndex = 10;
            this.nudTiemAdjustSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudTiemAdjustSeconds.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // lblTimehAdjust
            // 
            this.lblTimehAdjust.AutoSize = true;
            this.lblTimehAdjust.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblTimehAdjust.ForeColor = System.Drawing.Color.White;
            this.lblTimehAdjust.Location = new System.Drawing.Point(366, 177);
            this.lblTimehAdjust.Name = "lblTimehAdjust";
            this.lblTimehAdjust.Size = new System.Drawing.Size(65, 19);
            this.lblTimehAdjust.TabIndex = 0;
            this.lblTimehAdjust.Text = "이동간격";
            // 
            // pnlTitleBar
            // 
            this.pnlTitleBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(25)))), ((int)(((byte)(25)))));
            this.pnlTitleBar.Controls.Add(this.btnClose);
            this.pnlTitleBar.Controls.Add(this.btnResize);
            this.pnlTitleBar.Controls.Add(this.btnMinimize);
            this.pnlTitleBar.Controls.Add(this.btnSettings);
            this.pnlTitleBar.Controls.Add(this.lblTitle);
            this.pnlTitleBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitleBar.Location = new System.Drawing.Point(0, 0);
            this.pnlTitleBar.Name = "pnlTitleBar";
            this.pnlTitleBar.Size = new System.Drawing.Size(1280, 42);
            this.pnlTitleBar.TabIndex = 1;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Image = global::CamViewer.Properties.Resources.Close;
            this.btnClose.Location = new System.Drawing.Point(1226, 7);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(42, 32);
            this.btnClose.TabIndex = 3;
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // btnResize
            // 
            this.btnResize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnResize.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnResize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResize.Image = global::CamViewer.Properties.Resources.Minimum;
            this.btnResize.Location = new System.Drawing.Point(1178, 7);
            this.btnResize.Name = "btnResize";
            this.btnResize.Size = new System.Drawing.Size(42, 32);
            this.btnResize.TabIndex = 0;
            this.btnResize.UseVisualStyleBackColor = true;
            // 
            // btnMinimize
            // 
            this.btnMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimize.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnMinimize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMinimize.ForeColor = System.Drawing.Color.White;
            this.btnMinimize.Image = global::CamViewer.Properties.Resources.UnderBar;
            this.btnMinimize.ImageAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnMinimize.Location = new System.Drawing.Point(1130, 7);
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.Size = new System.Drawing.Size(42, 32);
            this.btnMinimize.TabIndex = 0;
            this.btnMinimize.UseVisualStyleBackColor = true;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.Image = global::CamViewer.Properties.Resources.Config;
            this.btnSettings.Location = new System.Drawing.Point(1082, 7);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(42, 32);
            this.btnSettings.TabIndex = 0;
            this.btnSettings.UseVisualStyleBackColor = true;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(15, 11);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(122, 15);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "POSCAM CamViewer";
            // 
            // pnlTimeLIne
            // 
            this.pnlTimeLIne.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlTimeLIne.Location = new System.Drawing.Point(0, 380);
            this.pnlTimeLIne.Name = "pnlTimeLIne";
            this.pnlTimeLIne.Size = new System.Drawing.Size(1280, 80);
            this.pnlTimeLIne.TabIndex = 0;
            // 
            // tplMenuArea
            // 
            this.tplMenuArea.ColumnCount = 2;
            this.tplMenuArea.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tplMenuArea.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tplMenuArea.Controls.Add(this.pnlPalyerMenu, 1, 0);
            this.tplMenuArea.Controls.Add(this.pnlSearchArea, 0, 0);
            this.tplMenuArea.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.tplMenuArea.Location = new System.Drawing.Point(0, 460);
            this.tplMenuArea.Name = "tplMenuArea";
            this.tplMenuArea.RowCount = 1;
            this.tplMenuArea.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this.tplMenuArea.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tplMenuArea.Size = new System.Drawing.Size(1280, 260);
            this.tplMenuArea.TabIndex = 0;
            // 
            // pnlPalyerMenu
            // 
            this.pnlPalyerMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.pnlPalyerMenu.Controls.Add(this.btnSync);
            this.pnlPalyerMenu.Controls.Add(this.lblPlaybackSyncStatus);
            this.pnlPalyerMenu.Controls.Add(this.cboPlaybackSpeed);
            this.pnlPalyerMenu.Controls.Add(this.lblSeconds);
            this.pnlPalyerMenu.Controls.Add(this.label4);
            this.pnlPalyerMenu.Controls.Add(this.nudTiemAdjustSeconds);
            this.pnlPalyerMenu.Controls.Add(this.btnStop);
            this.pnlPalyerMenu.Controls.Add(this.lblTimehAdjust);
            this.pnlPalyerMenu.Controls.Add(this.btnSeekForward10);
            this.pnlPalyerMenu.Controls.Add(this.btnPlayPause);
            this.pnlPalyerMenu.Controls.Add(this.btnSeekBackward10);
            this.pnlPalyerMenu.Controls.Add(this.btnRewind);
            this.pnlPalyerMenu.Controls.Add(this.lblPlaybackDateTime);
            this.pnlPalyerMenu.Controls.Add(this.lblPlaybackDateTimeTitle);
            this.pnlPalyerMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlPalyerMenu.Location = new System.Drawing.Point(643, 3);
            this.pnlPalyerMenu.Name = "pnlPalyerMenu";
            this.pnlPalyerMenu.Size = new System.Drawing.Size(634, 254);
            this.pnlPalyerMenu.TabIndex = 0;
            // 
            // btnSync
            // 
            this.btnSync.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(109)))), ((int)(((byte)(53)))), ((int)(((byte)(209)))));
            this.btnSync.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSync.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.btnSync.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(109)))), ((int)(((byte)(53)))), ((int)(((byte)(209)))));
            this.btnSync.Location = new System.Drawing.Point(405, 30);
            this.btnSync.Name = "btnSync";
            this.btnSync.Size = new System.Drawing.Size(130, 35);
            this.btnSync.TabIndex = 10;
            this.btnSync.Text = "영상 동기화";
            this.btnSync.UseVisualStyleBackColor = true;
            // 
            // lblPlaybackSyncStatus
            // 
            this.lblPlaybackSyncStatus.AutoSize = true;
            this.lblPlaybackSyncStatus.ForeColor = System.Drawing.Color.White;
            this.lblPlaybackSyncStatus.Location = new System.Drawing.Point(111, 214);
            this.lblPlaybackSyncStatus.Name = "lblPlaybackSyncStatus";
            this.lblPlaybackSyncStatus.Size = new System.Drawing.Size(39, 15);
            this.lblPlaybackSyncStatus.TabIndex = 0;
            this.lblPlaybackSyncStatus.Text = "label5";
            // 
            // cboPlaybackSpeed
            // 
            this.cboPlaybackSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboPlaybackSpeed.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboPlaybackSpeed.FormattingEnabled = true;
            this.cboPlaybackSpeed.Location = new System.Drawing.Point(182, 174);
            this.cboPlaybackSpeed.Name = "cboPlaybackSpeed";
            this.cboPlaybackSpeed.Size = new System.Drawing.Size(58, 25);
            this.cboPlaybackSpeed.TabIndex = 9;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(110, 177);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 19);
            this.label4.TabIndex = 0;
            this.label4.Text = "재생속도";
            // 
            // btnStop
            // 
            this.btnStop.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnStop.FlatAppearance.BorderSize = 0;
            this.btnStop.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnStop.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStop.Image = global::CamViewer.Properties.Resources.Stop;
            this.btnStop.Location = new System.Drawing.Point(280, 80);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 75);
            this.btnStop.TabIndex = 7;
            this.btnStop.UseVisualStyleBackColor = true;
            // 
            // btnSeekForward10
            // 
            this.btnSeekForward10.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatAppearance.BorderSize = 0;
            this.btnSeekForward10.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSeekForward10.Image = global::CamViewer.Properties.Resources.Forward;
            this.btnSeekForward10.Location = new System.Drawing.Point(460, 80);
            this.btnSeekForward10.Name = "btnSeekForward10";
            this.btnSeekForward10.Size = new System.Drawing.Size(75, 75);
            this.btnSeekForward10.TabIndex = 6;
            this.btnSeekForward10.UseVisualStyleBackColor = true;
            // 
            // btnPlayPause
            // 
            this.btnPlayPause.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnPlayPause.FlatAppearance.BorderSize = 0;
            this.btnPlayPause.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnPlayPause.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnPlayPause.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPlayPause.Image = global::CamViewer.Properties.Resources.Paly;
            this.btnPlayPause.Location = new System.Drawing.Point(370, 80);
            this.btnPlayPause.Name = "btnPlayPause";
            this.btnPlayPause.Size = new System.Drawing.Size(75, 75);
            this.btnPlayPause.TabIndex = 5;
            this.btnPlayPause.UseVisualStyleBackColor = true;
            // 
            // btnSeekBackward10
            // 
            this.btnSeekBackward10.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekBackward10.FlatAppearance.BorderSize = 0;
            this.btnSeekBackward10.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekBackward10.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekBackward10.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSeekBackward10.Image = global::CamViewer.Properties.Resources.Reverse;
            this.btnSeekBackward10.Location = new System.Drawing.Point(100, 80);
            this.btnSeekBackward10.Name = "btnSeekBackward10";
            this.btnSeekBackward10.Size = new System.Drawing.Size(75, 75);
            this.btnSeekBackward10.TabIndex = 4;
            this.btnSeekBackward10.UseVisualStyleBackColor = true;
            // 
            // btnRewind
            // 
            this.btnRewind.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnRewind.FlatAppearance.BorderSize = 0;
            this.btnRewind.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnRewind.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnRewind.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRewind.Image = global::CamViewer.Properties.Resources.Rewind;
            this.btnRewind.Location = new System.Drawing.Point(190, 80);
            this.btnRewind.Name = "btnRewind";
            this.btnRewind.Size = new System.Drawing.Size(75, 75);
            this.btnRewind.TabIndex = 3;
            this.btnRewind.UseVisualStyleBackColor = true;
            // 
            // lblPlaybackDateTime
            // 
            this.lblPlaybackDateTime.AutoSize = true;
            this.lblPlaybackDateTime.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblPlaybackDateTime.ForeColor = System.Drawing.Color.White;
            this.lblPlaybackDateTime.Location = new System.Drawing.Point(220, 38);
            this.lblPlaybackDateTime.Name = "lblPlaybackDateTime";
            this.lblPlaybackDateTime.Size = new System.Drawing.Size(15, 19);
            this.lblPlaybackDateTime.TabIndex = 0;
            this.lblPlaybackDateTime.Text = "-";
            // 
            // lblPlaybackDateTimeTitle
            // 
            this.lblPlaybackDateTimeTitle.AutoSize = true;
            this.lblPlaybackDateTimeTitle.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblPlaybackDateTimeTitle.ForeColor = System.Drawing.Color.White;
            this.lblPlaybackDateTimeTitle.Location = new System.Drawing.Point(100, 38);
            this.lblPlaybackDateTimeTitle.Name = "lblPlaybackDateTimeTitle";
            this.lblPlaybackDateTimeTitle.Size = new System.Drawing.Size(65, 19);
            this.lblPlaybackDateTimeTitle.TabIndex = 0;
            this.lblPlaybackDateTimeTitle.Text = "재생시간";
            // 
            // PlayerView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.ClientSize = new System.Drawing.Size(1280, 720);
            this.Controls.Add(this.tlpBody);
            this.Controls.Add(this.pnlTimeLIne);
            this.Controls.Add(this.tplMenuArea);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(1100, 700);
            this.Name = "PlayerView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PlayerView";
            this.tlpBody.ResumeLayout(false);
            this.pnlRightVideo.ResumeLayout(false);
            this.pnlRightVideo.PerformLayout();
            this.pnlLeftVideo.ResumeLayout(false);
            this.pnlLeftVideo.PerformLayout();
            this.pnlSearchArea.ResumeLayout(false);
            this.pnlSearchArea.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudTiemAdjustSeconds)).EndInit();
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.tplMenuArea.ResumeLayout(false);
            this.pnlPalyerMenu.ResumeLayout(false);
            this.pnlPalyerMenu.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
             
        private System.Windows.Forms.TableLayoutPanel tlpBody;
        private System.Windows.Forms.Panel pnlLeftVideo;
        private System.Windows.Forms.Label lblLeftVideoEmpty;
        private System.Windows.Forms.Panel pnlRightVideo;
        private System.Windows.Forms.Label lblRightVideoEmpty;
        private System.Windows.Forms.Panel pnlSearchArea;
        private System.Windows.Forms.Label lblCounterNo;
        private System.Windows.Forms.DateTimePicker dtpSearchStartDate;
        private System.Windows.Forms.Label lblSearchStartDate;
        private System.Windows.Forms.ComboBox cboCounterNo;
        private System.Windows.Forms.ComboBox cboStartSecond;
        private System.Windows.Forms.ComboBox cboStartMinute;
        private System.Windows.Forms.ComboBox cboStartHour;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.NumericUpDown nudTiemAdjustSeconds;
        private System.Windows.Forms.Label lblTimehAdjust;
        private System.Windows.Forms.Label lblSeconds;
        private System.Windows.Forms.Panel pnlTitleBar;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnResize;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.ComboBox cboEndSecond;
        private System.Windows.Forms.ComboBox cboEndMinute;
        private System.Windows.Forms.ComboBox cboEndHour;
        private System.Windows.Forms.DateTimePicker dtpSearchEndDate;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel pnlTimeLIne;
        private System.Windows.Forms.TableLayoutPanel tplMenuArea;
        private System.Windows.Forms.Panel pnlPalyerMenu;
        private System.Windows.Forms.Label lblPlaybackSyncStatus;
        private System.Windows.Forms.ComboBox cboPlaybackSpeed;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnSeekForward10;
        private System.Windows.Forms.Button btnPlayPause;
        private System.Windows.Forms.Button btnSeekBackward10;
        private System.Windows.Forms.Button btnRewind;
        private System.Windows.Forms.Label lblPlaybackDateTime;
        private System.Windows.Forms.Label lblPlaybackDateTimeTitle;
        private System.Windows.Forms.Button btnSync;
        private System.Windows.Forms.ComboBox cmbRenderSelect;
        private System.Windows.Forms.Label lblRender;
    }
}