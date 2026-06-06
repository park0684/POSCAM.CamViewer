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
            this.pnlSearchArea = new System.Windows.Forms.Panel();
            this.lblSecondsBefore = new System.Windows.Forms.Label();
            this.nudSearchAdjustSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblSearchAdjust = new System.Windows.Forms.Label();
            this.btnSearch = new System.Windows.Forms.Button();
            this.cboSecond = new System.Windows.Forms.ComboBox();
            this.cboMinute = new System.Windows.Forms.ComboBox();
            this.cboHour = new System.Windows.Forms.ComboBox();
            this.cboAmPm = new System.Windows.Forms.ComboBox();
            this.dtpSearchDate = new System.Windows.Forms.DateTimePicker();
            this.lblSearchDateTime = new System.Windows.Forms.Label();
            this.cboCounterNo = new System.Windows.Forms.ComboBox();
            this.lblCounterNo = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnFastForward = new System.Windows.Forms.Button();
            this.btnSeekForward10 = new System.Windows.Forms.Button();
            this.btnPlayPause = new System.Windows.Forms.Button();
            this.btnSeekBackward10 = new System.Windows.Forms.Button();
            this.btnFastReverse = new System.Windows.Forms.Button();
            this.lblPlaybackDateTime = new System.Windows.Forms.Label();
            this.lblPlaybackDateTimeTitle = new System.Windows.Forms.Label();
            this.pnlLeftVideo = new System.Windows.Forms.Panel();
            this.lblLeftVideoEmpty = new System.Windows.Forms.Label();
            this.pnlTitleBar = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnResize = new System.Windows.Forms.Button();
            this.btnMinimize = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.pnlBody = new System.Windows.Forms.Panel();
            this.tlpBody.SuspendLayout();
            this.pnlRightVideo.SuspendLayout();
            this.pnlSearchArea.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudSearchAdjustSeconds)).BeginInit();
            this.panel2.SuspendLayout();
            this.pnlLeftVideo.SuspendLayout();
            this.pnlTitleBar.SuspendLayout();
            this.pnlBody.SuspendLayout();
            this.SuspendLayout();
            // 
            // tlpBody
            // 
            this.tlpBody.ColumnCount = 2;
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBody.Controls.Add(this.pnlRightVideo, 1, 0);
            this.tlpBody.Controls.Add(this.pnlSearchArea, 0, 1);
            this.tlpBody.Controls.Add(this.panel2, 1, 1);
            this.tlpBody.Controls.Add(this.pnlLeftVideo, 0, 0);
            this.tlpBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpBody.Location = new System.Drawing.Point(0, 0);
            this.tlpBody.Name = "tlpBody";
            this.tlpBody.RowCount = 2;
            this.tlpBody.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 250F));
            this.tlpBody.Size = new System.Drawing.Size(1280, 678);
            this.tlpBody.TabIndex = 0;
            // 
            // pnlRightVideo
            // 
            this.pnlRightVideo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.pnlRightVideo.Controls.Add(this.lblRightVideoEmpty);
            this.pnlRightVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRightVideo.Location = new System.Drawing.Point(643, 3);
            this.pnlRightVideo.Name = "pnlRightVideo";
            this.pnlRightVideo.Size = new System.Drawing.Size(634, 614);
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
            // pnlSearchArea
            // 
            this.pnlSearchArea.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.pnlSearchArea.Controls.Add(this.lblSecondsBefore);
            this.pnlSearchArea.Controls.Add(this.nudSearchAdjustSeconds);
            this.pnlSearchArea.Controls.Add(this.lblSearchAdjust);
            this.pnlSearchArea.Controls.Add(this.btnSearch);
            this.pnlSearchArea.Controls.Add(this.cboSecond);
            this.pnlSearchArea.Controls.Add(this.cboMinute);
            this.pnlSearchArea.Controls.Add(this.cboHour);
            this.pnlSearchArea.Controls.Add(this.cboAmPm);
            this.pnlSearchArea.Controls.Add(this.dtpSearchDate);
            this.pnlSearchArea.Controls.Add(this.lblSearchDateTime);
            this.pnlSearchArea.Controls.Add(this.cboCounterNo);
            this.pnlSearchArea.Controls.Add(this.lblCounterNo);
            this.pnlSearchArea.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSearchArea.Location = new System.Drawing.Point(3, 623);
            this.pnlSearchArea.Name = "pnlSearchArea";
            this.pnlSearchArea.Size = new System.Drawing.Size(634, 244);
            this.pnlSearchArea.TabIndex = 0;
            // 
            // lblSecondsBefore
            // 
            this.lblSecondsBefore.AutoSize = true;
            this.lblSecondsBefore.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSecondsBefore.ForeColor = System.Drawing.Color.White;
            this.lblSecondsBefore.Location = new System.Drawing.Point(266, 150);
            this.lblSecondsBefore.Name = "lblSecondsBefore";
            this.lblSecondsBefore.Size = new System.Drawing.Size(37, 19);
            this.lblSecondsBefore.TabIndex = 11;
            this.lblSecondsBefore.Text = "초전";
            // 
            // nudSearchAdjustSeconds
            // 
            this.nudSearchAdjustSeconds.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.nudSearchAdjustSeconds.Location = new System.Drawing.Point(200, 148);
            this.nudSearchAdjustSeconds.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.nudSearchAdjustSeconds.Name = "nudSearchAdjustSeconds";
            this.nudSearchAdjustSeconds.Size = new System.Drawing.Size(60, 25);
            this.nudSearchAdjustSeconds.TabIndex = 10;
            this.nudSearchAdjustSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudSearchAdjustSeconds.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            // 
            // lblSearchAdjust
            // 
            this.lblSearchAdjust.AutoSize = true;
            this.lblSearchAdjust.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSearchAdjust.ForeColor = System.Drawing.Color.White;
            this.lblSearchAdjust.Location = new System.Drawing.Point(80, 150);
            this.lblSearchAdjust.Name = "lblSearchAdjust";
            this.lblSearchAdjust.Size = new System.Drawing.Size(93, 19);
            this.lblSearchAdjust.TabIndex = 9;
            this.lblSearchAdjust.Text = "검색시간조정";
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
            this.btnSearch.Location = new System.Drawing.Point(485, 75);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(80, 80);
            this.btnSearch.TabIndex = 8;
            this.btnSearch.UseVisualStyleBackColor = false;
            // 
            // cboSecond
            // 
            this.cboSecond.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSecond.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboSecond.FormattingEnabled = true;
            this.cboSecond.Location = new System.Drawing.Point(395, 108);
            this.cboSecond.Name = "cboSecond";
            this.cboSecond.Size = new System.Drawing.Size(58, 25);
            this.cboSecond.TabIndex = 7;
            // 
            // cboMinute
            // 
            this.cboMinute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMinute.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboMinute.FormattingEnabled = true;
            this.cboMinute.Location = new System.Drawing.Point(330, 108);
            this.cboMinute.Name = "cboMinute";
            this.cboMinute.Size = new System.Drawing.Size(58, 25);
            this.cboMinute.TabIndex = 6;
            // 
            // cboHour
            // 
            this.cboHour.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboHour.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboHour.FormattingEnabled = true;
            this.cboHour.Location = new System.Drawing.Point(265, 108);
            this.cboHour.Name = "cboHour";
            this.cboHour.Size = new System.Drawing.Size(58, 25);
            this.cboHour.TabIndex = 5;
            // 
            // cboAmPm
            // 
            this.cboAmPm.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAmPm.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboAmPm.FormattingEnabled = true;
            this.cboAmPm.Location = new System.Drawing.Point(200, 108);
            this.cboAmPm.Name = "cboAmPm";
            this.cboAmPm.Size = new System.Drawing.Size(58, 25);
            this.cboAmPm.TabIndex = 4;
            // 
            // dtpSearchDate
            // 
            this.dtpSearchDate.CalendarFont = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchDate.Location = new System.Drawing.Point(200, 73);
            this.dtpSearchDate.Name = "dtpSearchDate";
            this.dtpSearchDate.Size = new System.Drawing.Size(125, 25);
            this.dtpSearchDate.TabIndex = 3;
            // 
            // lblSearchDateTime
            // 
            this.lblSearchDateTime.AutoSize = true;
            this.lblSearchDateTime.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSearchDateTime.ForeColor = System.Drawing.Color.White;
            this.lblSearchDateTime.Location = new System.Drawing.Point(80, 78);
            this.lblSearchDateTime.Name = "lblSearchDateTime";
            this.lblSearchDateTime.Size = new System.Drawing.Size(93, 19);
            this.lblSearchDateTime.TabIndex = 2;
            this.lblSearchDateTime.Text = "영상검색일시";
            // 
            // cboCounterNo
            // 
            this.cboCounterNo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboCounterNo.FormattingEnabled = true;
            this.cboCounterNo.Location = new System.Drawing.Point(200, 30);
            this.cboCounterNo.Name = "cboCounterNo";
            this.cboCounterNo.Size = new System.Drawing.Size(70, 25);
            this.cboCounterNo.TabIndex = 1;
            // 
            // lblCounterNo
            // 
            this.lblCounterNo.AutoSize = true;
            this.lblCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblCounterNo.ForeColor = System.Drawing.Color.White;
            this.lblCounterNo.Location = new System.Drawing.Point(85, 35);
            this.lblCounterNo.Name = "lblCounterNo";
            this.lblCounterNo.Size = new System.Drawing.Size(79, 19);
            this.lblCounterNo.TabIndex = 0;
            this.lblCounterNo.Text = "계산대번호";
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.panel2.Controls.Add(this.btnFastForward);
            this.panel2.Controls.Add(this.btnSeekForward10);
            this.panel2.Controls.Add(this.btnPlayPause);
            this.panel2.Controls.Add(this.btnSeekBackward10);
            this.panel2.Controls.Add(this.btnFastReverse);
            this.panel2.Controls.Add(this.lblPlaybackDateTime);
            this.panel2.Controls.Add(this.lblPlaybackDateTimeTitle);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(643, 623);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(634, 244);
            this.panel2.TabIndex = 0;
            // 
            // btnFastForward
            // 
            this.btnFastForward.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastForward.FlatAppearance.BorderSize = 0;
            this.btnFastForward.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastForward.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastForward.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFastForward.Image = global::CamViewer.Properties.Resources.FastForward;
            this.btnFastForward.Location = new System.Drawing.Point(460, 80);
            this.btnFastForward.Name = "btnFastForward";
            this.btnFastForward.Size = new System.Drawing.Size(75, 75);
            this.btnFastForward.TabIndex = 7;
            this.btnFastForward.UseVisualStyleBackColor = true;
            // 
            // btnSeekForward10
            // 
            this.btnSeekForward10.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatAppearance.BorderSize = 0;
            this.btnSeekForward10.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnSeekForward10.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSeekForward10.Image = global::CamViewer.Properties.Resources.Forward;
            this.btnSeekForward10.Location = new System.Drawing.Point(370, 80);
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
            this.btnPlayPause.Location = new System.Drawing.Point(280, 80);
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
            this.btnSeekBackward10.Location = new System.Drawing.Point(190, 80);
            this.btnSeekBackward10.Name = "btnSeekBackward10";
            this.btnSeekBackward10.Size = new System.Drawing.Size(75, 75);
            this.btnSeekBackward10.TabIndex = 4;
            this.btnSeekBackward10.UseVisualStyleBackColor = true;
            // 
            // btnFastReverse
            // 
            this.btnFastReverse.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastReverse.FlatAppearance.BorderSize = 0;
            this.btnFastReverse.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastReverse.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.btnFastReverse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFastReverse.Image = global::CamViewer.Properties.Resources.FastReverse;
            this.btnFastReverse.Location = new System.Drawing.Point(100, 80);
            this.btnFastReverse.Name = "btnFastReverse";
            this.btnFastReverse.Size = new System.Drawing.Size(75, 75);
            this.btnFastReverse.TabIndex = 3;
            this.btnFastReverse.UseVisualStyleBackColor = true;
            // 
            // lblPlaybackDateTime
            // 
            this.lblPlaybackDateTime.AutoSize = true;
            this.lblPlaybackDateTime.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblPlaybackDateTime.ForeColor = System.Drawing.Color.White;
            this.lblPlaybackDateTime.Location = new System.Drawing.Point(220, 38);
            this.lblPlaybackDateTime.Name = "lblPlaybackDateTime";
            this.lblPlaybackDateTime.Size = new System.Drawing.Size(15, 19);
            this.lblPlaybackDateTime.TabIndex = 2;
            this.lblPlaybackDateTime.Text = "-";
            // 
            // lblPlaybackDateTimeTitle
            // 
            this.lblPlaybackDateTimeTitle.AutoSize = true;
            this.lblPlaybackDateTimeTitle.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblPlaybackDateTimeTitle.ForeColor = System.Drawing.Color.White;
            this.lblPlaybackDateTimeTitle.Location = new System.Drawing.Point(100, 38);
            this.lblPlaybackDateTimeTitle.Name = "lblPlaybackDateTimeTitle";
            this.lblPlaybackDateTimeTitle.Size = new System.Drawing.Size(93, 19);
            this.lblPlaybackDateTimeTitle.TabIndex = 1;
            this.lblPlaybackDateTimeTitle.Text = "영상재생일시";
            // 
            // pnlLeftVideo
            // 
            this.pnlLeftVideo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.pnlLeftVideo.Controls.Add(this.lblLeftVideoEmpty);
            this.pnlLeftVideo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeftVideo.Location = new System.Drawing.Point(3, 3);
            this.pnlLeftVideo.Name = "pnlLeftVideo";
            this.pnlLeftVideo.Size = new System.Drawing.Size(634, 614);
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
            // pnlBody
            // 
            this.pnlBody.Controls.Add(this.tlpBody);
            this.pnlBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBody.Location = new System.Drawing.Point(0, 42);
            this.pnlBody.Name = "pnlBody";
            this.pnlBody.Size = new System.Drawing.Size(1280, 678);
            this.pnlBody.TabIndex = 2;
            // 
            // PlayerView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.ClientSize = new System.Drawing.Size(1280, 720);
            this.Controls.Add(this.pnlBody);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(1100, 650);
            this.Name = "PlayerView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PlayerView";
            this.tlpBody.ResumeLayout(false);
            this.pnlRightVideo.ResumeLayout(false);
            this.pnlRightVideo.PerformLayout();
            this.pnlSearchArea.ResumeLayout(false);
            this.pnlSearchArea.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudSearchAdjustSeconds)).EndInit();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.pnlLeftVideo.ResumeLayout(false);
            this.pnlLeftVideo.PerformLayout();
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.pnlBody.ResumeLayout(false);
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
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ComboBox cboAmPm;
        private System.Windows.Forms.DateTimePicker dtpSearchDate;
        private System.Windows.Forms.Label lblSearchDateTime;
        private System.Windows.Forms.ComboBox cboCounterNo;
        private System.Windows.Forms.ComboBox cboSecond;
        private System.Windows.Forms.ComboBox cboMinute;
        private System.Windows.Forms.ComboBox cboHour;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.NumericUpDown nudSearchAdjustSeconds;
        private System.Windows.Forms.Label lblSearchAdjust;
        private System.Windows.Forms.Label lblSecondsBefore;
        private System.Windows.Forms.Button btnSeekBackward10;
        private System.Windows.Forms.Button btnFastReverse;
        private System.Windows.Forms.Label lblPlaybackDateTime;
        private System.Windows.Forms.Label lblPlaybackDateTimeTitle;
        private System.Windows.Forms.Button btnFastForward;
        private System.Windows.Forms.Button btnSeekForward10;
        private System.Windows.Forms.Button btnPlayPause;
        private System.Windows.Forms.Panel pnlTitleBar;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnResize;
        private System.Windows.Forms.Button btnMinimize;
        private System.Windows.Forms.Panel pnlBody;
    }
}