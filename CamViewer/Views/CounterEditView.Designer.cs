namespace CamViewer.Views
{
    partial class CounterEditView
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
            this.grpCounterInfo = new System.Windows.Forms.GroupBox();
            this.cboScreenPosition = new System.Windows.Forms.ComboBox();
            this.lblScreenPosition = new System.Windows.Forms.Label();
            this.lblChannelRange = new System.Windows.Forms.Label();
            this.nudChannelNo = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.lblNvr = new System.Windows.Forms.Label();
            this.nudCounterNo = new System.Windows.Forms.NumericUpDown();
            this.lblCounterNo = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.cboNvr = new System.Windows.Forms.ComboBox();
            this.grpCounterInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudChannelNo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCounterNo)).BeginInit();
            this.SuspendLayout();
            // 
            // grpCounterInfo
            // 
            this.grpCounterInfo.Controls.Add(this.cboNvr);
            this.grpCounterInfo.Controls.Add(this.cboScreenPosition);
            this.grpCounterInfo.Controls.Add(this.lblScreenPosition);
            this.grpCounterInfo.Controls.Add(this.lblChannelRange);
            this.grpCounterInfo.Controls.Add(this.nudChannelNo);
            this.grpCounterInfo.Controls.Add(this.label1);
            this.grpCounterInfo.Controls.Add(this.lblNvr);
            this.grpCounterInfo.Controls.Add(this.nudCounterNo);
            this.grpCounterInfo.Controls.Add(this.lblCounterNo);
            this.grpCounterInfo.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.grpCounterInfo.Location = new System.Drawing.Point(15, 15);
            this.grpCounterInfo.Name = "grpCounterInfo";
            this.grpCounterInfo.Size = new System.Drawing.Size(300, 220);
            this.grpCounterInfo.TabIndex = 0;
            this.grpCounterInfo.TabStop = false;
            this.grpCounterInfo.Text = "계산대 정보";
            // 
            // cboScreenPosition
            // 
            this.cboScreenPosition.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboScreenPosition.Location = new System.Drawing.Point(140, 171);
            this.cboScreenPosition.Name = "cboScreenPosition";
            this.cboScreenPosition.Size = new System.Drawing.Size(121, 29);
            this.cboScreenPosition.TabIndex = 0;
            // 
            // lblScreenPosition
            // 
            this.lblScreenPosition.AutoSize = true;
            this.lblScreenPosition.Location = new System.Drawing.Point(20, 175);
            this.lblScreenPosition.Name = "lblScreenPosition";
            this.lblScreenPosition.Size = new System.Drawing.Size(90, 21);
            this.lblScreenPosition.TabIndex = 7;
            this.lblScreenPosition.Text = "스크린위치";
            // 
            // lblChannelRange
            // 
            this.lblChannelRange.AutoSize = true;
            this.lblChannelRange.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblChannelRange.Location = new System.Drawing.Point(123, 143);
            this.lblChannelRange.Name = "lblChannelRange";
            this.lblChannelRange.Size = new System.Drawing.Size(147, 19);
            this.lblChannelRange.TabIndex = 6;
            this.lblChannelRange.Text = "입력 가능 채널: 1 ~ 1";
            // 
            // nudChannelNo
            // 
            this.nudChannelNo.Location = new System.Drawing.Point(140, 111);
            this.nudChannelNo.Name = "nudChannelNo";
            this.nudChannelNo.Size = new System.Drawing.Size(120, 29);
            this.nudChannelNo.TabIndex = 5;
            this.nudChannelNo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudChannelNo.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(36, 115);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 21);
            this.label1.TabIndex = 4;
            this.label1.Text = "채널번호";
            // 
            // lblNvr
            // 
            this.lblNvr.AutoSize = true;
            this.lblNvr.Location = new System.Drawing.Point(68, 75);
            this.lblNvr.Name = "lblNvr";
            this.lblNvr.Size = new System.Drawing.Size(42, 21);
            this.lblNvr.TabIndex = 2;
            this.lblNvr.Text = "NVR";
            // 
            // nudCounterNo
            // 
            this.nudCounterNo.Location = new System.Drawing.Point(140, 31);
            this.nudCounterNo.Name = "nudCounterNo";
            this.nudCounterNo.Size = new System.Drawing.Size(120, 29);
            this.nudCounterNo.TabIndex = 1;
            this.nudCounterNo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudCounterNo.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // lblCounterNo
            // 
            this.lblCounterNo.AutoSize = true;
            this.lblCounterNo.Location = new System.Drawing.Point(20, 35);
            this.lblCounterNo.Name = "lblCounterNo";
            this.lblCounterNo.Size = new System.Drawing.Size(90, 21);
            this.lblCounterNo.TabIndex = 0;
            this.lblCounterNo.Text = "계산대번호";
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.btnClose.Location = new System.Drawing.Point(215, 248);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 40);
            this.btnClose.TabIndex = 11;
            this.btnClose.Text = "닫기";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.Font = new System.Drawing.Font("맑은 고딕", 12F);
            this.btnSave.Location = new System.Drawing.Point(109, 248);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 40);
            this.btnSave.TabIndex = 10;
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // cboNvr
            // 
            this.cboNvr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboNvr.FormattingEnabled = true;
            this.cboNvr.Location = new System.Drawing.Point(140, 72);
            this.cboNvr.Name = "cboNvr";
            this.cboNvr.Size = new System.Drawing.Size(121, 29);
            this.cboNvr.TabIndex = 8;
            // 
            // CounterEditView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(330, 300);
            this.ControlBox = false;
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.grpCounterInfo);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.HelpButton = true;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "CounterEditView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CounterEditView";
            this.grpCounterInfo.ResumeLayout(false);
            this.grpCounterInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudChannelNo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCounterNo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpCounterInfo;
        private System.Windows.Forms.NumericUpDown nudCounterNo;
        private System.Windows.Forms.Label lblCounterNo;
        private System.Windows.Forms.NumericUpDown nudChannelNo;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblNvr;
        private System.Windows.Forms.Label lblChannelRange;
        private System.Windows.Forms.ComboBox cboScreenPosition;
        private System.Windows.Forms.Label lblScreenPosition;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox cboNvr;
    }
}