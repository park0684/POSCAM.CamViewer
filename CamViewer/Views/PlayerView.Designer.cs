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
            this.label1 = new System.Windows.Forms.Label();
            this.nudPlaydjustSeconds = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.lblSecondsBefore = new System.Windows.Forms.Label();
            this.nudSearchAdjustSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblSearchAdjust = new System.Windows.Forms.Label();
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
            this.panel2 = new System.Windows.Forms.Panel();
            this.cboPlaybackSpeed = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
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
            ((System.ComponentModel.ISupportInitialize)(this.nudPlaydjustSeconds)).BeginInit();
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
            this.tlpBody.Size = new System.Drawing.Size(1280, 858);
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
            this.pnlSearchArea.Controls.Add(this.label1);
            this.pnlSearchArea.Controls.Add(this.nudPlaydjustSeconds);
            this.pnlSearchArea.Controls.Add(this.label2);
            this.pnlSearchArea.Controls.Add(this.lblSecondsBefore);
            this.pnlSearchArea.Controls.Add(this.nudSearchAdjustSeconds);
            this.pnlSearchArea.Controls.Add(this.lblSearchAdjust);
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
            this.pnlSearchArea.Location = new System.Drawing.Point(3, 623);
            this.pnlSearchArea.Name = "pnlSearchArea";
            this.pnlSearchArea.Size = new System.Drawing.Size(634, 244);
            this.pnlSearchArea.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(513, 196);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(23, 19);
            this.label1.TabIndex = 14;
            this.label1.Text = "초";
            // 
            // nudPlaydjustSeconds
            // 
            this.nudPlaydjustSeconds.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.nudPlaydjustSeconds.Location = new System.Drawing.Point(447, 194);
            this.nudPlaydjustSeconds.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.nudPlaydjustSeconds.Name = "nudPlaydjustSeconds";
            this.nudPlaydjustSeconds.Size = new System.Drawing.Size(60, 25);
            this.nudPlaydjustSeconds.TabIndex = 13;
            this.nudPlaydjustSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudPlaydjustSeconds.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(327, 196);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(93, 19);
            this.label2.TabIndex = 12;
            this.label2.Text = "영상재생시간";
            // 
            // lblSecondsBefore
            // 
            this.lblSecondsBefore.AutoSize = true;
            this.lblSecondsBefore.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSecondsBefore.ForeColor = System.Drawing.Color.White;
            this.lblSecondsBefore.Location = new System.Drawing.Point(271, 194);
            this.lblSecondsBefore.Name = "lblSecondsBefore";
            this.lblSecondsBefore.Size = new System.Drawing.Size(37, 19);
            this.lblSecondsBefore.TabIndex = 11;
            this.lblSecondsBefore.Text = "초전";
            // 
            // nudSearchAdjustSeconds
            // 
            this.nudSearchAdjustSeconds.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.nudSearchAdjustSeconds.Location = new System.Drawing.Point(205, 192);
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
            this.lblSearchAdjust.Location = new System.Drawing.Point(85, 194);
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
            this.btnSearch.Location = new System.Drawing.Point(542, 80);
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
            this.cboEndSecond.Location = new System.Drawing.Point(469, 130);
            this.cboEndSecond.Name = "cboEndSecond";
            this.cboEndSecond.Size = new System.Drawing.Size(58, 25);
            this.cboEndSecond.TabIndex = 7;
            // 
            // cboStartSecond
            // 
            this.cboStartSecond.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartSecond.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartSecond.FormattingEnabled = true;
            this.cboStartSecond.Location = new System.Drawing.Point(469, 83);
            this.cboStartSecond.Name = "cboStartSecond";
            this.cboStartSecond.Size = new System.Drawing.Size(58, 25);
            this.cboStartSecond.TabIndex = 7;
            // 
            // cboEndMinute
            // 
            this.cboEndMinute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEndMinute.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboEndMinute.FormattingEnabled = true;
            this.cboEndMinute.Location = new System.Drawing.Point(404, 130);
            this.cboEndMinute.Name = "cboEndMinute";
            this.cboEndMinute.Size = new System.Drawing.Size(58, 25);
            this.cboEndMinute.TabIndex = 6;
            // 
            // cboStartMinute
            // 
            this.cboStartMinute.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartMinute.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartMinute.FormattingEnabled = true;
            this.cboStartMinute.Location = new System.Drawing.Point(404, 83);
            this.cboStartMinute.Name = "cboStartMinute";
            this.cboStartMinute.Size = new System.Drawing.Size(58, 25);
            this.cboStartMinute.TabIndex = 6;
            // 
            // cboEndHour
            // 
            this.cboEndHour.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEndHour.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboEndHour.FormattingEnabled = true;
            this.cboEndHour.Location = new System.Drawing.Point(339, 130);
            this.cboEndHour.Name = "cboEndHour";
            this.cboEndHour.Size = new System.Drawing.Size(58, 25);
            this.cboEndHour.TabIndex = 5;
            // 
            // cboStartHour
            // 
            this.cboStartHour.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStartHour.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboStartHour.FormattingEnabled = true;
            this.cboStartHour.Location = new System.Drawing.Point(339, 83);
            this.cboStartHour.Name = "cboStartHour";
            this.cboStartHour.Size = new System.Drawing.Size(58, 25);
            this.cboStartHour.TabIndex = 5;
            // 
            // dtpSearchEndDate
            // 
            this.dtpSearchEndDate.CalendarFont = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchEndDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchEndDate.Location = new System.Drawing.Point(208, 131);
            this.dtpSearchEndDate.Name = "dtpSearchEndDate";
            this.dtpSearchEndDate.Size = new System.Drawing.Size(125, 25);
            this.dtpSearchEndDate.TabIndex = 3;
            // 
            // dtpSearchStartDate
            // 
            this.dtpSearchStartDate.CalendarFont = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchStartDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.dtpSearchStartDate.Location = new System.Drawing.Point(208, 84);
            this.dtpSearchStartDate.Name = "dtpSearchStartDate";
            this.dtpSearchStartDate.Size = new System.Drawing.Size(125, 25);
            this.dtpSearchStartDate.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(88, 136);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(98, 19);
            this.label3.TabIndex = 2;
            this.label3.Text = "영상 종료시간";
            // 
            // lblSearchStartDate
            // 
            this.lblSearchStartDate.AutoSize = true;
            this.lblSearchStartDate.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblSearchStartDate.ForeColor = System.Drawing.Color.White;
            this.lblSearchStartDate.Location = new System.Drawing.Point(88, 91);
            this.lblSearchStartDate.Name = "lblSearchStartDate";
            this.lblSearchStartDate.Size = new System.Drawing.Size(98, 19);
            this.lblSearchStartDate.TabIndex = 2;
            this.lblSearchStartDate.Text = "영상 시작시간";
            // 
            // cboCounterNo
            // 
            this.cboCounterNo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboCounterNo.FormattingEnabled = true;
            this.cboCounterNo.Location = new System.Drawing.Point(208, 41);
            this.cboCounterNo.Name = "cboCounterNo";
            this.cboCounterNo.Size = new System.Drawing.Size(70, 25);
            this.cboCounterNo.TabIndex = 1;
            // 
            // lblCounterNo
            // 
            this.lblCounterNo.AutoSize = true;
            this.lblCounterNo.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblCounterNo.ForeColor = System.Drawing.Color.White;
            this.lblCounterNo.Location = new System.Drawing.Point(93, 46);
            this.lblCounterNo.Name = "lblCounterNo";
            this.lblCounterNo.Size = new System.Drawing.Size(79, 19);
            this.lblCounterNo.TabIndex = 0;
            this.lblCounterNo.Text = "계산대번호";
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.panel2.Controls.Add(this.cboPlaybackSpeed);
            this.panel2.Controls.Add(this.label4);
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
            // cboPlaybackSpeed
            // 
            this.cboPlaybackSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboPlaybackSpeed.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.cboPlaybackSpeed.FormattingEnabled = true;
            this.cboPlaybackSpeed.Location = new System.Drawing.Point(477, 35);
            this.cboPlaybackSpeed.Name = "cboPlaybackSpeed";
            this.cboPlaybackSpeed.Size = new System.Drawing.Size(58, 25);
            this.cboPlaybackSpeed.TabIndex = 9;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(405, 38);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(65, 19);
            this.label4.TabIndex = 8;
            this.label4.Text = "재생속도";
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
            this.lblPlaybackDateTimeTitle.Size = new System.Drawing.Size(65, 19);
            this.lblPlaybackDateTimeTitle.TabIndex = 1;
            this.lblPlaybackDateTimeTitle.Text = "재생시간";
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
            this.pnlBody.Size = new System.Drawing.Size(1280, 858);
            this.pnlBody.TabIndex = 2;
            // 
            // PlayerView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.ClientSize = new System.Drawing.Size(1280, 900);
            this.Controls.Add(this.pnlBody);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(1100, 900);
            this.Name = "PlayerView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "PlayerView";
            this.tlpBody.ResumeLayout(false);
            this.pnlRightVideo.ResumeLayout(false);
            this.pnlRightVideo.PerformLayout();
            this.pnlSearchArea.ResumeLayout(false);
            this.pnlSearchArea.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudPlaydjustSeconds)).EndInit();
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
        private System.Windows.Forms.DateTimePicker dtpSearchStartDate;
        private System.Windows.Forms.Label lblSearchStartDate;
        private System.Windows.Forms.ComboBox cboCounterNo;
        private System.Windows.Forms.ComboBox cboStartSecond;
        private System.Windows.Forms.ComboBox cboStartMinute;
        private System.Windows.Forms.ComboBox cboStartHour;
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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown nudPlaydjustSeconds;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cboEndSecond;
        private System.Windows.Forms.ComboBox cboEndMinute;
        private System.Windows.Forms.ComboBox cboEndHour;
        private System.Windows.Forms.DateTimePicker dtpSearchEndDate;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cboPlaybackSpeed;
    }
}