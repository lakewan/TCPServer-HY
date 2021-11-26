using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

using dayLogFiles;
using DataFormat;
using MySql.Data.MySqlClient;
using ToolsLibrary;
using System.Data;
using System.Timers;
using FluentScheduler;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace NewSockServer
{
    public delegate void ShowMessageDelegate(string msg);
    public delegate void addClientDelegate(string clientsn);
    public delegate void removeClientDelegate(string clientsn);

    public class AsyncTCPServer : IDisposable
    {
        #region Fields
        /// <summary>
        /// 服务器程序允许的最大客户端连接数
        /// </summary>
        private int _maxClient;
        /// <summary>
        /// 当前的连接的客户端数
        /// </summary>
        //private int _clientCount;
        /// <summary>
        /// 服务器使用的异步TcpListener
        /// </summary>
        private TcpListener _listener;
        /// <summary>
        /// 客户端会话列表
        /// </summary>
       #endregion
        private List<TCPClientState> _clients;
        private List<CommDevice> _commList;
        private List<ControlDevice> _controlList;
        private List<SensorDevice> _sensorList;
        private int _maxCommID;
        private int _maxSfjID;
        private int _maxControlID;
        private int _maxSfjControlID;
        private int _maxSensorID;
        private int _pendingTimes;
        private int _pendingTimeout;
        //private int _releaseInterval;
        private string _connectStr;
        private bool disposed = false;
        private int _collect_time;
        private int _get_state_time;
        private int _get_command_time;
        private string _communicationtype;
        private int _linked_timeout;

        //MQTT对接信息
        private string mqtt_host;
        private string mqtt_port;
        private MqttClient mqtt_client;
        //瀚云平台对接信息
        private string hy_mqtt_host;
        private string hy_mqtt_port;
        private MqttClient hy_mqtt_client;
        private MqttClient hy_sfj_client;
        private string hyproductkey;
        private string hyaccesskey;
        private string hyaccesssecret;
        private string hydevicesn;
        private string hydevicekey;
        private string hysfjkey;
        private string hysfjsn;
        private List<HY_CommInfo> _hyCommList;

        //云飞对接信息
        private string yf_pestid;
        private string yf_sporeid;
        private string yfserverapi;
        private string photoaddr;
        private HttpListener httpListen;

        private string sfjcomm;

        // Events
        public event addClientDelegate addClientEvent;
        public event removeClientDelegate removeClientEvent;
        public event ShowMessageDelegate ShowMsgEvent;

        //public List<string> _linkedList = new List<string>();
        //public List<string> _forbidedList = new List<string>();
        //private ConcurrentDictionary<string, int> _PendingDict = new ConcurrentDictionary<string, int>();
        //private ConcurrentDictionary<string, TCPClientState> _iptoStateDict = new ConcurrentDictionary<string, TCPClientState>();
        //private ConcurrentDictionary<string, string> _commtoipportDict = new ConcurrentDictionary<string, string>();

        #region Properties
        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 监听的IP地址
        /// </summary>
        public IPAddress Address { get; private set; }
        /// <summary>
        /// 监听的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 通信使用的编码
        /// </summary>
        public Encoding Encoding { get; set; }

        #endregion

        #region 构造函数


        ///// <summary>
        ///// 异步TCP服务器
        ///// </summary>
        ///// <param name="listenPort">监听的端口</param>
        //public AsyncTCPServer(int listenPort) :this(IPAddress.Any, listenPort)
        //{

        //}

        ///// <summary>
        ///// 异步TCP服务器
        ///// </summary>
        ///// <param name="localEP">监听的终结点</param>
        //public AsyncTCPServer(IPEndPoint localEP) :this(localEP.Address, localEP.Port)
        //{

        //}

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localIPAddress">监听的IP地址</param>
        /// <param name="listenPort">监听的端口</param>
        public AsyncTCPServer()
        {
            try
            {
                string currPath = Directory.GetCurrentDirectory();
                IniFile iniFile = new IniFile(currPath + "\\config.ini");
                string dbserver = iniFile.IniReadValue("Database", "server");
                string dbport = iniFile.IniReadValue("Database", "port");
                string dbdatabase = iniFile.IniReadValue("Database", "database");
                string dbuser = iniFile.IniReadValue("Database", "user");
                string dbpassword = iniFile.IniReadValue("Database", "password");
                string dbcharset = iniFile.IniReadValue("Database", "charset");
                _communicationtype = iniFile.IniReadValue("Settings", "commnicationtype");
                _pendingTimes = Convert.ToInt32(iniFile.IniReadValue("Settings", "pending_times"));
                _pendingTimeout = Convert.ToInt32(iniFile.IniReadValue("Settings", "pending_timeout"));
                //_releaseInterval = Convert.ToInt32(iniFile.IniReadValue("Settings", "realive_interval"));
                _collect_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "collect_time")) * 1000;
                _get_state_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "get_state_time")) * 1000;
                _get_command_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "get_command_time")) * 1000;
                _linked_timeout = Convert.ToInt32(iniFile.IniReadValue("Settings", "linked_overtime")) * 1000;
                mqtt_host = iniFile.IniReadValue("MQTT", "mqtt_host");
                mqtt_port = iniFile.IniReadValue("MQTT", "mqtt_port");

                yf_pestid = iniFile.IniReadValue("PREST", "yfpestid");
                yf_sporeid = iniFile.IniReadValue("PREST", "yfsporeid");
                photoaddr = iniFile.IniReadValue("PREST", "photoaddr");
                yfserverapi = iniFile.IniReadValue("PREST", "apiserver");
                hy_mqtt_host = iniFile.IniReadValue("HY_MQTT", "hy_mqtt_host");
                hy_mqtt_port = iniFile.IniReadValue("HY_MQTT", "hy_mqtt_port");
                hyproductkey = iniFile.IniReadValue("HY_MQTT", "productkey");
                hyaccesskey = iniFile.IniReadValue("HY_MQTT", "accesskey");
                hyaccesssecret = iniFile.IniReadValue("HY_MQTT", "accesssecret");
                hydevicesn = iniFile.IniReadValue("HY_MQTT", "devicesn");
                hydevicekey = iniFile.IniReadValue("HY_MQTT", "devicekey");
                hysfjsn = iniFile.IniReadValue("HY_MQTT", "sfjsn");
                hysfjkey = iniFile.IniReadValue("HY_MQTT", "sfjkey");
                sfjcomm = iniFile.IniReadValue("HY_MQTT", "sfjcomm");


                Address = IPAddress.Parse(iniFile.IniReadValue("Listen", "serverip"));
                Port = Int32.Parse(iniFile.IniReadValue("Listen", "port"));
                Encoding = Encoding.Default;
                _connectStr = "server='" + dbserver + "'" + ";port= '" + dbport + "'" + ";user='" + dbuser + "'" + ";password='" + dbpassword + "'" + ";database= '" + dbdatabase + "'";
                _maxCommID = 0;
                _maxSfjID = 0;
                _maxControlID = 0;
                _maxSensorID = 0;
                _maxSfjControlID = 0;
                _commList = new List<CommDevice>();
                _controlList = new List<ControlDevice>();
                _sensorList = new List<SensorDevice>();
                _hyCommList = new List<HY_CommInfo>();
                _maxClient = 100000;
                _clients = new List<TCPClientState>();
                _listener = new TcpListener(Address, Port);
                _listener.AllowNatTraversal(true);
                IsRunning = false;

            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 初始化服务器(" + Port + ")失败" + err.Message;
                EveryDayLog.Write(msg);
            }

        }

        #endregion

        #region Method

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start()
        {
            if (!IsRunning)
            {
                try
                {
                    IsRunning = true;
                    string msg = null;
                    if (mqtt_host.Length > 0 && mqtt_port.Length > 0)
                    {
                        MQTT_ini(mqtt_host, mqtt_port);
                    }


                    //初始化设备
                    Thread iniDevThrd = new Thread(initDev_Thrd);
                    iniDevThrd.IsBackground = true;
                    iniDevThrd.Start();

                    Thread getCmdThrd = new Thread(getCommand_Thrd);
                    getCmdThrd.IsBackground = true;
                    getCmdThrd.Start();

                    Thread linkedOverTime = new Thread(overtimeClient_Thrd);
                    linkedOverTime.IsBackground = true;
                    linkedOverTime.Start();


                    _listener.Start();
                    _listener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), _listener);

                    Thread initHttpThrd = new Thread(new ThreadStart(InitHttp));
                    initHttpThrd.IsBackground = true;
                    initHttpThrd.Start();
                    InitHYPlat();
                    HY_Connect();

                    //Thread hyReconnectThrd = new Thread(new ThreadStart(HY_Reconnect));
                    //hyReconnectThrd.IsBackground = true;
                    //hyReconnectThrd.Start();

                    //事件处理
                    ClientConnected += new EventHandler<AsyncEventArgs>(onClientConnected);
                    DataReceived += new EventHandler<AsyncEventArgs>(onDataReceived);

                    msg = " 服务器(" + Address + ":" + Port + ")开始监听";
                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                }
                catch (Exception err)
                {
                    string msg = DateTime.Now.ToString() + " 服务器(" + Address + ":" + Port + ")监听失败：" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }

            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;

            }
            if (mqtt_client != null)
            {
                mqtt_client.Disconnect();
            }
            if (hy_mqtt_client != null)
            {
                hy_mqtt_client.Disconnect();
            }
            if ((httpListen != null) && httpListen.IsListening)
            {
                httpListen.Close();
            }
            _listener.Stop();
            string msg = " 停止监听";
            ShowMsgEvent(DateTime.Now.ToString() + msg);
            EveryDayLog.Write(DateTime.Now.ToString() + msg);
            lock (_clients)
            {
                //关闭所有客户端连接
                CloseAllClient();
            }
        }

        /// <summary>
        /// 处理客户端连接的函数
        /// </summary>
        /// <param name="ar"></param>
        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (IsRunning)
            {
                //TcpListener tcpListener = (TcpListener)ar.AsyncState;

                TcpClient client = _listener.EndAcceptTcpClient(ar);
                byte[] buffer = new byte[client.ReceiveBufferSize];
                TCPClientState state = new TCPClientState(client, buffer)
                {
                    clientStatus = 1,
                    lastTime = DateTime.Now.ToString(),
                    faildTimes = 0,
                    clientComm = null
                };
                RaiseClientConnected(state);
                try
                {
                    NetworkStream stream = state.NetworkStream;
                    //开始异步读取数据
                    stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceived, state);
                    _listener.BeginAcceptTcpClient(
                      new AsyncCallback(HandleTcpClientAccepted), ar.AsyncState);
                }
                catch (Exception err)
                {
                    string msg = null;
                    msg = " 启动或读取(";
                    if (Address != null)
                    {
                        msg += Address.ToString();
                    }
                    msg = " 启动或读取(" + Port.ToString() + ")数据失败：" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
        }

        /// <summary>
        /// 数据接受回调函数
        /// </summary>
        /// <param name="ar"></param>
        private void HandleDataReceived(IAsyncResult ar)
        {
            if (IsRunning)
            {
                TCPClientState state = (TCPClientState)ar.AsyncState;
                if ((state.clientStatus > 2) || !state.TcpClient.Connected)
                {
                    string msg = DateTime.Now.ToString() + " 通讯客户端()处于断开或关闭状态,不再继续处理数据";
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);

                }
                else
                {
                    NetworkStream stream = state.NetworkStream;
                    int recv = 0;
                    try
                    {
                        recv = stream.EndRead(ar);
                    }
                    catch
                    {
                        recv = 0;
                    }
                    if (recv < 1)
                    {
                        state.clientStatus = 3;
                        return;
                    }
                    // received byte and trigger event notification
                    byte[] buff = new byte[recv];
                    Buffer.BlockCopy(state.Buffer, 0, buff, 0, recv);
                    //触发数据收到事件
                    AsyncEventArgs argstate = new AsyncEventArgs(BitConverter.ToString(buff), state);
                    argstate._msg = FormatFunc.byteToHexStr(buff);
                    RaiseDataReceived(argstate);

                    // continue listening for tcp datagram packets
                    stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceived, state);
                }
            }
        }

        /// <summary>
        /// 处理http收到的数据
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns>0-成功，1-有topic单IMEI为空，3-虫情设备下线，4-虫情设备CMD错误
        /// 6-虫情插入新数据错误，7-虫情更新状态错误
        /// 
        /// 
        /// </returns>
        private string HandleRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            {
                string sRecv = null;
                string msg = null;
                try
                {
                    string sSensorSQL;
                    string sCommMemo;
                    string sCommSQL;
                    string sHYSendStr2;
                    string sHYSendStr4;
                    string sIMEI = null;
                    string sImageURL = null;
                    string sResultImage = null;
                    string sResult = null;
                    string sDeviceID;
                    string sFilePath;
                    string sResultFilePath = null;
                    HY_CommInfo pestComm;
                    HY_CommInfo sporeComm;
                    List<byte> list = new List<byte>();
                    byte[] buffer = new byte[4096];
                    int len = 0;
                    int readLen = 0;
                    do
                    {
                        readLen = request.InputStream.Read(buffer, 0, buffer.Length);
                        len += readLen;
                        list.AddRange(buffer);
                    } while (readLen != 0);
                    if (len != 0)
                    {
                        string sReturn;
                        sRecv = Encoding.UTF8.GetString(list.ToArray(), 0, len);
                        JObject jObj = (JObject)JsonConvert.DeserializeObject(sRecv);
                        ShowMsgEvent(DateTime.Now.ToString() + " 接收数据:" + sRecv);
                        EveryDayLog.Write(DateTime.Now.ToString() + " 接收数据:" + sRecv);
                        string sTopic = null;
                        string sCmd = null;
                        try
                        {
                            try
                            {
                                sTopic = jObj["topic"].ToString();
                            }
                            catch
                            {

                            }

                            if ((sTopic != null) && (jObj["payload"]["cmd"] != null))
                            {
                                sCmd = jObj["payload"]["cmd"].ToString();
                                if (sTopic.Contains("/yfkj/cbd/pub/"))
                                {
                                    sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (device_id,Device_Code,reporttime,ReportValue,Block_ID) VALUES  ";
                                    sCommSQL = "UPDATE yw_d_commnication_tbl SET MEMO = ";
                                    if (jObj["payload"]["ext"]["imei"] == null)
                                    {
                                        ShowMsgEvent(DateTime.Now.ToString() + " 处理http数据发生错误，IMEI为空");
                                        EveryDayLog.Write(DateTime.Now.ToString() + " 处理http数据发生错误，IMEI为空");
                                        return "1";
                                    }
                                    sIMEI = jObj["payload"]["ext"]["imei"].ToString();
                                    if (getCommbySN(yf_pestid, 1) != null)
                                    {
                                        sCommMemo = null;
                                        sHYSendStr2 = null;
                                        sHYSendStr4 = null;
                                        if (sCmd.Equals("data"))
                                        {
                                            YF_Pest_Data pestData = new YF_Pest_Data
                                            {
                                                ah = (Convert.ToInt32(jObj["payload"]["ext"]["ah"].ToString()) * 0.1).ToString()
                                            };
                                            sSensorSQL = sSensorSQL + "(22,'YFC01I16-02',now(),'" + pestData.ah + "',1),";
                                            pestData.at = (Convert.ToInt32(jObj["payload"]["ext"]["at"].ToString()) * 0.1).ToString();
                                            sSensorSQL = sSensorSQL + "(21,'YFC01I16-01',now(),'" + pestData.at + "',1),";
                                            pestData.hrt = jObj["payload"]["ext"]["hrt"].ToString();
                                            sSensorSQL = sSensorSQL + "(23,'YFC01I16-03',now(),'" + pestData.hrt + "',1),";
                                            pestData.lux = jObj["payload"]["ext"]["lux"].ToString();
                                            sSensorSQL = sSensorSQL + "(24,'YFC01I16-04',now(),'" + pestData.lux + "',1),";
                                            pestData.vbat = jObj["payload"]["ext"]["vbat"].ToString();
                                            sSensorSQL = sSensorSQL + "(25,'YFC01I16-05',now(),'" + pestData.vbat + "',1),";
                                            sHYSendStr2 = ((((sHYSendStr2 + "\"RH\":" + pestData.ah + ",") + "\"Temp\":" + pestData.at + ",") + "\"Temp@2\":" + pestData.hrt + ",") + "\"lightIntensity\":" + pestData.lux + ",") + "\"voltage\":" + pestData.vbat + ",";
                                            sHYSendStr4 = ((sHYSendStr4 + "\"RH\":{\"name\":\"环境湿度\"},") + "\"Temp\":{\"name\":\"环境温度\"}," + "\"Temp@2\":{\"name\":\"加热仓温度\"},") + "\"lightIntensity\":{\"name\":\"光照强度\"}," + "\"voltage\":{\"name\":\"电池电压\"},";
                                            pestData.batStatus = jObj["payload"]["ext"]["batStatus"].ToString();
                                            if (pestData.batStatus.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "电池状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"bat_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"bat_stat\":{\"name\":\"电池状态\"},";
                                            }
                                            else if (pestData.batStatus.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "电池状态:欠压;";
                                                sHYSendStr2 = sHYSendStr2 + "\"bat_stat\":\"欠压\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"bat_stat\":{\"name\":\"电池状态\"},";
                                            }
                                            pestData.imei = jObj["payload"]["ext"]["imei"].ToString();
                                            pestData.lamp = jObj["payload"]["ext"]["lamp"].ToString();
                                            if (pestData.lamp.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "灯管状态:灭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"灯管状态\"},";
                                            }
                                            else if (pestData.lamp.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "灯管状态:亮;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"灯管状态\"},";
                                            }
                                            pestData.lat = jObj["payload"]["ext"]["lat"].ToString();
                                            sCommMemo = sCommMemo + "经度:" + pestData.lat + ";";
                                            pestData.lng = jObj["payload"]["ext"]["lng"].ToString();
                                            sCommMemo = sCommMemo + "纬度:" + pestData.lng + ";";
                                            pestData.lps = jObj["payload"]["ext"]["lps"].ToString();
                                            if (pestData.lps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "光控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"light_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"light_stat\":{\"name\":\"光控状态\"},";
                                            }
                                            else if (pestData.lps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "光控状态:光控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"light_stat\":\"光控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"light_stat\":{\"name\":\"光控状态\"},";
                                            }
                                            pestData.rps = jObj["payload"]["ext"]["rps"].ToString();
                                            if (pestData.rps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            else if (pestData.rps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:雨控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"雨控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            pestData.stamp = jObj["payload"]["ext"]["stamp"].ToString();
                                            pestData.tps = jObj["payload"]["ext"]["tps"].ToString();
                                            if (pestData.tps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "温控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"temp_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"temp_stat\":{\"name\":\"温控状态\"},";
                                            }
                                            else if (pestData.tps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "温控状态:温控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"temp_stat\":\"温控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"temp_stat\":{\"name\":\"温控状态\"},";
                                            }
                                            pestData.ws = jObj["payload"]["ext"]["ws"].ToString();
                                            if (pestData.ws.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:待机;";
                                                sHYSendStr2 = sHYSendStr2 + "\"running\":\"idle\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"running\":{\"name\":\"工作状态\"},";
                                            }
                                            else if (pestData.ws.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:工作;";
                                                sHYSendStr2 = sHYSendStr2 + "\"running\":\"busy\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"running\":{\"name\":\"工作状态\"},";
                                            }

                                        }
                                        else if (sCmd.Equals("status"))
                                        {
                                            YF_Pest_Status pestStatus = new YF_Pest_Status();
                                            pestStatus.csq = jObj["payload"]["ext"]["csq"].ToString();
                                            sSensorSQL = sSensorSQL + "(26,'YFC01I16-06',now(),'" + pestStatus.csq + "',1),";
                                            sHYSendStr2 = sHYSendStr2 + "\"csq\":" + pestStatus.csq + ",";
                                            sHYSendStr4 = sHYSendStr4 + "\"csq\":{\"name\":\"信号强度\"},";
                                            pestStatus.dnds = jObj["payload"]["ext"]["dnds"].ToString();
                                            if (pestStatus.dnds.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "下仓门:待机;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"下仓门状态\"},";
                                            }
                                            else if (pestStatus.dnds.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "下仓门:工作;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"下仓门状态\"},";
                                            }
                                            pestStatus.gs = jObj["payload"]["ext"]["gs"].ToString();
                                            if (pestStatus.gs.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "通道状态:排水;";
                                                sHYSendStr2 = sHYSendStr2 + "\"pass_stat\":\"排水\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"pass_stat\":{\"name\":\"通道状态\"},";
                                            }
                                            else if (pestStatus.gs.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "通道状态:落虫;";
                                                sHYSendStr2 = sHYSendStr2 + "\"pass_stat\":\"落虫\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"pass_stat\":{\"name\":\"通道状态\"},";
                                            }
                                            pestStatus.hs = jObj["payload"]["ext"]["hs"].ToString();
                                            if (pestStatus.hs.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "加热状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"heat_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"heat_stat\":{\"name\":\"加热状态\"},";
                                            }
                                            else if (pestStatus.hs.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "加热状态:加热;";
                                                sHYSendStr2 = sHYSendStr2 + "\"heat_stat\":\"加热\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"heat_stat\":{\"name\":\"加热状态\"},";
                                            }
                                            pestStatus.imei = jObj["payload"]["ext"]["imei"].ToString();
                                            pestStatus.lamp = jObj["payload"]["ext"]["lamp"].ToString();
                                            if (pestStatus.lamp.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "灯管状态:灭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@2\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@2\":{\"name\":\"灯管状态\"},";
                                            }
                                            else if (pestStatus.lamp.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "灯管状态:亮;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@2\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@2\":{\"name\":\"灯管状态\"},";
                                            }
                                            pestStatus.lat = jObj["payload"]["ext"]["lat"].ToString();
                                            sCommMemo = sCommMemo + "经度:" + pestStatus.lat + ";";
                                            pestStatus.lng = jObj["payload"]["ext"]["lng"].ToString();
                                            sCommMemo = sCommMemo + "纬度:" + pestStatus.lng + ";";
                                            pestStatus.lps = jObj["payload"]["ext"]["lps"].ToString();
                                            if (pestStatus.lps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "光控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"light_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"light_stat\":{\"name\":\"光控状态\"},";
                                            }
                                            else if (pestStatus.lps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "光控状态:光控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"light_stat\":\"光控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"light_stat\":{\"name\":\"光控状态\"},";
                                            }
                                            pestStatus.lux = jObj["payload"]["ext"]["lux"].ToString();
                                            sSensorSQL = sSensorSQL + "(24,'YFC01I16-04',now(),'" + pestStatus.lux + "',1),";
                                            sHYSendStr2 = sHYSendStr2 + "\"lightIntensity\":" + pestStatus.lux + ",";
                                            sHYSendStr4 = sHYSendStr4 + "\"lightIntensity\":{\"name\":\"光照强度\"},";
                                            pestStatus.rps = jObj["payload"]["ext"]["rps"].ToString();
                                            if (pestStatus.rps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            else if (pestStatus.rps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:雨控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"雨控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            pestStatus.stamp = jObj["payload"]["ext"]["stamp"].ToString();
                                            pestStatus.tps = jObj["payload"]["ext"]["tps"].ToString();
                                            if (pestStatus.tps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "温控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"temp_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"temp_stat\":{\"name\":\"温控状态\"},";
                                            }
                                            else if (pestStatus.tps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "温控状态:温控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"temp_stat\":\"温控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"temp_stat\":{\"name\":\"温控状态\"},";
                                            }
                                            pestStatus.ts = jObj["payload"]["ext"]["ts"].ToString();
                                            if (pestStatus.ts.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "时控状态:光控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"runMode\":\"lightMode\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"runMode\":{\"name\":\"时控状态\"},";
                                            }
                                            else if (pestStatus.ts.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "时控状态:时控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"runMode\":\"timeMode\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"runMode\":{\"name\":\"时控状态\"},";
                                            }
                                            pestStatus.upds = jObj["payload"]["ext"]["upds"].ToString();
                                            if (pestStatus.upds.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "上仓门:关闭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@3\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@3\":{\"name\":\"上仓门状态\"},";
                                            }
                                            else if (pestStatus.upds.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "上仓门:打开;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@3\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@3\":{\"name\":\"上仓门状态\"},";
                                            }
                                            pestStatus.ws = jObj["payload"]["ext"]["ws"].ToString();
                                            if (pestStatus.ws.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:待机;";
                                                sHYSendStr2 = sHYSendStr2 + "\"running\":\"idle\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"running\":{\"name\":\"工作状态\"},";
                                            }
                                            else if (pestStatus.ws.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:工作;";
                                                sHYSendStr2 = sHYSendStr2 + "\"running\":\"busy\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"running\":{\"name\":\"工作状态\"},";
                                            }
                                        }
                                        else if (sCmd.Equals("offline"))
                                        {
                                            //设备下线
                                            return "3";
                                        }
                                        else
                                        {
                                            return "4";
                                        }
                                        //存传感器数据
                                        if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                                        {
                                            sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                                            MySqlConnection sensorConn = new MySqlConnection(_connectStr);
                                            MySqlCommand sensorCommand = new MySqlCommand(sSensorSQL, sensorConn);
                                            sensorConn.Open();
                                            try
                                            {
                                                if (sensorCommand.ExecuteNonQuery() > 0)
                                                {
                                                    msg = " 虫情设备(" + yf_pestid + ")插入最新数据成功：" + sSensorSQL;
                                                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                }
                                            }
                                            catch (Exception err)
                                            {
                                                msg = " 虫情设备(" + yf_pestid + ")插入最新数据：" + sSensorSQL + "，失败：" + err.Message;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                return "6";
                                            }
                                            finally
                                            {
                                                if (sensorConn.State == ConnectionState.Open)
                                                {
                                                    sensorConn.Close();
                                                }
                                            }
                                        }
                                        //存控制器状态
                                        if (sCommMemo.Substring(sCommMemo.Length - 1).Equals(";"))
                                        {
                                            sCommSQL = sCommSQL + "'" + sCommMemo.Substring(0, sCommMemo.Length - 1) + "' where id = 4";
                                            MySqlConnection commConnn = new MySqlConnection(_connectStr);
                                            MySqlCommand commCommand = new MySqlCommand(sCommSQL, commConnn);
                                            commConnn.Open();
                                            try
                                            {
                                                if (commCommand.ExecuteNonQuery() > 0)
                                                {
                                                    msg = " 虫情设备(" + yf_pestid + ")更新状态成功:" + sCommSQL;
                                                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                }
                                            }
                                            catch (Exception err)
                                            {
                                                msg = " 虫情设备（" + yf_pestid + "）更新状态：" + sCommSQL + ",失败：" + err.Message;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                return "7";
                                            }
                                            finally
                                            {
                                                if (commConnn.State == ConnectionState.Open)
                                                {
                                                    commConnn.Close();
                                                }
                                            }
                                        }
                                        //向瀚云平台发送数据
                                        pestComm = findHYCommbySn(yf_pestid, false);

                                        if ((pestComm != null) && (hy_mqtt_client != null))
                                        {
                                            string sHYSendStr1 = "{\"devs\":{\"" + pestComm.deviceName + "\":{\"app\":{";
                                            string sHYSendStr3 = "},\"metadata\":{\"name\":\"" + pestComm.deviceName + "\",\"type\":22,\"props\":{";
                                            string content = sHYSendStr1 + sHYSendStr2.Substring(0, sHYSendStr2.Length - 1);
                                            content += sHYSendStr3 + sHYSendStr4.Substring(0, sHYSendStr4.Length - 1) + "}}}}}";
                                            if (content.Length > 0)
                                            {
                                                string b = content;
                                                HY_PublishInfo(pestComm, string.Concat(content));
                                                ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送虫情数据:" + content);
                                                EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送虫情数据:" + content);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ShowMsgEvent(DateTime.Now.ToString() + " 处理http数据发生错误，找不到虫情设备：" + sIMEI);
                                        EveryDayLog.Write(DateTime.Now.ToString() + " 处理http数据发生错误，找不到虫情设备：" + sIMEI);
                                        return "8";
                                    }

                                }
                                else if (sTopic.Contains("/yfkj/bzy"))
                                {

                                    sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (device_id,Device_Code,reporttime,ReportValue,Block_ID) VALUES  ";
                                    sCommSQL = "UPDATE yw_d_commnication_tbl SET MEMO = ";
                                    if (jObj["payload"]["ext"]["imei"] == null)
                                    {
                                        ShowMsgEvent(DateTime.Now.ToString() + " 发生错误，IMEI为空");
                                        EveryDayLog.Write(DateTime.Now.ToString() + " 发生错误，IMEI为空");
                                        return "9";
                                    }
                                    sIMEI = jObj["payload"]["ext"]["imei"].ToString();
                                    sCommMemo = null;
                                    sHYSendStr2 = null;
                                    sHYSendStr4 = null;
                                    if (getCommbySN(yf_sporeid, 1) != null)
                                    {
                                        if (sCmd.Equals("status"))
                                        {
                                            YF_Spore_Status sporeStatus = new YF_Spore_Status();
                                            sporeStatus.at = jObj["payload"]["ext"]["at"].ToString();
                                            sSensorSQL = sSensorSQL + "(27,'YFC02I16-01',now(),'" + sporeStatus.at + "',1),";
                                            sporeStatus.ah = jObj["payload"]["ext"]["ah"].ToString();
                                            sSensorSQL = sSensorSQL + "(28,'YFC02I16-02',now(),'" + sporeStatus.ah + "',1),";
                                            sporeStatus.box_temp = jObj["payload"]["ext"]["box_tem"].ToString();
                                            sSensorSQL = sSensorSQL + "(29,'YFC02I16-03',now(),'" + sporeStatus.box_temp + "',1),";
                                            sporeStatus.csq = jObj["payload"]["ext"]["csq"].ToString();
                                            sSensorSQL = sSensorSQL + "(33,'YFC02I16-07',now(),'" + sporeStatus.csq + "',1),";
                                            sporeStatus.pre_temp = jObj["payload"]["ext"]["pre_temp"].ToString();
                                            sSensorSQL = sSensorSQL + "(30,'YFC02I16-04',now(),'" + sporeStatus.pre_temp + "',1),";
                                            sporeStatus.set_temp = jObj["payload"]["ext"]["set_temp"].ToString();
                                            sSensorSQL = sSensorSQL + "(31,'YFC02I16-05',now(),'" + sporeStatus.set_temp + "',1),";
                                            sporeStatus.v_bat = jObj["payload"]["ext"]["v_bat"].ToString();
                                            sSensorSQL = sSensorSQL + "(32,'YFC02I16-06',now(),'" + sporeStatus.v_bat + "',1),";
                                            sporeStatus.cul_time = jObj["payload"]["ext"]["cul_time"].ToString();
                                            sSensorSQL = sSensorSQL + "(34,'YFC02I16-08',now(),'" + sporeStatus.cul_time + "',1),";
                                            sporeStatus.staytime = jObj["payload"]["ext"]["staytime"].ToString();
                                            sSensorSQL = sSensorSQL + "(35,'YFC02I16-09',now(),'" + sporeStatus.staytime + "',1),";
                                            sporeStatus.drop_time = jObj["payload"]["ext"]["drop_time"].ToString();
                                            sSensorSQL = sSensorSQL + "(36,'YFC02I16-10',now(),'" + sporeStatus.drop_time + "',1),";
                                            sHYSendStr2 = (((((((((sHYSendStr2 + "\"Temp\":" + sporeStatus.at + ",") + "\"RH\":" + sporeStatus.ah + ",") + "\"Temp@2\":" + sporeStatus.box_temp + ",") + "\"csq\":" + sporeStatus.csq + ",") + "\"preTemp\":" + sporeStatus.pre_temp + ",") + "\"setTemp\":" + sporeStatus.set_temp + ",") + "\"voltage\":" + sporeStatus.v_bat + ",") + "\"culTime\":" + sporeStatus.cul_time + ",") + "\"staytime\":" + sporeStatus.staytime + ",") + "\"drop_time\":" + sporeStatus.drop_time + ",";
                                            sHYSendStr4 = ((((sHYSendStr4 + "\"Temp\":{\"name\":\"环境温度\"}," + "\"RH\":{\"name\":\"环境湿度\"},") + "\"Temp@2\":{\"name\":\"机箱温度\"}," + "\"csq\":{\"name\":\"信号强度\"},") + "\"preTemp\":{\"name\":\"保温仓当前温度\"}," + "\"setTemp\":{\"name\":\"保温仓设定温度\"},") + "\"voltage\":{\"name\":\"电压\"}," + "\"culTime\":{\"name\":\"培养时间\"},") + "\"staytime\":{\"name\":\"已培养时间\"}," + "\"drop_time\":{\"name\":\"滴液时间\"},";
                                            sporeStatus.on_off = jObj["payload"]["ext"]["on_off"].ToString();
                                            if (sporeStatus.on_off.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "设备开关:关闭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"设备开关\"},";
                                            }
                                            else if (sporeStatus.on_off.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "设备开关:开启;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@1\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@1\":{\"name\":\"设备开关\"},";
                                            }
                                            sporeStatus.bat_sta = jObj["payload"]["ext"]["bat_sta"].ToString();
                                            if (sporeStatus.bat_sta.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "电池状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"bat_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"bat_stat\":{\"name\":\"电池状态\"},";
                                            }
                                            else if (sporeStatus.bat_sta.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "电池状态:电量过低;";
                                                sHYSendStr2 = sHYSendStr2 + "\"bat_stat\":\"电量过低\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"bat_stat\":{\"name\":\"电池状态\"},";
                                            }
                                            sporeStatus.usb_sta = jObj["payload"]["ext"]["usb_sta"].ToString();
                                            if (sporeStatus.usb_sta.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "摄像头状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"usb_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"usb_stat\":{\"name\":\"摄像头状态\"},";
                                            }
                                            else if (sporeStatus.usb_sta.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "摄像头状态:异常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"usb_stat\":\"异常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"usb_stat\":{\"name\":\"摄像头状态\"},";
                                            }
                                            sporeStatus.wind_sw = jObj["payload"]["ext"]["wind_sw"].ToString();
                                            if (sporeStatus.wind_sw.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "风机开关:关闭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@2\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@2\":{\"name\":\"风机开关\"},";
                                            }
                                            else if (sporeStatus.wind_sw.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "风机开关:开启;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@2\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@2\":{\"name\":\"风机开关\"},";
                                            }
                                            sporeStatus.cold_sw = jObj["payload"]["ext"]["cold_sw"].ToString();
                                            if (sporeStatus.cold_sw.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "制冷机开关:关闭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@3\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@3\":{\"name\":\"制冷机开关\"},";
                                            }
                                            else if (sporeStatus.cold_sw.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "制冷机开关:开启;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@3\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@3\":{\"name\":\"制冷机开关\"},";
                                            }
                                            sporeStatus.rps = jObj["payload"]["ext"]["rps"].ToString();
                                            if (sporeStatus.rps.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:正常;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"正常\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            else if (sporeStatus.rps.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "雨控状态:雨控;";
                                                sHYSendStr2 = sHYSendStr2 + "\"rain_stat\":\"雨控\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"rain_stat\":{\"name\":\"雨控状态\"},";
                                            }
                                            sporeStatus.work_sta = jObj["payload"]["ext"]["work_sta"].ToString();
                                            if (sporeStatus.work_sta.Equals("0"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:关闭;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@7\":\"false\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@7\":{\"name\":\"工作状态\"},";
                                            }
                                            else if (sporeStatus.work_sta.Equals("1"))
                                            {
                                                sCommMemo = sCommMemo + "工作状态:工作;";
                                                sHYSendStr2 = sHYSendStr2 + "\"onOff@7\":\"true\",";
                                                sHYSendStr4 = sHYSendStr4 + "\"onOff@7\":{\"name\":\"工作状态\"},";
                                            }
                                        }
                                        else if (sCmd.Equals("offline"))
                                        {
                                            return "10";
                                        }
                                        else
                                        {
                                            msg = " 孢子设备(" + yf_sporeid + ")获取非解析数据";
                                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            sReturn = "5";
                                            return "11";
                                        }
                                        //存传感器数据
                                        if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                                        {
                                            sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                                            MySqlConnection sensorConn = new MySqlConnection(_connectStr);
                                            MySqlCommand sensorCommand = new MySqlCommand(sSensorSQL, sensorConn);
                                            sensorConn.Open();
                                            try
                                            {
                                                if (sensorCommand.ExecuteNonQuery() > 0)
                                                {
                                                    msg = " 孢子设备(" + yf_sporeid + ")插入最新数据成功：" + sSensorSQL;
                                                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                }
                                            }
                                            catch (Exception err)
                                            {
                                                msg = " 孢子设备(" + yf_sporeid + ")插入最新数据：" + sSensorSQL + "，失败：" + err.Message;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                return "12";
                                            }
                                            finally
                                            {
                                                if (sensorConn.State == ConnectionState.Open)
                                                {
                                                    sensorConn.Close();
                                                }
                                            }
                                        }
                                        //存控制器状态
                                        if (sCommMemo.Substring(sCommMemo.Length - 1).Equals(";"))
                                        {
                                            sCommSQL = sCommSQL + "'" + sCommMemo.Substring(0, sCommMemo.Length - 1) + "' where id = 4";
                                            MySqlConnection commConnn = new MySqlConnection(_connectStr);
                                            MySqlCommand commCommand = new MySqlCommand(sCommSQL, commConnn);
                                            commConnn.Open();
                                            try
                                            {
                                                if (commCommand.ExecuteNonQuery() > 0)
                                                {
                                                    msg = " 孢子设备(" + yf_sporeid + ")更新状态成功:" + sCommSQL;
                                                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                }
                                            }
                                            catch (Exception err)
                                            {
                                                msg = " 孢子设备（" + yf_sporeid + "）更新状态：" + sCommSQL + ",失败：" + err.Message;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                return "13";
                                            }
                                            finally
                                            {
                                                if (commConnn.State == ConnectionState.Open)
                                                {
                                                    commConnn.Close();
                                                }
                                            }
                                        }

                                        //向瀚云平台发送数据
                                        sporeComm = findHYCommbySn(yf_sporeid, false);

                                        if ((sporeComm != null) && (hy_mqtt_client != null))
                                        {
                                            string sHYSendStr1 = "{\"devs\":{\"" + sporeComm.deviceName + "\":{\"app\":{";
                                            string sHYSendStr3 = "},\"metadata\":{\"name\":\"" + sporeComm.deviceName + "\",\"type\":23,\"props\":{";
                                            string content = sHYSendStr1 + sHYSendStr2.Substring(0, sHYSendStr2.Length - 1);
                                            content += sHYSendStr3 + sHYSendStr4.Substring(0, sHYSendStr4.Length - 1) + "}}}}}";
                                            if (content.Length > 0)
                                            {
                                                HY_PublishInfo(sporeComm, string.Concat(content));
                                                ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送孢子数据:" + content);
                                                EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送孢子数据:" + content);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        ShowMsgEvent(DateTime.Now.ToString() + " 发生错误，找不到孢子设备：" + sIMEI);
                                        EveryDayLog.Write(DateTime.Now.ToString() + " 发生错误，找不到孢子设备：" + sIMEI);
                                        return "14";

                                    }
                                }
                                else
                                {
                                    ShowMsgEvent(DateTime.Now.ToString() + " 处理http数据发生错误，Topic为非法格式：" + sTopic);
                                    EveryDayLog.Write(DateTime.Now.ToString() + " 处理http数据发生错误，Topic为非法格式：" + sTopic);
                                    return "15";
                                }

                            }
                            else if (sTopic == null)
                            {

                                if (jObj["imei"] == null)
                                {
                                    ShowMsgEvent(DateTime.Now.ToString() + " 处理http URL数据发生错误，IMEI为空");
                                    EveryDayLog.Write(DateTime.Now.ToString() + " 处理http URL数据发生错误，IMEI为空");
                                    return "16";
                                }
                                sIMEI = jObj["imei"].ToString();
                                if (sIMEI.Equals(yf_pestid))
                                {
                                    try
                                    {

                                        sImageURL = jObj["Image"].ToString();
                                        sResultImage = jObj["Result_image"].ToString();
                                        sResult = jObj["Result"].ToString();
                                        char[] separator = new char[] { '#' };
                                        sResult.Split(separator);
                                    }
                                    catch
                                    {

                                    }
                                    sFilePath = null;
                                    sFilePath = null;
                                    sDeviceID = "4";
                                    pestComm = findHYCommbySn(sIMEI, false);
                                    if ((pestComm != null) && hy_mqtt_client != null)
                                    {
                                        string content = "{\"devs\":{\"prest\":{\"app\":{\"imageUrl\":\"" + sImageURL + "\",\"result_image\":\"";
                                        content += sResultImage + "\",\"result_desc\":\"" + sResult + "\",\"deviceId\":\"" + hydevicekey;
                                        content += "\"},\"metadata\":{\"name\":\"" + pestComm.deviceName + "\",\"type\":22}}}}";
                                        if (content.Length > 0)
                                        {
                                            HY_PublishInfo(pestComm, content);
                                            ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送虫情图片:" + content);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送虫情图片:" + content);
                                        }
                                    }
                                    if (sImageURL != null && sImageURL.Length > 10)
                                    {
                                        try
                                        {
                                            string sFileName = "CQI-" + DateTime.Now.ToString("yyMMddHHmmssms") + FormatFunc.StrRandom(5, false) + ".jpg";
                                            sFilePath = photoaddr + sFileName;
                                            FormatFunc.WriteBytesToFile(sFilePath, FormatFunc.GetBytesFromUrl(sImageURL));
                                        }
                                        catch (Exception err)
                                        {
                                            msg = " 获取虫情设备拍照图片发生错误：" + err.Message.ToString();
                                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            return "17";
                                        };
                                        pestComm = findHYCommbySn(yf_pestid, false);
                                    }
                                    else if (sResultImage != null && sResultImage.Length > 10)
                                    {
                                        try
                                        {
                                            string sFileName = "CQR-" + DateTime.Now.ToString("yyMMddHHmmssms") + FormatFunc.StrRandom(5, false) + ".jpg";
                                            sResultFilePath = photoaddr + sFileName;
                                            FormatFunc.WriteBytesToFile(sResultFilePath, FormatFunc.GetBytesFromUrl(sResultImage));
                                        }
                                        catch (Exception err)
                                        {
                                            msg = " 获取虫情设备结果图片发生错误：" + err.Message.ToString();
                                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            return "18";
                                        }
                                        string sTimestamp = FormatFunc.getTimeStamp().ToString();
                                        string cmdText = "insert into yw_d_image (comm_id,photograph,photograph_source,result_image,result_source,result_desc,createtime) values ";
                                        cmdText += "('" + sDeviceID + "','" + sFilePath + "','" + sImageURL + "','" + sFilePath + "','" + sResultImage + "','" + sResult + "','" + sTimestamp + "'),";
                                        cmdText += "'" + sDeviceID + "',";
                                        MySqlConnection resultConn = new MySqlConnection(_connectStr);
                                        MySqlCommand resultCommand = new MySqlCommand(cmdText, resultConn);
                                        resultConn.Open();
                                        try
                                        {
                                            if (resultCommand.ExecuteNonQuery() > 0)
                                            {
                                                msg = " 虫情设备下载图片成功:" + cmdText;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            }
                                        }
                                        catch (Exception err)
                                        {
                                            string[] textArray8 = new string[] { DateTime.Now.ToString(), " 虫情设备下载图片失败:", cmdText, ",错误：", err.Message };
                                            msg = string.Concat(textArray8);
                                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            return "19";
                                        }
                                        finally
                                        {
                                            if (resultConn.State == ConnectionState.Open)
                                            {
                                                resultConn.Close();
                                            }
                                        }


                                    }
                                    else
                                    {
                                        msg = " 虫情设备没有图片地址";
                                        ShowMsgEvent(DateTime.Now.ToString() + msg);
                                        EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                        return "20";
                                    }


                                }
                                else if (sIMEI.Equals(yf_sporeid))
                                {
                                    sporeComm = findHYCommbySn(sIMEI, false);

                                    if (sporeComm != null)
                                    {
                                        sImageURL = jObj["Image"].ToString();
                                        sDeviceID = "5";
                                        if ((sporeComm != null) && hy_mqtt_client != null)
                                        {
                                            string content = "{\"devs\":{\"robe\":{\"app\":{\"imageUrl\":\"" + sImageURL + "\",\"deviceId\":\"" + hydevicekey;
                                            content += "\"},\"metadata\":{\"name\":\"" + sporeComm.deviceName + "\",\"type\":23}}}}";
                                            if (content.Length > 0)
                                            {
                                                HY_PublishInfo(sporeComm, content);
                                                ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送孢子图片:" + content);
                                                EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送孢子图片:" + content);
                                            }
                                        }
                                        if (sImageURL != null && sImageURL.Length > 10 )
                                        {
                                            string fileName = "BZ-" + DateTime.Now.ToString("yyMMddHHmmssms") + FormatFunc.StrRandom(5, false) + ".jpg";
                                            string filePath = photoaddr + fileName;
                                            FormatFunc.WriteBytesToFile(filePath, FormatFunc.GetBytesFromUrl(sImageURL));
                                            string sNowTime = FormatFunc.getTimeStamp().ToString();
                                            string sImageSQL = "insert into yw_d_image (comm_id,photograph,photograph_source,createtime) values ";
                                            sImageSQL += "('" + sDeviceID + "','" + filePath + "','" + sImageURL + "','" + sNowTime + "')";

                                            MySqlConnection sporeImageConn = new MySqlConnection(_connectStr);
                                            MySqlCommand sporeImageCommand = new MySqlCommand(sImageSQL, sporeImageConn);
                                            sporeImageConn.Open();
                                            try
                                            {
                                                if (sporeImageCommand.ExecuteNonQuery() > 0)
                                                {
                                                    msg = " 孢子设备下载图片成功:" + sImageSQL;
                                                    ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                    EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                }
                                            }
                                            catch (Exception exception8)
                                            {
                                                msg = " 孢子设备下载图片失败:" + sImageSQL + ",错误：" + exception8.Message;
                                                ShowMsgEvent(DateTime.Now.ToString() + msg);
                                                EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                                return "21";
                                            }
                                            finally
                                            {
                                                if (sporeImageConn.State == ConnectionState.Open)
                                                {
                                                    sporeImageConn.Close();
                                                }
                                            }

                                        }
                                        else
                                        {
                                            msg = " 孢子设备没有图片地址";
                                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                                            return "22";
                                        }
                                    }
                                    else
                                    {
                                        return "23";
                                    }

                                }
                            }
                            else
                            {
                                ShowMsgEvent(DateTime.Now.ToString() + " 处理http数据，Topic为非法格式：" + sTopic + ",或Cmd为空");
                                EveryDayLog.Write(DateTime.Now.ToString() + " 处理http数据，Topic为非法格式：" + sTopic + ",或Cmd为空");
                                return "24";
                            }
                        }
                        catch (Exception err)
                        {
                            ShowMsgEvent(DateTime.Now.ToString() + "处理http数据：" + sRecv + "，发生错误：" + err.Message);
                            EveryDayLog.Write(DateTime.Now.ToString() + " 处理http数据：" + sRecv + "，发生错误：" + err.Message);
                            return "25";
                        }
                    }

                }
                catch
                {
                    response.StatusDescription = "404";
                    response.StatusCode = 404;
                    ShowMsgEvent(DateTime.Now.ToString() + " 接收http数据发生错误，处理结束");
                    EveryDayLog.Write(DateTime.Now.ToString() + " 接收http数据发生错误，处理结束");
                    return "26";
                }

                response.StatusDescription = "200";
                response.StatusCode = 200;
                ShowMsgEvent(DateTime.Now.ToString() + " 接收http数据处理结束");
                EveryDayLog.Write(DateTime.Now.ToString() + " 接收http数据处理结束");
                return "0";
            }

        }




        public void onClientConnected(object sender, AsyncEventArgs e)
        {
            TCPClientState state = e._state;
            string ipport = state.TcpClient.Client.RemoteEndPoint.ToString();
            string msg = null;
            try
            {
                _clients.Add(state);
                msg = DateTime.Now.ToString() + " 接受客户端(" + ipport + ")新连接";
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " 接受客户端(" + ipport + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }





        public void onDataReceived(object sender, AsyncEventArgs e)
        {
            TCPClientState state = e._state;
            string sRecvData = e._msg.Replace(" ", "");
            string ipport = state.TcpClient.Client.RemoteEndPoint.ToString();
            string msg = null;
            msg = DateTime.Now.ToString() + " 接受数据:" + sRecvData;
            ShowMsgEvent(msg);
            EveryDayLog.Write(msg);

            CommDevice clientComm = null;
            string gw_sn = null;
            if (isHeartBeat(sRecvData) != null)
            {
                clientComm = isHeartBeat(sRecvData);
                if (clientComm != null)
                {
                    gw_sn = clientComm.serial_num;
                }

            }
            //连接请求
            if (state.clientStatus == 1)
            {
                //心跳数据
                if (gw_sn != null)
                {
                    try
                    {
                        //KLC客户端
                        if (sRecvData.Substring(0, 12).ToUpper().Equals("150122220010"))
                        {

                            TCPClientState oldClient = findStatebySN(gw_sn, true);

                            //已存在连接
                            if (oldClient != null)
                            {
                                int tp = (int)DateTime.Now.Subtract(Convert.ToDateTime(oldClient.lastTime)).Duration().TotalSeconds;
                                if (tp < 300)
                                {
                                    msg = DateTime.Now.ToString() + " 拒绝KLC客户端(" + gw_sn + ")连接请求：与上一个已连接间隔<5分钟接";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    return;
                                }
                                else
                                {
                                    oldClient.clientStatus = 3;
                                    msg = DateTime.Now.ToString() + " 接受KLC客户端(" + gw_sn + ")连接请求，关闭原有连接，创建新连接";
                                }
                            }
                            else
                            {
                                Send(state, "15012222000180");
                                msg = DateTime.Now.ToString() + " 接受KLC客户端(" + gw_sn + ")连接请求，并响应握手数据：15012222000180";
                            }
                            state.faildTimes = 0;
                            state.lastTime = DateTime.Now.ToString();
                            state.clientStatus = 2;
                            state.clientComm = clientComm;
                            addClientEvent(gw_sn);
                            Task.Run(() => collectDataorState(state));
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else
                        {
                            TCPClientState oldstate = findStatebySN(gw_sn, true);
                            if (oldstate != null)
                            {
                                if (((int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds) <= 300)
                                {
                                    msg = DateTime.Now.ToString() + " 拒绝客户端(" + gw_sn + ")连接请求：与上一个已连接间隔<=5分钟";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    return;
                                }
                                else
                                {
                                    state.clientStatus = 3;
                                    msg = DateTime.Now.ToString() + " 接受客户端(" + gw_sn + ")连接请求，关闭原有连接，创建新连接";
                                }
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " 接受客户端(" + gw_sn + ")连接请求，建立连接";
                            }
                            state.faildTimes = 0;
                            state.lastTime = DateTime.Now.ToString();
                            state.clientStatus = 2;
                            state.clientComm = clientComm;
                            addClientEvent(gw_sn);
                            Task.Run(() => collectDataorState(state));
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 接受客户端(" + gw_sn + ")心跳数据:" + sRecvData + "，解析发生错误:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                else
                {
                    state.faildTimes++;
                    msg = DateTime.Now.ToString() + " 从待连接客户端(" + ipport + ")接受非心跳数据:" + sRecvData + ",已失败次数：" + state.faildTimes.ToString();
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);

                }

            }
            //在线
            else if (state.clientStatus == 2 && state.clientComm != null)
            {
                //心跳数据
                if (gw_sn != null)
                {
                    if (string.Equals(state.clientComm.commtype, "JXC", StringComparison.OrdinalIgnoreCase))
                    {
                        int collectInterval = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                        if(collectInterval<600)
                        {
                            msg = " 精讯设备上传数据太频繁，暂不记录，记录时间保持>10分钟";
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else
                        {
                            state.lastTime = DateTime.Now.ToString();
                            state.faildTimes = 0;
                            JXC_handlerecv(state.clientComm, sRecvData);
                        }
                        
                    }
                    else
                    {
                        state.faildTimes = 0;
                        state.lastTime = DateTime.Now.ToString();
                        msg = DateTime.Now.ToString() + " 从已连接客户端(" + gw_sn + ")接受心跳";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                //处理其他数据
                else
                {
                    gw_sn = sRecvData.Substring(0, 16);
                    bool bComm = false;
                    if (state.clientComm.commtype.Equals("KLC", StringComparison.OrdinalIgnoreCase))
                    {
                        bComm = true;
                    }
                    else if (gw_sn.Equals(state.clientComm.serial_num, StringComparison.OrdinalIgnoreCase))
                    {
                        bComm = true;
                    }
                    if (!bComm)
                    {
                        state.faildTimes++;
                        msg = DateTime.Now.ToString() + " 已连接客户端(" + state.clientComm.serial_num + ")获取非法数据" + sRecvData;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        return;
                    }
                    else
                    {
                        switch (state.clientComm.commtype.ToUpper())
                            {
                                case "PLC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    PLC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "KLC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    KLC_handlerecv(state.clientComm, sRecvData.Substring(12));
                                    break;
                                case "FKC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    FKC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "XPC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    XPC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "XPH":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    XPH_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "YYC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    YYC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "SFJ-0804":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    SFJ_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "SFJ-1200":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    SFJ_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                case "DYC":
                                    state.lastTime = DateTime.Now.ToString();
                                    state.faildTimes = 0;
                                    DYC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                    break;
                                default:
                                    state.faildTimes++;
                                    msg = DateTime.Now.ToString() + " 处理未知类型已连接客户端(" + state.clientComm.serial_num + ")数据" + sRecvData;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;
                            }
                    }

                }

            }
            else
            {
                msg = DateTime.Now.ToString() + " 通讯客户端(" + ipport + ")处于断开或关闭状态" + sRecvData;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }

        }


        /// <summary>
        /// 从接受数据中获取通讯设备序列号
        /// </summary>
        /// <param name="ipport"></param>
        /// <param name="recvdata"></param>
        /// <returns></returns>
        public CommDevice isHeartBeat(string recvdata)
        {
            string commSN = null;
            try
            {
                if (recvdata.Length == 16 || recvdata.Length == 44)
                {
                    if (recvdata.Substring(0, 12).Equals("150122220010") && recvdata.Length == 44) //昆仑海岸网关请求连接
                    {
                        //150122220010 31323030323031393036313231303938
                        commSN = FormatFunc.HexToAscii(recvdata.Substring(12));
                        ///////////////////////////////发送回应数据
                    }
                    else if (recvdata.Length == 16)
                    {
                        commSN = recvdata;
                    }
                }
                else if ((recvdata.Length == 66) && recvdata.Substring(0, 6).Equals("FEDC01", StringComparison.OrdinalIgnoreCase))
                {
                    commSN = recvdata.Substring(6, 12);
                }
                else
                {
                    return null;
                }

                foreach (CommDevice one in _commList)
                {
                    if (one.serial_num.ToUpper().Equals(commSN))
                    {
                        return one;
                    }

                }

            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 判断心跳数据发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return null;
            }
            return null;
        }


        public void initDev_Thrd()
        {
            while (IsRunning)
            {
                string sSensorSQL = null;
                string msg = null;
                if (_communicationtype.Contains("1"))
                {

                    int maxLatestCommID = 0;
                    int maxLatestControlID = 0;
                    int maxLatestSensorID = 0;

                    //初始化通讯设备
                    try
                    {
                        //是否有新的通讯设备,通过设备ID，必须自增长,如通过comm_list，太耗时
                        sSensorSQL = "SELECT MAX(id) as maxid FROM yw_d_commnication_tbl";
                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn))
                            {
                                maxLatestCommID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网通讯设备最大ID失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxLatestCommID > _maxCommID)
                            {
                                //如果有新设备
                                sSensorSQL = "SELECT a.ID,a.Code,a.SerialNumber,a.CodeAddress,a.Company_ID,c.ClassName,`Interval`,b.ParamValue,e.id AS hyid,f.product_name AS productname FROM yw_d_commnication_tbl a ";
                                sSensorSQL += "LEFT JOIN yw_d_devicemodel_tbl b ON a.Model_ID = b.ID ";
                                sSensorSQL += "LEFT JOIN yw_d_deviceclass_tbl c ON b.DeviceClass_ID= c.ID ";
                                sSensorSQL += "LEFT JOIN `ys_parameter_tbl` d ON b.`Formula` = d.`Parameter_Key` ";
                                sSensorSQL += "LEFT JOIN `yw_d_hanyun_gl` e ON a.`id` = e.`ky_commid` ";
                                sSensorSQL += "LEFT JOIN  `yw_d_hanyun_product` f ON e.hy_produce_id = f.id ";
                                sSensorSQL += "WHERE a.UsingState='1' AND b.State='1' AND d.Parameter_Class = 'YW-JSGS' ";
                                sSensorSQL += "AND a.ID > '" + _maxCommID + "' AND e.hy_devicename != 'sfj' Order by a.ID asc";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSensorSQL, conn2);
                                using (MySqlDataReader msdr2 = myCmd2.ExecuteReader())
                                {
                                    while (msdr2.Read())
                                    {
                                        //
                                        //commid,commcode,serial_num,commaddr,companyid,commtype,commpara,commclass,passnum
                                        CommDevice commdev = new CommDevice();
                                        commdev.commid = (msdr2.IsDBNull(0)) ? 0 : msdr2.GetInt32("ID");
                                        commdev.commcode = (msdr2.IsDBNull(1)) ? "" : msdr2.GetString("Code");
                                        commdev.serial_num = (msdr2.IsDBNull(2)) ? "" : msdr2.GetString("SerialNumber");
                                        commdev.commaddr = (msdr2.IsDBNull(3)) ? "" : msdr2.GetString("CodeAddress");
                                        commdev.companyid = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("Company_ID");
                                        commdev.commtype = (msdr2.IsDBNull(5)) ? "" : msdr2.GetString("ClassName");
                                        commdev.commpara = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("Interval");
                                        commdev.passnum = (msdr2.IsDBNull(7)) ? 0 : msdr2.GetInt32("ParamValue");
                                        commdev.hyid = (msdr2.IsDBNull(8)) ? 0 : msdr2.GetInt32("hyid");
                                        commdev.heartstr = (msdr2.IsDBNull(2)) ? "" : msdr2.GetString("SerialNumber");
                                        commdev.commclass = 1;
                                        _commList.Add(commdev);
                                        if (commdev.commtype.Equals("MQT") && mqtt_client != null)
                                        {
                                            MQTT_Connect(commdev.serial_num);
                                        }
                                    }
                                }
                            }
                            _maxCommID = maxLatestCommID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网通讯设备失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化控制设备
                    try
                    {

                        sSensorSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSensorSQL = "SELECT MAX(id) as maxid FROM yw_d_controller_tbl";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn))
                            {
                                maxLatestControlID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网控制设备最大ID失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxLatestControlID > _maxControlID)
                            {
                                //如果有新设备

                                sSensorSQL = "SELECT a.ID,a.Code,a.PortNum,b.SignalType,c.SerialNumber,a.TravelTime,a.Block_ID FROM yw_d_controller_tbl a  ";
                                sSensorSQL += "LEFT JOIN yw_d_devicemodel_tbl  b ON  a.Model_ID = b.ID ";
                                sSensorSQL += "LEFT JOIN yw_d_commnication_tbl c ON a.Commucation_ID = c.ID ";
                                sSensorSQL += "LEFT JOIN ys_parameter_tbl d ON b.Formula = d.Parameter_Key ";
                                sSensorSQL += "LEFT JOIN yw_d_deviceclass_tbl e ON b.DeviceClass_ID = e.ID ";
                                sSensorSQL += "WHERE a.UsingState = '1' AND b.State = '1' AND c.UsingState = '1'  AND d.Parameter_Class = 'YW-JSGS' ";
                                sSensorSQL += "AND a.ID > '" + _maxControlID + "' order by a.ID asc";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSensorSQL, conn2);
                                using (MySqlDataReader msdr2 = myCmd2.ExecuteReader())
                                {
                                    while (msdr2.Read())
                                    {
                                        //
                                        //devid,devcode,devaddr,devtype,commnum,devpara,blockid,devformula,devclass
                                        ControlDevice controldev = new ControlDevice();
                                        controldev.devid = (msdr2.IsDBNull(0)) ? 0 : msdr2.GetInt32("ID");
                                        controldev.devcode = (msdr2.IsDBNull(1)) ? "" : msdr2.GetString("Code");
                                        controldev.devaddr = (msdr2.IsDBNull(2)) ? 0 : msdr2.GetInt32("PortNum");
                                        controldev.devtype = (msdr2.IsDBNull(3)) ? "" : msdr2.GetString("SignalType");
                                        controldev.commnum = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("SerialNumber");
                                        controldev.devpara = (msdr2.IsDBNull(5)) ? 0 : msdr2.GetInt32("TravelTime"); ;
                                        controldev.blockid = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("Block_ID"); ;
                                        controldev.devformula = "0"; ;
                                        controldev.devclass = 10;
                                        _controlList.Add(controldev);
                                    }
                                }

                            }
                            _maxControlID = maxLatestControlID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网控制设备失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化传感器设备
                    try
                    {
                        sSensorSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSensorSQL = "SELECT MAX(id) as maxid FROM yw_d_sensor_tbl";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn))
                            {
                                maxLatestSensorID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网传感器最大ID失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxLatestSensorID > _maxSensorID)
                            {
                                //如果有新设备
                                sSensorSQL = "SELECT a.ID,a.Code,a.PortNum,b.SignalType,c.SerialNumber,a.CorrectValue,a.Block_ID,d.Parameter_Value FROM yw_d_sensor_tbl a ";
                                sSensorSQL += "LEFT JOIN yw_d_devicemodel_tbl  b ON  a.Model_ID = b.ID ";
                                sSensorSQL += "LEFT JOIN yw_d_commnication_tbl c ON a.Commucation_ID = c.ID ";
                                sSensorSQL += "LEFT JOIN ys_parameter_tbl d ON b.Formula= d.Parameter_Key ";
                                sSensorSQL += "LEFT JOIN yw_d_deviceclass_tbl e ON b.DeviceClass_ID= e.ID ";
                                sSensorSQL += "WHERE a.UsingState = '1' AND b.State = '1' AND c.UsingState = '1' AND d.Parameter_Class = 'YW-JSGS' ";
                                sSensorSQL += "AND a.ID > '" + _maxSensorID + "' order by a.ID asc ";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSensorSQL, conn2);
                                using (MySqlDataReader msdr2 = myCmd2.ExecuteReader())
                                {
                                    while (msdr2.Read())
                                    {
                                        //
                                        //devid,devcode,devaddr,devtype,commnum,devpara,blockid,devformula,devclass
                                        SensorDevice sensordev = new SensorDevice();
                                        sensordev.devid = (msdr2.IsDBNull(0)) ? 0 : msdr2.GetInt32("ID");
                                        sensordev.devcode = (msdr2.IsDBNull(1)) ? "" : msdr2.GetString("Code");
                                        sensordev.devaddr = (msdr2.IsDBNull(2)) ? 0 : msdr2.GetInt32("PortNum");
                                        sensordev.devtype = (msdr2.IsDBNull(3)) ? "" : msdr2.GetString("SignalType");
                                        sensordev.commnum = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("SerialNumber");
                                        sensordev.devpara = (msdr2.IsDBNull(5)) ? 0 : msdr2.GetInt32("CorrectValue"); ;
                                        sensordev.blockid = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("Block_ID"); ;
                                        sensordev.devformula = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("Parameter_Value"); ;
                                        sensordev.devclass = 11;
                                        _sensorList.Add(sensordev);
                                    }
                                }

                            }
                            _maxSensorID = maxLatestSensorID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网传感器失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }


                if (_communicationtype.Contains("2"))
                {
                    int maxLatestSfjID = 0;
                    int maxLatestSfjControlID = 0;
                    //初始化水肥机设备
                    try
                    {
                        sSensorSQL = null;
                        //是否有新的水肥机设备,通过设备ID，必须自增长
                        sSensorSQL = "SELECT MAX(id) as maxid FROM sfyth_plc";
                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn))
                            {
                                maxLatestSfjID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机PLC最大ID失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxLatestSfjID > _maxSfjID)
                            {
                                //如果有新设备
                                sSensorSQL = "SELECT a.ID,PLC_Name,PLC_Number,PLC_Address,Company_ID,PLC_GWType,TotalPass, e.id AS hyid  FROM sfyth_plc a ";
                                sSensorSQL += "LEFT JOIN `yw_d_hanyun_gl` e ON a.`id` = e.`ky_commid` ";
                                sSensorSQL += "LEFT JOIN `yw_d_hanyun_product` f ON e.`hy_produce_id` = f.`id` " + "WHERE e.hy_devicename = 'sfj'  ";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSensorSQL, conn2);
                                using (MySqlDataReader msdr2 = myCmd2.ExecuteReader())
                                {
                                    while (msdr2.Read())
                                    {
                                        //
                                        //commid,commcode,serial_num,commaddr,companyid,commtype,commpara,commclass,passnum
                                        CommDevice commdev = new CommDevice();
                                        commdev.commid = (msdr2.IsDBNull(0)) ? 0 : msdr2.GetInt32("ID");
                                        commdev.commcode = (msdr2.IsDBNull(1)) ? "" : msdr2.GetString("PLC_Name");
                                        commdev.serial_num = (msdr2.IsDBNull(2)) ? "" : msdr2.GetString("PLC_Number");
                                        commdev.commaddr = (msdr2.IsDBNull(3)) ? "" : msdr2.GetString("PLC_Address");
                                        commdev.companyid = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("Company_ID");
                                        commdev.commtype = (msdr2.IsDBNull(5)) ? "" : msdr2.GetString("PLC_GWType");
                                        //commdev.commpara = (msdr2.IsDBNull(6)) ? 0 :msdr2.GetInt32("PassNumber");
                                        commdev.passnum = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("TotalPass");
                                        commdev.hyid = (msdr2.IsDBNull(7)) ? 0 : msdr2.GetInt32("hyid");
                                        commdev.commclass = 2;
                                        _commList.Add(commdev);
                                    }
                                }

                            }
                            _maxSfjID = maxLatestSfjID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机通讯设备失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化控制设备
                    try
                    {
                        sSensorSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSensorSQL = "SELECT MAX(id) as maxid FROM sfyth_device";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn))
                            {
                                maxLatestSfjControlID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机控制器最大ID失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxLatestSfjControlID > _maxSfjControlID)
                            {
                                //如果有新设备
                                sSensorSQL = "SELECT a.ID,a.Device_Name,a.Device_Address,a.Device_Type,a.PLC_Number FROM sfyth_device a ";
                                sSensorSQL += "LEFT JOIN sfyth_plc b ON a.PLC_Number = b.PLC_Number WHERE a.Is_Delete = 0 AND b.Is_Delete = 0";
                                sSensorSQL += "where ID > '" + _maxSfjControlID + "' order by id asc";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSensorSQL, conn2);
                                using (MySqlDataReader msdr2 = myCmd2.ExecuteReader())
                                {
                                    while (msdr2.Read())
                                    {
                                        //
                                        //devid,devcode,devaddr,devtype,commnum,devpara,blockid,devformula,devclass
                                        ControlDevice controldev = new ControlDevice();
                                        controldev.devid = (msdr2.IsDBNull(0)) ? 0 : msdr2.GetInt32("ID");
                                        controldev.devcode = (msdr2.IsDBNull(1)) ? "" : msdr2.GetString("Device_Name");
                                        controldev.devaddr = (msdr2.IsDBNull(2)) ? 0 : msdr2.GetInt32("Device_Address");
                                        controldev.devtype = (msdr2.IsDBNull(3)) ? "" : msdr2.GetString("Device_Type");
                                        controldev.commnum = (msdr2.IsDBNull(4)) ? "" : msdr2.GetString("PLC_Number");
                                        controldev.devpara = 0;
                                        controldev.blockid = 0;
                                        controldev.devformula = "0";
                                        controldev.devclass = 20;
                                        _controlList.Add(controldev);
                                    }
                                }

                            }
                            _maxSfjControlID = maxLatestSfjControlID;
                        }
                    }
                    catch (Exception err)
                    {

                        msg = DateTime.Now.ToString() + " 初始化水肥机控制设备失败:" + err.Message;
                        EveryDayLog.Write(msg);
                    }
                }
                Thread.Sleep(28800000);
            }
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="state">接收数据的客户端会话</param>
        /// <param name="data">数据报文</param>
        public void Send(TCPClientState state, string data)
        {
            try
            {
                if (state.clientStatus == 2 && state.TcpClient.Client.Connected)
                {
                    //byte[] a = System.Text.Encoding.Default.GetBytes(data);
                    byte[] a = FormatFunc.strToHexByte(data);
                    Send(state.TcpClient, a);
                }
            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 向(" + state.TcpClient.Client.RemoteEndPoint.ToString() + ")发送数据:" + data + ",发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="state">接收数据的客户端会话</param>
        /// <param name="data">数据报文</param>
        public void Send(TCPClientState state, byte[] data)
        {
            try
            {
                if (state.clientStatus == 2 && state.TcpClient.Client.Connected)
                {
                    RaisePrepareSend(state);
                    Send(state.TcpClient, data);
                }
            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 向(" + state.TcpClient.Client.RemoteEndPoint.ToString() + ")发送数据:" + FormatFunc.ByteToString(data) + ",发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }

        }

        /// <summary>
        /// 异步发送数据至指定的客户端
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="data">报文</param>
        public void Send(TcpClient client, byte[] data)
        {
            string msg = null;
            try
            {
                if (!IsRunning)
                {
                    //throw new InvalidProgramException("This TCP Scoket server has not been started.");
                    msg = DateTime.Now.ToString() + "This TCP Scoket server has not been started";
                    EveryDayLog.Write(msg);
                    return;
                }
                else if (client == null || client.Client == null)
                {
                    //throw new ArgumentNullException("client");
                    msg = DateTime.Now.ToString() + "client is null or closed";
                    EveryDayLog.Write(msg);
                    return;
                }
                else if (data == null)
                {
                    //throw new ArgumentNullException("data");
                    msg = DateTime.Now.ToString() + "data is null";
                    EveryDayLog.Write(msg);
                    return;
                }
                else if (client.Connected)
                {
                    client.GetStream().BeginWrite(data, 0, data.Length, SendDataEnd, client);
                    msg = DateTime.Now.ToString() + " 向(" + client.Client.RemoteEndPoint.ToString() + ")发送数据:" + FormatFunc.byteToHexStr(data);
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " 向(" + client.Client.RemoteEndPoint.ToString() + ")发送数据:" + FormatFunc.byteToHexStr(data) + ",发生错误:" + err.Message;
                EveryDayLog.Write(msg);
            }

        }

        /// <summary>
        /// 发送数据完成处理函数
        /// </summary>
        /// <param name="ar">目标客户端Socket</param>
        private void SendDataEnd(IAsyncResult ar)
        {
            ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
            RaiseCompletedSend(null);
        }
        //private void SendDataEnd(IAsyncResult ar)
        //{
        //    int bytesSent = 0;
        //    // Retrieve the socket from the state object.     
        //    TcpClient handler = (TcpClient)ar.AsyncState;
        //    // Complete sending the data to the remote device.     
        //    bytesSent = handler.Client.EndSend(ar);

        //    //handler.Shutdown(SocketShutdown.Both);
        //    //handler.Close();
        //}
        #endregion


        #region 客户端数据处理
        ///<summary>
        ///
        /// </summary>
        /// 
        public void PLC_handlerecv(CommDevice comm, string sdata)
        {
            //粘包处理
            ////if (sdata.Substring(sdata.Length - 4).Replace(" ", "").Contains(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)))) ;
            ////{ 
            ////}
            //#0000201910180001 020503e8ff000c79 020f 0c50(addr) 0010(out num) 02(byte num) 3f02(数据) a6b1(CRC验证)
            string toHandleData = null;
            string sSensorSQL = null;
            string sOneState = null;
            int iLen = 0;
            string msg = null;
            if (sdata.Substring(2, 2).ToUpper().Equals("0F"))
            {
                toHandleData = sdata.Substring(14, sdata.Length - 18);
                iLen = Convert.ToInt16(sdata.Substring(8, 4), 16);
            }
            //数据粘包
            else if (sdata.Substring(2, 2).Equals("05") && (sdata.Length > 34 && sdata.Substring(18, 2).ToUpper().Equals("0F")))
            {

                toHandleData = sdata.Substring(30, sdata.Length - 34);
                iLen = Convert.ToInt16(sdata.Substring(24, 4), 16);


            }
            if (toHandleData != null)
            {
                sSensorSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                string devstate = null;
                for (int i = 0; i * 2 < toHandleData.Length; i++)
                {
                    //状态按寄存器顺序，小端模式:低字节在前，高字节在后,每个字节再转为低位在前，形成从小到大的开关量
                    devstate = devstate + FormatFunc.reverseString(FormatFunc.HexString2BinString(toHandleData.Substring(2 * i, 2), true, true));
                }
                for (int i = 0; i < iLen; i++)
                {
                    try
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            if (oneControl.devtype.ToUpper().Equals("1"))
                            {
                                if (devstate.Substring(i, 1).ToUpper().Equals("1"))
                                {
                                    sOneState = "1";
                                }
                                else if (devstate.Substring(i, 1).ToUpper().Equals("0"))
                                {
                                    sOneState = "2";

                                }

                            }
                            //行程开关，开-基数端口，关-偶数端口
                            else if (oneControl.devtype.ToUpper().Equals("3"))
                            {
                                if (devstate.Substring(i, 1).ToUpper().Equals("1") && devstate.Substring(i + 1, 1).ToUpper().Equals("0"))
                                {
                                    sOneState = "1";
                                    i += 1;
                                }
                                else if (devstate.Substring(i, 1).ToUpper().Equals("0") && devstate.Substring(i + 1, 1).ToUpper().Equals("1"))
                                {
                                    sOneState = "2";
                                    i += 1;

                                }
                                else if (devstate.Substring(i, 1).ToUpper().Equals("0") && devstate.Substring(i + 1, 1).ToUpper().Equals("0"))
                                {
                                    sOneState = "3";
                                    i += 1;
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 行程开关状态数据解析失败:开和关同时为开启状态";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }

                            }
                            sSensorSQL += "('" + oneControl.devid + "','" + sOneState + "'),";
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 行程开关状态数据解析失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                }
                if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                {
                    sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " PLC通讯设备(" + comm.serial_num + ")更新状态成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " PLC通讯设备(" + comm.serial_num + ")更新状态失败:" + sSensorSQL;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    finally
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            conn.Close();
                        }
                    }
                }
            }
        }


        public void KLC_handlerecv(CommDevice comm, string sdata)
        {
            // sdata='010320018367de022304e90233000103238fb0033309d81423aa8b143300060483666c'
            int iLen = Convert.ToInt16(sdata.Substring(4, 2), 16);
            string sAddr = sdata.Substring(0, 2);
            string sOrder = sdata.Substring(2, 2);
            string datatemp = sdata.Substring(6, sdata.Length - 6);
            if (iLen * 2 == (sdata.Length - 6))
            {
                string sSensorSQL = null;
                string msg = null;
                //传感器数据
                if (sAddr.Equals("01") && sOrder.Equals("03"))
                {
                    sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                    for (int i = 0; i < 8; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);

                        if (oneDev != null)
                        {
                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            string sensorRecv = datatemp.Substring(0, 8);
                            datatemp = datatemp.Substring(8, datatemp.Length - 8);
                            string sensorFormat = ("00000000" + FormatFunc.HexString2BinString(sensorRecv.Substring(2, 2), true, true));
                            sensorFormat = sensorFormat.Substring(sensorFormat.Length - 8);
                            int signalofdata = 0;   //有无符号，0-无，1-有
                            int typeofdata = 0;    //数值类型，0-数值，1-开关
                            int longdata = 0;   //长整数数值标识，0-双字节，1-四字节
                            int highofdata = 0;     //四字节数字节标识，0-低2字节，1-高2字节
                            int dotbit = 0;     //小数位数
                            string sensorData = null;
                            double sensorValue = 0;  //传感器真实值
                            string sSensorValue = null;
                            try
                            {
                                if (sensorFormat.Substring(0, 1).Equals("0"))
                                {
                                    signalofdata = 0;   //无符号数
                                }
                                else
                                {
                                    signalofdata = 1;
                                }
                                if (sensorFormat.Substring(1, 1).Equals("0"))
                                {
                                    typeofdata = 0;     //数字量
                                }
                                else
                                {
                                    typeofdata = 1;
                                }
                                if (sensorFormat.Substring(2, 1).Equals("0"))
                                {
                                    longdata = 0;       //双字节
                                }
                                else
                                {
                                    longdata = 1;
                                }

                                if (sensorFormat.Substring(3, 1).Equals("0"))
                                {
                                    highofdata = 0;     //小端模式，低位在前
                                }
                                else
                                {
                                    highofdata = 1;     //大端模式，高位在前      
                                }
                                dotbit = Convert.ToInt32(FormatFunc.BinByteToDecString("00000" + sensorFormat.Substring(5, 3), 8)[0]);
                                if (longdata == 0) //2个字节
                                {
                                    sensorData = sensorRecv.Substring(4, 4);
                                    if (signalofdata == 0)
                                    {
                                        sensorValue = Convert.ToInt16(sensorData, 16) / Math.Pow(10, dotbit);
                                    }
                                    else
                                    {
                                        sensorValue = Convert.ToInt16(sensorData, 16) / Math.Pow(10, dotbit);
                                    }

                                }
                                else //四个字节
                                {
                                    if (highofdata == 0)
                                    {
                                        sensorData = sdata.Substring(i * 8 + 18, 4) + sensorRecv.Substring(4, 4);

                                    }
                                    else
                                    {
                                        sensorData = sensorRecv.Substring(4, 4) + sdata.Substring(i * 8 + 18, 4);
                                    }
                                    if (signalofdata == 0)
                                    {
                                        sensorValue = Convert.ToInt32(sensorData, 16) / Math.Pow(10, dotbit);
                                    }
                                    else
                                    {
                                        sensorValue = Convert.ToInt32(sensorData, 16) / Math.Pow(10, dotbit);
                                    }
                                    datatemp = datatemp.Substring(8, datatemp.Length - 8);
                                }
                            }
                            catch (Exception err)
                            {
                                msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")处理通道数据错误:" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "SHIDU":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "BEAM":
                                    sSensorValue = sensorValue.ToString("#");
                                    break;
                                case "CO2":
                                    sSensorValue = sensorValue.ToString("#");
                                    break;
                                case "Q-WENDU":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "Q-SHIDU":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "PH":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "EC":
                                    sSensorValue = sensorValue.ToString("0.##");
                                    break;
                                case "SWD":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "SWZ":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                case "ORP":
                                    sSensorValue = sensorValue.ToString("0.#");
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;
                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sSensorValue + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
                else if (sAddr.Equals("02") && sOrder.Equals("03"))
                {
                    sSensorSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                    //02 03 08 A1 40 FF FF A2 40 FF FF
                    string sstate = null;
                    for (int i = 0; i < 2; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            string recvState = sdata.Substring(i * 8 + 10, 4).ToUpper();
                            if (recvState.Equals("FFFF"))
                            {
                                sstate = "1";
                            }
                            else if (recvState.ToUpper().Equals("0000"))
                            {
                                sstate = "2";
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")获取状态错误标识:" + recvState;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                continue;
                            }
                            sSensorSQL += "('" + oneControl.devid + "','" + sstate + "'),";

                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")控制通道更新状态成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " KLC设备(" + comm.serial_num + ")控制通道更新状态失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
            }
        }
        public void FKC_handlerecv(CommDevice comm, string sdata)
        {
            string sOrder = sdata.Substring(2, 2);
            string sSensorSQL = null;
            string msg = null;
            string sensorRecv = null;
            int sensorValue;
            string sensorData = null;
            //020300207fff02ea00000000000000000000000000e400de00007fff0000003f00020290f305
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {
                //0000201912100001007000000000000000000000000000000000543F

                sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                if (sOrder.Equals("03"))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);
                        if (oneDev != null)
                        {
                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            sensorRecv = sdata.Substring(i * 4 + 8, 4);   //这是和新普惠的区别，飞科数据长度占2个字节

                            if (!(sensorRecv.ToUpper().Equals("7FFF")))
                            {
                                sensorValue = Convert.ToInt32(sensorRecv, 16);
                            }
                            else
                            {
                                continue;
                            }
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorData = (sensorValue / 10.0).ToString("0.#"); ;
                                    break;
                                case "SHIDU":
                                    sensorData = (sensorValue / 10.0).ToString("0.#"); ;
                                    break;
                                case "BEAM":
                                    sensorData = (sensorValue * 10.0).ToString();
                                    break;
                                case "CO2":
                                    sensorData = sensorValue.ToString();
                                    break;
                                case "Q-WENDU":
                                    sensorData = (sensorValue / 10.0).ToString("0.#"); ;
                                    break;
                                case "Q-SHIDU":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "PH":
                                    sensorData = (sensorValue / 100.0).ToString("0.##");
                                    break;
                                case "EC":
                                    sensorData = (sensorValue / 100.0).ToString("0.##");
                                    break;
                                case "FS":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "FX":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "YL":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "QY":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "SWD":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "SWZ":
                                    sensorData = (sensorValue / 100.0).ToString("0.##");
                                    break;
                                case "ORP":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "SPH":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "SEC":
                                    sensorData = (sensorValue / 1000.0).ToString("0.###");
                                    break;
                                case "VOC":
                                    sensorData = (sensorValue / 10.0).ToString("0.#");
                                    break;
                                case "PM25":
                                    sensorData = sensorValue.ToString();
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;

                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorData + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
                //0000201912100001 007000000000000000000000000000000000543F
                else if (sOrder.Equals("70"))
                {
                    sSensorSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                    for (int i = 0; i < 16; i++)
                    {
                        string sstate = null;
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            if (sdata.Substring(i * 2 + 4, 2).Equals("01"))
                            {
                                sstate = "1";
                            }
                            else if (sdata.Substring(i * 2 + 4, 2).Equals("00"))
                            {
                                sstate = "2";
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")获取状态错误标识:" + sdata.Substring(i * 2 + 4, 2);
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                continue;
                            }
                            sSensorSQL += "('" + oneControl.devid + "','" + sstate + "'),";
                        }

                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")控制通道更新状态成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " FKC设备(" + comm.serial_num + ")控制通道更新状态失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }

                }
            }
        }


        public void XPC_handlerecv(CommDevice comm, string sdata)
        {
            //020300207fff02ea00000000000000000000000000e400de00007fff0000003f00020290f305
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {
                string sOrder = sdata.Substring(2, 2);
                string sSensorSQL = null;
                string msg = null;
                if (sOrder.Equals("03"))
                {
                    sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                    string sensorRecv = null;
                    double sensorValue = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);
                        if (oneDev != null)
                        {
                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            sensorRecv = sdata.Substring(i * 4 + 6, 4);   //这是和飞科的区别，新普惠数据长度占1个字节
                            if (!(sensorRecv.ToUpper().Equals("7FFF")))
                            {
                                sensorValue = Convert.ToInt32(sensorRecv, 16);
                            }
                            else
                            {
                                continue;
                            }
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SHIDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "BEAM":
                                    sensorValue = sensorValue * 10.0;
                                    break;
                                case "CO2":
                                    break;
                                case "Q-WENDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "Q-SHIDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "PH":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "EC":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "FS":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "FX":
                                    break;
                                case "YL":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "QY":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SWD":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SWZ":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "ORP":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SPH":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SEC":
                                    sensorValue = sensorValue / 1000.0;
                                    break;
                                case "VOC":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "PM25":
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " XPC设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    continue;

                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " XPC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " XPC设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
            }
        }
        public void XPH_handlerecv(CommDevice comm, string sdata)
        {
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {
                string sOrder = sdata.Substring(2, 2);
                string sSensorSQL = null;
                sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                string sControlSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                string msg = null;

                if (sOrder.Equals("03"))
                {
                    string sensorRecv = null;
                    double sensorValue = 0;
                    //string sensorData = null;
                    string controlData = null;
                    int controlValue = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);
                        if (oneDev != null)
                        {
                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            sensorRecv = sdata.Substring(i * 4 + 8, 4);   //这是和新普惠的区别，飞科数据长度占2个字节

                            if (!(sensorRecv.ToUpper().Equals("7FFF")))
                            {
                                sensorValue = Convert.ToInt32(sensorRecv, 16);
                            }
                            else
                            {
                                continue;
                            }
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SHIDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "BEAM":
                                    sensorValue = sensorValue * 10.0;
                                    break;
                                case "CO2":
                                    break;
                                case "Q-WENDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "Q-SHIDU":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "PH":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "EC":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "FS":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "FX":
                                    break;
                                case "YL":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "QY":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SWD":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SWZ":
                                    sensorValue = sensorValue / 100.0;
                                    break;
                                case "ORP":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SPH":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "SEC":
                                    sensorValue = sensorValue / 1000.0;
                                    break;
                                case "VOC":
                                    sensorValue = sensorValue / 10.0;
                                    break;
                                case "PM25":
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;
                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }

                    for (int j = 0; j < 32; j++)
                    {
                        Object oneDev = findDevbyAddr(j + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            controlData = sdata.Substring(j * 2 + 72, 2);   //这是和飞科的区别，控制状态和采集状态一起采集

                            if (controlData.Equals("00"))
                            {
                                controlValue = 1;
                            }
                            else if (controlData.Equals("01"))
                            {
                                controlValue = 2;
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " XPH控制设备(" + oneControl.devcode + ")采集错误控制状态:" + controlData;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            sControlSQL += "('" + oneControl.devid + "','" + controlValue.ToString() + "'),";
                        }
                    }
                    if (sControlSQL.Substring(sControlSQL.Length - 2).Equals("),"))
                    {

                        sControlSQL = sControlSQL.Substring(0, sControlSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";

                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sControlSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新状态成功:" + sControlSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新状态失败:" + sControlSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
                else if (sOrder.Equals("10"))
                {
                    int startAddr = 0;
                    int numControl = 0;
                    string stateStr = null;
                    startAddr = Convert.ToInt32(sdata.Substring(8, 2));
                    numControl = Convert.ToInt32(sdata.Substring(10, 2));
                    stateStr = sdata.Substring(12, sdata.Length - 12);
                    sSensorSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                    string singleState = null;
                    int controlState = 0;
                    if (numControl > 0)
                    {
                        int i = 0;
                        while (true)
                        {
                            Object oneDev = findDevbyAddr((startAddr + i) + 1, comm.serial_num, 10);
                            if (oneDev != null)
                            {
                                ControlDevice device2 = (ControlDevice)oneDev;
                                singleState = stateStr.Substring(i * 2, 2);
                                if (singleState.Equals("00"))
                                {
                                    controlState = 2;
                                }
                                else if (singleState.Equals("01"))
                                {
                                    controlState = 1;
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " XPH控制设备(" + device2.devcode + ")采集控制状态错误:" + singleState;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                                sSensorSQL += "('" + device2.devid.ToString() + "','" + controlState.ToString() + "'),";

                            }
                            i++;
                        }
                    }

                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                        MySqlConnection connection = new MySqlConnection(_connectStr);
                        MySqlCommand command2 = new MySqlCommand(sSensorSQL, connection);
                        connection.Open();
                        try
                        {
                            if (command2.ExecuteNonQuery() > 0)
                            {
                                msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新状态成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception exception2)
                        {
                            msg = DateTime.Now.ToString() + " XPH设备(" + comm.serial_num + ")插入最新状态失败:" + sSensorSQL + ",错误：" + exception2.Message;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (connection.State == ConnectionState.Open)
                            {
                                connection.Close();
                            }
                        }
                    }
                }

            }


        }

        public void YYC_handlerecv(CommDevice comm, string sdata)
        {
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {
                string sOrder = sdata.Substring(2, 2);
                string sSensorSQL = null;
                string msg = null;
                if (sOrder.Equals("03"))
                {
                    sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                    string sensorRecv = null;
                    double sensorValue = 0;
                    string sensorData = null;
                    for (int i = 0; i < 16; i++)
                    {
                        sensorRecv = sdata.Substring(i * 8 + 10, 4) + sdata.Substring(i * 8 + 6, 4);
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);
                        if (oneDev != null)
                        {

                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            sensorValue = FormatFunc.hextofloat(sensorRecv);
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "SHIDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "BEAM":
                                    sensorData = sensorValue.ToString("f0");
                                    break;
                                case "CO2":
                                    sensorData = sensorValue.ToString("f0");
                                    break;
                                case "Q-WENDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "Q-SHIDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "PH":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "EC":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "FS":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "FX":
                                    sensorData = sensorValue.ToString("f0");
                                    break;
                                case "YL":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "QY":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "SWD":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "SWZ":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "ORP":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "SPH":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "SEC":
                                    sensorData = sensorValue.ToString("f3");
                                    break;
                                case "VOC":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "PM25":
                                    sensorData = sensorValue.ToString("f0");
                                    break;
                                case "Y-WENDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "Y-SHIDU":
                                    sensorData = sensorValue.ToString("f1");
                                    break;
                                case "GS-D":
                                    sensorData = sensorValue.ToString("f2");
                                    break;
                                case "JG-D":
                                    sensorData = sensorValue.ToString("f2");
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " YYC设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;

                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorData + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " YYC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " YYC设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }
                }
            }
        }


        public void SFJ_handlerecv(CommDevice comm, string sdata)
        {
            //if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))

            string sOrder = sdata.Substring(2, 2);
            string sSensorSQL = null;
            string msg = null;
            if (sOrder.Equals("10") && sdata.Length > 30)
            {
                string oneData = null;
                int[] tasklog = new int[32];
                for (int i = 0; i < 32; i++)
                {
                    tasklog[i] = Convert.ToInt16(sdata.Substring(i * 4 + 14, 4), 16);
                }
                string isRemote = tasklog[0].ToString();
                string taskArea = tasklog[1].ToString();
                int taskstate = tasklog[2];
                double water_set = tasklog[3] / 10;
                int inteval_set = tasklog[4];
                double ferter_set = tasklog[5] / 10;
                string tasktype = tasklog[12].ToString();
                string taskid = tasklog[16].ToString();
                string taskdate = tasklog[17].ToString() + "-" + rightSub(("00" + tasklog[18].ToString()), 2) + '-' + rightSub(("00" + tasklog[19].ToString()), 2);
                string starttime = rightSub("00" + tasklog[20].ToString(), 2) + ":" + rightSub("00" + tasklog[21].ToString(), 2) + ":" + rightSub("00" + tasklog[22].ToString(), 2);
                string endtime = rightSub("00" + tasklog[26].ToString(), 2) + ":" + rightSub("00" + tasklog[27].ToString(), 2) + ":" + rightSub("00" + tasklog[28].ToString(), 2);
                double water_real = tasklog[29] / 10;
                int interval_real = tasklog[30];
                double ferter_real = tasklog[31] / 10;
                string sArea = null;
                string sFertcomm = null;
                string bitArea = rightSub("000000000000000" + Convert.ToString((Convert.ToInt32(taskArea)), 2), 16);
                bitArea = FormatFunc.reverseString(bitArea);
                if (comm.commtype.ToUpper().Equals("SFJ-0804"))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (bitArea.Substring(i, 1).Equals("1"))
                        {
                            sArea = sArea + bitArea.Substring(i, 1) + ",";
                        }
                    }
                    if (sArea.Substring(sArea.Length - 1).Equals(","))
                    {
                        sArea = sArea.Substring(0, sArea.Length - 1);
                    }
                    if (tasktype.Equals("1"))
                    {
                        if (bitArea.Substring(8, 1).Equals("1"))
                        {
                            sFertcomm = sFertcomm + "A,";
                        }
                        if (bitArea.Substring(9, 1).Equals("1"))
                        {
                            sFertcomm = sFertcomm + "B,";
                        }
                        if (bitArea.Substring(10, 1).Equals("1"))
                        {
                            sFertcomm = sFertcomm + "C,";
                        }
                        if (bitArea.Substring(11, 1).Equals("1"))
                        {
                            sFertcomm = sFertcomm + "D,";
                        }
                        if (sFertcomm.Substring(sFertcomm.Length - 1).Equals(","))
                        {
                            sFertcomm = sFertcomm.Substring(0, sFertcomm.Length - 1);
                        }

                    }
                }
                else if (comm.commtype.ToUpper().Equals("SFJ-1200"))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (bitArea.Substring(i, 1).Equals("1"))
                        {
                            sArea = sArea + (i + 1).ToString() + ",";
                        }
                    }
                    if (sArea.Substring(sArea.Length - 1).Equals(","))
                    {
                        sArea = sArea.Substring(0, sArea.Length - 1);
                    }
                }
                if (taskstate > 0 && taskstate < 3)
                {
                    sSensorSQL = "INSERT INTO sfyth_log (R_Start,R_End,R_Interval,R_Gquantity,R_Squantity,T_Area,T_Fertilize,R_State,T_Date,T_ID,T_Type,Company_ID,PLC_Number,DO_Type) ";
                    sSensorSQL += "values('" + starttime + "','" + endtime + "','" + interval_real.ToString() + "','" + water_real.ToString() + "','" + ferter_real.ToString() + "','" + sArea + "',";
                    sSensorSQL += "'" + sFertcomm + "','" + taskstate.ToString() + "','" + taskdate + "','" + taskid.ToString() + "','" + tasktype.ToString() + "','" + comm.companyid + "','" + comm.serial_num + "','" + isRemote.ToString() + "')";
                }

                MySqlConnection conn = new MySqlConnection(_connectStr);
                MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                conn.Open();
                try
                {
                    int iSQLResult = myCmd.ExecuteNonQuery();
                    if (iSQLResult > 0)
                    {
                        msg = DateTime.Now.ToString() + " SFJ设备(" + comm.serial_num + ")插入新的任务日志成功:" + sSensorSQL;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " SFJ设备(" + comm.serial_num + ")插入插入新的任务日志失败:" + sSensorSQL;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
                finally
                {
                    if (conn.State == ConnectionState.Open)
                    {
                        conn.Close();
                    }
                }
            }
            //#0000201910180001 020503e8ff000c79 020f0c500010023f02a6b1指令回传
            else if (sOrder.ToUpper() == "0F" || (sOrder == "05" && sdata.Length > 52 && sdata.Substring(34, 2).ToUpper() == "0F"))
            {
                sSensorSQL = "insert into sfyth_device(id,State) values ";
                string devstate = sdata.Substring(sdata.Length - 6, 2) + sdata.Substring(sdata.Length - 8, 2);
                devstate = FormatFunc.HexString2BinString(devstate, true, true);
                devstate = rightSub("0000000000000000" + FormatFunc.reverseString(devstate), 16);
                for (int i = 0; i < 16; i++) //控制器扩展？？？
                {
                    object oneDev = findDevbyAddr(i + 1, comm.serial_num, 20);
                    if (oneDev != null)
                    {
                        ControlDevice oneControl = (ControlDevice)oneDev;
                        sSensorSQL += "('" + oneControl.devid + "','" + devstate[i].ToString() + "'),";
                    }

                }
                if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                {
                    //sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                    sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE State = values(State)";
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " SFJ设备(" + comm.serial_num + ")更新状态成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " SFJ设备(" + comm.serial_num + ")更新状态失败:" + sSensorSQL;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    finally
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            conn.Close();
                        }
                    }
                }
            }

        }

        //打药系统，没收传感器数据
        public void DYC_handlerecv(CommDevice comm, string sdata)
        {

            string sOrder = sdata.Substring(2, 2).ToUpper();
            string sSensorSQL = null;
            string msg = null;
            string toHandleData = null;
            int iLen = 0;
            string sOneState = null;
            //打药系统数据采集臭氧浓度
            if (sOrder.Equals("03") && sdata.Length == 44)
            {
                sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                string sensorRecv = sdata.Substring(6, 4);
                double sensorValue = Convert.ToInt32(sensorRecv, 16);
                object oneDev = findDevbyAddr(1, comm.serial_num, 11);
                if (oneDev != null)
                {
                    SensorDevice oneSensor = (SensorDevice)oneDev;
                    sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                }

                if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                {
                    sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " DYC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " DYC设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL + ",错误：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    finally
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            conn.Close();
                        }
                    }
                }
            }

            if (sOrder.Equals("0F"))
            {
                toHandleData = sdata.Substring(14, sdata.Length - 18);
                iLen = Convert.ToInt16(sdata.Substring(8, 4), 16);
            }
            //数据粘包
            else if (sOrder.Equals("05") && (sdata.Length > 34 && sdata.Substring(18, 2).ToUpper().Equals("0F")))
            {

                toHandleData = sdata.Substring(30, sdata.Length - 34);
                iLen = Convert.ToInt16(sdata.Substring(24, 4), 16);
            }
            if (toHandleData != null)
            {
                sSensorSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                string devstate = null;
                for (int i = 0; i * 2 < toHandleData.Length; i++)
                {
                    //状态按寄存器顺序，小端模式:低字节在前，高字节在后,每个字节再转为低位在前，形成从小到大的开关量
                    devstate = devstate + FormatFunc.reverseString(FormatFunc.HexString2BinString(toHandleData.Substring(2 * i, 2), true, true));
                }
                for (int i = 0; i < iLen; i++)
                {
                    try
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            if (oneControl.devtype.Equals("1"))
                            {
                                if (devstate.Substring(i, 1).Equals("1"))
                                {
                                    sOneState = "1";
                                }
                                else if (devstate.Substring(i, 1).Equals("0"))
                                {
                                    sOneState = "2";

                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 打药设备状态数据非法:" + devstate.Substring(i, 1);
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    continue;
                                }

                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " 打药设备控制通道类型非法:" + oneControl.devtype;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                continue;
                            }
                            sSensorSQL += "('" + oneControl.devid + "','" + sOneState + "'),";
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 打药设备状态数据解析失败:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                }
                if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                {
                    sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " 打药设备(" + comm.serial_num + ")更新状态成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 打药设备(" + comm.serial_num + ")更新状态失败:" + sSensorSQL;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    finally
                    {
                        if (conn.State == ConnectionState.Open)
                        {
                            conn.Close();
                        }
                    }
                }
            }

        }

        public bool MQTT_ini(string mqtt_host, string mqtt_port)
        {
            bool bFlag = false;
            string msg;
            try
            {
                // 实例化Mqtt客户端 
                mqtt_client = new MqttClient(mqtt_host, Convert.ToInt32(mqtt_port), false, null, null, MqttSslProtocols.None);
                // 注册接收消息事件 
                mqtt_client.MqttMsgPublishReceived += MQT_handlerecv;
                string clientId = Guid.NewGuid().ToString();
                mqtt_client.Connect(clientId, "hongshanlv", "12345678");
                msg = DateTime.Now.ToString() + " MQTT连接服务器(" + mqtt_host + ":" + mqtt_port + ")成功";
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return true;

            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " MQTT连接服务器(" + mqtt_host + ":" + mqtt_port + ")失败:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return false;
            }

        }


        public void MQTT_Connect(string comm_sn)
        {

            string topic = null;

            //订阅主题 "/mqtt/test"， 订阅质量 QoS 2
            topic = "stds/up/CL/" + comm_sn;
            int msgID = 0;
            msgID = mqtt_client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            if (msgID > 0)
            {
                string msg = DateTime.Now.ToString() + " MQTT设备(" + comm_sn + ")订阅成功，消息ID:" + msgID.ToString();
                addClientEvent(comm_sn);
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }

            //topic = "stds/up/CL/+";
            //client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }

        public void HY_Connect()
        {
            try
            {
                hy_mqtt_client = new MqttClient("mqtt.hanclouds.com");
                string clientId = "d:" + hyproductkey + ":" + hydevicesn;
                hy_mqtt_client.MqttMsgPublishReceived += new MqttClient.MqttMsgPublishEventHandler(HY_MqttMsgPublishReceived);
                string result = hy_mqtt_client.Connect(clientId, hyproductkey, hyaccesskey + ":" + hyaccesssecret, true, 180).ToString();
                string msg = null;
                if ((result.Length <= 0) || !result.Equals("0"))
                {
                    msg = "瀚云物联网设备登录失败，失败返回代码：" + result;
                    ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                    EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                }
                else
                {
                    msg = "瀚云物联网设备登录成功,返回代码：" + result;
                    ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                    EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                }
            }
            catch (Exception err)
            {
                string msg = "瀚云物联网设备登录发生错误：" + err.Message;
                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
            }
        }

        public void InitHYPlat()
        {
            if ((hy_mqtt_host.Length > 0) && (hy_mqtt_port.Length > 0))
            {
                try
                {
                    using (MySqlConnection initHYConn = new MySqlConnection(_connectStr))
                    {
                        if (initHYConn.State == ConnectionState.Closed)
                        {
                            initHYConn.Open();
                        }
                        using (MySqlDataReader reader = new MySqlCommand(((((("SELECT gl.id AS glid,gl.hy_devicekey AS devicekey ,gl.hy_sn AS devicesn,hy_p.product_key AS productkey, " + "hy_p.access_key AS accesskey,hy_p.access_secret AS accesssecret,hy_p.stream_sign AS streamsign, " + "ky_c.id AS kycommid,ky_c.SerialNumber AS kyserial,gl.hy_devicename AS devicename FROM yw_d_hanyun_gl gl ") + "LEFT JOIN yw_d_hanyun_product hy_p ON gl.hy_produce_id = hy_p.id " + "LEFT JOIN yw_d_commnication_tbl ky_c ON gl.ky_commid = ky_c.ID ") + "WHERE hy_produce_id IS NOT NULL AND ky_commid IS NOT NULL AND gl.hy_devicename != 'sfj' " + "UNION ") + "SELECT gl.id AS glid,gl.hy_devicekey AS devicekey ,gl.hy_sn AS devicesn,hy_p.product_key AS productkey, " + "hy_p.access_key AS accesskey,hy_p.access_secret AS accesssecret,hy_p.stream_sign AS streamsign, ") + "sfj.id AS kycommid,sfj.PLC_Number AS kyserial,gl.hy_devicename AS devicename FROM yw_d_hanyun_gl gl " + "LEFT JOIN yw_d_hanyun_product hy_p ON gl.hy_produce_id = hy_p.id ") + "LEFT JOIN sfyth_plc sfj ON gl.ky_commid = sfj.ID " + "WHERE hy_produce_id IS NOT NULL AND ky_commid IS NOT NULL AND gl.hy_devicename = 'sfj' ", initHYConn).ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                HY_CommInfo item = new HY_CommInfo
                                {
                                    deviceID = reader.IsDBNull(0) ? "" : reader.GetString("glid"),
                                    deviceKey = reader.IsDBNull(1) ? "" : reader.GetString("devicekey"),
                                    deviceSN = reader.IsDBNull(2) ? "" : reader.GetString("devicesn"),
                                    productKey = reader.IsDBNull(3) ? "" : reader.GetString("productkey"),
                                    accessKey = reader.IsDBNull(4) ? "" : reader.GetString("accesskey"),
                                    accessSecret = reader.IsDBNull(5) ? "" : reader.GetString("accesssecret"),
                                    streamTag = reader.IsDBNull(6) ? "" : reader.GetString("streamsign"),
                                    kyCommID = reader.IsDBNull(7) ? 0 : reader.GetInt32("kycommid"),
                                    kyCommSerial = reader.IsDBNull(8) ? "" : reader.GetString("kyserial"),
                                    deviceName = reader.IsDBNull(9) ? "" : reader.GetString("devicename"),
                                    mqttClient = null,
                                    isOnline = 0
                                };
                                _hyCommList.Add(item);
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    EveryDayLog.Write(DateTime.Now.ToString() + " 瀚云设备初始化:" + err.Message);
                }
            }
        }

        public void MQT_handlerecv(object sender, MqttMsgPublishEventArgs e)
        {
            string a = (string.Format("subscriber,topic:{0},content:{1}", e.Topic, Encoding.UTF8.GetString(e.Message)));
            string mqtttop = e.Topic;
            string mqttpayload = Encoding.UTF8.GetString(e.Message);
            string[] mqtttoparray = mqtttop.Split('/');
            string commsn = mqtttoparray[mqtttoparray.Length - 1];
            string msg = null;
            string sHYSendStr1 = null;
            string sHYSendStr2 = null;
            string sHYSendStr3 = null;
            string sHYSendStr4 = null;

            msg = DateTime.Now.ToString() + " MQTT设备(" + commsn + ")接受数据:" + mqttpayload;
            ShowMsgEvent(msg);
            EveryDayLog.Write(msg);


            string sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
            CommDevice oneComm = getCommbySN(commsn, 1);
            if (oneComm != null)
            {
                int subGroupID = 0;
                string sSensorValue = null;
                Dictionary<string, string> payloadDict = FormatFunc.JsonToDictionary(mqttpayload);
                subGroupID = Convert.ToInt32(payloadDict["id"]);
                if (subGroupID > 0)
                {
                    foreach (KeyValuePair<string, string> kvp in payloadDict)
                    {
                        int _sensorIndex = getSensorbyFoumula(kvp.Key, commsn, subGroupID);
                        if (_sensorIndex >= 0)
                        {

                            switch (_sensorList[_sensorIndex].devformula)
                            {
                                case "airH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"Temp@1\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"Temp@1\":{\"name\":\"空气温度\",\"type\":3},";
                                    break;
                                case "airT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"RH@1\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"RH@1\":{\"name\":\"空气湿度\",\"type\":3},";
                                    break;
                                case "ill":
                                    sSensorValue = kvp.Value;
                                    sHYSendStr2 = sHYSendStr2 + "\"lightIntensity\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"lightIntensity\":{\"name\":\"光照强度\",\"type\":5},";
                                    break;
                                case "soilH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"RH@2\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"RH@2\":{\"name\":\"土壤湿度\",\"type\":2},";
                                    break;
                                case "soilT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"Temp@2\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"Temp@2\":{\"name\":\"土壤温度\",\"type\":3},";
                                    break;
                                case "soilC":
                                    sSensorValue = kvp.Value;
                                    sHYSendStr2 = sHYSendStr2 + "\"EC\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"EC\":{\"name\":\"土壤电导率\",\"type\":33},";
                                    break;
                                case "co2":
                                    sSensorValue = kvp.Value;
                                    sHYSendStr2 = sHYSendStr2 + "\"CO2\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"CO2\":{\"name\":\"二氧化碳\",\"type\":6},";
                                    break;
                                case "dPH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"PH\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"PH\":{\"name\":\"土壤PH\",\"type\":1},";
                                    break;
                                case "DO":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"O2\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"O2\":{\"name\":\"溶解氧\",\"type\":27},";
                                    break;
                                case "level":
                                    sSensorValue = kvp.Value;
                                    sHYSendStr2 = sHYSendStr2 + "\"level\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"level\":{\"name\":\"水位\"},";
                                    break;
                                case "LiquidT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"Temp@3\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"Temp@3\":{\"name\":\"水温\",\"type\":29},";
                                    break;
                                case "windS":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"speed\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"speed\":{\"name\":\"风速\",\"type\":9},";
                                    break;
                                case "windD":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"direction\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"direction\":{\"name\":\"风向\",\"type\":10},";
                                    break;
                                case "atm":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"pressure\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"pressure\":{\"name\":\"大气压力\",\"type\":4},";
                                    break;
                                case "rainF":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"fall\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"fall\":{\"name\":\"雨量\",\"type\":11},";
                                    break;
                                case "sDur":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"sDur\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"sDur\":{\"name\":\"日照时数\"},";
                                    break;
                                case "eCap":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.01).ToString("#.##");
                                    sHYSendStr2 = sHYSendStr2 + "\"eCap\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"eCap\":{\"name\":\"蒸发量\"},";
                                    break;
                                case "LiquidC":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"LiquidC\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"LiquidC\":{\"name\":\"液体电导率\",\"type\":33},";
                                    break;
                                case "LiquidP":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"LiquidP\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"LiquidP\":{\"name\":\"液体PH\",\"type\":28},";
                                    break;
                                case "ORP":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"ORP\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"ORP\":{\"name\":\"氧化还原电位\",\"type\":32},";
                                    break;
                                case "wPH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.01).ToString("#.#");
                                    sHYSendStr2 = sHYSendStr2 + "\"LiquidP\":" + sSensorValue.ToString() + ",";
                                    sHYSendStr4 = sHYSendStr4 + "\"LiquidP\":{\"name\":\"液体PH\",\"type\":28},";
                                    break;
                                default:
                                    break;
                            }
                            sSensorSQL = sSensorSQL + "('" + _sensorList[_sensorIndex].devid.ToString() + "','" + _sensorList[_sensorIndex].devcode + "', Now() ,'" + sSensorValue + "','" + _sensorList[_sensorIndex].blockid.ToString() + "'),";
                        }
                    }
                    if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                    {
                        sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " MQT设备(" + commsn + ")插入最新数据成功:" + sSensorSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " MQT设备(" + commsn + ")插入最新数据失败:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open)
                            {
                                conn.Close();
                            }
                        }
                    }

                    if (oneComm.hyid > 0)
                    {
                        HY_CommInfo hyComm = findHYCommbySn(oneComm.serial_num, false);
                        if ((hyComm != null) && (hy_mqtt_client != null))
                        {
                            sHYSendStr1 = "{\"devs\":{\"" + hyComm.deviceName + "\":{\"app\":{";
                            sHYSendStr3 = "},\"metadata\":{\"name\":\"" + hyComm.deviceName + "\",\"type\":21,\"props\":{";
                            string content = sHYSendStr1 + sHYSendStr2.Substring(0, sHYSendStr2.Length - 1) + sHYSendStr3;
                            content += sHYSendStr4.Substring(0, sHYSendStr4.Length - 1) + "}}}}}";
                            if (content.Length > 0)
                            {
                                HY_PublishInfo(hyComm, content);
                                ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送气象站或土壤数据:" + content);
                                EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送气象站或土壤数据:" + content);
                            }
                        }
                    }


                }


            }
        }

        private void HY_SFJ_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string str = "未接入数据";
            try
            {
                string msg = null;
                string strHYRecv = Encoding.UTF8.GetString(e.Message);
                ShowMsgEvent(DateTime.Now.ToString() + " 接受瀚云平台水肥机指令：" + strHYRecv);
                EveryDayLog.Write(DateTime.Now.ToString() + " 接受瀚云平台水肥机指令：" + strHYRecv);
                JObject jHYObj = (JObject)JsonConvert.DeserializeObject(strHYRecv);
                CommandInfo oneCmd = null;
                if (jHYObj["app"] == null)
                {
                    if (jHYObj["devs"] != null)
                    {
                        if (jHYObj["devs"]["16"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["16"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["16"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["16"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 16;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["8"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["8"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["8"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["8"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 8;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["7"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["7"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["7"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["7"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 7;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["6"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["6"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["6"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["6"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 6;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["5"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["5"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["5"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["5"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 5;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["4"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["4"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["4"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["4"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 4;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["3"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["3"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["3"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["3"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 3;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["2"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["2"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["2"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["2"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 2;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else if (jHYObj["devs"]["1"] != null)
                        {
                            TCPClientState state = findStatebySN(sfjcomm, false);
                            if (state != null)
                            {
                                oneCmd = new CommandInfo
                                {
                                    client = state,
                                    serialNumber = sfjcomm,
                                    scheduledTime = DateTime.Now.ToString()
                                };
                                if (jHYObj["devs"]["1"]["app"]["fertilizeValve"] != null)
                                {
                                    if (!jHYObj["devs"]["1"]["app"]["fertilizeValve"].ToString().Equals("0"))
                                    {
                                        if (!jHYObj["devs"]["1"]["app"]["fertilizeValve"].ToString().Equals("1"))
                                        {
                                            msg = "从瀚云平台接受非法水肥机控制指令";
                                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                                            return;
                                        }
                                        else
                                        {
                                            oneCmd.actOrder = "AC-OPEN";
                                        }
                                    }
                                    else
                                    {
                                        oneCmd.actOrder = "AC-CLOSE";
                                    }
                                    oneCmd.deviceID = 1;
                                    SFJ_sendOrder(oneCmd);
                                }
                            }
                            else
                            {
                                msg = "水肥机(" + oneCmd.fertNumSet.ToString() + ")不在线，无法执行任务";
                                ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                                EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                            }
                        }
                        else
                        {
                            msg = "从瀚云平台接受非法控制指令";
                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                        }
                    }
                }
                else
                {
                    TCPClientState state = findStatebySN(sfjcomm, false);
                    if (state == null)
                    {
                        msg = "水肥机不在线，无法执行控制";
                        ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                        EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                    }
                    else
                    {
                        oneCmd = new CommandInfo
                        {
                            fertNumSet = (jHYObj["app"]["totalFlow"] == null) ? 10 : ((Convert.ToInt32(jHYObj["app"]["totalFlow"]) <= 0) ? 10 : Convert.ToInt32(jHYObj["app"]["totalFlow"]))
                        };
                        string str3 = null;
                        oneCmd.actOrder = "SEND-TASK";
                        oneCmd.warterInterval = 0;
                        oneCmd.scheduledTime = DateTime.Now.ToString("yyyy-MM-dd");
                        oneCmd.createTime = DateTime.Now.ToString("HH:mm:ss");
                        oneCmd.serialNumber = sfjcomm;
                        oneCmd.client = null;
                        oneCmd.waterNumSet = 0;
                        oneCmd.taskFert = "A,B,C";
                        oneCmd.warterInterval = 100;
                        if ((jHYObj["devs"]["1"] != null) && jHYObj["devs"]["1"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "1";
                        }
                        if ((jHYObj["devs"]["2"] != null) && jHYObj["devs"]["2"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "2," + str3;
                        }
                        if ((jHYObj["devs"]["3"] != null) && jHYObj["devs"]["3"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "3," + str3;
                        }
                        if ((jHYObj["devs"]["4"] != null) && jHYObj["devs"]["4"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "4," + str3;
                        }
                        if ((jHYObj["devs"]["5"] != null) && jHYObj["devs"]["5"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "5," + str3;
                        }
                        if ((jHYObj["devs"]["6"] != null) && jHYObj["devs"]["6"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "6," + str3;
                        }
                        if ((jHYObj["devs"]["7"] != null) && jHYObj["devs"]["7"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "7," + str3;
                        }
                        if ((jHYObj["devs"]["8"] != null) && jHYObj["devs"]["8"]["app"]["fertilizeValve"].ToString().Equals("1"))
                        {
                            str3 = "8," + str3;
                        }
                        if (str3.Length <= 0)
                        {
                            str3 = "1,2,3";
                        }
                        else if (str3.Substring(str3.Length - 1).Equals(","))
                        {
                            str3 = str3.Substring(0, str3.Length - 1);
                        }
                        oneCmd.taskArea = str3;
                        oneCmd.client = state;
                        oneCmd.taskType = "1";
                        SFJ_sendOrder(oneCmd);
                        msg = "从瀚云平台接受施肥指令,施肥量：" + oneCmd.fertNumSet.ToString() + ",施肥通道：" + oneCmd.taskFert;
                        ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                        EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                    }
                }
            }
            catch (Exception exception)
            {
                string str4 = "收到订阅信息:" + str + ",处理时发生错误：" + exception.Message;
                ShowMsgEvent(DateTime.Now.ToString() + " " + str4);
                EveryDayLog.Write(DateTime.Now.ToString() + " " + str4);
            }
        }










        #endregion

        #region 获取状态和数据
        ///<summary>
        ///主动采集数据
        ///</summary>
        public void collectDataorState(TCPClientState state)
        {
            string commtype = state.clientComm.commtype;
            switch (commtype)
            {
                case "FKC":
                    FKC_getStateorDataThrd(state);
                    break;
                case "XPC":
                    XPC_getStateorDataThrd(state);
                    break;
                case "XPH":
                    XPH_getStateorDataThrd(state);
                    break;
                case "KLC":
                    KLC_getStateorDataThrd(state);
                    break;
                case "YYC":
                    YYC_getStateorDataThrd(state);
                    break;
                default:
                    break;
            }
        }

        public void KLC_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara * 1000;
            string commaddr = state.clientComm.commaddr;
            string msg;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 300000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 300000;
            }

            while (state != null && state.clientStatus == 2)
            {
                try
                {
                    //开始采集数据
                    sendStr = "15010000000601";
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0020"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    msg = DateTime.Now.ToString() + " KLC设备(" + comm_sn + ")发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Send(state, sendStr);

                    Thread.Sleep(30000);
                    //获取状态指令
                    sendStr = "15010000000602";
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0004"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    Send(state, sendStr);
                    msg = DateTime.Now.ToString() + " KLC设备(" + comm_sn + ")发送状态获取指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Thread.Sleep(collect_time);
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " KLC设备(" + comm_sn + ")采集数据或状态，发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
        }
        public void FKC_getStateorDataThrd(TCPClientState state)
        {

            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara * 1000;
            string commaddr = state.clientComm.commaddr;
            string msg;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 300000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 300000;
            }

            while (state != null && state.clientStatus == 2 && IsRunning)
            {
                try
                {
                    //开始采集数据
                    sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0010"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    msg = DateTime.Now.ToString() + " FKC设备(" + comm_sn + ")开始发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Send(state, sendStr);

                    Thread.Sleep(30000);
                    sendStr = "00700054";
                    Send(state, sendStr);
                    msg = DateTime.Now.ToString() + " FKC设备（" + comm_sn + "）发送状态获取指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Thread.Sleep(collect_time);
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " FKC设备(" + comm_sn + ")采集数据或状态，发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
        }

        /// <summary>
        /// 新普惠modbus协议不支持控制
        /// </summary>
        /// <param name="state"></param>
        public void XPC_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara * 1000;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 300000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 300000;
            }
            while (state != null && state.clientStatus == 2)
            {
                try
                {
                    //开始采集数据，如果是Modbus协议，接受数据的数据域长度为1个字节，飞科的为2个字节
                    sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0010"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    string msg = DateTime.Now.ToString() + " XPC设备(" + comm_sn + ")开始发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Send(state, sendStr);
                    Thread.Sleep(collect_time);
                }
                catch (Exception err)
                {
                    string msg = DateTime.Now.ToString() + " XPC设备(" + comm_sn + ")采集数据或状态，发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }

        }

        public void XPH_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara * 1000;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 300000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 300000;
            }

            while (state != null && state.clientStatus == 2)
            {
                try
                {
                    //开始采集数据
                    sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    //sendStr += "0010"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    string msg = DateTime.Now.ToString() + " XPH设备(" + comm_sn + ")开始发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Send(state, sendStr);
                    Thread.Sleep(collect_time);
                }
                catch (Exception err)
                {
                    string msg = DateTime.Now.ToString() + " XPH设备(" + comm_sn + ")采集数据或状态，发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }


        }

        public void YYC_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara * 1000;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 300000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 300000;
            }

            while (state != null && state.clientStatus == 2)
            {
                try
                {
                    //开始采集数据，如果是Modbus协议，接受数据的数据域长度为1个字节，飞科的为2个字节
                    sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0020"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    string msg = DateTime.Now.ToString() + " YYC设备(" + comm_sn + ")开始发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Send(state, sendStr);
                    Thread.Sleep(collect_time);
                }
                catch (Exception err)
                {
                    string msg = DateTime.Now.ToString() + " YYC设备(" + comm_sn + ")采集数据或状态，发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
        }
        public void getCommand_Thrd()
        {
            while (IsRunning)
            {

                string msg = null;
                string sSensorSQL = null;
                string resultSQL = null;
                string comm_sn = null;
                List<CommandInfo> cmdList = new List<CommandInfo>();
                if (_communicationtype.Contains("1"))
                {
                    //未来增加重发次数
                    sSensorSQL = "SELECT a.id,a.Device_ID,a.ActOrder,a.actparam,a.scheduledtime,a.createtime,c.serialNumber,b.code ";
                    sSensorSQL += "FROM yw_c_control_log_tbl a ";
                    sSensorSQL += "LEFT JOIN yw_d_controller_tbl b ON a.Device_ID = b.ID ";
                    sSensorSQL += "LEFT JOIN yw_d_commnication_tbl c ON b.Commucation_ID = c.ID ";
                    sSensorSQL += "where (a.ActOrder = 'AC-OPEN' OR a.ActOrder = 'AC-CLOSE' OR a.ActOrder = 'AC-STOP') AND a.`ExecuteResult` < 4 ";
                    sSensorSQL += "AND (ISNULL(a.ScheduledTime) OR NOW() > a.ScheduledTime) AND (c.SerialNumber IS NOT NULL) ";
                    //sSensorSQL += "AND (ISNULL(a.ScheduledTime) OR (NOW() > a.ScheduledTime AND DATE_ADD(a.ScheduledTime,INTERVAL 10 MINUTE)>NOW())) AND (c.SerialNumber IS NOT NULL) ";
                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        MySqlDataReader msdr = myCmd.ExecuteReader();
                        while (msdr.Read())
                        {
                            msg = DateTime.Now.ToString() + " 发现有任务";
                            ShowMsgEvent(msg);
                            int commandID = 0;
                            string sResult = null;
                            EveryDayLog.Write(msg);
                            try
                            {
                                string schdTime = (msdr.IsDBNull(4)) ? "" : msdr.GetString("scheduledtime");
                                commandID = (msdr.IsDBNull(0)) ? 0 : msdr.GetInt32("id");
                                comm_sn = (msdr.IsDBNull(6)) ? "" : msdr.GetString("serialNumber");
                                int orderOverTime = 0;
                                if (schdTime.Length > 0)
                                {
                                    orderOverTime = (int)DateTime.Now.Subtract(Convert.ToDateTime(schdTime)).Duration().TotalSeconds;
                                }

                                //未超时10分钟
                                if (schdTime.Length == 0 || (orderOverTime < 600 && orderOverTime >= 0))
                                {
                                    TCPClientState clientstate = findStatebySN(comm_sn);
                                    if (clientstate != null)
                                    {
                                        CommandInfo cmdInfo = new CommandInfo();
                                        cmdInfo.commandID = commandID;
                                        cmdInfo.deviceID = (msdr.IsDBNull(1)) ? 0 : msdr.GetInt32("Device_ID");
                                        cmdInfo.actOrder = (msdr.IsDBNull(2)) ? "" : msdr.GetString("ActOrder");
                                        cmdInfo.actparam = (msdr.IsDBNull(3)) ? 0 : Convert.ToInt32(msdr.GetString("actparam"));
                                        cmdInfo.scheduledTime = schdTime;
                                        cmdInfo.createTime = (msdr.IsDBNull(5)) ? "" : msdr.GetString("createtime");
                                        cmdInfo.serialNumber = comm_sn;
                                        cmdInfo.deviceCode = (msdr.IsDBNull(7)) ? "" : msdr.GetString("code");
                                        cmdInfo.client = clientstate;
                                        lock (cmdList)
                                        {
                                            cmdList.Add(cmdInfo);
                                        }
                                        sResult = "4";

                                    }
                                    else
                                    {
                                        sResult = "7";
                                        msg = DateTime.Now.ToString() + " 通讯设备(" + comm_sn + ")不在线，指令无法执行";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    sResult = "9";
                                    msg = DateTime.Now.ToString() + " ID(" + commandID.ToString() + ")指令超时，指令无法执行";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                sResult = "8";
                                msg = DateTime.Now.ToString() + "ID(" + commandID.ToString() + "),执行时发生未知错误" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            finally
                            {
                                if (commandID > 0)
                                {
                                    resultSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult= '" + sResult + "' where id = '" + commandID.ToString() + "'";
                                    using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                                    {
                                        if (conn2.State == ConnectionState.Closed)
                                        {
                                            conn2.Open();
                                        }
                                        MySqlCommand resultCmd = new MySqlCommand(resultSQL, conn2);
                                        int iResult = resultCmd.ExecuteNonQuery();
                                        if (iResult > 0)
                                        {
                                            msg = DateTime.Now.ToString() + " 物联网控制指令ID:" + commandID.ToString() + " 已执行或超时关闭";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                        else
                                        {
                                            msg = DateTime.Now.ToString() + " 物联网控制指令ID:" + commandID.ToString() + " 已执行或超时关闭失败";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                    }
                                }

                            }


                        }
                    }
                }
                if (_communicationtype.Contains("2"))
                {
                    //获取指令
                    sSensorSQL = "SELECT a.ID, a.Device_ID,ActOrder,b.Device_Address,a.ScheduledTime,a.CreateTime,c.PLC_Number FROM sfyth_control_log a ";
                    sSensorSQL += "LEFT JOIN sfyth_device b ON a.Device_ID = b.ID  LEFT JOIN sfyth_plc c ON a.PLC_Number = c.PLC_Number ";
                    sSensorSQL += "WHERE a.ActState < 4 AND (ISNULL(a.ScheduledTime) OR (NOW() > a.ScheduledTime AND DATE_ADD(a.ScheduledTime,INTERVAL 10 MINUTE)>NOW())) ";

                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        MySqlDataReader msdr = myCmd.ExecuteReader();
                        while (msdr.Read())
                        {
                            int commandID = 0;
                            string sResult = null;
                            try
                            {
                                string schdTime = (msdr.IsDBNull(4)) ? "" : msdr.GetString("scheduledtime");
                                commandID = (msdr.IsDBNull(0)) ? 0 : msdr.GetInt32("id");
                                int orderOverTime = 0;
                                comm_sn = (msdr.IsDBNull(6)) ? "" : msdr.GetString("PLC_Number");
                                if (schdTime.Length > 0)
                                {
                                    orderOverTime = (int)DateTime.Now.Subtract(Convert.ToDateTime(schdTime)).Duration().TotalSeconds;
                                }
                                // 超时10分钟
                                //不超时或预定时间为空
                                if (schdTime.Length == 0 || (orderOverTime < 600 && orderOverTime >= 0))
                                {
                                    TCPClientState clientstate = findStatebySN(comm_sn);
                                    if (clientstate != null)
                                    {
                                        //a.ID, a.Device_ID,ActOrder,b.Device_Address,a.ScheduledTime,a.CreateTime,c.PLC_Number
                                        CommandInfo cmdInfo = new CommandInfo();
                                        cmdInfo.commandID = (msdr.IsDBNull(0)) ? 0 : msdr.GetInt32("id");
                                        cmdInfo.deviceID = (msdr.IsDBNull(1)) ? 0 : msdr.GetInt32("Device_ID");
                                        cmdInfo.actOrder = (msdr.IsDBNull(2)) ? "" : msdr.GetString("ActOrder");
                                        cmdInfo.actparam = (msdr.IsDBNull(3)) ? 0 : Convert.ToInt32(msdr.GetString("Device_Address"));
                                        cmdInfo.scheduledTime = (msdr.IsDBNull(4)) ? "" : msdr.GetString("ScheduledTime");
                                        cmdInfo.createTime = (msdr.IsDBNull(5)) ? "" : msdr.GetString("CreateTime");
                                        cmdInfo.serialNumber = (msdr.IsDBNull(6)) ? "" : msdr.GetString("PLC_Number");
                                        cmdInfo.client = clientstate;
                                        sResult = "30";
                                        lock (cmdList)
                                        {
                                            cmdList.Add(cmdInfo);
                                        }
                                    }
                                    else
                                    {
                                        sResult = "7";
                                        msg = DateTime.Now.ToString() + " 水肥机编号为(" + comm_sn + ")不在线，指令无法执行";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    sResult = "9";
                                    msg = DateTime.Now.ToString() + " 指令ID为(" + commandID.ToString() + ")超时或时间未到，指令无法执行";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                sResult = "8";
                                msg = DateTime.Now.ToString() + "  指令ID为(" + commandID.ToString() + ")执行指令发生错误:" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            finally
                            {
                                if (commandID > 0)
                                {
                                    resultSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='" + sResult + "' where id = '" + commandID.ToString() + "'";
                                    using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                                    {
                                        if (conn2.State == ConnectionState.Closed)
                                        {
                                            conn2.Open();
                                        }
                                        MySqlCommand resultCmd = new MySqlCommand(resultSQL, conn2);
                                        int iResult = resultCmd.ExecuteNonQuery();
                                        if (iResult > 0)
                                        {
                                            msg = DateTime.Now.ToString() + " 水肥机控制指令ID:" + commandID.ToString() + " 已执行或超时关闭";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                        else
                                        {
                                            msg = DateTime.Now.ToString() + " 水肥机控制指令ID:" + commandID.ToString() + " 已执行或超时关闭失败";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                    }
                                }
                            }


                        }
                    }

                    //获取任务
                    sSensorSQL = "SELECT T_Start,T_Interval,T_Gquantity,T_Squantity,T_Area,T_Fertilize,a.T_Date,T_Type,a.T_ID,b.PLC_Number FROM  sfyth_task a ";
                    sSensorSQL += "LEFT JOIN sfyth_plc b ON b.PLC_Number = a.PLC_Number WHERE a.T_State=0 AND (ISNULL(a.T_Start) OR (NOW() > a.T_Start AND  ";
                    sSensorSQL += "DATE_ADD(DATE_FORMAT(CONCAT(a.T_Date,' ',a.T_Start),'%Y-%m-%d %H:%i:%s'),INTERVAL 10 MINUTE)>NOW())) ORDER BY a.T_ID ASC ";
                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSensorSQL, conn);
                        MySqlDataReader msdr = myCmd.ExecuteReader();
                        while (msdr.Read())
                        {
                            int commandID = 0;
                            comm_sn = (msdr.IsDBNull(9)) ? "" : msdr.GetString("PLC_Number");
                            try
                            {
                                string schdTime = ((msdr.IsDBNull(0)) ? "" : msdr.GetString("T_Date")).Substring(0, 10) + " " + ((msdr.IsDBNull(0)) ? "" : msdr.GetString("T_Start"));
                                commandID = (msdr.IsDBNull(0)) ? 0 : msdr.GetInt32("T_ID");
                                int orderOverTime = 0;
                                if (schdTime.Length >= 1)
                                {
                                    orderOverTime = (int)DateTime.Now.Subtract(Convert.ToDateTime(schdTime)).Duration().TotalSeconds;
                                }
                                // 超时10分钟

                                //未超时10分钟或者
                                if (schdTime.Length > 1 || (orderOverTime < 600 && orderOverTime >= 0))
                                {
                                    TCPClientState clientstate = findStatebySN(comm_sn);
                                    if (clientstate != null)
                                    {
                                        //T_Start,T_Interval,T_Gquantity,T_Squantity,T_Area,T_Fertilize,a.T_Date,T_Type,a.T_ID,b.PLC_Number
                                        CommandInfo cmdInfo = new CommandInfo();
                                        cmdInfo.commandID = (msdr.IsDBNull(8)) ? 0 : msdr.GetInt32("T_ID");
                                        cmdInfo.actOrder = "SEND-TASK";
                                        cmdInfo.warterInterval = (msdr.IsDBNull(1)) ? 0 : Convert.ToInt32(msdr.GetString("T_Interval"));
                                        cmdInfo.scheduledTime = ((msdr.IsDBNull(6)) ? "" : msdr.GetString("T_Date")); ;
                                        cmdInfo.createTime = (msdr.IsDBNull(0)) ? "" : msdr.GetString("T_Start");
                                        cmdInfo.serialNumber = (msdr.IsDBNull(9)) ? "" : msdr.GetString("PLC_Number");
                                        cmdInfo.client = clientstate;
                                        cmdInfo.waterNumSet = (msdr.IsDBNull(2)) ? 0 : msdr.GetInt32("T_Gquantity");
                                        cmdInfo.fertNumSet = (msdr.IsDBNull(3)) ? 0 : msdr.GetInt32("T_Squantity");
                                        cmdInfo.taskArea = (msdr.IsDBNull(4)) ? "" : msdr.GetString("T_Area");
                                        cmdInfo.taskFert = (msdr.IsDBNull(5)) ? "" : msdr.GetString("T_Fertilize");
                                        cmdInfo.taskType = (msdr.IsDBNull(7)) ? "" : msdr.GetString("T_Type");
                                        lock (cmdList)
                                        {
                                            cmdList.Add(cmdInfo);
                                        }

                                    }
                                    else
                                    {
                                        msg = DateTime.Now.ToString() + " 水肥机(" + comm_sn + ")不在线，指令无法执行";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 水肥机任务ID为(" + commandID.ToString() + ")超时或时间未到，指令无法执行";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                msg = DateTime.Now.ToString() + " 水肥机任务ID为(" + commandID.ToString() + ")执行指令发生错误:" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            finally
                            {
                                if (commandID > 0)
                                {
                                    resultSQL = "update sfyth_task set T_State='1' where T_ID = '" + commandID.ToString() + "'";
                                    using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                                    {
                                        if (conn2.State == ConnectionState.Closed)
                                        {
                                            conn2.Open();
                                        }
                                        MySqlCommand resultCmd = new MySqlCommand(resultSQL, conn2);
                                        int iResult = resultCmd.ExecuteNonQuery();
                                        if (iResult > 0)
                                        {
                                            msg = DateTime.Now.ToString() + " 水肥机任务ID:" + commandID.ToString() + " 已执行或超时关闭";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                        else
                                        {
                                            msg = DateTime.Now.ToString() + " 水肥机任务ID:" + commandID.ToString() + " 已执行或超时关闭失败";
                                            ShowMsgEvent(msg);
                                            EveryDayLog.Write(msg);
                                        }
                                    }
                                }

                            }


                        }
                    }


                }
                while (cmdList.Count > 0)
                {
                    CommandInfo cmdInfo = cmdList[0];
                    cmdList.RemoveAt(0);
                    TCPClientState client = cmdInfo.client;
                    if (client != null)
                    {
                        string comm_type = client.clientComm.commtype.ToUpper();
                        switch (comm_type)
                        {
                            case "PLC":
                                Task.Run(() => PLC_sendOrder(cmdInfo));
                                break;
                            case "KLC":
                                Task.Run(() => KLC_sendOrder(cmdInfo));
                                break;
                            case "FKC":
                                Task.Run(() => FKC_sendOrder(cmdInfo));
                                break;
                            case "XPC":
                                //Task.Run(() => XPC_sendOrder(cmdInfo));
                                break;
                            case "XPH":
                                Task.Run(() => XPH_sendOrder(cmdInfo));
                                break;
                            case "YYC":
                                //Task.Run(() => YYC_sendOrder(cmdInfo));
                                break;
                            case "SFJ-0804":
                            case "SFJ-1200":
                                Task.Run(() => SFJ_sendOrder(cmdInfo));
                                break;
                            case "DYC":
                                Task.Run(() => DYC_sendOrder(cmdInfo));
                                break;
                            case "MQC":
                                //Task.Run(() => MQT_sendOrder(cmdInfo));
                                break;
                            default:
                                msg = DateTime.Now.ToString() + " 编号(" + comm_sn + ")的未知类型:" + comm_type;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                break;
                        }
                    }

                }
                Thread.Sleep(_get_command_time);
            }

        }

        public void overtimeClient_Thrd()
        {
            while (IsRunning)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    try
                    {

                        TCPClientState clientstate = _clients[i];
                        int tp = (int)DateTime.Now.Subtract(Convert.ToDateTime(clientstate.lastTime)).Duration().TotalSeconds;
                        if (clientstate.clientStatus < 3)
                        {
                            if (tp > _linked_timeout || (!clientstate.TcpClient.Connected))
                            {
                                clientstate.clientStatus = 3;
                            }
                        }
                        else if (clientstate.clientStatus == 3)
                        {
                            if (tp > _linked_timeout + 600000)
                            {
                                clientstate.clientStatus = 4;
                                clientstate.Close();
                            }
                        }
                        else if (clientstate.clientStatus == 4)
                        {
                            _clients.Remove(clientstate);
                            string commsn = null;
                            commsn = clientstate.clientComm.serial_num;
                            if (commsn != null)
                            {
                                removeClientEvent(commsn);
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        string msg = DateTime.Now.ToString() + " 检测过时设备(" + _clients[i].clientComm.serial_num + ")发生错误:" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                }
                Thread.Sleep(300000);//5分钟检测1次
            }
        }


        public void PLC_sendOrder(CommandInfo oneCmd)
        {
            string msg = null;
            string sSendStr = null;
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 1);
            try
            {
                if (oneCmd.actparam > 0)
                {
                    Thread.Sleep(oneCmd.actparam * 1000);//延迟
                }
                switch (oneCmd.actOrder)
                {
                    case "AC-OPEN":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 5000, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                            Thread.Sleep(500); //间隔0.5秒

                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")未知的设备类型:" + ((ControlDevice)oneControl).devtype;

                        }
                        break;
                    case "AC-CLOSE":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 4999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 4999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                            Thread.Sleep(500); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 2000, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            msg = DateTime.Now.ToString() + " 设备(" + oneControl.devcode + " )发送" + oneCmd.actOrder + "指令:" + sSendStr;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + " )是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    case "AC-STOP":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 4999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 4999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            Thread.Sleep(500); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 5000, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " PLC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        public void KLC_sendOrder(CommandInfo oneCmd)
        {
            string msg = null;
            string sSendStr = null;
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 1);
            try
            {
                if (oneCmd.actparam > 0)
                {
                    Thread.Sleep(oneCmd.actparam * 1000);//延迟
                }
                switch (oneCmd.actOrder)
                {
                    case "AC-OPEN":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = "15010000000B0210";
                            sSendStr += Convert.ToString((oneControl.devaddr - 1) * 2, 16).PadLeft(4, '0');
                            sSendStr += "000204" + "A" + oneControl.devaddr.ToString() + "40FFFF";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        break;
                    case "AC-CLOSE":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = "15010000000B0210";
                            sSendStr += Convert.ToString((oneControl.devaddr - 1) * 2, 16).PadLeft(4, '0');
                            sSendStr += "000204" + "A" + oneControl.devaddr.ToString() + "400000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        break;
                    default:
                        break;
                }
                Thread.Sleep(10000);
                //获取状态指令
                string sendStr = "15010000000602";
                sendStr += "03";
                sendStr += "0000"; //起始地址
                sendStr += "0004"; //数量
                sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                Send(oneCmd.client, sendStr);
                msg = DateTime.Now.ToString() + " FKC设备（" + oneCmd.serialNumber + "）发送状态获取指令:" + sendStr;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " KLC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        public void FKC_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSendStr = null;
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 1);
            try
            {
                if (oneCmd.actparam > 0)
                {
                    Thread.Sleep(oneCmd.actparam * 1000);//延迟
                }
                switch (oneCmd.actOrder)
                {
                    case "AC-OPEN":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1, 16).PadLeft(2, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);

                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);

                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;

                        }
                        break;
                    case "AC-CLOSE":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    case "AC-STOP":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "72";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    default:
                        break;

                }

                Thread.Sleep(10000);
                string sendStr = "00700054";
                Send(oneCmd.client, sendStr);
                msg = DateTime.Now.ToString() + " FKC设备（" + oneCmd.serialNumber + "）发送状态获取指令:" + sendStr;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " FKC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        public void XPH_sendOrder(CommandInfo oneCmd)
        {
            string msg = null;
            string sSendStr = null;
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 1);
            try
            {
                if (oneCmd.actparam > 0)
                {
                    Thread.Sleep(oneCmd.actparam * 1000);//延迟
                }
                switch (oneCmd.actOrder)
                {
                    case "AC-OPEN":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr - 1, 16).PadLeft(2, '0');
                            sSendStr += "0101";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);

                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr - 1, 16).PadLeft(2, '0');
                            sSendStr += "0101";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);

                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号为(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;

                        }
                        break;
                    case "AC-CLOSE":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr - 1, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0101";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    case "AC-STOP":
                        if (((ControlDevice)oneControl).devtype.Equals("1"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr - 1, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else if (((ControlDevice)oneControl).devtype.Equals("3"))
                        {
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr - 1, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            Thread.Sleep(200); //间隔0.2秒
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10007A";
                            sSendStr += Convert.ToString(oneControl.devaddr, 16).PadLeft(2, '0');
                            sSendStr += "0100";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                        }
                        else
                        {
                            msg = DateTime.Now.ToString() + " 设备编号(" + oneControl.devcode + ")是未知的设备类型:" + ((ControlDevice)oneControl).devtype;
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " XPH设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        public void DYC_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSendStr = null;
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 1);
            if (oneComm != null && oneControl != null)
            {
                try
                {
                    if (oneCmd.actparam > 0)
                    {
                        Thread.Sleep(oneCmd.actparam * 1000);//延迟
                    }
                    if (oneCmd.actOrder == null)
                    {
                        msg = DateTime.Now.ToString() + " DYC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + "指令为空";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        return;
                    }
                    switch (oneCmd.actOrder)
                    {
                        case "AC-OPEN":
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 1999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            break;
                        case "AC-CLOSE":
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 4999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " DYC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
            }
            else
            {
                msg = DateTime.Now.ToString() + " DYC设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + "设备或控制通道不存在";
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }

        }
        public void SFJ_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSensorSQL = null;
            string sSendStr = null;
            string sArea = null;
            string sFert = null;
            sSensorSQL = "";
            CommDevice oneComm = getCommbySN(oneCmd.serialNumber, 2);
            try
            {
                string sfjCommand = oneCmd.actOrder;
                if (sfjCommand == null)
                {
                    try
                    {
                        if (oneCmd.deviceID <= 0)
                        {
                            msg = " 瀚云平台控制指令ID:0 执行完成";
                            ShowMsgEvent(DateTime.Now.ToString() + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 控制指令ID:" + oneCmd.commandID.ToString() + " 执行完成错误"+err.Message ;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }


                }
                else
                {
                    switch (sfjCommand)
                    {
                        case "AC-OPEN":
                            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 20);
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 999, 16).PadLeft(4, '0');
                            sSendStr += "FF00";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            sSensorSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
                            break;
                        case "AC-CLOSE":
                            oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 20);
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "05";
                            sSendStr += Convert.ToString(oneControl.devaddr + 999, 16).PadLeft(4, '0');
                            sSendStr += "0000";
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            sSensorSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
                            break;
                        case "SEND-TASK":
                            if (oneCmd.waterNumSet != 0 && oneCmd.fertNumSet != 0)
                            {
                                break;
                            }
                            else if (oneCmd.waterNumSet == 0 && oneCmd.warterInterval == 0)
                            {
                                break;
                            }
                            else
                            {
                                if (oneComm.commtype.ToUpper().Equals("SFJ-0804"))
                                {
                                    string[] tempArea = oneCmd.taskArea.Split(',');
                                    char[] cArea = new char[8] { '0', '0', '0', '0', '0', '0', '0', '0' };
                                    //根据灌区id,确定灌区的16进制数
                                    foreach (string oneArea in tempArea)
                                    {
                                        oneControl = (ControlDevice)findDevbyID(Convert.ToInt32(oneArea), 20);
                                        cArea[oneControl.devaddr - 1] = '1';

                                    }
                                    if (oneCmd.taskType.Equals("1"))
                                    {
                                        if (oneCmd.taskFert.Contains("A"))
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        else
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        if (oneCmd.taskFert.Contains("A"))
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        else
                                        {
                                            sFert = "0" + sFert;
                                        }
                                        if (oneCmd.taskFert.Contains("B"))
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        else
                                        {
                                            sFert = "0" + sFert;
                                        }
                                        if (oneCmd.taskFert.Contains("C"))
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        else
                                        {
                                            sFert = "0" + sFert;
                                        }
                                        if (oneCmd.taskFert.Contains("D"))
                                        {
                                            sFert = "1" + sFert;
                                        }
                                        else
                                        {
                                            sFert = "0" + sFert;
                                        }
                                        sArea = FormatFunc.reverseString((string.Join("", cArea) + sFert));
                                        sArea = sArea.PadLeft(16, '0');
                                    }

                                }
                                else if (oneComm.commtype.ToUpper().Equals("SFJ-1200"))
                                {
                                    string[] tempArea = oneCmd.taskArea.Split(',');
                                    char[] cArea = new char[12] { '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0' };
                                    foreach (string oneArea in tempArea)
                                    {
                                        oneControl = (ControlDevice)findDevbyID(Convert.ToInt32(oneArea), 20);
                                        cArea[oneControl.devaddr - 1] = '1';
                                    }
                                    sArea = FormatFunc.reverseString((string.Join("", cArea)));
                                    //sArea = FormatFunc.reverseString(sArea).PadLeft(16, '0');
                                }
                            }

                            string outs = string.Format("{0:x}", Convert.ToInt32(sArea, 2));
                            sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                            sSendStr += "10";       //指令
                            sSendStr += "0129";     //#起始地址,40298(减1)
                            sSendStr += "000D";     //寄存器数量n
                            sSendStr += "1A";       //字节数2*n
                            sSendStr += "0001";     //远程控制    寄存器40298
                            sSendStr += outs.PadLeft(4, '0');   //设定灌区
                            sSendStr += "0000"; //任务状态
                            sSendStr += Convert.ToString(oneCmd.waterNumSet * 10, 16).PadLeft(4, '0');  //灌溉量
                            sSendStr += Convert.ToString(oneCmd.warterInterval, 16).PadLeft(4, '0');    //灌溉时长
                            sSendStr += Convert.ToString(oneCmd.fertNumSet * 10, 16).PadLeft(4, '0');    //施肥量
                            sSendStr += "0000"; //设定年
                            sSendStr += "0000"; //设定月
                            sSendStr += "0000"; //设定日
                            sSendStr += "0000"; //设定时
                            sSendStr += "0000"; //设定分
                            sSendStr += "0000"; //设定秒
                            sSendStr += oneCmd.taskType.PadLeft(4, '0'); //任务类型:0-灌溉，1-施肥
                            sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                            Send(oneCmd.client, sSendStr);
                            sSensorSQL = "UPDATE sfyth_task SET T_State='1'  where T_ID = '" + oneCmd.commandID.ToString() + "'";
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " SFJ设备(" + oneCmd.serialNumber + ")执行指令ID(" + oneCmd.commandID.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
            try
            {
                //sSensorSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSensorSQL, conn2);
                    int iResult = resultCmd.ExecuteNonQuery();
                    if (iResult > 0)
                    {
                        msg = DateTime.Now.ToString() + " 控制指令ID:" + oneCmd.commandID.ToString() + " 执行完成，更新日志成功";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    else
                    {
                        msg = DateTime.Now.ToString() + " 控制指令ID:" + oneCmd.commandID.ToString() + " 执行完成，更新日志失败";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);

                    }
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " 控制指令ID:" + oneCmd.commandID.ToString() + " 执行错误:" + err.Message; ;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="for_value"></param>
        /// <param name="comm_sn"></param>
        /// <param name="devclass">10-控制器，11-传感器，20-水肥机控制器，21-水肥机传感器</param>
        /// <returns></returns>
        public object findDevbyAddr(int for_value, string comm_sn, int devclass)
        {
            try
            {
                if (devclass == 10 || devclass == 20)
                {
                    if (_controlList.Count > 0)
                    {
                        for (int i = _controlList.Count - 1; i >= 0; i--)
                        {
                            if (_controlList[i].devaddr == for_value && _controlList[i].commnum == comm_sn)
                            {
                                return _controlList[i];
                            }
                        }
                    }

                }
                else if (devclass == 11 || devclass == 21)
                {
                    if (_sensorList.Count > 0)
                    {
                        for (int i = _sensorList.Count - 1; i >= 0; i--)
                        {
                            if (_sensorList[i].devaddr == for_value && _sensorList[i].commnum == comm_sn)
                            {
                                return _sensorList[i];
                            }
                        }
                    }

                }
            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 通过地址查找设备(" + comm_sn + "," + for_value.ToString() + "," + devclass.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return null;
            }
            return null;

        }

        public object findDevbyID(int for_value, int devclass)
        {
            try
            {
                if (devclass == 10 || devclass == 20)
                {
                    if (_controlList.Count > 0)
                    {
                        for (int i = _controlList.Count - 1; i >= 0; i--)
                        {
                            if (_controlList[i].devid == for_value)
                            {
                                return _controlList[i];
                            }

                        }
                    }

                }
                else if (devclass == 11 || devclass == 21)
                {
                    if (_sensorList.Count > 0)
                    {
                        for (int i = _sensorList.Count - 1; i >= 0; i--)
                        {
                            if (_sensorList[i].devid == for_value)
                            {
                                return _sensorList[i];
                            }

                        }
                    }

                }
            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 通过ID查找设备(" + for_value.ToString() + ")发生错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return null;
            }
            return null;
        }

        public int findClientbySN(string comm_sn)
        {
            try
            {
                if (_clients.Count > 0)
                {
                    int num = _clients.Count - 1;
                    while (true)
                    {
                        if (num < 0)
                        {
                            break;
                        }
                        if ((_clients[num].TcpClient == null) || (_clients[num].TcpClient.Client == null))
                        {
                            num--;
                            continue;
                        }
                        return (((_clients[num].clientComm == null) || !_clients[num].clientComm.serial_num.Equals(comm_sn)) ? 6 : ((_clients[num].clientStatus != 2) ? ((_clients[num].clientStatus >= 2) ? 5 : 1) : 2));
                    }

                    for (int i = _clients.Count - 1; i >= 0; i--)
                    {
                        if ((_clients[num].TcpClient != null) && (_clients[num].TcpClient.Client != null))
                        {

                            if (_clients[num].clientComm == null || !_clients[num].clientComm.serial_num.Equals(comm_sn))
                            {
                                return 6;
                            }
                            else if (_clients[num].clientStatus != 2)
                            {
                                if (_clients[num].clientStatus >= 2)
                                {
                                    return 5;
                                }
                                else
                                    return 1;

                            }
                            else
                            {
                                return 2;
                            }
                        }

                    }


                }
                return -1;
            }
            catch (Exception exception)
            {
                string msg = DateTime.Now.ToString() + " 查找通讯设备(" + comm_sn + ")通讯客户端发生错误:" + exception.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return -1;
            }
        }

        public HY_CommInfo findHYCommbySn(string kySn, bool bOnLine)
        {
            try
            {
                if (_hyCommList.Count > 0)
                {
                    for (int i = _hyCommList.Count - 1; i >= 0; i--)
                    {
                        if (_hyCommList[i].kyCommSerial.Equals(kySn))
                        {
                            if (!bOnLine)
                            {
                                return _hyCommList[i];
                            }
                            else
                            {
                                if (_hyCommList[i].isOnline == 1)
                                {
                                    return _hyCommList[i];
                                }
                            }
                        }

                    }
                }
                return null;
            }
            catch (Exception exception)
            {
                string msg = DateTime.Now.ToString() + " 查找瀚云通讯设备ID(" + kySn + ")通讯客户端发生错误:" + exception.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return null;
            }

        }

        public TCPClientState findStatebySN(string comm_sn, bool bOnLine)
        {
            TCPClientState state = null;
            try
            {
                if (_clients.Count > 0)
                {
                    for (int i = _clients.Count - 1; i >= 0; i--)
                    {
                        if ((_clients[i].TcpClient != null) && ((_clients[i].TcpClient.Client.Connected != false) && ((_clients[i].clientComm != null) && _clients[i].clientComm.serial_num.Equals(comm_sn))))
                        {
                            if (bOnLine)
                            {
                                return _clients[i];
                            }
                            else
                            {
                                if (_clients[i].clientStatus != 2)
                                {
                                    return null;
                                }
                                else
                                {
                                    return _clients[i];
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception exception)
            {
                string msg = DateTime.Now.ToString() + " 查找通讯设备(" + comm_sn + ")通讯客户端发生错误:" + exception.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
            return state;
        }





        public TCPClientState findStatebySN(string comm_sn)
        {
            try
            {
                if (_clients.Count > 0)
                {
                    for (int i = _clients.Count - 1; i >= 0; i--)
                    {
                        if (_clients[i].clientComm != null && _clients[i].TcpClient != null && _clients[i].TcpClient.Client != null)
                        {
                            if (_clients[i].clientComm.serial_num.Equals(comm_sn))
                            {
                                return _clients[i];
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 查找通讯设备(" + comm_sn + ")通讯客户端发生错误:" + err.Message; ;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return null;
            }
            return null;
        }

        //1个通道地址多个传感器，MQTT
        public int getSensorbyFoumula(string for_value, string comm_sn, int para)
        {
            int _index = -1;
            if (_sensorList.Count > 0)
            {
                for (int i = _sensorList.Count - 1; i >= 0; i--)
                {
                    if ((_sensorList[i].devformula.ToUpper().Equals(for_value.ToUpper())) && (_sensorList[i].commnum.ToUpper().Equals(comm_sn.ToUpper())) && _sensorList[i].devpara == para)
                    {
                        _index = i;
                        return _index;
                    }
                }
            }
            return _index;
        }

        public CommDevice getCommbySN(String comm_sn, int commclass)
        {
            if (_commList.Count > 0)
            {
                for (int i = _commList.Count - 1; i >= 0; i--)
                {
                    if (_commList[i].serial_num.ToUpper().Equals(comm_sn) && _commList[i].commclass == commclass)
                    {
                        return _commList[i];
                    }
                }
            }
            return null;
        }

        protected List<T> DataReaderToList<T>(MySqlDataReader SDR) where T : class
        {
            List<T> ListData = new List<T>();

            if (SDR.HasRows)
            {
                ListData.Clear();
                while (SDR.Read())
                {
                    object Obj = System.Activator.CreateInstance(typeof(T));
                    Type ObjType = Obj.GetType();

                    for (int i = 0; i < SDR.FieldCount; i++)
                    {
                        PropertyInfo PI = ObjType.GetProperty(SDR.GetName(i));
                        if (PI != null)
                        {
                            string PTName = PI.PropertyType.Name.ToString();
                            string FullName = PI.PropertyType.FullName;
                            string Name = PI.Name;

                            object Value = PI.GetValue(Obj, null);

                            switch (PI.PropertyType.ToString())
                            {
                                case "System.Int64":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt64(SDR[Name]), null);
                                    break;
                                case "System.Byte[]":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : (byte[])SDR[Name], null);
                                    break;
                                case "System.Boolean":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToBoolean(SDR[Name]), null);
                                    break;
                                case "System.String":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToString(SDR[Name]), null);
                                    break;
                                case "System.DateTime":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDateTime(SDR[Name]), null);
                                    break;
                                case "System.Decimal":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDecimal(SDR[Name]), null);
                                    break;
                                case "System.Double":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDouble(SDR[Name]), null);
                                    break;
                                case "System.Int32":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt32(SDR[Name]), null);
                                    break;
                                case "System.Single":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToSingle(SDR[Name]), null);
                                    break;
                                case "System.Byte":
                                    PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToByte(SDR[Name]), null);
                                    break;
                                default:
                                    int Chindex = PTName.IndexOf("Nullable");
                                    if (FullName.IndexOf("System.Int64") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt64(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Boolean") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToBoolean(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.String") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToString(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.DateTime") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDateTime(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Decimal") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDecimal(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Double") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToDouble(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Int32") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt32(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Single") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToSingle(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Byte") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToByte(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Int16") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt16(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Int16") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt16(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Int32") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt32(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.Int64") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToInt64(SDR[Name]), null);
                                    }

                                    if (FullName.IndexOf("System.SByte") >= 0)
                                    {
                                        PI.SetValue(Obj, SDR.IsDBNull(SDR.GetOrdinal(Name)) ? Value : Convert.ToSByte(SDR[Name]), null);
                                    }
                                    break;
                            }

                        }
                    }
                    ListData.Add(Obj as T);
                }
            }
            if (!SDR.IsClosed)
                SDR.Close();

            return ListData;
        }
        public string rightSub(string text, int num)
        {
            int len = text.Length;
            if (len < num)
            {
                return null;
            }
            else
            {
                return text.Substring(len - num);
            }

        }


        #endregion




        #region 事件

        /// <summary>
        /// 与客户端的连接已建立事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> ClientConnected;
        /// <summary>
        /// 与客户端的连接已断开事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> ClientDisconnected;


        /// <summary>
        /// 触发客户端连接事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseClientConnected(TCPClientState state)
        {
            if (ClientConnected != null)
            {
                ClientConnected(this, new AsyncEventArgs(state));
            }
        }
        /// <summary>
        /// 触发客户端连接断开事件
        /// </summary>
        /// <param name="client"></param>
        private void RaiseClientDisconnected(TCPClientState state)
        {
            if (ClientDisconnected != null)
            {
                ClientDisconnected(this, new AsyncEventArgs("连接断开"));
            }
        }

        /// <summary>
        /// 接收到数据事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> DataReceived;

        private void RaiseDataReceived(AsyncEventArgs argesstate)
        {
            if (DataReceived != null)
            {
                DataReceived(this, argesstate);
            }
        }

        /// <summary>
        /// 发送数据前的事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> PrepareSend;

        /// <summary>
        /// 触发发送数据前的事件
        /// </summary>
        /// <param name="state"></param>
        private void RaisePrepareSend(TCPClientState state)
        {
            if (PrepareSend != null)
            {
                PrepareSend(this, new AsyncEventArgs(state));
            }
        }

        /// <summary>
        /// 数据发送完毕事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> CompletedSend;

        /// <summary>
        /// 触发数据发送完毕的事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseCompletedSend(TCPClientState state)
        {
            if (CompletedSend != null)
            {
                CompletedSend(this, new AsyncEventArgs(state));
            }
        }
        private void HY_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
        }
        


        public void HY_Reconnect()
        {
            while (IsRunning)
            {
                if ((hy_mqtt_client != null) && !hy_mqtt_client.IsConnected)
                {
                    try
                    {
                        string clientId = "d:" + hyproductkey + ":" + hydevicesn;
                        hy_mqtt_client.MqttMsgPublishReceived += new MqttClient.MqttMsgPublishEventHandler(HY_MqttMsgPublishReceived);
                        string returnval = hy_mqtt_client.Connect(clientId, hyproductkey, hyaccesskey + ":" + hyaccesssecret, true, 180).ToString();
                        string msg = null;
                        if ((returnval.Length <= 0) || !returnval.Equals("0"))
                        {
                            msg = "瀚云物联网设备重新登录失败，失败返回代码：" + returnval;
                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                        }
                        else
                        {
                            msg = "瀚云物联网设备重新登录成功,返回代码：" + returnval;
                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                        }
                    }
                    catch (Exception exception)
                    {
                        string msg = "瀚云物联网设备重新登录失败，发生错误：" + exception.ToString();
                        ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                        EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                    }
                }
                if ((hy_sfj_client != null) && !hy_sfj_client.IsConnected)
                {
                    try
                    {
                        string clientId = "d:" + hyproductkey + ":" + hysfjsn;
                        hy_sfj_client.MqttMsgPublishReceived += new MqttClient.MqttMsgPublishEventHandler(HY_SFJ_MqttMsgPublishReceived);
                        string returnval = hy_sfj_client.Connect(clientId, hyproductkey, hyaccesskey + ":" + hyaccesssecret, true, 180).ToString();
                        string msg = null;
                        if ((returnval.Length > 0) && returnval.Equals("0"))
                        {
                            msg = "瀚云水肥机设备重新登录成功,返回代码：" + returnval;
                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                        }
                        else
                        {
                            msg = "瀚云水肥机重新登录失败，失败返回代码：" + returnval;
                            ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                            EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                        }
                    }
                    catch (Exception exception2)
                    {
                        string msg = "瀚云水肥机重新登录失败，发生错误：" + exception2.ToString();
                        ShowMsgEvent(DateTime.Now.ToString() + " " + msg);
                        EveryDayLog.Write(DateTime.Now.ToString() + " " + msg);
                    }
                }
                Thread.Sleep(12000);
            }
        }

        private void HY_PublishInfo(object sender, string content)
        {
            //HY_CommInfo info = (HY_CommInfo)sender;
            //try
            //{
            //    string strTopic = "data/" + hydevicekey + "/" + info.streamTag;
            //    byte qosLevel = 0;
            //    string msg = null;
            //    if (((hy_mqtt_client != null) && !string.IsNullOrEmpty(strTopic)) && !string.IsNullOrEmpty(content))
            //    {
            //        hy_mqtt_client.Publish(strTopic, Encoding.UTF8.GetBytes(content), qosLevel, true).ToString();
            //        msg = "瀚云平台发布物联网数据：" + content;
            //        ShowMsgEvent(DateTime.Now.ToString() + " " + info.kyCommSerial + msg);
            //        EveryDayLog.Write(DateTime.Now.ToString() + " " + info.kyCommSerial + msg);
            //    }
            //}
            //catch (Exception exception)
            //{
            //    string msg = "瀚云平台发布物联网数据，发生错误：" + exception.Message;
            //    ShowMsgEvent(DateTime.Now.ToString() + " " + info.kyCommSerial + msg);
            //    EveryDayLog.Write(DateTime.Now.ToString() + " " + info.kyCommSerial + msg);
            //}
        }

        public void InitHttp()
        {
            try
            {
                httpListen = new HttpListener();
                //httpListen.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                httpListen.Prefixes.Add(yfserverapi);
                httpListen.Start();
                httpListen.BeginGetContext(new AsyncCallback(httpResult), null);
                ShowMsgEvent(DateTime.Now.ToString() + " Http服务端" + yfserverapi + "初始化完毕，正在等待客户端请求");
                EveryDayLog.Write(DateTime.Now.ToString() + " Http服务端" + yfserverapi + "初始化完毕，正在等待客户端请求");
            }
            catch (Exception err)
            {
                string msg = "启动HttpListener服务失败，错误：";
                ShowMsgEvent(DateTime.Now.ToString() + msg + err.ToString());
                EveryDayLog.Write(DateTime.Now.ToString() + msg + err.ToString());
            }
        }
        private void httpResult(IAsyncResult ar)
        {
            string guid = Guid.NewGuid().ToString();
            if ((httpListen == null) || !httpListen.IsListening)
            {
                return;
            }
            else
            {
                try
                {
                    httpListen.BeginGetContext(new AsyncCallback(httpResult), null);
                    ShowMsgEvent(DateTime.Now.ToString() + " 接到新的请求:" + guid);
                    EveryDayLog.Write(DateTime.Now.ToString() + " 接到新的请求:" + guid);
                    HttpListenerContext contextStr = httpListen.EndGetContext(ar);
                    HttpListenerRequest httpRequest = contextStr.Request;
                    HttpListenerResponse httpResponse = contextStr.Response;
                    contextStr.Response.ContentType = "text/plain;charset=UTF-8";
                    contextStr.Response.AddHeader("Content-type", "text/plain");
                    contextStr.Response.ContentEncoding = Encoding.UTF8;
                    string s = null;
                    if ((httpRequest.HttpMethod != "POST") || (httpRequest.InputStream == null))
                    {
                        s = "不是post请求或者传过来的数据为空";
                    }
                    else
                    {
                        s = HandleRequest(httpRequest, httpResponse);

                    }
                    byte[] bytes = Encoding.UTF8.GetBytes(s);
                    using (Stream stream = httpResponse.OutputStream)
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }

                }
                catch (Exception exception)
                {
                    ShowMsgEvent(DateTime.Now.ToString() + " 网络蹦了：" + exception.ToString());
                    EveryDayLog.Write(DateTime.Now.ToString() + " 网络蹦了：" + exception.ToString());
                }
            }
            ShowMsgEvent(DateTime.Now.ToString() + " Http请求处理完成:" + guid);
            EveryDayLog.Write(DateTime.Now.ToString() + " Http请求处理完成:" + guid);
        }


        public void JXC_handlerecv(CommDevice comm, string sdata)
        {
            string inisensorstr = null;
            double sensordata = 0.0;
            string sensorstr = null;
            int i = 0;
            SensorDevice oneSensor = null;
            int sensornum = Convert.ToInt16(sdata.Substring(28, 4), 16) / 4;
            string sSensorSQL = null;
            string msg = null;
            string sHYSendStr2 = null;
            string sHYSendStr4 = null;
            if (!sdata.Substring(26, 2).Equals("03") || (sensornum != 4))
            {
                return;
            }
            else
            {
                sSensorSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                while (i < sensornum)
                {
                    inisensorstr = sdata.Substring((i * 8) + 32, 8);
                    oneSensor = (SensorDevice)findDevbyAddr(i + 1, comm.serial_num, 11);
                    if (oneSensor != null)
                    {
                        sensordata = Convert.ToInt32(inisensorstr, 16);
                        string devformula = oneSensor.devformula;
                        if (devformula != null)
                        {
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"Temp@2\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"Temp@2\":{\"name\":\"土壤温度\"},";
                                        break;
                                    }
                                case "SHIDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"RH@2\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"RH@2\":{\"name\":\"土壤湿度\"},";
                                        break;
                                    }
                                case "Q-WENDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"Temp@1\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"Temp@1\":{\"name\":\"空气温度\"},";
                                        break;
                                    }
                                case "Q-SHIDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"RH@1\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"RH@1\":{\"name\":\"空气湿度\"},";
                                        break;
                                    }
                                case "Y-WENDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"Temp@3\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"Temp@3\":{\"name\":\"叶面温度\",\"type\":34},";
                                        break;
                                    }

                                case "Y-SHIDU":
                                    {
                                        sensorstr = (sensordata * 0.1).ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"RH@3\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"RH@3\":{\"name\":\"叶面湿度\",\"type\":34},";
                                        break;
                                    }

                                case "DM-GS":
                                    {
                                        sensorstr = sensordata.ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"millimeter@1\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"millimeter@1\":{\"name\":\"果实直径\",\"type\":41},";
                                        break;
                                    }

                                case "DM-JD":
                                    {
                                        sensorstr = sensordata.ToString("0.#");
                                        sHYSendStr2 = sHYSendStr2 + "\"millimeter@2\":" + sensorstr.ToString() + ",";
                                        sHYSendStr4 = sHYSendStr4 + "\"millimeter@2\":{\"name\":\"茎秆直径\",\"type\":41},";
                                        break;
                                    }
                                default:
                                    msg = DateTime.Now.ToString() + " JXC设备(" + comm.serial_num + ")未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;
                            }
                            sSensorSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorstr + "','" + oneSensor.blockid + "'),";

                        }
                    }
                    else
                    {
                        msg = DateTime.Now.ToString() + " JXC设备(" + comm.serial_num + ")的第" + (i + 1).ToString() + "通道传感器不存在";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        continue;
                    }
                    i++;

                }
                if (sSensorSQL.Substring(sSensorSQL.Length - 2).Equals("),"))
                {
                    sSensorSQL = sSensorSQL.Substring(0, sSensorSQL.Length - 1);
                    MySqlConnection connection = new MySqlConnection(_connectStr);
                    MySqlCommand command = new MySqlCommand(sSensorSQL, connection);
                    connection.Open();
                    try
                    {
                        if (command.ExecuteNonQuery() > 0)
                        {
                            msg = DateTime.Now.ToString() + " JXC设备(" + comm.serial_num + ")插入最新数据成功:" + sSensorSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception exception)
                    {
                        msg = DateTime.Now.ToString() + " JXC设备(" + comm.serial_num + ")插入最新数据失败:" + sSensorSQL + ",错误：" + exception.Message;

                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    finally
                    {
                        if (connection.State == ConnectionState.Open)
                        {
                            connection.Close();
                        }
                    }
                }
                if ((comm != null) && (comm.hyid > 0))
                {
                    HY_CommInfo hyComm = findHYCommbySn(comm.serial_num, false);
                    if ((hyComm != null) && (hy_mqtt_client != null))
                    {
                        string sHYSendStr1 = "{\"devs\":{\"" + hyComm.deviceName + "\":{\"app\":{";
                        string sHYSendStr3 = "},\"metadata\":{\"name\":\"" + hyComm.deviceName + "\",\"type\":35,\"props\":{";
                        string content = sHYSendStr1 + sHYSendStr2.Substring(0, sHYSendStr2.Length - 1) + sHYSendStr3;
                        content += sHYSendStr4.Substring(0, sHYSendStr4.Length - 1) + "}}}}}";
                        if (content.Length > 0)
                        {
                            HY_PublishInfo(hyComm, content);
                            ShowMsgEvent(DateTime.Now.ToString() + " 向瀚云平台发送植物生理数据:" + content);
                            EveryDayLog.Write(DateTime.Now.ToString() + " 向瀚云平台发送植物生理数据:" + content);
                        }
                    }
                }
            }
        }





        /// <summary>
        /// 网络错误事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> NetError;
        /// <summary>
        /// 触发网络错误事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseNetError(TCPClientState state)
        {
            if (NetError != null)
            {
                NetError(this, new AsyncEventArgs(state));
            }
        }

        /// <summary>
        /// 异常事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> OtherException;
        /// <summary>
        /// 触发异常事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseOtherException(TCPClientState state, string descrip)
        {
            if (OtherException != null)
            {
                OtherException(this, new AsyncEventArgs(descrip, state));
            }
        }
        private void RaiseOtherException(TCPClientState state)
        {
            RaiseOtherException(state, "");
        }

        #endregion

        #region Close
        /// <summary>
        /// 关闭一个与客户端之间的会话
        /// </summary>
        /// <param name="state">需要关闭的客户端会话对象</param>
        public void Close(TCPClientState state)
        {
            try
            {
                if (state != null)
                {
                    //string ipport = state.TcpClient.Client.RemoteEndPoint.ToString();
                    string commsn = null;
                    commsn = state.clientComm.serial_num;
                    if (commsn != null)
                    {
                        removeClientEvent(commsn);
                    }
                    state.Close();
                    _clients.Remove(state);
                    //TODO 触发关闭事件
                }
            }
            catch (Exception err)
            {
                string msg = "关闭客户端发送错误:" + err.Message;
                EveryDayLog.Write(msg);

            }



        }
        /// <summary>
        /// 关闭所有的客户端会话,与所有的客户端连接会断开
        /// </summary>
        public void CloseAllClient()
        {
            foreach (TCPClientState client in _clients)
            {
                Close(client);
            }
            _clients.Clear();
        }
        #endregion

        #region 释放
        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release 
        /// both managed and unmanaged resources; <c>false</c> 
        /// to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();
                        if (_listener != null)
                        {
                            _listener = null;
                        }
                    }
                    catch (SocketException)
                    {
                        //TODO
                        RaiseOtherException(null);
                    }
                }
                disposed = true;
            }
        }
        #endregion
    }
    /// <summary>
    /// 异步SOCKET TCP 中用来存储客户端状态信息的类
    /// </summary>
    public class TCPClientState
    {

        /// <summary>
        /// 与客户端相关的TcpClient
        /// </summary>
        public TcpClient TcpClient { get; private set; }

        /// <summary>
        /// 获取缓冲区
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// 获取网络流
        /// </summary>
        public NetworkStream NetworkStream
        {
            get { return TcpClient.GetStream(); }
        }

        /// <summary>
        /// 状态,0-未连接，1-待连接，2-已连接，3-黑名单,4-已断开
        /// </summary>
        private int _clientStatus;

        public bool _isolder;

        /// <summary>
        /// 最新接受数据时间
        /// </summary>
        private string _lastTime;

        /// <summary>
        /// 失败连接次数
        /// </summary>
        private int _faildTimes;

        /// <summary>
        /// 客户通讯设备
        /// </summary>
        private CommDevice _clientComm;

        public int clientStatus
        {
            get
            {
                return _clientStatus;
            }
            set
            {
                _clientStatus = value;
            }
        }
        public string lastTime
        {
            get
            {
                return _lastTime;
            }
            set
            {
                _lastTime = value;
            }
        }
        public int faildTimes
        {
            get
            {
                return _faildTimes;
            }
            set
            {
                _faildTimes = value;
            }
        }
        public CommDevice clientComm
        {
            get
            {
                return _clientComm;
            }
            set
            {
                _clientComm = value;
            }
        }

        public TCPClientState(TcpClient tcpClient, byte[] buffer)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            TcpClient = tcpClient;
            Buffer = buffer;
        }


        /// <summary>
        /// 关闭
        /// </summary>
        public void Close()
        {
            //关闭数据的接受和发送
            TcpClient.Close();
            Buffer = null;
        }
        public string getCommSN()
        {
            return _clientComm.commcode;
        }

    }
    /// <summary>
    /// 异步Socket TCP事件参数类
    /// </summary>
    public class AsyncEventArgs : EventArgs
    {
        /// <summary>
        /// 提示信息
        /// </summary>
        public string _msg;

        /// <summary>
        /// 客户端状态封装类
        /// </summary>
        public TCPClientState _state;

        /// <summary>
        /// 是否已经处理过了
        /// </summary>
        public bool IsHandled { get; set; }

        public AsyncEventArgs(string msg)
        {
            this._msg = msg;
            IsHandled = false;
        }
        public AsyncEventArgs(TCPClientState state)
        {
            this._state = state;
            IsHandled = false;
        }
        public AsyncEventArgs(string msg, TCPClientState state)
        {
            this._msg = msg;
            this._state = state;
            IsHandled = false;
        }
    }

    /// <summary>
    /// 通讯设备
    /// <param name="commid">编号:水肥机没有编号，以ID代替</param>
    /// <param name="commcode">序列号</param>
    /// <param name="serial_nmu">序列号</param>
    /// <param name="commaddr">通讯地址 </param>
    /// <param name="companyid">所属公司ID</param>
    /// <param name="commtype">类型:PLC、KLC、FKC、XPC、YYC、SFJ-1200、SFJ-0804</param>
    /// <param name="commpara">参数，水肥机-肥液路数，通讯设备-采集间隔</param>
    /// <param name="passnum">通道数</param>
    /// <param name="commclass">通讯设备类别，0-物联网，1-水肥机</param>
    /// 
    /// </summary>

    //a.ID,a.Code,a.SerialNumber,a.CodeAddress,a.Company_ID,c.ClassName,`Interval`,b.ParamValue
    public class CommDevice
    {
        // Methods
        // Properties
        public int commid { get; set; }
        public string commcode { get; set; }
        public string serial_num { get; set; }
        public string commaddr { get; set; }
        public string companyid { get; set; }
        public string commtype { get; set; }
        public int commpara { get; set; }
        public int passnum { get; set; }
        public int commclass { get; set; }
        public string heartstr { get; set; }
        public int hyid { get; set; }

        public CommDevice()
        { }
    }


    /// <summary>
    /// 控制器设备
    /// <param name="devid">设备id</param>
    /// <param name="devcode">设备编号，水肥机没有编号，以ID代替</param>
    /// <param name="devaddr">设备地址</param>
    /// <param name="devtype">设备类型:1-开关，2-脉冲，3-行程</param>
    /// <param name="devpara">设备参数:控制器-行程时长 </param>
    /// <param name="commu">所属网关序列号</param>
    /// <param name="blockid">设备地块</param>
    /// <param name="devformula">0,无意义</param>
    /// <param name="devclass">10-物联网设备，20-水肥机，30-棚博士</param>
    /// </summary>
    public class ControlDevice
    {
        public int devid { get; set; }
        public string devcode { get; set; }
        public int devaddr { get; set; }
        public string devtype { get; set; }
        public int devpara { get; set; }
        public string commnum { get; set; }
        public int blockid { get; set; }
        public string devformula { get; set; }
        public int devclass { get; set; }
        public ControlDevice()
        {
        }
    }

    /// <summary>
    /// 传感器设备
    /// <param name="devid">设备id</param>
    /// <param name="devcode">设备编号，水肥机没有编号，以ID代替</param>
    /// <param name="devaddr">设备地址</param>
    /// <param name="devtype">1-数字，2-4-20ma电流，3-0-20ma电流，4-0-5v电压，5-0-10v,6-高低电平</param>
    /// <param name="devpara">设备参数,暂定为MQTT中多传感器的组号 </param>
    /// <param name="commu">所属网关序列号</param>
    /// <param name="blockid">设备地块</param>
    /// <param name="devformula">公式:WENDU、SHIDU、Q-WENDU、Q-SHIDU、CO2、BEAM、EC、PH、FS、FX、YL、QY、OPR</param>
    /// <param name="devclass">11-物联网设备，21-水肥机，31-棚博士 </param>
    /// </summary>

    public class SensorDevice
    {

        public int devid { get; set; }
        public string devcode { get; set; }
        public int devaddr { get; set; }
        public string devtype { get; set; }
        public int devpara { get; set; }
        public string commnum { get; set; }
        public int blockid { get; set; }
        public string devformula { get; set; }
        public int devclass { get; set; }

        public SensorDevice()
        {
        }
    }
    /// <summary>
    /// <para name="commanID">命令ID</para>
    /// <para name="deviceID">设备ID</para>
    /// <para name="actOrder">命令</para>
    /// <para name="actparam">命令参数:物联网-延迟时间,水肥机-灌溉时间</para>
    /// <para name="scheduledTime">预计执行时间，水肥机-开始日期</para>
    /// <para name="createTime">创建时间,水肥机-开始时间</para>
    /// <para name="serialNumber">通讯设备编号</para>
    /// <para name="deviceCode">设备编码:物联网设备编码</para>
    /// <para name="client">连接客户端</para>
    /// <para name="waterNumSet">灌溉设定量</para>
    /// <para name="fertNumSet">肥液设定量</para>
    /// <para name="taskArea">灌区选择</para>
    /// <para name="taskFert">肥液通道选择</para>
    /// <para name="taskType">任务类型:0-灌溉，1-施肥</para>

    /// </summary>
    public class CommandInfo
    {
        public int commandID
        { get; set; }
        public int deviceID
        { get; set; }
        public string actOrder
        { get; set; }
        public int actparam
        { get; set; }
        public string scheduledTime
        { get; set; }
        public string createTime
        { get; set; }
        public string serialNumber
        { get; set; }
        public string deviceCode
        { get; set; }
        public TCPClientState client
        { get; set; }
        public int waterNumSet
        { get; set; }
        public int fertNumSet
        { get; set; }
        public int warterInterval
        { get; set; }
        public string taskArea
        { get; set; }
        public string taskFert
        { get; set; }
        public string taskType
        { get; set; }
        public CommandInfo()
        {
        }
    }

    public class HY_CommInfo
    {
        // Methods
        public HY_CommInfo()
        { }

        // Properties
        public string deviceName { get; set; }
        public string deviceID { get; set; }
        public string deviceKey { get; set; }
        public string deviceSN { get; set; }
        public string productKey { get; set; }
        public string accessKey { get; set; }
        public string accessSecret { get; set; }
        public string streamTag { get; set; }
        public int kyCommID { get; set; }
        public string kyCommSerial { get; set; }
        public MqttClient mqttClient { get; set; }
        public int isOnline { get; set; }

    }

    public class YF_Pest_Status
    {
        // Methods
        public YF_Pest_Status()
        { }

        // Properties
        public string topic { get; set; }
        public string cmd { get; set; }
        public string imei { get; set; }
        public string iccid { get; set; }
        public string csq { get; set; }
        public string dtype { get; set; }
        public string dver { get; set; }
        public string rps { get; set; }
        public string lps { get; set; }
        public string tps { get; set; }
        public string gs { get; set; }
        public string upds { get; set; }
        public string dnds { get; set; }
        public string hs { get; set; }
        public string ts { get; set; }
        public string lat { get; set; }
        public string lng { get; set; }
        public string stamp { get; set; }
        public string ws { get; set; }
        public string lamp { get; set; }
        public string lux { get; set; }
        public string vs { get; set; }
        public string shake { get; set; }
        public string proj { get; set; }

    }
    public class YF_Pest_Data
    {
        // Methods
        public YF_Pest_Data()
        { }

        // Properties
        public string topic { get; set; }
        public string cmd { get; set; }
        public string imei { get; set; }
        public string at { get; set; }
        public string ah { get; set; }
        public string hrt { get; set; }
        public string rps { get; set; }
        public string lps { get; set; }
        public string tps { get; set; }
        public string lat { get; set; }
        public string lng { get; set; }
        public string stamp { get; set; }
        public string ws { get; set; }
        public string lamp { get; set; }
        public string lux { get; set; }
        public string batStatus { get; set; }
        public string vbat { get; set; }


    }

    public class YF_Spore_Status
    {
        // Methods
        public YF_Spore_Status()
        { }

        // Properties
        public string topic { get; set; }
        public string cmd { get; set; }
        public string imei { get; set; }
        public string iccid { get; set; }
        public string csq { get; set; }
        public string alti { get; set; }
        public string lat { get; set; }
        public string lng { get; set; }
        public string dtype { get; set; }
        public string on_off { get; set; }
        public string dver { get; set; }
        public string v_bat { get; set; }
        public string bat_sta { get; set; }
        public string usb_sta { get; set; }
        public string imgres { get; set; }
        public string wind_sw { get; set; }
        public string cold_sw { get; set; }
        public string coll_time { get; set; }
        public string drop_time { get; set; }
        public string set_temp { get; set; }
        public string pre_temp { get; set; }
        public string at { get; set; }
        public string ah { get; set; }
        public string rps { get; set; }
        public string stamp { get; set; }
        public string datt { get; set; }
        public string staytime { get; set; }
        public string cul_time { get; set; }
        public string work_sta { get; set; }
        public string proj { get; set; }
        public string box_temp { get; set; }

    }
}
