namespace CamViewer.Views
{
    partial class LoginView
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
            this.lblStoreId = new System.Windows.Forms.Label();
            this.lblStorePassword = new System.Windows.Forms.Label();
            this.txtStoreId = new System.Windows.Forms.TextBox();
            this.txtStorePassword = new System.Windows.Forms.TextBox();
            this.lblMessage = new System.Windows.Forms.Label();
            this.btnLogin = new System.Windows.Forms.Button();
            this.pnlBottom = new System.Windows.Forms.Panel();
            this.btnExit = new System.Windows.Forms.Button();
            this.txtDeviceName = new System.Windows.Forms.TextBox();
            this.lblDeviceName = new System.Windows.Forms.Label();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblStoreId
            // 
            this.lblStoreId.AutoSize = true;
            this.lblStoreId.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblStoreId.Location = new System.Drawing.Point(25, 45);
            this.lblStoreId.Name = "lblStoreId";
            this.lblStoreId.Size = new System.Drawing.Size(65, 19);
            this.lblStoreId.TabIndex = 0;
            this.lblStoreId.Text = "매장코드";
            // 
            // lblStorePassword
            // 
            this.lblStorePassword.AutoSize = true;
            this.lblStorePassword.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblStorePassword.Location = new System.Drawing.Point(25, 85);
            this.lblStorePassword.Name = "lblStorePassword";
            this.lblStorePassword.Size = new System.Drawing.Size(65, 19);
            this.lblStorePassword.TabIndex = 0;
            this.lblStorePassword.Text = "비밀번호";
            // 
            // txtStoreId
            // 
            this.txtStoreId.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.txtStoreId.Location = new System.Drawing.Point(135, 41);
            this.txtStoreId.Name = "txtStoreId";
            this.txtStoreId.Size = new System.Drawing.Size(210, 25);
            this.txtStoreId.TabIndex = 1;
            // 
            // txtStorePassword
            // 
            this.txtStorePassword.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.txtStorePassword.Location = new System.Drawing.Point(135, 81);
            this.txtStorePassword.Name = "txtStorePassword";
            this.txtStorePassword.Size = new System.Drawing.Size(210, 25);
            this.txtStorePassword.TabIndex = 2;
            // 
            // lblMessage
            // 
            this.lblMessage.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblMessage.ForeColor = System.Drawing.Color.Red;
            this.lblMessage.Location = new System.Drawing.Point(25, 165);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(340, 30);
            this.lblMessage.TabIndex = 0;
            // 
            // btnLogin
            // 
            this.btnLogin.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogin.Location = new System.Drawing.Point(240, 12);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(75, 30);
            this.btnLogin.TabIndex = 4;
            this.btnLogin.Text = "로그인";
            this.btnLogin.UseVisualStyleBackColor = true;
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.btnExit);
            this.pnlBottom.Controls.Add(this.btnLogin);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.Location = new System.Drawing.Point(0, 245);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.Size = new System.Drawing.Size(420, 55);
            this.pnlBottom.TabIndex = 0;
            // 
            // btnExit
            // 
            this.btnExit.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.Location = new System.Drawing.Point(325, 12);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(75, 30);
            this.btnExit.TabIndex = 5;
            this.btnExit.Text = "종료";
            this.btnExit.UseVisualStyleBackColor = true;
            // 
            // txtDeviceName
            // 
            this.txtDeviceName.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.txtDeviceName.Location = new System.Drawing.Point(135, 121);
            this.txtDeviceName.Name = "txtDeviceName";
            this.txtDeviceName.Size = new System.Drawing.Size(210, 25);
            this.txtDeviceName.TabIndex = 3;
            // 
            // lblDeviceName
            // 
            this.lblDeviceName.AutoSize = true;
            this.lblDeviceName.Font = new System.Drawing.Font("맑은 고딕", 10F);
            this.lblDeviceName.Location = new System.Drawing.Point(25, 125);
            this.lblDeviceName.Name = "lblDeviceName";
            this.lblDeviceName.Size = new System.Drawing.Size(59, 19);
            this.lblDeviceName.TabIndex = 3;
            this.lblDeviceName.Text = "PC 이름";
            // 
            // LoginView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(420, 300);
            this.Controls.Add(this.txtDeviceName);
            this.Controls.Add(this.lblDeviceName);
            this.Controls.Add(this.pnlBottom);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.txtStorePassword);
            this.Controls.Add(this.txtStoreId);
            this.Controls.Add(this.lblStorePassword);
            this.Controls.Add(this.lblStoreId);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginView";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "캠뷰어 로그인";
            this.pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblStoreId;
        private System.Windows.Forms.Label lblStorePassword;
        private System.Windows.Forms.TextBox txtStoreId;
        private System.Windows.Forms.TextBox txtStorePassword;
        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.TextBox txtDeviceName;
        private System.Windows.Forms.Label lblDeviceName;
    }
}