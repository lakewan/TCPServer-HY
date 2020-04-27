using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace UDPServer
{

    public partial class UDPServerForm : Form
    {
        private static Socket udpServer;
        static EndPoint point = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 6900);
        delegate void WriteLog(string msg);
        WriteLog WriteLogDelegate;

        public UDPServerForm()
        {
            InitializeComponent();
            WriteLogDelegate = new WriteLog(AddLog);
            txtPort.Text = "6900";
            txtServerIP.Text = "192.168.1.4";
            //CheckForIllegalCrossThreadCalls = false;
        }
        /// <summary>  
        /// 用于UDP发送的网络服务类  
        /// </summary>  
        //private UdpClient udpcSend;
        /// <summary>  
        /// 用于UDP接收的网络服务类  
        /// </summary>  
        //private UdpClient udpcRecv;

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //1,创建socket
            udpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //2,绑定ip跟端口号
            try
            {
                udpServer.Bind(new IPEndPoint(IPAddress.Parse(txtServerIP.Text), Convert.ToInt32(txtPort.Text)));
                lsbRecvMsg.Items.Add(" IP:" + txtServerIP.Text + ",Port:" + txtPort.Text + "绑定成功，开始监听！");
            }
            catch (Exception err)
            {
                this.WriteLogDelegate("IP:" + txtServerIP.Text + ",Port:" + txtPort.Text + "绑定错误："+err.Message);
            }
            
        }

        public  void AddLog(string msg)
        {
            lsbRecvMsg.Items.Add(DateTime.Now.ToString() + " "+msg);
            
        }



        private void btnSendto_Click(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

       

        /// <summary>  
        /// 按钮：发送数据  
        /// </summary>  
        /// <param name="sender"></param>  
        /// <param name="e"></param>




    }
}
