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

namespace SockServer
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
        private int _clientCount;
        /// <summary>
        /// 服务器使用的异步TcpListener
        /// </summary>
        public TcpListener _listener;
        /// <summary>
        /// 客户端会话列表
        /// </summary>
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
        private int _releaseInterval;
        private string _connectStr;
        private bool disposed = false;
        private int _collect_time;
        private int _get_state_time;
        private int _get_command_time;
        private string _communicationtype;
        private int _linked_timeout;

        public event ShowMessageDelegate ShowMsgEvent;
        public event addClientDelegate addClientEvent;
        public event removeClientDelegate removeClientEvent;


        public List<string> _linkedList = new List<string>();
        public List<string> _forbidedList = new List<string>();
        private ConcurrentDictionary<string, int> _PendingDict = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, TCPClientState> _iptoStateDict = new ConcurrentDictionary<string, TCPClientState>();
        private ConcurrentDictionary<string, string> _commtoipportDict = new ConcurrentDictionary<string, string>();

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
        //public AsyncTCPServer(int listenPort) : this(IPAddress.Any, listenPort)
        //{

        //}

        ///// <summary>
        ///// 异步TCP服务器
        ///// </summary>
        ///// <param name="localEP">监听的终结点</param>
        //public AsyncTCPServer(IPEndPoint localEP) : this(localEP.Address, localEP.Port)
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
                _releaseInterval = Convert.ToInt32(iniFile.IniReadValue("Settings", "realive_interval"));
                _collect_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "collect_time")) * 1000;
                _get_state_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "get_state_time")) * 1000;
                _get_command_time = Convert.ToInt32(iniFile.IniReadValue("Settings", "get_command_time")) * 1000;
                _linked_timeout = Convert.ToInt32(iniFile.IniReadValue("Settings", "linked_overtime")) * 1000;


                Address = IPAddress.Parse(iniFile.IniReadValue("Listen", "serverip"));
                Port = Int32.Parse(iniFile.IniReadValue("Listen", "port"));
                this.Encoding = Encoding.Default;
                _connectStr = "server='" + dbserver + "'" + ";port= '" + dbport + "'" + ";user='" + dbuser + "'" + ";password='" + dbpassword + "'" + ";database= '" + dbdatabase + "'";
                _maxCommID = 0;
                _maxSfjID = 0;
                _maxControlID = 0;
                _maxSensorID = 0;
                _maxSfjControlID = 0;
                _commList = new List<CommDevice>();
                _controlList = new List<ControlDevice>();
                _sensorList = new List<SensorDevice>();
                _maxClient = 100000;
                _clients = new List<TCPClientState>();
                _listener = new TcpListener(Address, Port);
                _listener.AllowNatTraversal(true);
                IsRunning = false;

            }
            catch (Exception err)
            {
                string msg = DateTime.Now.ToString() + " 初始化服务器(" + this.Port + ")失败" + err.Message;
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

                    _listener.Start();
                    _listener.BeginAcceptTcpClient(
                    new AsyncCallback(HandleTcpClientAccepted), _listener);
                    //事件处理
                    ClientConnected += new EventHandler<AsyncEventArgs>(onClientConnected);
                    DataReceived += new EventHandler<AsyncEventArgs>(onDataReceived);
                    Thread iniDevThrd = new Thread(initDev);
                    iniDevThrd.Start();
                    Thread.Sleep(2000);
                    //Task.Run(() => getCommand_Thrd());
                    Thread getCmdThrd = new Thread(getCommand_Thrd);


                    getCmdThrd.Start();
                    //Task.Run(() => MQTT_Connect());

                    Thread linkedOverTime = new Thread(overtimeClientThrd);
                    linkedOverTime.Start();
                    string msg = DateTime.Now.ToString() + " 服务器(" + this.Port + ")开始监听";
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
                catch (Exception err)
                {
                    string msg = DateTime.Now.ToString() + " 服务器(" + this.Port + ")监听失败" + err.Message;
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
                _listener.Stop();
                lock (_clients)
                {
                    //关闭所有客户端连接
                    CloseAllClient();
                }

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

                TCPClientState state
                  = new TCPClientState(client, buffer) {
                    lastTime = DateTime.Now.ToString(),
                    faildTimes = 0,
                    _isolder = false,
                    clientComm = null
                  };

                lock (_clients)
                {
                    //_clients.Add(state);
                    RaiseClientConnected(state);
                }

                NetworkStream stream = state.NetworkStream;
                //开始异步读取数据
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceived, state);

                _listener.BeginAcceptTcpClient(
                  new AsyncCallback(HandleTcpClientAccepted), ar.AsyncState);
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

                if (recv == 0)
                {
                    // connection has been closed
                    //lock (_clients)
                    //{
                    //    //_clients.Remove(state);
                    //    //触发客户端连接断开事件
                    //    RaiseClientDisconnected(state);
                    //    return;
                    //}
                    Close(state);
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
        public void onClientConnected(object sender, AsyncEventArgs e)
        {
            TCPClientState state = e._state;
            string ipport = state.TcpClient.Client.RemoteEndPoint.ToString();
            string msg = null;
            if (_PendingDict.TryGetValue(ipport, out int pendingTimes))
            {
                msg = DateTime.Now.ToString() + " 新连接接入时待接受已有客户端：" + ipport;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);

                if (_iptoStateDict.TryRemove(ipport, out TCPClientState olderstate))
                {
                    state.lastTime = olderstate.lastTime;
                    _clients.Remove(olderstate);
                    olderstate.Close();
                    msg = DateTime.Now.ToString() + " 新连接接入时关闭待接受客户端：" + ipport;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    int tp = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                    if (pendingTimes + 1 > _pendingTimes || (tp > _pendingTimeout))
                    {
                        state.Close();
                    }
                    else
                    {
                        state.clientStatus = 1;
                        _clients.Add(state);
                        _PendingDict.TryAdd(ipport, pendingTimes + 1);
                        _iptoStateDict.TryAdd(ipport, state);
                        msg = DateTime.Now.ToString() + " 更新连接：" + ipport;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
            }
            else
            {
                state.clientStatus = 1;
                state.lastTime = DateTime.Now.ToString();
                _clients.Add(state);
                _PendingDict.TryAdd(ipport, 0);
                _iptoStateDict.TryAdd(ipport, state);

                msg = DateTime.Now.ToString() + " 接受新连接：" + ipport;
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
            msg = DateTime.Now.ToString() + " 接受数据：" + sRecvData;
            ShowMsgEvent(msg);
            EveryDayLog.Write(msg);
            Object clientComm = null;
            string gw_sn = null;
            clientComm = isHeartBeat(sRecvData);
            if (clientComm != null)
            {
                gw_sn = ((CommDevice)clientComm).serial_num;
            }

            //连接缓冲字典
            if (state.clientStatus == 1)
            {
                if(gw_sn == null)
                {
                    state.faildTimes++;
                    msg = DateTime.Now.ToString() + " 从待连接客户端（" + gw_sn + "）接受非心跳数据：" + sRecvData+ ",已失败次数: "+state.faildTimes.ToString();
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }

                //心跳数据
                else
                {
                    try
                    {
                        if (sRecvData.Substring(0, 12).ToUpper().Equals("150122220010"))
                        {
                            TCPClientState clientstate = findStatebySN(gw_sn, true);
                            if(clientstate !=null)
                            {
                                if(((int)DateTime.Now.Subtract(Convert.ToDateTime(clientstate.lastTime)).Duration().TotalSeconds) <= 300)
                                {
                                    msg = DateTime.Now.ToString() + "待连接客户端(" + gw_sn + ")与上一个已连接间隔<=5分钟，不允许再次连接";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);

                                }
                                else
                                {
                                    clientstate.clientStatus = 3;
                                    msg = DateTime.Now.ToString() + " 从待连接客户端(" + gw_sn + ")接受请求，关闭原有连接，准备创建新连接";
                                    this.ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }

                            }
                            state.faildTimes = 0;
                            state.lastTime = DateTime.Now.ToString();
                            state.clientStatus = 2;
                            state.clientComm = clientComm;
                            addClientEvent(gw_sn);
                            Task.Run(() => this.collectDataorState(state));
                            msg = DateTime.Now.ToString() + " 从待连接客户端(" + gw_sn + ")请求，建立连接和接受数据";
                            this.ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);

                        }
                        //已有连接
                        if (_commtoipportDict != null && (_commtoipportDict.TryGetValue(gw_sn, out string clientip)))
                        {
                            //删除老的连接
                            _linkedList.Remove(clientip);
                            _iptoStateDict.TryRemove(clientip, out TCPClientState olderstate);
                            if (_commtoipportDict.TryRemove(gw_sn, out string olderipport))
                            {
                                //_clients.Remove(olderstate);
                                //olderstate.Close();
                                olderstate._isolder = true;

                                msg = DateTime.Now.ToString() + " 接受心跳数据，待授权和已授权中同时存在：" + gw_sn + ",删除已连接中的客户端";
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                Thread.Sleep(2000);
                                try
                                {


                                    //增加新的连接

                                    state.faildTimes = 0;
                                    state.lastTime = DateTime.Now.ToString();
                                    state.clientStatus = 2;
                                    state.clientComm = (CommDevice)clientComm;
                                    state._isolder = false;
                                    _PendingDict.TryRemove(ipport, out int b);
                                    _linkedList.Add(ipport);
                                    _commtoipportDict.TryAdd(gw_sn, ipport);
                                    _iptoStateDict.TryAdd(ipport, state);
                                    Task.Run(() => collectDataorState(state));
                                    msg = DateTime.Now.ToString() + " 从待授权客户端（" + gw_sn + "）接受心跳，删除旧的已授权连接，并将新连接从待授权转入已授权";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                                catch (Exception err)
                                {
                                    msg = DateTime.Now.ToString() + " 删除旧连接，添加新连接事变，监听失败" + err.Message;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }


                            }

                        }
                        else //新连接
                        {
                            if (sRecvData.Substring(0, 12).ToUpper().Equals("150122220010"))
                            {
                                Send(state, "15012222000180");
                            }
                            if (_PendingDict.TryRemove(ipport, out int pendtimes))
                            {
                                state.faildTimes = 0;
                                state.lastTime = DateTime.Now.ToString();
                                state.clientStatus = 2;
                                state.clientComm = (CommDevice)clientComm;
                                state._isolder = false;
                                _linkedList.Add(ipport);
                                _commtoipportDict.TryAdd(gw_sn, ipport);
                                addClientEvent(gw_sn);
                                Task.Run(() => collectDataorState(state));
                                msg = DateTime.Now.ToString() + " 从待授权客户端（" + gw_sn + "）接受心跳，并将新连接从待授权转入已授权";
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }

                    }

                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 处理待授权客户端（" + gw_sn + ")心跳数据：" + sRecvData + "失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                else
                {
                    msg = DateTime.Now.ToString() + " 处理待授权客户端（" + ipport + ")非心跳数据：" + sRecvData;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);

                    int tsp = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                    //超时转入黑名单
                    if (tsp > _pendingTimeout)
                    {
                        state.faildTimes = 0;
                        state.clientStatus = 3;
                        state.lastTime = DateTime.Now.ToString();
                        _PendingDict.TryRemove(ipport, out int b);
                        if (!_forbidedList.Contains(ipport))
                        {
                            _forbidedList.Add(ipport);
                            msg = DateTime.Now.ToString() + " 待授权客户端（" + ipport + ")持续超时，转入黑名单";
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }

                    }

                }

            }
            else if (_linkedList.Contains(ipport))
            {
                //心跳数据
                if (gw_sn != null)
                {
                    state.faildTimes++;
                    state.lastTime = DateTime.Now.ToString();
                    msg = DateTime.Now.ToString() + " 从授权客户端（" + gw_sn + ")接受心跳";
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }
                //处理其他数据
                else
                {
                    gw_sn = sRecvData.Substring(0, 16);
                    bool bComm = false;
                    if (state.clientComm.commtype.ToUpper().Equals("KLC"))
                    {
                        bComm = true;
                    }
                    else if (gw_sn.ToUpper().Equals(state.clientComm.serial_num))
                    {
                        bComm = true;
                    }
                    if (!bComm)
                    {
                        state.faildTimes++;
                        msg = DateTime.Now.ToString() + " 授权客户端（" + state.clientComm.serial_num + ")获取非法数据" + sRecvData;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        return;
                    }
                    else
                    {
                        switch (state.clientComm.commtype)
                        {
                            case "PLC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                PLC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "KLC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                KLC_handlerecv(state.clientComm, sRecvData.Substring(12));
                                break;
                            case "FKC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                FKC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "XPC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                XPC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "YYC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                YYC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "SFJ-0804":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                SFJ_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "SFJ-1200":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                SFJ_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            case "DYC":
                                state.lastTime = DateTime.Now.ToString();
                                state.faildTimes = 0;
                                state.clientStatus = 2;
                                DYC_handlerecv(state.clientComm, sRecvData.Substring(16));
                                break;
                            //case "MQT":
                            //    state.lastTime = DateTime.Now.ToString();
                            //    state.faildTimes = 0;
                            //    state.clientStatus = 2;
                            //    MQT_handlerecv(state.clientComm, sRecvData.Substring(16));
                            //    break;
                            default:
                                state.faildTimes++;
                                msg = DateTime.Now.ToString() + " 处理未知类型授权客户端（" + state.clientComm.serial_num + ")数据" + sRecvData;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                break;
                        }
                    }

                }

            }
            else if (_forbidedList.Contains(ipport))
            {
                try
                {
                    gw_sn = state.clientComm.serial_num;
                    msg = DateTime.Now.ToString() + " 从黑名单客户端（" + gw_sn + ")接受数据";
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    int tsp = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                    if (_iptoStateDict == null || (!_iptoStateDict.TryGetValue(ipport, out TCPClientState onestate)))
                    {
                        if (!_iptoStateDict.TryAdd(ipport, state))
                        {
                            msg = DateTime.Now.ToString() + " 将黑名单中IP增加到iptostation字典失败";
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    //已达复活时间
                    if (tsp > _releaseInterval)
                    {
                        _forbidedList.Remove(ipport);
                        _iptoStateDict.TryRemove(ipport, out TCPClientState s);
                        gw_sn = s.clientComm.serial_num;
                        _commtoipportDict.TryRemove(gw_sn, out string b);
                        _clients.Remove(state);
                        state.Close();
                        msg = DateTime.Now.ToString() + " 黑名单客户端（" + gw_sn + ")达到复活时间，已释放";
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " 处理黑名单客户端（" + gw_sn + ")数据：" + sRecvData + "失败：" + err.Message;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                }

            }
            else
            {
                state.faildTimes = 0;
                state.clientStatus = 1;
                int tsp = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                if (tsp > _pendingTimeout)
                {
                    _iptoStateDict.TryRemove(ipport, out TCPClientState s);
                    _clients.Remove(s);
                    s.Close();
                }

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

                if (recvdata.Substring(0, 12).ToUpper().Equals("150122220010") && recvdata.Length == 44) //昆仑海岸网关请求连接
                {
                    //150122220010 31323030323031393036313231303938
                    commSN = FormatFunc.HexToAscii(recvdata.Substring(12));
                    ///////////////////////////////发送回应数据
                }
                else if (recvdata.Length == 16)
                {
                    commSN = recvdata;
                }
                else if ((recvdata.Length == 66) && string.Equals(recvdata.Substring(0, 6), "FEDC01", StringComparison.OrdinalIgnoreCase))
                {
                    commSN = recvdata.Substring(6, 12);
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
                string msg = DateTime.Now.ToString() + " 判断心跳数据失败： " + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                return sReturn;
            }
            return sReturn;
        }


        public void initDev()
        {
            while (IsRunning)
            {
                string sSQL = null;
                string msg = null;
                if (_communicationtype.Contains("1"))
                {

                    int maxCommID = 0;
                    int maxControlID = 0;
                    int maxSensorID = 0;

                    //初始化通讯设备
                    try
                    {
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSQL = "SELECT MAX(id) as maxid FROM yw_d_commnication_tbl";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSQL, conn))
                            {
                                maxCommID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网通讯设备最大ID失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxCommID > _maxCommID)
                            {
                                //如果有新设备
                                sSQL = "SELECT a.ID,a.Code,a.SerialNumber,a.CodeAddress,a.Company_ID,c.ClassName,`Interval`,b.ParamValue FROM yw_d_commnication_tbl a ";
                                sSQL += "LEFT JOIN yw_d_devicemodel_tbl b ON a.Model_ID = b.ID ";
                                sSQL += "LEFT JOIN yw_d_deviceclass_tbl c ON b.DeviceClass_ID= c.ID ";
                                sSQL += "LEFT JOIN `ys_parameter_tbl` d ON b.`Formula` = d.`Parameter_Key` ";
                                sSQL += "WHERE a.UsingState='1' AND b.State='1' AND d.Parameter_Class = 'YW-JSGS' ";
                                sSQL += "AND a.ID > '" + _maxCommID + "' Order by a.ID asc";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSQL, conn2);
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
                                        commdev.commclass = 1;
                                        _commList.Add(commdev);
                                    }
                                }
                            }
                            _maxCommID = maxCommID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网通讯设备失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化控制设备
                    try
                    {

                        sSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSQL = "SELECT MAX(id) as maxid FROM yw_d_controller_tbl";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSQL, conn))
                            {
                                maxControlID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网控制设备最大ID失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxControlID > _maxControlID)
                            {
                                //如果有新设备
                                sSQL = "SELECT a.ID,a.Code,a.PortNum,b.SignalType,c.SerialNumber,a.TravelTime,a.Block_ID FROM yw_d_controller_tbl a  ";
                                sSQL += "LEFT JOIN yw_d_devicemodel_tbl  b ON  a.Model_ID = b.ID ";
                                sSQL += "LEFT JOIN yw_d_commnication_tbl c ON a.Commucation_ID = c.ID ";
                                sSQL += "LEFT JOIN ys_parameter_tbl d ON b.Formula = d.Parameter_Key ";
                                sSQL += "LEFT JOIN yw_d_deviceclass_tbl e ON b.DeviceClass_ID = e.ID ";
                                sSQL += "WHERE a.UsingState = '1' AND b.State = '1' AND c.UsingState = '1'  order by a.ID asc";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSQL, conn2);
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
                            _maxControlID = maxControlID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网控制设备失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化传感器设备
                    try
                    {
                        sSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSQL = "SELECT MAX(id) as maxid FROM yw_d_sensor_tbl";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSQL, conn))
                            {
                                maxSensorID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网传感器最大ID失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxSensorID > _maxSensorID)
                            {
                                //如果有新设备
                                sSQL = "SELECT a.ID,a.Code,a.PortNum,b.SignalType,c.SerialNumber,a.CorrectValue,a.Block_ID,d.Parameter_Value FROM yw_d_sensor_tbl a ";
                                sSQL += "LEFT JOIN yw_d_devicemodel_tbl  b ON  a.Model_ID = b.ID ";
                                sSQL += "LEFT JOIN yw_d_commnication_tbl c ON a.Commucation_ID = c.ID ";
                                sSQL += "LEFT JOIN ys_parameter_tbl d ON b.Formula= d.Parameter_Key ";
                                sSQL += "LEFT JOIN yw_d_deviceclass_tbl e ON b.DeviceClass_ID= e.ID ";
                                sSQL += "WHERE a.UsingState = '1' AND b.State = '1' AND c.UsingState = '1' AND d.Parameter_Class = 'YW-JSGS' order by a.ID asc ";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSQL, conn2);
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
                                        sensordev.devclass = 10;
                                        _sensorList.Add(sensordev);
                                    }
                                }

                            }
                            _maxSensorID = maxSensorID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取物联网传感器失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }


                if (_communicationtype.Contains("2"))
                {
                    int maxSfjID = 0;
                    int maxSfjControlID = 0;
                    //初始化水肥机设备
                    try
                    {
                        sSQL = null;
                        //是否有新的水肥机设备,通过设备ID，必须自增长
                        sSQL = "SELECT MAX(id) as maxid FROM sfyth_plc";
                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSQL, conn))
                            {
                                maxSfjID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机PLC最大ID失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxSfjID > _maxSfjID)
                            {
                                //如果有新设备
                                sSQL = "SELECT ID,PLC_Name,PLC_Number,PLC_Address,Company_ID,PLC_GWType,TotalPass  FROM sfyth_plc ";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSQL, conn2);
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
                                        //commdev.commpara = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("PassNumber");
                                        commdev.passnum = (msdr2.IsDBNull(6)) ? 0 : msdr2.GetInt32("TotalPass");
                                        commdev.commclass = 2;
                                        _commList.Add(commdev);
                                    }
                                }

                            }
                            _maxSfjID = maxSfjID;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机通讯设备失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                    //初始化控制设备
                    try
                    {
                        sSQL = null;
                        //是否有新的通讯设备,通过设备ID，必须自增长
                        sSQL = "SELECT MAX(id) as maxid FROM sfyth_device";

                        using (MySqlConnection conn = new MySqlConnection(_connectStr))
                        {
                            if (conn.State == ConnectionState.Closed)
                            {
                                conn.Open();
                            }
                            using (MySqlCommand myCmd = new MySqlCommand(sSQL, conn))
                            {
                                maxSfjControlID = Convert.ToInt32(myCmd.ExecuteScalar());
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        msg = "获取水肥机控制器最大ID失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    try
                    {
                        using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                        {
                            if (maxSfjControlID > _maxSfjControlID)
                            {
                                //如果有新设备
                                sSQL = "SELECT a.ID,a.Device_Name,a.Device_Address,a.Device_Type,a.PLC_Number FROM sfyth_device a ";
                                sSQL += "LEFT JOIN sfyth_plc b ON a.PLC_Number = b.PLC_Number WHERE a.Is_Delete = 0 AND b.Is_Delete = 0";
                                if (conn2.State == ConnectionState.Closed)
                                {
                                    conn2.Open();
                                }
                                MySqlCommand myCmd2 = new MySqlCommand(sSQL, conn2);
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
                            _maxSfjControlID = maxSfjControlID;
                        }
                    }
                    catch (Exception err)
                    {

                        msg = DateTime.Now.ToString() + " 初始化水肥机控制设备失败： " + err.Message;
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
            byte[] a = FormatFunc.strToHexByte(data);
            Send(state.TcpClient, FormatFunc.strToHexByte(data));
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="state">接收数据的客户端会话</param>
        /// <param name="data">数据报文</param>
        public void Send(TCPClientState state, byte[] data)
        {
            RaisePrepareSend(state);
            Send(state.TcpClient, data);
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
                    msg = "This TCP Scoket server has not been started";
                    EveryDayLog.Write(msg);
                }
                if (client == null)
                {
                    //throw new ArgumentNullException("client");
                    msg = "client is null";
                    EveryDayLog.Write(msg);
                }
                if (data == null)
                {
                    //throw new ArgumentNullException("data");
                    msg = "data is null";
                    EveryDayLog.Write(msg);
                }
                if (client.Connected)
                {
                    client.GetStream().BeginWrite(data, 0, data.Length, SendDataEnd, client);
                }
            }
            catch (Exception err)
            {
                msg = DateTime.Now.ToString() + " 发送数据失败： " + err.Message;
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
            string sSQL = null;
            string sOneState = null;
            int iLen = 0;
            string msg = null;
            if (sdata.Substring(2, 2).ToUpper().Equals("0F"))
            {
                toHandleData = sdata.Substring(14, sdata.Length - 18);
                iLen = Convert.ToInt16(sdata.Substring(8, 4), 16);
            }
            else if (sdata.Substring(2, 2).ToUpper().Equals("05"))
            {
                if (sdata.Length > 21)
                {
                    if (sdata.Substring(18, 2).ToUpper().Equals("0F"))
                    {
                        toHandleData = sdata.Substring(30, sdata.Length - 34);
                        iLen = Convert.ToInt16(sdata.Substring(24, 4), 16);
                    }
                }

            }
            if (toHandleData != null)
            {
                sSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                string devstate = null;
                for (int i = 0; i * 2 < toHandleData.Length; i++)
                {
                    //状态按寄存器顺序，小端模式：低字节在前，高字节在后,每个字节再转为低位在前，形成从小到大的开关量
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
                                    msg = DateTime.Now.ToString() + " 行程开关状态数据解析失败：开和关同时为开启状态";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }

                            }
                            sSQL += "('" + oneControl.devid + "','" + sOneState + "'),";
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " 行程开关状态数据解析失败：" + err.Message;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }

                }
                if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                {
                    sSQL = sSQL.Substring(0, sSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " PLC通讯设备(" + comm.serial_num + ")更新状态成功：" + sSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " PLC通讯设备(" + comm.serial_num + ")更新状态失败：" + sSQL;
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
                string sSQL = null;
                string msg = null;
                //传感器数据
                if (sAddr.Equals("01") && sOrder.Equals("03"))
                {
                    sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
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
                                msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）处理通道数据错误:" + err.Message;
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
                                    msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;
                            }
                            sSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sSensorValue + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
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
                    sSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                    //02 03 08 A1 40 FF FF A2 40 FF FF
                    string sstate = null;
                    for (int i = 0; i < 2; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            string recvState = sdata.Substring(i * 8 + 10, 4).ToUpper();
                            if (recvState.ToUpper().Equals("FFFF"))
                            {
                                sstate = "1";
                            }
                            else if (recvState.ToUpper().Equals("0000"))
                            {
                                sstate = "2";
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）获取状态错误标识:" + recvState;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            sSQL += "('" + oneControl.devid + "','" + sstate + "'),";

                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）控制通道更新状态成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）控制通道更新状态失败:" + sSQL;
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
            //020300207fff02ea00000000000000000000000000e400de00007fff0000003f00020290f305
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {

                string sOrder = sdata.Substring(2, 2);
                string sSQL = null;
                sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                string msg = null;
                if (sOrder.Equals("03"))
                {
                    string sensorRecv = null;
                    double sensorValue = 0;
                    string sensorData = null;
                    for (int i = 0; i < 16; i++)
                    {
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 11);
                        if (oneDev != null)
                        {
                            SensorDevice oneSensor = (SensorDevice)oneDev;
                            sensorRecv = sdata.Substring(i * 4 + 8, 4);   //这是和新普惠的区别，飞科数据长度占2个字节

                            if (sensorRecv.ToUpper().ToUpper().Equals("7FFF"))
                            {
                                sensorValue = Convert.ToInt32(sensorRecv, 16);
                            }
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SHIDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "BEAM":
                                    sensorValue = sensorValue * 10;
                                    break;
                                case "CO2":
                                    break;
                                case "Q-WENDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "Q-SHIDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "PH":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "EC":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "FS":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "FX":
                                    break;
                                case "YL":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "QY":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SWD":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SWZ":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "ORP":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SPH":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SEC":
                                    sensorValue = sensorValue / 1000;
                                    break;
                                case "VOC":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "PM25":
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " FKC设备（" + comm.serial_num + "）未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;

                            }
                            sSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " FKC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " FKC设备（" + comm.serial_num + "）插入最新数据失败:" + sSQL;
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
                else if (sOrder.Equals("70"))
                {
                    sSQL = "insert into yw_d_controller_tbl(id,onoff) values ";
                    for (int i = 0; i < 16; i++)
                    {
                        string sstate = null;
                        Object oneDev = findDevbyAddr(i + 1, comm.serial_num, 10);
                        if (oneDev != null)
                        {
                            ControlDevice oneControl = (ControlDevice)oneDev;
                            if (sdata.Substring(i * 2 + 4, 2).Equals("1"))
                            {
                                sstate = "1";
                            }
                            else if (sdata.Substring(i * 2 + 4, 2).Equals("0"))
                            {
                                sstate = "2";
                            }
                            else
                            {
                                msg = DateTime.Now.ToString() + " KLC设备（" + comm.serial_num + "）获取状态错误标识:" + sdata.Substring(i * 2 + 4, 2);
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }

                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1) + " ON DUPLICATE KEY UPDATE onoff = values(onoff);";
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " FKC设备（" + comm.serial_num + "）控制通道更新状态成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " FKC设备（" + comm.serial_num + "）控制通道更新状态失败:" + sSQL;
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
                string sSQL = null;
                string msg = null;
                if (sOrder.Equals("03"))
                {
                    sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                    string sensorRecv = null;
                    double sensorValue = 0;
                    string sensorData = null;
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
                            switch (oneSensor.devformula)
                            {
                                case "WENDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SHIDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "BEAM":
                                    sensorValue = sensorValue * 10;
                                    break;
                                case "CO2":
                                    break;
                                case "Q-WENDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "Q-SHIDU":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "PH":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "EC":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "FS":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "FX":
                                    break;
                                case "YL":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "QY":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SWD":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SWZ":
                                    sensorValue = sensorValue / 100;
                                    break;
                                case "ORP":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SPH":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "SEC":
                                    sensorValue = sensorValue / 1000;
                                    break;
                                case "VOC":
                                    sensorValue = sensorValue / 10;
                                    break;
                                case "PM25":
                                    break;
                                default:
                                    msg = DateTime.Now.ToString() + " XPC设备（" + comm.serial_num + "）未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;

                            }
                            sSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " XPC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " XPC设备（" + comm.serial_num + "）插入最新数据失败:" + sSQL;
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


        public void YYC_handlerecv(CommDevice comm, string sdata)
        {
            if (sdata.Substring(sdata.Length - 4).ToUpper().Equals(FormatFunc.ToModbusCRC16(sdata.Substring(0, sdata.Length - 4)).ToUpper()))
            {
                string sOrder = sdata.Substring(2, 2);
                string sSQL = null;
                string msg = null;
                if (sOrder.Equals("03"))
                {
                    sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
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
                                    msg = DateTime.Now.ToString() + " YYC设备（" + comm.serial_num + "）未知的公式:" + oneSensor.devformula;
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                    break;

                            }
                            sSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorData + "','" + oneSensor.blockid + "'),";
                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " YYC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " YYC设备（" + comm.serial_num + "）插入最新数据失败:" + sSQL;
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
            string sSQL = null;
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
                    sSQL = "INSERT INTO sfyth_log (R_Start,R_End,R_Interval,R_Gquantity,R_Squantity,T_Area,T_Fertilize,R_State,T_Date,T_ID,T_Type,Company_ID,PLC_Number,DO_Type) ";
                    sSQL += "values('" + starttime + "','" + endtime + "','" + interval_real.ToString() + "','" + water_real.ToString() + "','" + ferter_real.ToString() + "','" + sArea + "',";
                    sSQL += "'" + sFertcomm + "','" + taskstate.ToString() + "','" + taskdate + "','" + taskid.ToString() + "','" + tasktype.ToString() + "','" + comm.companyid + "','" + comm.serial_num + "','" + isRemote.ToString() + "')";
                }

                MySqlConnection conn = new MySqlConnection(_connectStr);
                MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                conn.Open();
                try
                {
                    int iSQLResult = myCmd.ExecuteNonQuery();
                    if (iSQLResult > 0)
                    {
                        msg = DateTime.Now.ToString() + " SFJ设备（" + comm.serial_num + "）插入新的任务日志成功:" + sSQL;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
                catch (Exception err)
                {
                    msg = DateTime.Now.ToString() + " SFJ设备（" + comm.serial_num + "）插入插入新的任务日志失败:" + sSQL;
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
                sSQL = "insert into sfyth_device(id,State) values ";
                string devstate = sdata.Substring(sdata.Length - 6, 2) + sdata.Substring(sdata.Length - 8, 2);
                devstate = FormatFunc.HexString2BinString(devstate, true, true);
                devstate = rightSub("0000000000000000" + FormatFunc.reverseString(devstate), 16);
                for (int i = 0; i < 16; i++) //控制器扩展？？？
                {
                    object oneDev = findDevbyAddr(i + 1, comm.serial_num, 20);
                    if (oneDev != null)
                    {
                        ControlDevice oneControl = (ControlDevice)oneDev;
                        sSQL += "('" + oneControl.devid + "','" + devstate[i].ToString() + "'),";
                    }

                }
                if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                {
                    //sSQL = sSQL.Substring(0, sSQL.Length - 1);
                    sSQL = sSQL.Substring(0, sSQL.Length - 1) + " ON DUPLICATE KEY UPDATE State = values(State)";
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " SFJ设备（" + comm.serial_num + "）更新状态成功:" + sSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " SFJ设备（" + comm.serial_num + "）更新状态失败:" + sSQL;
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


        public void DYC_handlerecv(CommDevice comm, string sdata)
        {

            string sOrder = sdata.Substring(2, 2);
            string sSQL = null;
            string msg = null;
            if (sOrder.Equals("03") && sdata.Length == 44)
            {
                sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
                string sensorRecv = sdata.Substring(6, 4);
                double sensorValue = Convert.ToInt32(sensorRecv, 16);
                object oneDev = findDevbyAddr(1, comm.serial_num, 11);
                if (oneDev != null)
                {
                    SensorDevice oneSensor = (SensorDevice)oneDev;
                    sSQL += "('" + oneSensor.devid + "','" + oneSensor.devcode + "',now(),'" + sensorValue.ToString() + "','" + oneSensor.blockid + "'),";
                }

                if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                {
                    sSQL = sSQL.Substring(0, sSQL.Length - 1);
                    MySqlConnection conn = new MySqlConnection(_connectStr);
                    MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                    conn.Open();
                    try
                    {
                        int iSQLResult = myCmd.ExecuteNonQuery();
                        if (iSQLResult > 0)
                        {
                            msg = DateTime.Now.ToString() + " XPC设备（" + comm.serial_num + "）插入最新数据成功:" + sSQL;
                            ShowMsgEvent(msg);
                            EveryDayLog.Write(msg);
                        }
                    }
                    catch (Exception err)
                    {
                        msg = DateTime.Now.ToString() + " XPC设备（" + comm.serial_num + "）插入最新数据失败:" + sSQL;
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
        public void MQTT_Connect()
        {
            Thread.Sleep(60000);
            string topic = null;
            // MQTT服务器IP地址
            string host = "119.3.215.67";
            int serverPort = 1883;
            // 实例化Mqtt客户端 
            MqttClient client = new MqttClient(host, Convert.ToInt32(serverPort), false, null, null, MqttSslProtocols.None);

            // 注册接收消息事件 
            client.MqttMsgPublishReceived += MQT_handlerecv;

            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId, "kywlw", "12345678");

            //订阅主题 "/mqtt/test"， 订阅质量 QoS 2
            foreach (CommDevice onecomm in _commList)
            {
                if (onecomm.commtype.ToUpper().Equals("MQT"))
                {
                    topic = "stds/up/CL/" + onecomm.serial_num;
                    int msgID = 0;
                    msgID = client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                    if (msgID > 0)
                    {
                        string msg = DateTime.Now.ToString() + " MQTT设备（" + onecomm.serial_num + "）订阅成功，消息ID:" + msgID.ToString();
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                }
            }
            //topic = "stds/up/CL/+";
            //client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }

        public void MQT_handlerecv(object sender, MqttMsgPublishEventArgs e)
        {
            string a = (string.Format("subscriber,topic:{0},content:{1}", e.Topic, Encoding.UTF8.GetString(e.Message)));
            string mqtttop = e.Topic;
            string mqttpayload = Encoding.UTF8.GetString(e.Message);
            string[] mqtttoparray = mqtttop.Split('/');
            string commsn = mqtttoparray[mqtttoparray.Length - 1];
            string msg = null;
            msg = DateTime.Now.ToString() + " MQTT设备（" + commsn + "）接受数据:" + mqttpayload;
            ShowMsgEvent(msg);
            EveryDayLog.Write(msg);


            string sSQL = "INSERT INTO yw_c_sensordata_tbl (Device_ID,Device_Code,ReportTime,ReportValue,Block_ID) values ";
            object one = getCommbySN(commsn, 1);
            if (one != null)
            {
                int subGroupID = 0;
                string sSensorValue = null;
                CommDevice oneComm = (CommDevice)one;
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
                                    break;
                                case "airT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "ill":
                                    sSensorValue = kvp.Value;
                                    break;
                                case "soilH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "soilT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "soilC":
                                    sSensorValue = kvp.Value;
                                    break;
                                case "co2":
                                    sSensorValue = kvp.Value;
                                    break;
                                case "dPH":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "DO":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "level":
                                    sSensorValue = kvp.Value;
                                    break;
                                case "LiquidT":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "windS":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "windD":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "atm":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "rainF":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "sDur":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "eCap":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.01).ToString("#.##");
                                    break;
                                case "LiquidC":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "LiquidP":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                case "ORP":
                                    sSensorValue = (Convert.ToInt32(kvp.Value) * 0.1).ToString("#.#");
                                    break;
                                default:
                                    break;
                            }
                            sSQL = sSQL + "('" + _sensorList[_sensorIndex].devid.ToString() + "','" + _sensorList[_sensorIndex].devcode + "', Now() ,'" + sSensorValue + "','" + _sensorList[_sensorIndex].blockid.ToString() + "'),";
                        }
                    }
                    if (sSQL.Substring(sSQL.Length - 2).Equals("),"))
                    {
                        sSQL = sSQL.Substring(0, sSQL.Length - 1);
                        MySqlConnection conn = new MySqlConnection(_connectStr);
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        conn.Open();
                        try
                        {
                            int iSQLResult = myCmd.ExecuteNonQuery();
                            if (iSQLResult > 0)
                            {
                                msg = DateTime.Now.ToString() + " MQT设备（" + commsn + "）插入最新数据成功:" + sSQL;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                        }
                        catch (Exception err)
                        {
                            msg = DateTime.Now.ToString() + " MQT设备（" + commsn + "）插入最新数据失败:" + sSQL;
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
            int comm_interval = state.clientComm.commpara;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 5 * 60 * 1000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 5 * 60 * 1000;
            }
            int startCollect = collect_time - _get_state_time;
            int starttime = FormatFunc.getTimeStamp();
            while (state != null && _commtoipportDict.TryGetValue(comm_sn, out string ipport) && state._isolder == false)
            {
                int nowstamp = FormatFunc.getTimeStamp();
                int timeGap = nowstamp - starttime;
                if (timeGap >= startCollect)
                {
                    //开始采集数据
                    Thread.Sleep(collect_time - timeGap);
                    sendStr = "15010000000601";
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0020"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    Send(state, sendStr);
                    string msg = DateTime.Now.ToString() + " KLC设备（" + comm_sn + "）发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    // 重置启动时间
                    starttime = FormatFunc.getTimeStamp();
                }
                else if (timeGap >= _get_state_time)
                {
                    //开始获取状态
                    sendStr = "15010000000602";
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0004"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    Send(state, sendStr);
                    string msg = DateTime.Now.ToString() + " KLC设备（" + comm_sn + "）发送状态获取指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Thread.Sleep(_get_state_time);
                }
                else
                {
                    //时间片小于状态获取时间
                    Thread.Sleep(_get_state_time);
                }
            }
        }
        public void FKC_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 5 * 60 * 1000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 5 * 60 * 1000;
            }


            int startCollect = collect_time - _get_state_time;
            int starttime = FormatFunc.getTimeStamp();
            while (state != null && _commtoipportDict.TryGetValue(comm_sn, out string ipport) && state._isolder == false)
            {
                int nowstamp = FormatFunc.getTimeStamp();
                int timeGap = nowstamp - starttime;
                if (timeGap >= startCollect)
                {
                    //开始采集数据
                    Thread.Sleep(collect_time - nowstamp);
                    sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                    sendStr += "03";
                    sendStr += "0000"; //起始地址
                    sendStr += "0010"; //数量
                    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                    Send(state, sendStr);
                    string msg = DateTime.Now.ToString() + " FKC设备（" + comm_sn + "）发送数据采集指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    // 重置启动时间
                    starttime = FormatFunc.getTimeStamp();

                }
                else if (timeGap >= _get_state_time)
                {
                    //开始获取状态
                    sendStr = "00700054";
                    Send(state, sendStr);
                    string msg = DateTime.Now.ToString() + " YYC设备（" + comm_sn + "）发送状态获取指令:" + sendStr;
                    ShowMsgEvent(msg);
                    EveryDayLog.Write(msg);
                    Thread.Sleep(_get_state_time);
                }
                else
                {
                    //时间片小于状态获取时间
                    Thread.Sleep(_get_state_time);
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
            int comm_interval = state.clientComm.commpara;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 5 * 60 * 1000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 5 * 60 * 1000;
            }

            while (state != null && _commtoipportDict.TryGetValue(comm_sn, out string ipport) && state._isolder == false)
            {
                //开始采集数据，如果是Modbus协议，接受数据的数据域长度为1个字节，飞科的为2个字节
                sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                sendStr += "03";
                sendStr += "0000"; //起始地址
                sendStr += "0010"; //数量
                sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                Send(state, sendStr);
                string msg = DateTime.Now.ToString() + " XPC设备（" + comm_sn + "）发送数据采集指令:" + sendStr;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                Thread.Sleep(collect_time);
            }

            //////XPH协议
            ////while (state != null && _commtoipportDict.TryGetValue(comm_sn, out string ipport))
            ////{
            ////    //开始采集数据，如果是XPH协议，接受数据的数据域长度为2个字节和飞科的一样，返回数据包含了继电器状态
            ////    sendStr = state.clientComm.commpara.ToString().PadLeft(2, '0');
            ////    sendStr += "03";
            ////    sendStr += "0000"; //起始地址
            ////    sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
            ////    Send(state, sendStr);
            ////    Thread.Sleep(_get_state_time);
            ////}
        }




        public void YYC_getStateorDataThrd(TCPClientState state)
        {
            string comm_sn = state.clientComm.serial_num;
            int comm_interval = state.clientComm.commpara;
            string commaddr = state.clientComm.commaddr;
            int collect_time = 0;
            string sendStr = null;
            //采集时间>5分钟
            if (comm_interval > 5 * 60 * 1000)
            {
                collect_time = (int)comm_interval;
            }
            else
            {
                collect_time = 5 * 60 * 1000;
            }

            while (state != null && _commtoipportDict.TryGetValue(comm_sn, out string ipport) && state._isolder == false)
            {
                //开始采集数据，如果是Modbus协议，接受数据的数据域长度为1个字节，飞科的为2个字节
                sendStr = state.clientComm.commaddr.ToString().PadLeft(2, '0');
                sendStr += "03";
                sendStr += "0000"; //起始地址
                sendStr += "0020"; //数量
                sendStr += FormatFunc.ToModbusCRC16(sendStr, true);
                Send(state, sendStr);
                string msg = DateTime.Now.ToString() + " YYC设备（" + comm_sn + "）发送数据采集指令:" + sendStr;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                Thread.Sleep(collect_time);
            }
        }
        public void getCommand_Thrd()
        {
            while (IsRunning)
            {

                string msg = null;
                string sSQL = null;
                string resultSQL = null;
                string comm_sn = null;
                List<CommandInfo> cmdList = new List<CommandInfo>();
                if (_communicationtype.Contains("1"))
                {
                    //未来增加重发次数
                    sSQL = "SELECT a.id,a.Device_ID,a.ActOrder,a.actparam,a.scheduledtime,a.createtime,c.serialNumber,b.code ";
                    sSQL += "FROM yw_c_control_log_tbl a ";
                    sSQL += "LEFT JOIN yw_d_controller_tbl b ON a.Device_ID = b.ID ";
                    sSQL += "LEFT JOIN yw_d_commnication_tbl c ON b.Commucation_ID = c.ID ";
                    sSQL += "where (a.ActOrder = 'AC-OPEN' OR a.ActOrder = 'AC-CLOSE' OR a.ActOrder = 'AC-STOP') AND a.`ExecuteResult` < 4 ";
                    sSQL += "AND (ISNULL(a.ScheduledTime) OR (NOW() > a.ScheduledTime AND DATE_ADD(a.ScheduledTime,INTERVAL 10 MINUTE)>NOW())) AND (c.SerialNumber IS NOT NULL) ";
                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        MySqlDataReader msdr = myCmd.ExecuteReader();
                        while (msdr.Read())
                        {
                            msg = DateTime.Now.ToString() + " 发现有任务";
                            ShowMsgEvent(msg);
                            int commandID = 0;
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
                                // 超时10分钟
                                //未超时10分钟
                                if (schdTime.Length == 0 || (orderOverTime < 600 && orderOverTime >= 0))
                                {
                                    if (_commtoipportDict.TryGetValue(comm_sn, out string ipport))
                                    {
                                        if (_iptoStateDict.TryGetValue(ipport, out TCPClientState state))
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
                                            cmdInfo.client = state;
                                            lock (cmdList)
                                            {
                                                cmdList.Add(cmdInfo);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        msg = DateTime.Now.ToString() + " 物联网未找到编号为：" + comm_sn + " 的ipport为：" + ipport + " 的TcpClient";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 物联网未找到符合" + comm_sn + " 的通讯设备连接ipport";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                msg = DateTime.Now.ToString() + " 执行控制指令时，发送未知错误" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            finally
                            {
                                if (commandID > 0)
                                {
                                    resultSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult='4' where id = '" + commandID.ToString() + "'";
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
                    sSQL = "SELECT a.ID, a.Device_ID,ActOrder,b.Device_Address,a.ScheduledTime,a.CreateTime,c.PLC_Number FROM sfyth_control_log a ";
                    sSQL += "LEFT JOIN sfyth_device b ON a.Device_ID = b.ID  LEFT JOIN sfyth_plc c ON a.PLC_Number = c.PLC_Number ";
                    sSQL += "WHERE a.ActState = 0 AND (ISNULL(a.ScheduledTime) OR (NOW() > a.ScheduledTime AND DATE_ADD(a.ScheduledTime,INTERVAL 10 MINUTE)>NOW())) ";

                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
                        MySqlDataReader msdr = myCmd.ExecuteReader();
                        while (msdr.Read())
                        {
                            int commandID = 0;
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
                                    if (_commtoipportDict.TryGetValue(comm_sn, out string ipport))
                                    {
                                        if (_iptoStateDict.TryGetValue(ipport, out TCPClientState state))
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
                                            cmdInfo.client = state;
                                            lock (cmdList)
                                            {
                                                cmdList.Add(cmdInfo);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        msg = DateTime.Now.ToString() + " 水肥机未找到编号为：" + comm_sn + " 的ipport为：" + ipport + " 的TcpClient";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 水肥机未找到符合" + comm_sn + " 的水肥机设备连接ipport";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                msg = DateTime.Now.ToString() + " 执行水肥机控制指令时，发送未知错误" + err.Message;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                            }
                            finally
                            {
                                if (commandID > 0)
                                {
                                    resultSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='30' where id = '" + commandID.ToString() + "'";
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
                    sSQL = "SELECT T_Start,T_Interval,T_Gquantity,T_Squantity,T_Area,T_Fertilize,a.T_Date,T_Type,a.T_ID,b.PLC_Number FROM  sfyth_task a ";
                    sSQL += "LEFT JOIN sfyth_plc b ON b.PLC_Number = a.PLC_Number WHERE a.T_State=0 AND (ISNULL(a.T_Start) OR (NOW() > a.T_Start AND  ";
                    sSQL += "DATE_ADD(DATE_FORMAT(CONCAT(a.T_Date,' ',a.T_Start),'%Y-%m-%d %H:%i:%s'),INTERVAL 10 MINUTE)>NOW())) ORDER BY a.T_ID ASC ";
                    using (MySqlConnection conn = new MySqlConnection(_connectStr))
                    {
                        if (conn.State == ConnectionState.Closed)
                        {
                            conn.Open();
                        }
                        MySqlCommand myCmd = new MySqlCommand(sSQL, conn);
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
                                    if (_commtoipportDict.TryGetValue(comm_sn, out string ipport))
                                    {
                                        if (_iptoStateDict.TryGetValue(ipport, out TCPClientState state))
                                        {
                                            //T_Start,T_Interval,T_Gquantity,T_Squantity,T_Area,T_Fertilize,a.T_Date,T_Type,a.T_ID,b.PLC_Number
                                            CommandInfo cmdInfo = new CommandInfo();
                                            cmdInfo.commandID = (msdr.IsDBNull(8)) ? 0 : msdr.GetInt32("T_ID");
                                            cmdInfo.actOrder = "SEND-TASK";
                                            cmdInfo.warterInterval = (msdr.IsDBNull(1)) ? 0 : Convert.ToInt32(msdr.GetString("T_Interval"));
                                            cmdInfo.scheduledTime = ((msdr.IsDBNull(6)) ? "" : msdr.GetString("T_Date")); ;
                                            cmdInfo.createTime = (msdr.IsDBNull(0)) ? "" : msdr.GetString("T_Start");
                                            cmdInfo.serialNumber = (msdr.IsDBNull(9)) ? "" : msdr.GetString("PLC_Number");
                                            cmdInfo.client = state;
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
                                    }
                                    else
                                    {
                                        msg = DateTime.Now.ToString() + " 水肥机未找到编号为：" + comm_sn + " 的ipport为：" + ipport + " 的TcpClient";
                                        ShowMsgEvent(msg);
                                        EveryDayLog.Write(msg);
                                    }
                                }
                                else
                                {
                                    msg = DateTime.Now.ToString() + " 水肥机未找到符合" + comm_sn + " 的通讯设备连接ipport";
                                    ShowMsgEvent(msg);
                                    EveryDayLog.Write(msg);
                                }
                            }
                            catch (Exception err)
                            {
                                msg = DateTime.Now.ToString() + " 水肥机执行控制指令时，发送未知错误" + err.Message;
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
                            case "MQT":
                                //Task.Run(() => MQT_sendOrder(cmdInfo));
                                break;
                            default:
                                msg = DateTime.Now.ToString() + " 编号为：" + comm_sn + " 的未知类型：" + comm_type;
                                ShowMsgEvent(msg);
                                EveryDayLog.Write(msg);
                                break;
                        }
                    }

                }
                Thread.Sleep(_get_command_time);
            }

        }

        public void overtimeClientThrd()
        {
            while (true)
            {
                foreach (KeyValuePair<string, TCPClientState> one in _iptoStateDict)
                {
                    string lasttime = one.Value.lastTime;
                    TCPClientState state = one.Value;
                    int tp = (int)DateTime.Now.Subtract(Convert.ToDateTime(state.lastTime)).Duration().TotalSeconds;
                    if (tp > _linked_timeout || (!state.TcpClient.Connected))
                    {
                        state.Close();
                    }
                    else
                    {
                        if (state.TcpClient.Connected)
                        {
                            Send(state, "01020304");
                        }
                    }
                }
                foreach (TCPClientState client in _clients)
                {
                    if (client._isolder)
                    {
                        int tp = (int)DateTime.Now.Subtract(Convert.ToDateTime(client.lastTime)).Duration().TotalSeconds;
                        if (tp > 900 || (!client.TcpClient.Connected))
                        {
                            client.Close();
                            _clients.Remove(client);
                        }

                    }
                }

                Thread.Sleep(600000);//10分钟检测1次
            }
        }


        public void PLC_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSQL = null;
            string sSendStr = null;
            sSQL = "";
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = (CommDevice)getCommbySN(oneCmd.serialNumber, 1);
            if (oneCmd.actparam > 0)
            {
                Thread.Sleep(oneCmd.actparam);//延迟
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
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
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
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        Thread.Sleep(500); //间隔0.5秒

                        sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                        sSendStr += "05";
                        sSendStr += Convert.ToString(oneControl.devaddr + 1999, 16).PadLeft(4, '0');
                        sSendStr += "FF00";
                        sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                        Send(oneCmd.client, sSendStr);
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    else
                    {
                        msg = DateTime.Now.ToString() + " 设备编号为：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;

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
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
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
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                        Thread.Sleep(500); //间隔0.2秒
                        sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                        sSendStr += "05";
                        sSendStr += Convert.ToString(oneControl.devaddr + 2000, 16).PadLeft(4, '0');
                        sSendStr += "FF00";
                        sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                        Send(oneCmd.client, sSendStr);
                        msg = DateTime.Now.ToString() + " 设备（" + oneControl.devcode + " ）发送" + oneCmd.actOrder + "指令:" + sSendStr;
                        ShowMsgEvent(msg);
                        EveryDayLog.Write(msg);
                    }
                    else
                    {
                        msg = DateTime.Now.ToString() + " 设备编号：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;
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
                        msg = DateTime.Now.ToString() + " 设备编号：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;
                    }
                    break;
                default:
                    break;
            }
            try
            {
                sSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult='4' where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSQL, conn2);
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

        public void KLC_sendOrder(CommandInfo oneCmd)
        {
            string msg = null;
            string sSQL = null;
            string sSendStr = null;
            sSQL = "";
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = (CommDevice)getCommbySN(oneCmd.serialNumber, 1);
            if (oneCmd.actparam > 0)
            {
                Thread.Sleep(oneCmd.actparam);//延迟
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
            try
            {
                sSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult='4' where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSQL, conn2);
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
        public void FKC_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSQL = null;
            string sSendStr = null;
            sSQL = "";
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = (CommDevice)getCommbySN(oneCmd.serialNumber, 1);
            if (oneCmd.actparam > 0)
            {
                Thread.Sleep(oneCmd.actparam);//延迟
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
                        msg = DateTime.Now.ToString() + " 设备编号为：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;

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
                        msg = DateTime.Now.ToString() + " 设备编号：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;
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
                        msg = DateTime.Now.ToString() + " 设备编号：" + oneControl.devcode + " 为未知的设备类型：" + ((ControlDevice)oneControl).devtype;
                    }
                    break;
                default:
                    break;
            }
            try
            {
                sSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult='4' where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSQL, conn2);
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
                msg = DateTime.Now.ToString() + " 控制指令ID:" + oneCmd.commandID.ToString() + " 执行错误:" + err.Message;
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }

        public void DYC_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSQL = null;
            string sSendStr = null;
            sSQL = "";
            ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 10);
            CommDevice oneComm = (CommDevice)getCommbySN(oneCmd.serialNumber, 1);
            if (oneCmd.actparam > 0)
            {
                Thread.Sleep(oneCmd.actparam);//延迟
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
            try
            {
                sSQL = "update yw_c_control_log_tbl set ExecuteTime=now(),ExecuteResult='4' where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSQL, conn2);
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
        public void SFJ_sendOrder(CommandInfo oneCmd)
        {

            string msg = null;
            string sSQL = null;
            string sSendStr = null;
            string sArea = null;
            string sFert = null;
            sSQL = "";

            CommDevice oneComm = (CommDevice)getCommbySN(oneCmd.serialNumber, 2);
            ////if (oneCmd.actparam > 0)
            ////{
            ////    Thread.Sleep(oneCmd.actparam);//延迟
            ////}
            switch (oneCmd.actOrder)
            {
                case "AC-OPEN":
                    ControlDevice oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 20);
                    sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                    sSendStr += "05";
                    sSendStr += Convert.ToString(oneControl.devaddr + 999, 16).PadLeft(4, '0');
                    sSendStr += "FF00";
                    sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                    Send(oneCmd.client, sSendStr);
                    sSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
                    break;
                case "AC-CLOSE":
                    oneControl = (ControlDevice)findDevbyID(oneCmd.deviceID, 20);
                    sSendStr = Convert.ToString(Convert.ToInt32(oneComm.commaddr), 16).PadLeft(2, '0');
                    sSendStr += "05";
                    sSendStr += Convert.ToString(oneControl.devaddr + 999, 16).PadLeft(4, '0');
                    sSendStr += "0000";
                    sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                    Send(oneCmd.client, sSendStr);
                    sSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
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
                    sSendStr += oneCmd.taskType.PadLeft(4, '0'); //任务类型：0-灌溉，1-施肥
                    sSendStr += FormatFunc.ToModbusCRC16(sSendStr, true);
                    Send(oneCmd.client, sSendStr);
                    sSQL = "UPDATE sfyth_task SET T_State='1'  where T_ID = '" + oneCmd.commandID.ToString() + "'";
                    break;
                default:
                    break;
            }
            try
            {
                //sSQL = "update sfyth_control_log set ExecuteTime=now(),ActState='1'  where id = '" + oneCmd.commandID.ToString() + "'";
                using (MySqlConnection conn2 = new MySqlConnection(_connectStr))
                {
                    if (conn2.State == ConnectionState.Closed)
                    {
                        conn2.Open();
                    }
                    MySqlCommand resultCmd = new MySqlCommand(sSQL, conn2);
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
        /// <param name="devclass">10-控制器，11-传感器，20-水肥机 控制器，21-水肥机传感器</param>
        /// <returns></returns>
        public object findDevbyAddr(int for_value, string comm_sn, int devclass)
        {
            Object sReturn = null;

            if (devclass == 10 || devclass == 20)
            {
                foreach (ControlDevice one in _controlList)
                {
                    if (for_value == one.devaddr && comm_sn == one.commnum)
                    {
                        return one;
                    }

                }

            }
            else if (devclass == 11 || devclass == 21)
            {
                foreach (SensorDevice one in _sensorList)
                {
                    if (for_value == one.devaddr && comm_sn == one.commnum)
                    {
                        return one;
                    }

                }

            }
            return sReturn;
        }

        public object findDevbyID(int for_value, int devclass)
        {
            Object sReturn = null;

            if (devclass == 10 || devclass == 20)
            {
                foreach (ControlDevice one in _controlList)
                {
                    if (for_value == one.devid)
                    {
                        return one;
                    }

                }

            }
            else if (devclass == 11 || devclass == 21)
            {
                foreach (SensorDevice one in _sensorList)
                {
                    if (for_value == one.devid)
                    {
                        return one;
                    }

                }

            }
            return sReturn;
        }
        public TCPClientState findStatebySN(string comm_sn, bool bOnLine)
        {
            TCPClientState state;
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
                        if ((_clients[num].TcpClient == null) || ((_clients[num].TcpClient.Client == null) || ((this._clients[num].clientComm == null) || !this._clients[num].clientComm.serial_num.Equals(comm_sn))))
                        {
                            num--;
                            continue;
                        }
                        return (!bOnLine ? this._clients[num] : ((_clients[num].clientStatus != 2) ? null : this._clients[num]));
                    }
                }
                return null;
            }
            catch (Exception exception)
            {
                string[] textArray1 = new string[] { DateTime.Now.ToString(), " 查找通讯设备(", comm_sn, ")通讯客户端发生错误:", exception.Message };
                string msg = string.Concat(textArray1);
                this.ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
                state = null;
            }
            return state;
        }



        ////public int getCommbyMqtt(int for_value, string comm_sn, int devclass, int devpara)
        ////{
        ////    int _index = -1;
        ////    bool bResult = false;
        ////    if (devclass == 10 || devclass == 20)
        ////    {
        ////        if (_controlList.Count > 0)
        ////        {
        ////            for (int i = 0; i < _controlList.Count; i++)
        ////            {
        ////                if ((_controlList[i].devaddr == for_value) && (_controlList[i].devclass==devclass) && (_controlList[i].commnum == comm_sn) && (_controlList[i].devpara == devpara))
        ////                {
        ////                    _index = i;
        ////                    bResult = true;
        ////                    return _index;
        ////                }

        ////            }
        ////        }
        ////    }
        ////    if (devclass == 11 || devclass == 21)
        ////    {
        ////        if (_sensorList.Count > 0)
        ////        {
        ////            for (int i = 0; i < _sensorList.Count; i++)
        ////            {
        ////                if ((_sensorList[i].devaddr == for_value) && (_sensorList[i].devclass == devclass) && (_sensorList[i].commnum.ToUpper().Equals(comm_sn.ToUpper())) && (_sensorList[i].devpara == devpara))
        ////                {
        ////                    _index = i;
        ////                    bResult = true;
        ////                    return _index;
        ////                }

        ////            }
        ////        }
        ////    }
        ////    return _index;
        ////}

        public int getSensorbyFoumula(string for_value, string comm_sn, int para)
        {
            int _index = -1;
            if (_sensorList.Count > 0)
            {
                for (int i = 0; i < _sensorList.Count; i++)
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

        public object getCommbySN(String commSN, int commclass)
        {
            object retComm = new object();
            foreach (CommDevice comm in _commList)
            {
                if (comm.serial_num.ToUpper().Equals(commSN) && comm.commclass == commclass)
                {
                    return comm;
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
                    #region 一行数据赋值给一个对象
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
                    #endregion
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
                    //
                    string ipport = state.TcpClient.Client.RemoteEndPoint.ToString();
                    string commsn = null;
                    commsn = state.clientComm.serial_num;
                    if (commsn != null)
                    {
                        _commtoipportDict.TryRemove(commsn, out string olderip);
                        removeClientEvent(commsn);
                    }

                    if (_linkedList.Contains(ipport))
                    {
                        _linkedList.Remove(ipport);
                        removeClientEvent(state.clientComm.serial_num);
                    }
                    else if (_forbidedList.Contains(ipport))
                    {
                        _forbidedList.Remove(ipport);
                    }
                    _PendingDict.TryRemove(ipport, out int oldertime);
                    _iptoStateDict.TryRemove(ipport, out TCPClientState oldstate);

                    state.Close();
                    _clients.Remove(state);
                    _clientCount--;
                    //TODO 触发关闭事件
                }
            }
            catch (Exception err)
            {
                string msg = "关闭客户端发送错误：" + err.Message;
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
            _clientCount = 0;
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
            if (!this.disposed)
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
        public CommDevice? clientComm
        {
            get
            {
                return _clientComm;
            }
            set
            {
                _clientComm = (CommDevice)value;
            }
        }

        public TCPClientState(TcpClient tcpClient, byte[] buffer)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            this.TcpClient = tcpClient;
            this.Buffer = buffer;
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
    /// <param name="commid">编号：水肥机没有编号，以ID代替</param>
    /// <param name="commcode">序列号</param>
    /// <param name="serial_nmu">序列号</param>
    /// <param name="commaddr">通讯地址 </param>
    /// <param name="companyid">所属公司ID</param>
    /// <param name="commtype">类型：PLC、KLC、FKC、XPC、YYC、SFJ-1200、SFJ-0804</param>
    /// <param name="commpara">参数，水肥机-肥液路数，通讯设备-采集间隔</param>
    /// <param name="passnum">通道数</param>
    /// <param name="commclass">通讯设备类别，0-物联网，1-水肥机</param>
    /// 
    /// </summary>

    //a.ID,a.Code,a.SerialNumber,a.CodeAddress,a.Company_ID,c.ClassName,`Interval`,b.ParamValue
    public struct CommDevice
    {
        public int commid;
        public string commcode;
        public string serial_num;
        public string commaddr;
        public string companyid;
        public string commtype;
        public int commpara;
        public int passnum;
        public int commclass;
    }

    /// <summary>
    /// 控制器设备
    /// <param name="devid">设备id</param>
    /// <param name="devcode">设备编号，水肥机没有编号，以ID代替</param>
    /// <param name="devaddr">设备地址</param>
    /// <param name="devtype">设备类型:1-开关，2-脉冲，3-行程</param>
    /// <param name="devpara">设备参数：控制器-行程时长 </param>
    /// <param name="commu">所属网关序列号</param>
    /// <param name="blockid">设备地块</param>
    /// <param name="devformula">0,无意义</param>
    /// <param name="devclass">10-物联网设备，20-水肥机，30-棚博士</param>
    /// </summary>
    public struct ControlDevice
    {
        public int devid;
        public string devcode;
        public int devaddr;
        public string devtype;
        public int devpara;
        public string commnum;
        public int blockid;
        public string devformula;
        public int devclass;
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
    /// <param name="devformula">公式：WENDU、SHIDU、Q-WENDU、Q-SHIDU、CO2、BEAM、EC、PH、FS、FX、YL、QY、OPR</param>
    /// <param name="devclass">11-物联网设备，21-水肥机，31-棚博士 </param>
    /// </summary>

    public struct SensorDevice
    {
        public int devid;
        public string devcode;
        public int devaddr;
        public string devtype;
        public int devpara;
        public string commnum;
        public int blockid;
        public string devformula;
        public int devclass;
    }
    /// <summary>
    /// <para name="commanID">命令ID</para>
    /// <para name="deviceID">设备ID</para>
    /// <para name="actOrder">命令</para>
    /// <para name="actparam">命令参数:物联网-延迟时间,水肥机-灌溉时间</para>
    /// <para name="scheduledTime">预计执行时间，水肥机-开始日期</para>
    /// <para name="createTime">创建时间,水肥机-开始时间</para>
    /// <para name="serialNumber">通讯设备编号</para>
    /// <para name="deviceCode">设备编码：物联网设备编码</para>
    /// <para name="client">连接客户端</para>
    /// <para name="waterNumSet">灌溉设定量</para>
    /// <para name="fertNumSet">肥液设定量</para>
    /// <para name="taskArea">灌区选择</para>
    /// <para name="taskFert">肥液通道选择</para>
    /// <para name="taskType">任务类型：0-灌溉，1-施肥</para>

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
    }

}
#endregion