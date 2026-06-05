namespace CamViewer.Views
{
    partial class NvrEditView
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
            this.lblNvrNoTitle = new System.Windows.Forms.Label();
            this.grpBasicInfo = new System.Windows.Forms.GroupBox();
            this.txtConnectionType = new System.Windows.Forms.TextBox();
            this.lblConnectionType = new System.Windows.Forms.Label();
            this.cboVendor = new System.Windows.Forms.ComboBox();
            this.lblNvrNoValue = new System.Windows.Forms.Label();
            this.lblVendor = new System.Windows.Forms.Label();
            this.grpConnectionInfo = new System.Windows.Forms.GroupBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtUserId = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtHost = new System.Windows.Forms.TextBox();
            this.lblHost = new System.Windows.Forms.Label();
            this.nudPort = new System.Windows.Forms.NumericUpDown();
            this.nudChannelCount = new System.Windows.Forms.NumericUpDown();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.lblConnectionTestStatus = new System.Windows.Forms.Label();
            this.btnTestConnection = new System.Windows.Forms.Button();
            this.grpBasicInfo.SuspendLayout();
            this.grpConnectionInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudChannelCount)).BeginInit();
            this.SuspendLayout();
            // 
            // lblNvrNoTitle
            // 
            this.lblNvrNoTitle.AutoSize = true;
            this.lblNvrNoTitle.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblNvrNoTitle.Location = new System.Drawing.Point(42, 40);
            this.lblNvrNoTitle.Name = "lblNvrNoTitle";
            this.lblNvrNoTitle.Size = new System.Drawing.Size(74, 21);
            this.lblNvrNoTitle.TabIndex = 0;
            this.lblNvrNoTitle.Text = "NVR번호";
            // 
            // grpBasicInfo
            // 
            this.grpBasicInfo.Controls.Add(this.txtConnectionType);
            this.grpBasicInfo.Controls.Add(this.lblConnectionType);
            this.grpBasicInfo.Controls.Add(this.cboVendor);
            this.grpBasicInfo.Controls.Add(this.lblNvrNoValue);
            this.grpBasicInfo.Controls.Add(this.lblVendor);
            this.grpBasicInfo.Controls.Add(this.lblNvrNoTitle);
            this.grpBasicInfo.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.grpBasicInfo.Location = new System.Drawing.Point(12, 12);
            this.grpBasicInfo.Name = "grpBasicInfo";
            this.grpBasicInfo.Size = new System.Drawing.Size(480, 174);
            this.grpBasicInfo.TabIndex = 1;
            this.grpBasicInfo.TabStop = false;
            this.grpBasicInfo.Text = "기본 정보";
            // 
            // txtConnectionType
            // 
            this.txtConnectionType.Location = new System.Drawing.Point(140, 117);
            this.txtConnectionType.Name = "txtConnectionType";
            this.txtConnectionType.Size = new System.Drawing.Size(298, 29);
            this.txtConnectionType.TabIndex = 4;
            // 
            // lblConnectionType
            // 
            this.lblConnectionType.AutoSize = true;
            this.lblConnectionType.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblConnectionType.Location = new System.Drawing.Point(42, 120);
            this.lblConnectionType.Name = "lblConnectionType";
            this.lblConnectionType.Size = new System.Drawing.Size(74, 21);
            this.lblConnectionType.TabIndex = 3;
            this.lblConnectionType.Text = "접속방식";
            // 
            // cboVendor
            // 
            this.cboVendor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboVendor.FormattingEnabled = true;
            this.cboVendor.Location = new System.Drawing.Point(140, 77);
            this.cboVendor.Name = "cboVendor";
            this.cboVendor.Size = new System.Drawing.Size(298, 29);
            this.cboVendor.TabIndex = 2;
            // 
            // lblNvrNoValue
            // 
            this.lblNvrNoValue.AutoSize = true;
            this.lblNvrNoValue.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblNvrNoValue.Location = new System.Drawing.Point(147, 40);
            this.lblNvrNoValue.Name = "lblNvrNoValue";
            this.lblNvrNoValue.Size = new System.Drawing.Size(80, 21);
            this.lblNvrNoValue.TabIndex = 1;
            this.lblNvrNoValue.Text = "자동 지정";
            // 
            // lblVendor
            // 
            this.lblVendor.AutoSize = true;
            this.lblVendor.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblVendor.Location = new System.Drawing.Point(58, 80);
            this.lblVendor.Name = "lblVendor";
            this.lblVendor.Size = new System.Drawing.Size(58, 21);
            this.lblVendor.TabIndex = 0;
            this.lblVendor.Text = "제조사";
            // 
            // grpConnectionInfo
            // 
            this.grpConnectionInfo.Controls.Add(this.nudChannelCount);
            this.grpConnectionInfo.Controls.Add(this.nudPort);
            this.grpConnectionInfo.Controls.Add(this.txtPassword);
            this.grpConnectionInfo.Controls.Add(this.label4);
            this.grpConnectionInfo.Controls.Add(this.txtUserId);
            this.grpConnectionInfo.Controls.Add(this.label3);
            this.grpConnectionInfo.Controls.Add(this.label2);
            this.grpConnectionInfo.Controls.Add(this.label1);
            this.grpConnectionInfo.Controls.Add(this.txtHost);
            this.grpConnectionInfo.Controls.Add(this.lblHost);
            this.grpConnectionInfo.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.grpConnectionInfo.Location = new System.Drawing.Point(12, 206);
            this.grpConnectionInfo.Name = "grpConnectionInfo";
            this.grpConnectionInfo.Size = new System.Drawing.Size(480, 252);
            this.grpConnectionInfo.TabIndex = 2;
            this.grpConnectionInfo.TabStop = false;
            this.grpConnectionInfo.Text = "접속 정보";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(140, 197);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(298, 29);
            this.txtPassword.TabIndex = 12;
            this.txtPassword.UseSystemPasswordChar = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label4.Location = new System.Drawing.Point(37, 200);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(79, 21);
            this.label4.TabIndex = 11;
            this.label4.Text = "Password";
            // 
            // txtUserId
            // 
            this.txtUserId.Location = new System.Drawing.Point(140, 157);
            this.txtUserId.Name = "txtUserId";
            this.txtUserId.Size = new System.Drawing.Size(298, 29);
            this.txtUserId.TabIndex = 10;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label3.Location = new System.Drawing.Point(91, 160);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(25, 21);
            this.label3.TabIndex = 9;
            this.label3.Text = "ID";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label2.Location = new System.Drawing.Point(58, 120);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(58, 21);
            this.label2.TabIndex = 7;
            this.label2.Text = "채널수";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.label1.Location = new System.Drawing.Point(66, 77);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 21);
            this.label1.TabIndex = 7;
            this.label1.Text = "PORT";
            // 
            // txtHost
            // 
            this.txtHost.Location = new System.Drawing.Point(140, 37);
            this.txtHost.Name = "txtHost";
            this.txtHost.Size = new System.Drawing.Size(298, 29);
            this.txtHost.TabIndex = 6;
            // 
            // lblHost
            // 
            this.lblHost.AutoSize = true;
            this.lblHost.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblHost.Location = new System.Drawing.Point(27, 40);
            this.lblHost.Name = "lblHost";
            this.lblHost.Size = new System.Drawing.Size(89, 21);
            this.lblHost.TabIndex = 5;
            this.lblHost.Text = "IP / 도메인";
            // 
            // nudPort
            // 
            this.nudPort.Location = new System.Drawing.Point(140, 75);
            this.nudPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.nudPort.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudPort.Name = "nudPort";
            this.nudPort.Size = new System.Drawing.Size(120, 29);
            this.nudPort.TabIndex = 13;
            this.nudPort.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudPort.Value = new decimal(new int[] {
            37777,
            0,
            0,
            0});
            // 
            // nudChannelCount
            // 
            this.nudChannelCount.Location = new System.Drawing.Point(140, 118);
            this.nudChannelCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudChannelCount.Name = "nudChannelCount";
            this.nudChannelCount.Size = new System.Drawing.Size(120, 29);
            this.nudChannelCount.TabIndex = 14;
            this.nudChannelCount.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudChannelCount.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.btnClose.Location = new System.Drawing.Point(392, 469);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 40);
            this.btnClose.TabIndex = 9;
            this.btnClose.Text = "닫기";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.btnSave.Location = new System.Drawing.Point(286, 469);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 40);
            this.btnSave.TabIndex = 8;
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // lblConnectionTestStatus
            // 
            this.lblConnectionTestStatus.AutoSize = true;
            this.lblConnectionTestStatus.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.lblConnectionTestStatus.Location = new System.Drawing.Point(138, 479);
            this.lblConnectionTestStatus.Name = "lblConnectionTestStatus";
            this.lblConnectionTestStatus.Size = new System.Drawing.Size(42, 21);
            this.lblConnectionTestStatus.TabIndex = 10;
            this.lblConnectionTestStatus.Text = "상태";
            // 
            // btnTestConnection
            // 
            this.btnTestConnection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnTestConnection.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnTestConnection.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTestConnection.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.btnTestConnection.Location = new System.Drawing.Point(12, 469);
            this.btnTestConnection.Name = "btnTestConnection";
            this.btnTestConnection.Size = new System.Drawing.Size(120, 40);
            this.btnTestConnection.TabIndex = 11;
            this.btnTestConnection.Text = "연결 테스트";
            this.btnTestConnection.UseVisualStyleBackColor = true;
            // 
            // NvrEditView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(504, 521);
            this.Controls.Add(this.btnTestConnection);
            this.Controls.Add(this.lblConnectionTestStatus);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.grpConnectionInfo);
            this.Controls.Add(this.grpBasicInfo);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NvrEditView";
            this.Text = "NVR 등록";
            this.grpBasicInfo.ResumeLayout(false);
            this.grpBasicInfo.PerformLayout();
            this.grpConnectionInfo.ResumeLayout(false);
            this.grpConnectionInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudChannelCount)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblNvrNoTitle;
        private System.Windows.Forms.GroupBox grpBasicInfo;
        private System.Windows.Forms.GroupBox grpConnectionInfo;
        private System.Windows.Forms.Label lblConnectionType;
        private System.Windows.Forms.ComboBox cboVendor;
        private System.Windows.Forms.Label lblNvrNoValue;
        private System.Windows.Forms.Label lblVendor;
        private System.Windows.Forms.TextBox txtConnectionType;
        private System.Windows.Forms.TextBox txtHost;
        private System.Windows.Forms.Label lblHost;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtUserId;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown nudChannelCount;
        private System.Windows.Forms.NumericUpDown nudPort;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblConnectionTestStatus;
        private System.Windows.Forms.Button btnTestConnection;
    }
}