using System.Windows.Forms;

namespace TCPServer
{
    partial class TCPServerForm
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
            this.chkAutoReply = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.radRecvHex = new System.Windows.Forms.RadioButton();
            this.radRecvAscii = new System.Windows.Forms.RadioButton();
            this.lsbRecvMsg = new System.Windows.Forms.ListBox();
            this.grpRecvEncode = new System.Windows.Forms.GroupBox();
            this.grpSendEncode = new System.Windows.Forms.GroupBox();
            this.radSendASC = new System.Windows.Forms.RadioButton();
            this.radSendHex = new System.Windows.Forms.RadioButton();
            this.chkCRC16 = new System.Windows.Forms.CheckBox();
            this.btnClear = new System.Windows.Forms.Button();
            this.txtDataPort = new System.Windows.Forms.TextBox();
            this.txtDataUser = new System.Windows.Forms.TextBox();
            this.txtDataPassword = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.txtDataIP = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.txtDB = new System.Windows.Forms.TextBox();
            this.txtSendMsg = new System.Windows.Forms.TextBox();
            this.lstClient = new System.Windows.Forms.ListBox();
            this.label10 = new System.Windows.Forms.Label();
            this.grpRecvEncode.SuspendLayout();
            this.grpSendEncode.SuspendLayout();
            this.groupBox1.SuspendLayout();
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
            this.btnBind.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnBind.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBind.Location = new System.Drawing.Point(354, 6);
            this.btnBind.Name = "btnBind";
            this.btnBind.Size = new System.Drawing.Size(65, 30);
            this.btnBind.TabIndex = 3;
            this.btnBind.Text = "绑定";
            this.btnBind.UseVisualStyleBackColor = false;
            this.btnBind.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnDisconnect
            // 
            this.btnDisconnect.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnDisconnect.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDisconnect.Location = new System.Drawing.Point(444, 6);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(71, 30);
            this.btnDisconnect.TabIndex = 3;
            this.btnDisconnect.Text = "断开";
            this.btnDisconnect.UseVisualStyleBackColor = false;
            this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(181, 42);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 12);
            this.label3.TabIndex = 5;
            this.label3.Text = "接受消息";
            // 
            // btnSendto
            // 
            this.btnSendto.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnSendto.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSendto.Location = new System.Drawing.Point(629, 394);
            this.btnSendto.Name = "btnSendto";
            this.btnSendto.Size = new System.Drawing.Size(66, 28);
            this.btnSendto.TabIndex = 7;
            this.btnSendto.Text = "发送";
            this.btnSendto.UseVisualStyleBackColor = false;
            this.btnSendto.Click += new System.EventHandler(this.btnSendto_Click);
            // 
            // chkAutoReply
            // 
            this.chkAutoReply.AutoSize = true;
            this.chkAutoReply.Location = new System.Drawing.Point(703, 128);
            this.chkAutoReply.Name = "chkAutoReply";
            this.chkAutoReply.Size = new System.Drawing.Size(72, 16);
            this.chkAutoReply.TabIndex = 9;
            this.chkAutoReply.Text = "自动回复";
            this.chkAutoReply.UseVisualStyleBackColor = true;
            this.chkAutoReply.CheckedChanged += new System.EventHandler(this.chkAutoReply_CheckedChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(183, 378);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 12);
            this.label4.TabIndex = 11;
            this.label4.Text = "发送消息";
            // 
            // radRecvHex
            // 
            this.radRecvHex.AutoSize = true;
            this.radRecvHex.Checked = true;
            this.radRecvHex.Location = new System.Drawing.Point(6, 18);
            this.radRecvHex.Name = "radRecvHex";
            this.radRecvHex.Size = new System.Drawing.Size(41, 16);
            this.radRecvHex.TabIndex = 12;
            this.radRecvHex.TabStop = true;
            this.radRecvHex.Text = "HEX";
            this.radRecvHex.UseVisualStyleBackColor = true;
            this.radRecvHex.CheckedChanged += new System.EventHandler(this.radRecvHex_CheckedChanged);
            // 
            // radRecvAscii
            // 
            this.radRecvAscii.AutoSize = true;
            this.radRecvAscii.Location = new System.Drawing.Point(6, 39);
            this.radRecvAscii.Name = "radRecvAscii";
            this.radRecvAscii.Size = new System.Drawing.Size(53, 16);
            this.radRecvAscii.TabIndex = 12;
            this.radRecvAscii.Text = "ASCII";
            this.radRecvAscii.UseVisualStyleBackColor = true;
            this.radRecvAscii.CheckedChanged += new System.EventHandler(this.radRecvAscii_CheckedChanged);
            // 
            // lsbRecvMsg
            // 
            this.lsbRecvMsg.FormattingEnabled = true;
            this.lsbRecvMsg.ItemHeight = 12;
            this.lsbRecvMsg.Location = new System.Drawing.Point(183, 61);
            this.lsbRecvMsg.Name = "lsbRecvMsg";
            this.lsbRecvMsg.Size = new System.Drawing.Size(510, 304);
            this.lsbRecvMsg.TabIndex = 16;
            this.lsbRecvMsg.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.lsbRecvMsg_MouseDoubleClick);
            // 
            // grpRecvEncode
            // 
            this.grpRecvEncode.Controls.Add(this.radRecvAscii);
            this.grpRecvEncode.Controls.Add(this.radRecvHex);
            this.grpRecvEncode.Location = new System.Drawing.Point(701, 61);
            this.grpRecvEncode.Name = "grpRecvEncode";
            this.grpRecvEncode.Size = new System.Drawing.Size(75, 61);
            this.grpRecvEncode.TabIndex = 19;
            this.grpRecvEncode.TabStop = false;
            this.grpRecvEncode.Text = "数据类型";
            // 
            // grpSendEncode
            // 
            this.grpSendEncode.Controls.Add(this.radSendASC);
            this.grpSendEncode.Controls.Add(this.radSendHex);
            this.grpSendEncode.Location = new System.Drawing.Point(701, 394);
            this.grpSendEncode.Name = "grpSendEncode";
            this.grpSendEncode.Size = new System.Drawing.Size(70, 63);
            this.grpSendEncode.TabIndex = 19;
            this.grpSendEncode.TabStop = false;
            this.grpSendEncode.Text = "数据类型";
            // 
            // radSendASC
            // 
            this.radSendASC.AutoSize = true;
            this.radSendASC.Location = new System.Drawing.Point(6, 38);
            this.radSendASC.Name = "radSendASC";
            this.radSendASC.Size = new System.Drawing.Size(53, 16);
            this.radSendASC.TabIndex = 12;
            this.radSendASC.Text = "ASCII";
            this.radSendASC.UseVisualStyleBackColor = true;
            this.radSendASC.CheckedChanged += new System.EventHandler(this.radSendASC_CheckedChanged);
            // 
            // radSendHex
            // 
            this.radSendHex.AutoSize = true;
            this.radSendHex.Checked = true;
            this.radSendHex.Location = new System.Drawing.Point(6, 18);
            this.radSendHex.Name = "radSendHex";
            this.radSendHex.Size = new System.Drawing.Size(41, 16);
            this.radSendHex.TabIndex = 12;
            this.radSendHex.TabStop = true;
            this.radSendHex.Text = "HEX";
            this.radSendHex.UseVisualStyleBackColor = true;
            this.radSendHex.CheckedChanged += new System.EventHandler(this.radSendHex_CheckedChanged);
            // 
            // chkCRC16
            // 
            this.chkCRC16.AutoSize = true;
            this.chkCRC16.Location = new System.Drawing.Point(631, 432);
            this.chkCRC16.Name = "chkCRC16";
            this.chkCRC16.Size = new System.Drawing.Size(54, 16);
            this.chkCRC16.TabIndex = 20;
            this.chkCRC16.Text = "CRC16";
            this.chkCRC16.UseVisualStyleBackColor = true;
            this.chkCRC16.CheckedChanged += new System.EventHandler(this.chkCRC16_CheckedChanged);
            // 
            // btnClear
            // 
            this.btnClear.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnClear.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClear.Location = new System.Drawing.Point(535, 6);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(71, 30);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "清除";
            this.btnClear.UseVisualStyleBackColor = false;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // txtDataPort
            // 
            this.txtDataPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtDataPort.Location = new System.Drawing.Point(702, 233);
            this.txtDataPort.Name = "txtDataPort";
            this.txtDataPort.Size = new System.Drawing.Size(73, 21);
            this.txtDataPort.TabIndex = 2;
            this.txtDataPort.TextChanged += new System.EventHandler(this.txtDataPort_TextChanged);
            // 
            // txtDataUser
            // 
            this.txtDataUser.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtDataUser.Location = new System.Drawing.Point(702, 274);
            this.txtDataUser.Name = "txtDataUser";
            this.txtDataUser.Size = new System.Drawing.Size(75, 21);
            this.txtDataUser.TabIndex = 2;
            this.txtDataUser.TextChanged += new System.EventHandler(this.txtDataUser_TextChanged);
            // 
            // txtDataPassword
            // 
            this.txtDataPassword.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtDataPassword.Location = new System.Drawing.Point(702, 315);
            this.txtDataPassword.Name = "txtDataPassword";
            this.txtDataPassword.Size = new System.Drawing.Size(75, 21);
            this.txtDataPassword.TabIndex = 2;
            this.txtDataPassword.TextChanged += new System.EventHandler(this.txtDataPassword_TextChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(702, 217);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(65, 12);
            this.label5.TabIndex = 21;
            this.label5.Text = "数据库端口";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(702, 258);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(65, 12);
            this.label6.TabIndex = 21;
            this.label6.Text = "数据库用户";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(702, 299);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(65, 12);
            this.label7.TabIndex = 21;
            this.label7.Text = "数据库密码";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(702, 176);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(65, 12);
            this.label8.TabIndex = 21;
            this.label8.Text = "数据库地址";
            // 
            // txtDataIP
            // 
            this.txtDataIP.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtDataIP.Location = new System.Drawing.Point(702, 192);
            this.txtDataIP.Name = "txtDataIP";
            this.txtDataIP.Size = new System.Drawing.Size(73, 21);
            this.txtDataIP.TabIndex = 2;
            this.txtDataIP.TextChanged += new System.EventHandler(this.txtDataPort_TextChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.txtDB);
            this.groupBox1.Location = new System.Drawing.Point(696, 155);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(81, 233);
            this.groupBox1.TabIndex = 22;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "服务器设置";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(6, 184);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(41, 12);
            this.label9.TabIndex = 21;
            this.label9.Text = "数据库";
            // 
            // txtDB
            // 
            this.txtDB.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtDB.Location = new System.Drawing.Point(6, 199);
            this.txtDB.Name = "txtDB";
            this.txtDB.Size = new System.Drawing.Size(75, 21);
            this.txtDB.TabIndex = 2;
            this.txtDB.TextChanged += new System.EventHandler(this.txtDataPassword_TextChanged);
            // 
            // txtSendMsg
            // 
            this.txtSendMsg.AllowDrop = true;
            this.txtSendMsg.Location = new System.Drawing.Point(183, 400);
            this.txtSendMsg.Multiline = true;
            this.txtSendMsg.Name = "txtSendMsg";
            this.txtSendMsg.Size = new System.Drawing.Size(440, 48);
            this.txtSendMsg.TabIndex = 23;
            // 
            // lstClient
            // 
            this.lstClient.FormattingEnabled = true;
            this.lstClient.ItemHeight = 12;
            this.lstClient.Location = new System.Drawing.Point(12, 61);
            this.lstClient.MultiColumn = true;
            this.lstClient.Name = "lstClient";
            this.lstClient.Size = new System.Drawing.Size(150, 388);
            this.lstClient.TabIndex = 24;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label10.Location = new System.Drawing.Point(12, 42);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(83, 12);
            this.label10.TabIndex = 5;
            this.label10.Text = "已连接客户端";
            // 
            // TCPServerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(790, 468);
            this.Controls.Add(this.lstClient);
            this.Controls.Add(this.txtSendMsg);
            this.Controls.Add(this.txtDataPassword);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.txtDataUser);
            this.Controls.Add(this.txtDataIP);
            this.Controls.Add(this.txtDataPort);
            this.Controls.Add(this.chkCRC16);
            this.Controls.Add(this.grpSendEncode);
            this.Controls.Add(this.grpRecvEncode);
            this.Controls.Add(this.lsbRecvMsg);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.chkAutoReply);
            this.Controls.Add(this.btnSendto);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btnDisconnect);
            this.Controls.Add(this.btnBind);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtServerIP);
            this.Controls.Add(this.groupBox1);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.KeyPreview = true;
            this.Name = "TCPServerForm";
            this.Text = "UDP服务器";
            this.grpRecvEncode.ResumeLayout(false);
            this.grpRecvEncode.PerformLayout();
            this.grpSendEncode.ResumeLayout(false);
            this.grpSendEncode.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
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
        private System.Windows.Forms.CheckBox chkAutoReply;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RadioButton radRecvHex;
        private System.Windows.Forms.RadioButton radRecvAscii;
        private System.Windows.Forms.ListBox lsbRecvMsg;
        private System.Windows.Forms.GroupBox grpRecvEncode;
        private System.Windows.Forms.GroupBox grpSendEncode;
        private System.Windows.Forms.RadioButton radSendASC;
        private System.Windows.Forms.RadioButton radSendHex;
        private System.Windows.Forms.CheckBox chkCRC16;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.TextBox txtDataPort;
        private System.Windows.Forms.TextBox txtDataUser;
        private System.Windows.Forms.TextBox txtDataPassword;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox txtDataIP;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtSendMsg;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox txtDB;
        private System.Windows.Forms.ListBox lstClient;
        private System.Windows.Forms.Label label10;
    }
}

