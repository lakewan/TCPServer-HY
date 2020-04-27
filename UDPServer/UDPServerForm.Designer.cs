namespace UDPServer
{
    partial class UDPServerForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.txtServerIP = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.btnBind = new System.Windows.Forms.Button();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSendto = new System.Windows.Forms.Button();
            this.chkFeedback = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.radHex = new System.Windows.Forms.RadioButton();
            this.radAscii = new System.Windows.Forms.RadioButton();
            this.combClient = new System.Windows.Forms.ComboBox();
            this.chxCrc16 = new System.Windows.Forms.CheckBox();
            this.lsbSendMsg = new System.Windows.Forms.ListBox();
            this.lsbRecvMsg = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // txtServerIP
            // 
            this.txtServerIP.Location = new System.Drawing.Point(86, 12);
            this.txtServerIP.Name = "txtServerIP";
            this.txtServerIP.Size = new System.Drawing.Size(114, 21);
            this.txtServerIP.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(27, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "服务器IP:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(206, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 12);
            this.label2.TabIndex = 1;
            this.label2.Text = "端口号：";
            // 
            // txtPort
            // 
            this.txtPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtPort.Location = new System.Drawing.Point(254, 12);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(79, 21);
            this.txtPort.TabIndex = 2;
            // 
            // btnBind
            // 
            this.btnBind.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBind.Location = new System.Drawing.Point(353, 10);
            this.btnBind.Name = "btnBind";
            this.btnBind.Size = new System.Drawing.Size(75, 23);
            this.btnBind.TabIndex = 3;
            this.btnBind.Text = "绑定";
            this.btnBind.UseVisualStyleBackColor = true;
            this.btnBind.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnDisconnect
            // 
            this.btnDisconnect.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDisconnect.Location = new System.Drawing.Point(444, 10);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(75, 23);
            this.btnDisconnect.TabIndex = 3;
            this.btnDisconnect.Text = "断开";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(29, 57);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 12);
            this.label3.TabIndex = 5;
            this.label3.Text = "接受消息";
            // 
            // btnSendto
            // 
            this.btnSendto.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSendto.Location = new System.Drawing.Point(369, 378);
            this.btnSendto.Name = "btnSendto";
            this.btnSendto.Size = new System.Drawing.Size(75, 23);
            this.btnSendto.TabIndex = 7;
            this.btnSendto.Text = "发送";
            this.btnSendto.UseVisualStyleBackColor = true;
            this.btnSendto.Click += new System.EventHandler(this.btnSendto_Click);
            // 
            // chkFeedback
            // 
            this.chkFeedback.AutoSize = true;
            this.chkFeedback.Location = new System.Drawing.Point(444, 55);
            this.chkFeedback.Name = "chkFeedback";
            this.chkFeedback.Size = new System.Drawing.Size(72, 16);
            this.chkFeedback.TabIndex = 9;
            this.chkFeedback.Text = "自动回复";
            this.chkFeedback.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(29, 354);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 12);
            this.label4.TabIndex = 11;
            this.label4.Text = "发送消息";
            // 
            // radHex
            // 
            this.radHex.AutoSize = true;
            this.radHex.Location = new System.Drawing.Point(86, 55);
            this.radHex.Name = "radHex";
            this.radHex.Size = new System.Drawing.Size(41, 16);
            this.radHex.TabIndex = 12;
            this.radHex.TabStop = true;
            this.radHex.Text = "HEX";
            this.radHex.UseVisualStyleBackColor = true;
            // 
            // radAscii
            // 
            this.radAscii.AutoSize = true;
            this.radAscii.Location = new System.Drawing.Point(133, 55);
            this.radAscii.Name = "radAscii";
            this.radAscii.Size = new System.Drawing.Size(53, 16);
            this.radAscii.TabIndex = 12;
            this.radAscii.TabStop = true;
            this.radAscii.Text = "Ascii";
            this.radAscii.UseVisualStyleBackColor = true;
            // 
            // combClient
            // 
            this.combClient.FormattingEnabled = true;
            this.combClient.Location = new System.Drawing.Point(365, 436);
            this.combClient.Name = "combClient";
            this.combClient.Size = new System.Drawing.Size(151, 20);
            this.combClient.TabIndex = 14;
            // 
            // chxCrc16
            // 
            this.chxCrc16.AutoSize = true;
            this.chxCrc16.Location = new System.Drawing.Point(450, 382);
            this.chxCrc16.Name = "chxCrc16";
            this.chxCrc16.Size = new System.Drawing.Size(54, 16);
            this.chxCrc16.TabIndex = 9;
            this.chxCrc16.Text = "CRC16";
            this.chxCrc16.UseVisualStyleBackColor = true;
            // 
            // lsbSendMsg
            // 
            this.lsbSendMsg.FormattingEnabled = true;
            this.lsbSendMsg.ItemHeight = 12;
            this.lsbSendMsg.Location = new System.Drawing.Point(29, 369);
            this.lsbSendMsg.Name = "lsbSendMsg";
            this.lsbSendMsg.Size = new System.Drawing.Size(318, 88);
            this.lsbSendMsg.TabIndex = 15;
            // 
            // lsbRecvMsg
            // 
            this.lsbRecvMsg.FormattingEnabled = true;
            this.lsbRecvMsg.ItemHeight = 12;
            this.lsbRecvMsg.Location = new System.Drawing.Point(31, 73);
            this.lsbRecvMsg.Name = "lsbRecvMsg";
            this.lsbRecvMsg.Size = new System.Drawing.Size(485, 268);
            this.lsbRecvMsg.TabIndex = 16;
            // 
            // UDPServerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(531, 468);
            this.Controls.Add(this.lsbRecvMsg);
            this.Controls.Add(this.lsbSendMsg);
            this.Controls.Add(this.combClient);
            this.Controls.Add(this.radAscii);
            this.Controls.Add(this.radHex);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.chxCrc16);
            this.Controls.Add(this.chkFeedback);
            this.Controls.Add(this.btnSendto);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnDisconnect);
            this.Controls.Add(this.btnBind);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtServerIP);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.KeyPreview = true;
            this.Name = "UDPServerForm";
            this.Text = "UDP服务器";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtServerIP;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Button btnBind;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnSendto;
        private System.Windows.Forms.CheckBox chkFeedback;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RadioButton radHex;
        private System.Windows.Forms.RadioButton radAscii;
        private System.Windows.Forms.ComboBox combClient;
        private System.Windows.Forms.CheckBox chxCrc16;
        private System.Windows.Forms.ListBox lsbSendMsg;
        private System.Windows.Forms.ListBox lsbRecvMsg;
    }
}

