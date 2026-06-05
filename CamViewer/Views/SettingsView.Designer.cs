namespace CamViewer.Views
{
    partial class SettingsView
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
            this.grpNvr = new System.Windows.Forms.GroupBox();
            this.lblNvr = new System.Windows.Forms.Label();
            this.btnAddNvr = new System.Windows.Forms.Button();
            this.dgvNvr = new System.Windows.Forms.DataGridView();
            this.grpCounterMap = new System.Windows.Forms.GroupBox();
            this.btnAddCounterMap = new System.Windows.Forms.Button();
            this.dgvCounterMap = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.pnlBottom = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.lblConfigStatus = new System.Windows.Forms.Label();
            this.grpNvr.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNvr)).BeginInit();
            this.grpCounterMap.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCounterMap)).BeginInit();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpNvr
            // 
            this.grpNvr.Controls.Add(this.lblNvr);
            this.grpNvr.Controls.Add(this.btnAddNvr);
            this.grpNvr.Controls.Add(this.dgvNvr);
            this.grpNvr.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.grpNvr.Location = new System.Drawing.Point(9, 9);
            this.grpNvr.Name = "grpNvr";
            this.grpNvr.Size = new System.Drawing.Size(963, 239);
            this.grpNvr.TabIndex = 0;
            this.grpNvr.TabStop = false;
            // 
            // lblNvr
            // 
            this.lblNvr.AutoSize = true;
            this.lblNvr.Location = new System.Drawing.Point(6, 36);
            this.lblNvr.Name = "lblNvr";
            this.lblNvr.Size = new System.Drawing.Size(96, 25);
            this.lblNvr.TabIndex = 2;
            this.lblNvr.Text = "NVR 등록";
            // 
            // btnAddNvr
            // 
            this.btnAddNvr.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddNvr.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnAddNvr.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddNvr.Location = new System.Drawing.Point(831, 28);
            this.btnAddNvr.Name = "btnAddNvr";
            this.btnAddNvr.Size = new System.Drawing.Size(120, 40);
            this.btnAddNvr.TabIndex = 1;
            this.btnAddNvr.Text = "+ 추가";
            this.btnAddNvr.UseVisualStyleBackColor = true;
            // 
            // dgvNvr
            // 
            this.dgvNvr.AllowUserToAddRows = false;
            this.dgvNvr.AllowUserToDeleteRows = false;
            this.dgvNvr.AllowUserToResizeRows = false;
            this.dgvNvr.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvNvr.BackgroundColor = System.Drawing.Color.White;
            this.dgvNvr.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvNvr.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvNvr.Location = new System.Drawing.Point(11, 74);
            this.dgvNvr.MultiSelect = false;
            this.dgvNvr.Name = "dgvNvr";
            this.dgvNvr.ReadOnly = true;
            this.dgvNvr.RowHeadersVisible = false;
            this.dgvNvr.RowTemplate.Height = 23;
            this.dgvNvr.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvNvr.Size = new System.Drawing.Size(940, 149);
            this.dgvNvr.TabIndex = 0;
            // 
            // grpCounterMap
            // 
            this.grpCounterMap.Controls.Add(this.btnAddCounterMap);
            this.grpCounterMap.Controls.Add(this.dgvCounterMap);
            this.grpCounterMap.Controls.Add(this.label1);
            this.grpCounterMap.Location = new System.Drawing.Point(9, 266);
            this.grpCounterMap.Name = "grpCounterMap";
            this.grpCounterMap.Size = new System.Drawing.Size(963, 260);
            this.grpCounterMap.TabIndex = 1;
            this.grpCounterMap.TabStop = false;
            // 
            // btnAddCounterMap
            // 
            this.btnAddCounterMap.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddCounterMap.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnAddCounterMap.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddCounterMap.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.btnAddCounterMap.Location = new System.Drawing.Point(831, 22);
            this.btnAddCounterMap.Name = "btnAddCounterMap";
            this.btnAddCounterMap.Size = new System.Drawing.Size(120, 40);
            this.btnAddCounterMap.TabIndex = 5;
            this.btnAddCounterMap.Text = "+ 추가";
            this.btnAddCounterMap.UseVisualStyleBackColor = true;
            // 
            // dgvCounterMap
            // 
            this.dgvCounterMap.AllowUserToAddRows = false;
            this.dgvCounterMap.AllowUserToDeleteRows = false;
            this.dgvCounterMap.AllowUserToResizeRows = false;
            this.dgvCounterMap.BackgroundColor = System.Drawing.Color.White;
            this.dgvCounterMap.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dgvCounterMap.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCounterMap.Location = new System.Drawing.Point(11, 74);
            this.dgvCounterMap.MultiSelect = false;
            this.dgvCounterMap.Name = "dgvCounterMap";
            this.dgvCounterMap.ReadOnly = true;
            this.dgvCounterMap.RowHeadersVisible = false;
            this.dgvCounterMap.RowTemplate.Height = 23;
            this.dgvCounterMap.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCounterMap.Size = new System.Drawing.Size(940, 168);
            this.dgvCounterMap.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.label1.Location = new System.Drawing.Point(6, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(114, 25);
            this.label1.TabIndex = 3;
            this.label1.Text = "계산대 등록";
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.btnClose);
            this.pnlBottom.Controls.Add(this.btnSave);
            this.pnlBottom.Controls.Add(this.lblConfigStatus);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.Location = new System.Drawing.Point(0, 549);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.Size = new System.Drawing.Size(984, 64);
            this.pnlBottom.TabIndex = 2;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.btnClose.Location = new System.Drawing.Point(840, 12);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(120, 40);
            this.btnClose.TabIndex = 7;
            this.btnClose.Text = "닫기";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.Font = new System.Drawing.Font("맑은 고딕", 14F);
            this.btnSave.Location = new System.Drawing.Point(714, 12);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(120, 40);
            this.btnSave.TabIndex = 6;
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // lblConfigStatus
            // 
            this.lblConfigStatus.AutoSize = true;
            this.lblConfigStatus.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.lblConfigStatus.Location = new System.Drawing.Point(17, 37);
            this.lblConfigStatus.Name = "lblConfigStatus";
            this.lblConfigStatus.Size = new System.Drawing.Size(159, 15);
            this.lblConfigStatus.TabIndex = 4;
            this.lblConfigStatus.Text = "설정 버전: -   동기화 상태: -";
            // 
            // SettingsView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(984, 613);
            this.Controls.Add(this.pnlBottom);
            this.Controls.Add(this.grpCounterMap);
            this.Controls.Add(this.grpNvr);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "캠뷰어 설정";
            this.grpNvr.ResumeLayout(false);
            this.grpNvr.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNvr)).EndInit();
            this.grpCounterMap.ResumeLayout(false);
            this.grpCounterMap.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCounterMap)).EndInit();
            this.pnlBottom.ResumeLayout(false);
            this.pnlBottom.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpNvr;
        private System.Windows.Forms.DataGridView dgvNvr;
        private System.Windows.Forms.GroupBox grpCounterMap;
        private System.Windows.Forms.Button btnAddNvr;
        private System.Windows.Forms.Label lblNvr;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnAddCounterMap;
        private System.Windows.Forms.DataGridView dgvCounterMap;
        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblConfigStatus;
    }
}