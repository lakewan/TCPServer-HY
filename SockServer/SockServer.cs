using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading;

using dayLogFiles;
using DataFormat;
using MySql.Data.MySqlClient;


namespace SockServer
{
    public delegate void ShowMessageDelegate(string msg);

    public class AsynSocketUDPServer : IDisposable
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
        /// 服务器使用的同步socket
        /// </summary>
        private Socket _serverSock;

        /// <summary>
        /// 客户端会话列表
        /// </summary>
        private List<Object> _clients;

        private bool disposed = false;
        public event ShowMessageDelegate ShowMsgEvent;
        private ConcurrentDictionary<string, UDPClientState> _Devices = new ConcurrentDictionary<string, UDPClientState>();             //客户端,SN,IPPORT
        private ConcurrentDictionary<string, DateTime> _DevicesLastSeen = new ConcurrentDictionary<string, DateTime>();         //客户端最新时间,SN,
        private ConcurrentDictionary<string, DateTime> _DevicesKicked = new ConcurrentDictionary<string, DateTime>();           //黑名单客户
        private ConcurrentDictionary<string, DateTime> _DevicesTimedout = new ConcurrentDictionary<string, DateTime>();         //超时客户

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        #endregion

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

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="listenPort">监听的端口</param>
        public AsynSocketUDPServer(int listenPort)
            : this(IPAddress.Any, listenPort)
        {
        }

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localEP">监听的终结点</param>
        public AsynSocketUDPServer(IPEndPoint localEP)
            : this(localEP.Address, localEP.Port)
        {
        }

        /// <summary>
        /// 异步TCP服务器
        /// </summary>
        /// <param name="localIPAddress">监听的IP地址</param>
        /// <param name="listenPort">监听的端口</param>
        public AsynSocketUDPServer(IPAddress localIPAddress, int listenPort)
        {
            this.Address = localIPAddress;
            this.Port = listenPort;
            this.Encoding = Encoding.Default;

            _clients = new List<Object>();

            //_listener = new TcpListener(Address, Port);
            //_listener.AllowNatTraversal(true);
            _serverSock = new Socket(localIPAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
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
                IsRunning = true;
                _serverSock.Bind(new IPEndPoint(this.Address, this.Port));
                UDPClientState so = new UDPClientState();
                so.workSocket = _serverSock;

                _serverSock.BeginReceiveFrom(so.Buffer, 0, so.Buffer.Length, SocketFlags.None,
                    ref so.remote, new AsyncCallback(ReceiveDataAsync), null);
                //ClientConnected += new EventHandler<UDPClientState>(onClientAccept);


                string msg = DateTime.Now.ToString() + " 服务器(" + this.Port + ")开始监听";
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }
        string connectStr = "server=localhost" + ";port=3306" + ";user=root" + ";password=111111" + ";database=standbypower";

        /// <summary>
        /// 接收数据的方法
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveDataAsync(IAsyncResult ar)
        {
            UDPClientState so = ar.AsyncState as UDPClientState;
            //EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int len = -1;
            try
            {
                len = _serverSock.EndReceiveFrom(ar, ref so.remote);
                if (len > 0)
                {
                    so.sb.Append(Encoding.ASCII.GetString(so.Buffer, 0, len));

                    string sData = FormatFunc.AsciiToString(so.sb.ToString());
                    UDPClientState state = (UDPClientState)ar.AsyncState;
                    string sDeviceID = sData.Split(',')[0];
                    /*
                    if (!String.IsNullOrEmpty(sDeviceID) && sDeviceID.Length == 10)
                    {

                        if (!_Devices.TryGetValue(sDeviceID, out UDPClientState client))
                        {
                            state.status = "0";
                            state.lasttime = DateTime.Now.ToString();
                            state.deviceid = sDeviceID;
                            _Devices.TryAdd(sDeviceID, state);
                        }
                        else
                        {
                            state.status = "0";
                            state.lasttime = DateTime.Now.ToString();

                        }
                    }*/
                    RaiseDataReceived(so);
                }


            }
            catch (Exception)
            {
                //TODO 处理异常
                RaiseOtherException(so);
            }

            finally
            {
                if (IsRunning && _serverSock != null)
                    _serverSock.BeginReceiveFrom(so.Buffer, 0, so.Buffer.Length, SocketFlags.None,
                ref so.remote, new AsyncCallback(ReceiveDataAsync), so);
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
                _serverSock.Close();
                lock (_clients)
                {
                    //关闭所有客户端连接
                    CloseAllClient();
                }
                string msg = DateTime.Now.ToString() + " 服务器关闭";
                ShowMsgEvent(msg);
                EveryDayLog.Write(msg);
            }
        }








        public void onDataReceived(object sender, AsyncEventArgs e)
        {
            UDPClientState clientState = e._state;
            string sRecvData = e._msg;
            string msg = "system receive from(" + clientState.remote.ToString() + ") data:" + sRecvData;
            EveryDayLog.Write(msg);
            string sOrder, sPGN, sData;
            if (sRecvData != null)
            {
                string[] sGroupArray = sRecvData.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                for (int j = 0; j < sGroupArray.Length; j++)
                {
                    string sSQL = null;
                    string sHistorySQL = null;
                    string[] sArray = sGroupArray[j].Split(',');

                    if (sArray.Length == 4)
                    {
                        string sDateNum = sArray[0];
                        sOrder = sArray[1];
                        sPGN = sArray[2];
                        sData = sArray[3];

                        //_Devices.TryAdd()


                        //当前数据
                        string sDevID, sVer, sModNum, sMonoNum, sTempNum, sTotalCap, sState, sResistance, sMaxVolMono, sMaxVol;
                        string sMinVolMono, sMinVol, sMaxTemp, sMaxTempMono, sMinTemp, sMinTempMono, sMaxPress, sMaxTempDiff;
                        //string sFirstVol, sSendVol, sThirdVol, sFirsCurrent, sSendCurrent;
                        //string sThirdCurrent, sFirstSOC, sSendSOC, sThirdSOC;
                        //string iFirstCurrentState, iSencondCurrentState, iThirdCurrentState;

                        //List<PackInfo> packInfoList = new List<PackInfo>(3) { };
                        PackInfo[] arrayPack = new PackInfo[3] { new PackInfo(), new PackInfo(), new PackInfo() };

                        //状态
                        //string iBreakAuxDryContact, iStartContact, iChargeContact, iDischargeContact, iPreChargeContact, iFanContact, iInsulationContact;
                        //string iDCBreakerContact, iNegativeChargeContact, iBMUDryContact, iChargeDryContact, iWarnDryContact;

                        //报警
                        string iOverVol, iUnderVol, iBigGap, iOverTempPack, iDownTempPack, iBigGapTemp, iOverVolTotal;
                        string iDownVolTotal, iDownResistance, iOverSOC, iDownSOC, iOverCurrentCharge, iOverCurrentDischarge;
                        string iBMUCommucation;

                        //单体状态
                        //List<string> VolList = new List<string>();
                        //List<string> TempList = new List<string>();
                        //List<string> BlanceList = new List<string>();
                        string sVolList = null, sTempList = null, sBlanceList = null;


                        switch (sPGN)
                        {
                            case "000100": //基本信息
                                {
                                    sDevID = FormatFunc.HexToAscii(sData.Substring(0, 20));
                                    sVer = FormatFunc.HexToChar(sData.Substring(20, 2)) + "." + FormatFunc.HexToChar(sData.Substring(22, 2));
                                    sVer += "." + FormatFunc.HexToChar(sData.Substring(24, 2)) + "." + FormatFunc.HexToChar(sData.Substring(26, 2));
                                    sModNum = FormatFunc.HexToDec(sData.Substring(28, 2), false).ToString();
                                    sMonoNum = FormatFunc.HexToDec(sData.Substring(30, 2), false).ToString();
                                    sTempNum = FormatFunc.HexToDec(sData.Substring(32, 2), false).ToString();
                                    sTotalCap = (FormatFunc.HexToDec(sData.Substring(34, 4), false) * 0.1).ToString();
                                    sState = FormatFunc.HexToDec(sData.Substring(38, 2), false).ToString();
                                    sResistance = (FormatFunc.HexToDec(sData.Substring(40, 4), false) * 0.1).ToString();
                                    sMaxVol = (FormatFunc.HexToDec(sData.Substring(44, 4), false) * 0.1).ToString();
                                    sMaxVolMono = FormatFunc.HexToDec(sData.Substring(48, 4), false).ToString();
                                    sMinVol = (FormatFunc.HexToDec(sData.Substring(52, 4), false) * 0.1).ToString();
                                    sMinVolMono = FormatFunc.HexToDec(sData.Substring(56, 4), false).ToString();
                                    sMaxTemp = (FormatFunc.HexToDec(sData.Substring(60, 2), false) * 0.1).ToString();
                                    sMaxTempMono = FormatFunc.HexToDec(sData.Substring(62, 2), false).ToString();
                                    sMinTemp = (FormatFunc.HexToDec(sData.Substring(64, 2), false) * 0.1).ToString();
                                    sMinTempMono = FormatFunc.HexToDec(sData.Substring(66, 2), false).ToString();
                                    sMaxPress = FormatFunc.HexToDec(sData.Substring(68, 4), false).ToString();
                                    sMaxTempDiff = FormatFunc.HexToDec(sData.Substring(72, 2), false).ToString();

                                    //更新最新数据表
                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "system_ver='" + sVer + "', module_num='" + sModNum + "', module_monomer_num='" + sMonoNum + "',";
                                    sSQL += "module_temperature_num='" + sTempNum + "', total_capactity='" + sTotalCap + "',state='" + sState + "',";
                                    sSQL += "total_insulation_resistance='" + sResistance + "', max_voltage='" + sMaxVol + "', ";
                                    sSQL += "max_voltage_monomer='" + sMaxVolMono + "', min_voltage='" + sMinVol + "', min_voltage_monomer='" + sMinVolMono + "',";
                                    sSQL += "max_temperature='" + sMaxTemp + "',max_temperature_monomer='" + sMaxTempMono + "', ";
                                    sSQL += "min_temperature='" + sMinTemp + "', min_temperature_monomer='" + sMinTempMono + "', ";
                                    sSQL += "max_pressure_difference='" + sMaxPress + "', max_temperature_difference='" + sMaxTempDiff + "',";
                                    sSQL += "create_time = now()";

                                    //插入历史表
                                    sHistorySQL = "insert  into power_data_history(station_id,system_ver,module_num,module_monomer_num,";
                                    sHistorySQL += "module_temperature_num,total_capactity,state,total_insulation_resistance,max_voltage,";
                                    sHistorySQL += "max_voltage_monomer,min_voltage,min_voltage_monomer,max_temperature,max_temperature_monomer,";
                                    sHistorySQL += "min_temperature,min_temperature_monomer,max_pressure_difference,max_temperature_difference,create_time) values ( ";
                                    sHistorySQL += "'" + sDateNum + "', '" + sVer + "', '" + sModNum + "', '" + sMonoNum + "', '" + sTempNum + "', '" + sTotalCap + "','" + sState + "',";
                                    sHistorySQL += "'" + sResistance + "', '" + sMaxVol + "', '" + sMaxVolMono + "', '" + sMinVol + "', '" + sMinVolMono + "', '" + sMaxTemp + "',";
                                    sHistorySQL += "'" + sMaxTempMono + "', '" + sMinTemp + "', '" + sMinTempMono + "', '" + sMaxPress + "', '" + sMaxTempDiff + "',now())";


                                    break;
                                    //insert into `power_data_lastest`(`id`,`station_id`,`system_ver`,`module_num`,`module_monomer_num`,`module_temperature_num`,`total_capactity`,`state`,`total_insulation_resistance`,`max_voltage`,`max_voltage_monomer`,`min_voltage`,`min_voltage_monomer`,`max_temperature`,`max_temperature_monomer`,`min_temperature`,`min_temperature_monomer`,`max_pressure_difference`,`max_temperature_difference`,`first_total_voltage`,`first_total_current`,`first_soc`,`first_current_state`,`sencond_total_voltage`,`sencond_total_current`,`sencondr_soc`,`second_current_state`,`third_total_voltage`,`third_total_current`,`third_soc`,`third_current_state`,`start_contactor`,`charge_contactor`,`discharge_contactor`,`precharge_contactor`,`fan_contactor`,`insulation_monitor_contactor`,`dc_breaker_contactor`,`negative_charge_contactor`,`breaker_aux_drycontactor`,`bmu_drycontactor`,`charge_drycontactor`,`warn_drycontactor`,`monomer_overvoltage_warn`,`monomer_undervoltage_warn`,`monomer_biggap_warn`,`pack_overtemperature_warn`,`pack_downtemperature_warn`,`temperature_biggap_warn`,`totalvoltage_over_warn`,`totalvoltage_down_warn`,`resistance_down_warn`,`soc_over_warn`,`soc_down_warn`,`chargecurrent_over_warn`,`discharge_over_warn`,`bmu_commucation_state`,`monomer_voltage`,`monomer_temperature`,`monomer_blance_state`,`create_time`) values(1, '1241262560280956929', '1.01', 2, 8, 4, '15.8', 1, '1194.5', '6060.4', 8, '5801.3', 11, '38', 2, '32', 3, '205.6', '6', '6800.6', '38.5', '58.5', 0, '6820.9', '65.4', '75.0', 0, '', '', '', 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, '5921.0;6013.6;5994.0;5894.5;5955.8;5874.4;6012.3;6060.4;5990.9;5826.3;5801.3;5865.7;5992.5;5907.6;6003.6;6054.7', '39.5;38.1;39.6;40.2;41.0;37.9;37.5;38.6;60.1;69.7;68.6;69.2;59.5;60.2;58.8;59.4', '0;0;1;1;1;0;0;0;0;1;1;1;1;0;1;0', '2020-04-02 14:05:56');


                                }
                            case "000200": //并联信息
                                {
                                    int iPackNum = FormatFunc.HexToDec(sData.Substring(0, 2), false);
                                    string sVol, sCurrent, sSOC, sSensorState;

                                    int i = 0;
                                    for (i = 0; ((i < iPackNum) && (i * 14 + 2 < sData.Length)); i++)
                                    {
                                        sVol = (FormatFunc.HexToDec(sData.Substring(2 + i * 14, 4), false) * 0.1).ToString();
                                        sCurrent = (FormatFunc.HexToDec(sData.Substring(6 + i * 14, 4), false) * 0.1).ToString();
                                        sSOC = (FormatFunc.HexToDec(sData.Substring(10 + i * 14, 4), false) * 0.1).ToString();
                                        sSensorState = FormatFunc.HexToDec(sData.Substring(14 + i * 14, 2), false).ToString();
                                        arrayPack[i].sVol = sVol;
                                        arrayPack[i].sCurrent = sCurrent;
                                        arrayPack[i].sSOC = sSOC;
                                        arrayPack[i].sState = sSensorState;
                                    }
                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "first_total_voltage='" + arrayPack[0].sVol + "', first_total_current='" + arrayPack[0].sCurrent + "',";
                                    sSQL += "first_soc='" + arrayPack[0].sSOC + "' , first_current_state='" + arrayPack[0].sState + "' ,";
                                    sSQL += "second_total_voltage='" + arrayPack[1].sVol + "', second_total_current='" + arrayPack[1].sCurrent + "',";
                                    sSQL += "second_soc='" + arrayPack[1].sSOC + "' , second_current_state='" + arrayPack[1].sState + "', ";
                                    sSQL += "third_total_voltage='" + arrayPack[2].sVol + "' , third_total_current='" + arrayPack[2].sCurrent + "' ,";
                                    sSQL += "third_soc='" + arrayPack[2].sSOC + "' , third_current_state='" + arrayPack[2].sState + "',";
                                    sSQL += "create_time = now()";

                                    //历史记录
                                    sHistorySQL = "insert  into power_data_history(station_id,first_total_voltage,first_total_current,first_soc,";
                                    sHistorySQL += "first_current_state,second_total_voltage,second_total_current,second_soc,second_current_state,";
                                    sHistorySQL += "third_total_voltage,third_total_current,third_soc,third_current_state,create_time) values(";
                                    sHistorySQL += "'" + sDateNum + "','" + arrayPack[0].sVol + "', '" + arrayPack[0].sCurrent + "', '" + arrayPack[0].sSOC + "' , '" + arrayPack[0].sState + "' ,";
                                    sHistorySQL += "'" + arrayPack[1].sVol + "' , '" + arrayPack[1].sCurrent + "' , '" + arrayPack[1].sSOC + "' , '" + arrayPack[1].sState + "', ";
                                    sHistorySQL += "'" + arrayPack[2].sVol + "' , '" + arrayPack[2].sCurrent + "' , '" + arrayPack[2].sSOC + "' , '" + arrayPack[2].sState + "',now()) ";


                                    break;
                                }
                            case "000300": //继电器信息
                                {
                                    //sData = "F40110";
                                    sData = FormatFunc.HexString2BinString(sData, true, false);
                                    List<string> ContactList = new List<string>();
                                    ContactList = FormatFunc.BinByteToDecString(sData, 2);

                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "start_contactor='" + ContactList[0] + "' , charge_contactor='" + ContactList[1] + "' ,";
                                    sSQL += "discharge_contactor='" + ContactList[2] + "', precharge_contactor='" + ContactList[3] + "', ";
                                    sSQL += "fan_contactor = '" + ContactList[4] + "' ,insulation_monitor_contactor= '" + ContactList[5] + "' , ";
                                    sSQL += "dc_breaker_contactor='" + ContactList[6] + "' , negative_charge_contactor='" + ContactList[7] + "',";
                                    sSQL += " breaker_aux_drycontactor='" + ContactList[8] + "',bmu_drycontactor='" + ContactList[9] + "' ,";
                                    sSQL += " charge_drycontactor='" + ContactList[10] + "' , warn_drycontactor='" + ContactList[11] + "',";
                                    sSQL += "create_time = now()";

                                    sHistorySQL = "insert into power_data_history(station_id,start_contactor,charge_contactor,discharge_contactor,precharge_contactor,";
                                    sHistorySQL += "fan_contactor,insulation_monitor_contactor,dc_breaker_contactor,negative_charge_contactor,breaker_aux_drycontactor,";
                                    sHistorySQL += "bmu_drycontactor,charge_drycontactor,warn_drycontactor,create_time) values(";
                                    sHistorySQL += "'" + sDateNum + "', '" + ContactList[0] + "' , '" + ContactList[1] + "' , '" + ContactList[2] + "', '" + ContactList[3] + "', ";
                                    sHistorySQL += "'" + ContactList[4] + "' , '" + ContactList[5] + "' , '" + ContactList[6] + "' , '" + ContactList[7] + "', '" + ContactList[8] + "',";
                                    sHistorySQL += "'" + ContactList[9] + "' , '" + ContactList[10] + "' , '" + ContactList[11] + "',now())";


                                    break;
                                }
                            case "000400": //报警信息
                                {
                                    //sData = "00000000000040";
                                    sData = FormatFunc.HexString2BinString(sData, true, false);
                                    List<string> WarnList = new List<string>();
                                    WarnList = FormatFunc.BinByteToDecString(sData, 4);

                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "monomer_overvoltage_warn='" + WarnList[0] + "' ,monomer_undervoltage_warn= '" + WarnList[1] + "' ,";
                                    sSQL += "monomer_biggap_warn='" + WarnList[2] + "' , pack_overtemperature_warn='" + WarnList[3] + "', ";
                                    sSQL += "pack_downtemperature_warn='" + WarnList[4] + "' , temperature_biggap_warn='" + WarnList[5] + "' , ";
                                    sSQL += "totalvoltage_over_warn='" + WarnList[6] + "' , totalvoltage_down_warn='" + WarnList[7] + "', ";
                                    sSQL += " resistance_down_warn='" + WarnList[8] + "',soc_over_warn='" + WarnList[9] + "' ,";
                                    sSQL += " soc_down_warn= '" + WarnList[10] + "' ,chargecurrent_over_warn='" + WarnList[11] + "',";
                                    sSQL += " discharge_over_warn='" + WarnList[12] + "',bmu_commucation_state='" + WarnList[13] + "',";
                                    sSQL += "create_time = now()";

                                    sHistorySQL = "insert into power_data_history(station_id,monomer_overvoltage_warn,monomer_undervoltage_warn,monomer_biggap_warn,";
                                    sHistorySQL += "pack_overtemperature_warn,pack_downtemperature_warn,temperature_biggap_warn,totalvoltage_over_warn,";
                                    sHistorySQL += "totalvoltage_down_warn,resistance_down_warn,soc_over_warn,soc_down_warn,chargecurrent_over_warn,discharge_over_warn, ";
                                    sHistorySQL += "bmu_commucation_state,create_time ) values( ";
                                    sHistorySQL += "'" + sDateNum + "', '" + WarnList[0] + "' , '" + WarnList[1] + "' , '" + WarnList[2] + "' , '" + WarnList[3] + "', ";
                                    sHistorySQL += "'" + WarnList[4] + "' , '" + WarnList[5] + "' , '" + WarnList[6] + "' , '" + WarnList[7] + "', '" + WarnList[8] + "',";
                                    sHistorySQL += "'" + WarnList[9] + "' , '" + WarnList[10] + "' , '" + WarnList[11] + "', '" + WarnList[12] + "', '" + WarnList[13] + "',now() )";



                                    break;
                                }
                            case "000500": //单体电压
                                {
                                    List<string> VolList = new List<string>();

                                    int indexVol = FormatFunc.HexToDec(sData.Substring(0, 4), false);
                                    int lengthVol = FormatFunc.HexToDec(sData.Substring(4, 8), false);
                                    int i = 0;
                                    string sVolData;
                                    for (i = 0; (i * 4 + 12 <= sData.Length && i < lengthVol); i++)
                                    {
                                        sVolData = (FormatFunc.HexToDec(sData.Substring(i * 4 + 8, 4), false) * 0.1).ToString();
                                        VolList.Insert(i + indexVol - 1, sVolData);

                                    }
                                    foreach (var s in VolList)
                                    {
                                        sVolList += s + ";";
                                    }
                                    sVolList = sVolList.Substring(0, sVolList.Length - 1);

                                    //最新数据
                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "monomer_voltage =  '" + sVolList + "',create_time = now()";


                                    //历史记录
                                    sHistorySQL = "insert into power_data_history(station_id,monomer_voltage,create_time) values ('" + sDateNum + "', '" + sVolList + "',now())";
                                    break;
                                }
                            case "000600": //单体温度
                                {

                                    int indexTemp = FormatFunc.HexToDec(sData.Substring(0, 4), false);

                                    int i = 0;
                                    string sTempData;
                                    for (i = 0; (i * 2 + 4 <= sData.Length) && (i < indexTemp); i++)
                                    {
                                        sTempData = (FormatFunc.HexToDec(sData.Substring(i * 2 + 2, 2), true) - 50).ToString();
                                        sTempList += sTempData + ";";
                                    }

                                    sTempList = sTempList.Substring(0, sTempList.Length - 1);
                                    //最新数据
                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "monomer_temperature =  '" + sTempList + "',create_time = now()";

                                    //历史记录
                                    sHistorySQL = "insert into power_data_history (station_id,monomer_temperature,create_time) values (";
                                    sHistorySQL += " '" + sDateNum + "','" + sTempList + "',now())";
                                    break;
                                }
                            case "000700": //单体均衡
                                {
                                    int indexBlance = FormatFunc.HexToDec(sData.Substring(0, 4), false);
                                    string sData1 = FormatFunc.HexString2BinString(sData.Substring(4), true, false);
                                    foreach (var sb in sData1)
                                    {
                                        sBlanceList += sb + ";";
                                    }
                                    sBlanceList = sBlanceList.Substring(0, sBlanceList.Length - 1);
                                    //最新数据
                                    sSQL = "insert into power_data_lastest(station_id) values ('" + sDateNum + "') ON DUPLICATE KEY UPDATE ";
                                    sSQL += "monomer_blance_state =  '" + sBlanceList + "',create_time = now()";

                                    //历史记录
                                    sHistorySQL = "insert into power_data_history (station_id,monomer_blance_state,create_time) values (";
                                    sHistorySQL += "'" + sDateNum + "','" + sBlanceList + "',now())";

                                    break;
                                }
                            default:
                                {
                                    break;
                                }
                        }

                        MySqlConnection conn = new MySqlConnection(connectStr);
                        MySqlCommand insertCmd1 = new MySqlCommand(sSQL, conn);
                        MySqlCommand insertCmd2 = new MySqlCommand(sHistorySQL, conn);
                        string msg1 = DateTime.Now.ToString() + " 插入最新数据：" + sSQL;
                        ShowMsgEvent(msg1);
                        EveryDayLog.Write(msg1);
                        msg1 = DateTime.Now.ToString() + " 插入历史数据：" + sHistorySQL;
                        ShowMsgEvent(msg1);
                        EveryDayLog.Write(msg1);
                        try
                        {
                            conn.Open();
                            int iSQLResult = insertCmd1.ExecuteNonQuery();
                            if (iSQLResult != 0)
                            {
                                msg1 = DateTime.Now.ToString() + " 插入最新数据成功";
                                ShowMsgEvent(msg1);
                                EveryDayLog.Write(msg1);
                            }
                            iSQLResult = insertCmd2.ExecuteNonQuery();
                            if (iSQLResult != 0)
                            {
                                msg1 = DateTime.Now.ToString() + " 插入历史数据成功";
                                ShowMsgEvent(msg1);
                                EveryDayLog.Write(msg1);
                            }

                        }
                        catch (MySqlException ex)
                        {
                            msg1 = DateTime.Now.ToString() + " 插入数据错误：" + sSQL;
                            ShowMsgEvent(msg1);
                            EveryDayLog.Write(msg1);
                            EveryDayLog.Write(ex.Message);
                        }
                        finally
                        {
                            conn.Close();
                        }

                    }
                }
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="state">接收数据的客户端会话</param>
        /// <param name="data">数据报文</param>
        public void Send(string msg, EndPoint remote)
        {
            byte[] data = Encoding.Default.GetBytes(msg);
            try
            {
                RaisePrepareSend(null);
                _serverSock.BeginSendTo(data, 0, data.Length, SocketFlags.None, remote, new AsyncCallback(SendDataEnd), _serverSock);
            }
            catch (Exception)
            {
                //TODO 异常处理
                RaiseOtherException(null);
            }
        }

        /// <summary>
        /// 异步发送数据至指定的客户端
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="data">报文</param>
        public void Send(TcpClient client, byte[] data)
        {
            if (!IsRunning)
                throw new InvalidProgramException("This TCP Scoket server has not been started.");

            if (client == null)
                throw new ArgumentNullException("client");

            if (data == null)
                throw new ArgumentNullException("data");
            client.GetStream().BeginWrite(data, 0, data.Length, SendDataEnd, client);

            string msg = DateTime.Now.ToString() + " 服务器向客户端（" + client.Client.RemoteEndPoint.ToString() + "发送数据：" + Encoding.UTF8.GetString(data);
            ShowMsgEvent(msg);
            EveryDayLog.Write(msg);


        }

        /// <summary>
        /// 发送数据完成处理函数
        /// </summary>
        /// <param name="ar">目标客户端Socket</param>
        private void SendDataEnd(IAsyncResult ar)
        {
            ((Socket)ar.AsyncState).EndSendTo(ar);
            RaiseCompletedSend(null);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 
        /// </summary>
        //public event EventHandler<string> ShowMessage;
        /*
        private void RaiseShowMessage(string msg)
        {
            if (ClientConnected != null)
            {
                ShowMessage(this, msg);
            }
        }
        */


        /// <summary>
        /// 与客户端的连接已建立事件
        /// </summary>
        public event EventHandler<UDPClientState> ClientConnected;
        /// <summary>
        /// 与客户端的连接已断开事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> ClientDisconnected;


        /// <summary>
        /// 触发客户端连接事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseClientConnected(UDPClientState state)
        {
            if (ClientConnected != null)
            {
                ClientConnected(this, state);
            }
        }
        /// <summary>
        /// 触发客户端连接断开事件
        /// </summary>
        /// <param name="client"></param>
        private void RaiseClientDisconnected(UDPClientState state)
        {
            if (ClientDisconnected != null)
            {
                string msg = "system receive from(" + state.remote.ToString() + ") disconnect request has succeed";
                ClientDisconnected(this, new AsyncEventArgs("连接断开"));

            }
        }

        /// <summary>
        /// 接收到数据事件
        /// </summary>
        public event EventHandler<AsyncEventArgs> DataReceived;

        private void RaiseDataReceived(UDPClientState state)
        {
            if (DataReceived != null)
            {
                DataReceived(this, new AsyncEventArgs(state));

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
        private void RaisePrepareSend(UDPClientState state)
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
        private void RaiseCompletedSend(UDPClientState state)
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
        private void RaiseNetError(UDPClientState state)
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
        private void RaiseOtherException(UDPClientState state, string descrip)
        {
            if (OtherException != null)
            {
                OtherException(this, new AsyncEventArgs(descrip, state));
            }
        }
        private void RaiseOtherException(UDPClientState state)
        {
            RaiseOtherException(state, "");
        }

        #endregion

        #region Close
        /// <summary>
        /// 关闭一个与客户端之间的会话
        /// </summary>
        /// <param name="state">需要关闭的客户端会话对象</param>
        public void Close(UDPClientState state)
        {
            if (state != null)
            {
                //TODO 触发关闭事件
                //string sDeviceID = state.deviceid;
                ////if (_Devices.TryGetValue(sDeviceID, out UDPClientState client))
                ////{
                ////    _Devices.TryRemove(sDeviceID,out UDPClientState clientstate);
                ////}
                _clients.Remove(state);
                _clientCount--;


            }
        }
        /// <summary>
        /// 关闭所有的客户端会话,与所有的客户端连接会断开
        /// </summary>
        public void CloseAllClient()
        {
            foreach (UDPClientState client in _clients)
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
                        /* gaoxs
                        if (_listener != null)
                        {
                            _listener = null;
                        }
                        */
                        if (_serverSock != null)
                        {
                            _serverSock = null;
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
    public class PackInfo
    {
        private string _sVol = null;
        private string _sCurrent = null;
        private string _sSOC = null;
        private string _sState = null;
        public PackInfo()
        {
            _sVol = "";
            _sCurrent = "";
            _sSOC = "";
            _sState = "";
        }
        public string sVol
        {
            get { return _sVol; }
            set { _sVol = value; }
        }
        public string sCurrent
        {
            get { return _sCurrent; }
            set { _sCurrent = value; }
        }
        public string sSOC
        {
            get { return _sSOC; }
            set { _sSOC = value; }
        }
        public string sState
        {
            get { return _sState; }

            set { _sState = value; }
        }
    }



    public class UDPClientState
    {
        /// <summary>
        /// 与客户端相关的TcpClient
        /// </summary>
        public Socket workSocket = null;

        /// <summary>
        /// 缓冲区大小
        /// </summary>
        public const int BufferSize = 2048;

        /// <summary>
        /// 获取缓冲区
        /// </summary>
        public byte[] Buffer = new byte[BufferSize];

        /// <summary>
        /// 客户端状态，0-在线，1-超时，2-黑名单
        /// </summary>
        //public string status { get; set; }

        /// <summary>
        /// 最后接受数据时间
        /// </summary>
        //public string lasttime { get; set; }

        /// <summary>
        /// 设备id
        /// </summary>
        //public string deviceid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public StringBuilder sb = new StringBuilder();

        public EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
    }
    /// <summary>
    /// SOCKET 异步UDP 事件类
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
        public UDPClientState _state;

        /// <summary>
        /// 是否已经处理过了
        /// </summary>
        public bool IsHandled { get; set; }

        public AsyncEventArgs(string msg)
        {
            this._msg = msg;
            IsHandled = false;
        }
        public AsyncEventArgs(UDPClientState state)
        {
            this._state = state;
            IsHandled = false;
        }
        public AsyncEventArgs(string msg, UDPClientState state)
        {
            this._msg = msg;
            this._state = state;
            IsHandled = false;
        }
    }
}
