using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

//using UnityEngine;
using Newtonsoft.Json;

namespace DataFormat
{
    public class FormatFunc
    {

        public FormatFunc()
        {

        }
        /// <summary>
        /// 把string 类型转换为十六进制 比如 93 转换为147
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static int ConvertStringToHex(string str)
        {
            double var = 0;
            Int32 t = 0;
            int len = str.Length;
            if (var > 8) //最长8位  
                return -1;
            for (int i = 0; i < len; i++)
            {
                string y = str.Substring(i, 1);
                char x = System.Convert.ToChar(y);
                if (x >= 'a' && x <= 'f')//字符是可以比较大小的按ASCII值码大小比较；
                    t = x - 87;//a-f之间的ascii  
                else
                    t = x - 48;//0-9之间的ascii
                var += t * System.Math.Pow(16, len - i - 1);
            }
            return Int16.Parse(var.ToString());
        }
        /// <summary>
        /// 把字符串转换为十六进制字符串 比如字符串"AB" 会转换为字符串"4142"
        /// 字符串 "12"会转换为字符串"3132"  其中% 是我直接加的分隔符
        /// </summary>
        /// <param name="Commonstr"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        private static string StringToHexString(string Commonstr, Encoding encode)
        {
            byte[] b = encode.GetBytes(Commonstr);//按照指定编码将string编程字节数组
            string result = string.Empty;
            for (int i = 0; i < b.Length; i++)//逐字节变为16进制字符，以%隔开
            {
                result += Convert.ToString(b[i], 16);
            }
            return result;
        }

        /// <summary>
        /// 把十六进制字符串转换为字符串  比如字符串"41%42" 会转换为字符串"AB"  31%32 会被转换为12
        /// 很多时候应该是3132 两个连续的
        /// </summary>
        /// <param name="hs"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static string HexStringToString(string hs, Encoding encode)
        {
            //以%分割字符串，并去掉空字符
            string[] chars = hs.Split(new char[] { '%' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] b = new byte[chars.Length];
            //逐个字符变为16进制字节数据
            for (int i = 0; i < chars.Length; i++)
            {
                b[i] = Convert.ToByte(chars[i], 16);
            }
            //按照指定编码将字节数组变为字符串
            return encode.GetString(b);
        }


        /// <summary>
        /// 字符串转16进制字节数组
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] strToHexByte(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }

        /// <summary>
        /// 字节数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string byteToHexStr(byte[] bytes)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    returnStr += bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }

        /// <summary>
        /// 从汉字转换到16进制
        /// </summary>
        /// <param name="s"></param>
        /// <param name="charset">编码,如"utf-8","gb2312"</param>
        /// <param name="fenge">是否每字符用逗号分隔</param>
        /// <returns></returns>
        public static string ToHex(string s, string charset, bool fenge)
        {
            if ((s.Length % 2) != 0)
            {
                s += " ";//空格
                //throw new ArgumentException("s is not valid chinese string!");
            }
            System.Text.Encoding chs = System.Text.Encoding.GetEncoding(charset);
            byte[] bytes = chs.GetBytes(s);
            string str = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                str += string.Format("{0:X}", bytes[i]);
                if (fenge && (i != bytes.Length - 1))
                {
                    str += string.Format("{0}", ",");
                }
            }
            return str.ToLower();
        }

        ///<summary>
        /// 从16进制转换成汉字
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="charset">编码,如"utf-8","gb2312"</param>
        /// <returns></returns>
        public static string UnHex(string hex, string charset)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            hex = hex.Replace(",", "");
            hex = hex.Replace("\n", "");
            hex = hex.Replace("\\", "");
            hex = hex.Replace(" ", "");
            if (hex.Length % 2 != 0)
            {
                hex += "20";//空格
            }
            // 需要将 hex 转换成 byte 数组。
            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                try
                {
                    // 每两个字符是一个 byte。
                    bytes[i] = byte.Parse(hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
                }
                catch
                {
                    // Rethrow an exception with custom message.
                    throw new ArgumentException("hex is not a valid hex number!", "hex");
                }
            }
            System.Text.Encoding chs = System.Text.Encoding.GetEncoding(charset);
            return chs.GetString(bytes);
        }

        public static byte[] CRC16(byte[] data)
        {
            int len = data.Length;
            if (len > 0)
            {
                ushort crc = 0xFFFF;

                for (int i = 0; i < len; i++)
                {
                    crc = (ushort)(crc ^ (data[i]));
                    for (int j = 0; j < 8; j++)
                    {
                        crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
                    }
                }
                byte hi = (byte)((crc & 0xFF00) >> 8);  //高位置
                byte lo = (byte)(crc & 0x00FF);         //低位置

                return new byte[] { hi, lo };
            }
            return new byte[] { 0, 0 };
        }


        #region  ToCRC16
        public static string ToCRC16(string content)
        {
            return ToCRC16(content, Encoding.UTF8);
        }

        public static string ToCRC16(string content, bool isReverse)
        {
            return ToCRC16(content, Encoding.UTF8, isReverse);
        }

        public static string ToCRC16(string content, Encoding encoding)
        {
            return ByteToString(CRC16(encoding.GetBytes(content)), true);
        }

        public static string ToCRC16(string content, Encoding encoding, bool isReverse)
        {
            return ByteToString(CRC16(encoding.GetBytes(content)), isReverse);
        }

        public static string ToCRC16(byte[] data)
        {
            return ByteToString(CRC16(data), true);
        }

        public static string ToCRC16(byte[] data, bool isReverse)
        {
            return ByteToString(CRC16(data), isReverse);
        }
        #endregion

        #region  ToModbusCRC16
        public static string ToModbusCRC16(string s)
        {
            return ToModbusCRC16(s, true);
        }

        public static string ToModbusCRC16(string s, bool isReverse)
        {
            return ByteToString(CRC16(StringToHexByte(s)), isReverse);
        }

        public static string ToModbusCRC16(byte[] data)
        {
            return ToModbusCRC16(data, true);
        }

        public static string ToModbusCRC16(byte[] data, bool isReverse)
        {
            return ByteToString(CRC16(data), isReverse);
        }
        #endregion

        #region  ByteToString
        public static string ByteToString(byte[] arr, bool isReverse)
        {
            try
            {
                byte hi = arr[0], lo = arr[1];
                return Convert.ToString(isReverse ? hi + lo * 0x100 : hi * 0x100 + lo, 16).ToUpper().PadLeft(4, '0');
            }
            catch (Exception ex) { throw (ex); }
        }

        public static string ByteToString(byte[] arr)
        {
            try
            {
                return ByteToString(arr, true);
            }
            catch (Exception ex) { throw (ex); }
        }
        #endregion

        #region  StringToHexString
        public static string StringToHexString(string str)
        {
            StringBuilder s = new StringBuilder();
            foreach (short c in str.ToCharArray())
            {
                s.Append(c.ToString("X4"));
            }
            return s.ToString();
        }
        #endregion

        #region  StringToHexByte
        private static string ConvertChinese(string str)
        {
            StringBuilder s = new StringBuilder();
            foreach (short c in str.ToCharArray())
            {
                if (c <= 0 || c >= 127)
                {
                    s.Append(c.ToString("X4"));
                }
                else
                {
                    s.Append((char)c);
                }
            }
            return s.ToString();
        }

        private static string FilterChinese(string str)
        {
            StringBuilder s = new StringBuilder();
            foreach (short c in str.ToCharArray())
            {
                if (c > 0 && c < 127)
                {
                    s.Append((char)c);
                }
            }
            return s.ToString();
        }

        /// <summary>
        /// 字符串转16进制字符数组
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] StringToHexByte(string str)
        {
            return StringToHexByte(str, false);
        }

        /// <summary>
        /// 字符串转16进制字符数组
        /// </summary>
        /// <param name="str"></param>
        /// <param name="isFilterChinese">是否过滤掉中文字符</param>
        /// <returns></returns>
        public static byte[] StringToHexByte(string str, bool isFilterChinese)
        {
            string hex = isFilterChinese ? FilterChinese(str) : ConvertChinese(str);

            //清除所有空格
            hex = hex.Replace(" ", "");
            //若字符个数为奇数，补一个0
            hex += hex.Length % 2 != 0 ? "0" : "";

            byte[] result = new byte[hex.Length / 2];
            for (int i = 0, c = result.Length; i < c; i++)
            {
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return result;
        }
        #endregion

        public static string AsciiToString(string str)
        {
            int len = str.Length / 2;
            string strReturn = "";
            for (int i = 0; i < len; i++)

            {
                int asciiCode = Convert.ToInt32(str.Substring(i * 2, 2), 16);
                if (asciiCode >= 0 && asciiCode <= 255)
                {
                    System.Text.ASCIIEncoding asciiEncoding = new System.Text.ASCIIEncoding();
                    byte[] byteArray = new byte[] { (byte)asciiCode };
                    string strCharacter = asciiEncoding.GetString(byteArray);
                    strReturn += strCharacter;
                }
            }

            return strReturn;
        }
        public static string HexToAscii(string sData)
        {
            sData = sData.Replace(" ", "");
            byte[] buff = new byte[sData.Length / 2];
            int index = 0;
            for (int i = 0; i < sData.Length; i += 2)
            {
                buff[index] = Convert.ToByte(sData.Substring(i, 2), 16);
                ++index;
            }
            string result = Encoding.UTF8.GetString(buff);
            return result;

        }
        /// <summary>

        /// </summary>
        /// flag-大小端，true-大端，高位在前；false-小端，低位在前
        /// <param name="sData"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static int HexToDec(string sData, Boolean flag)
        {
            string s = null;
            if (flag == false)
            {
                for (int i = 0; i < sData.Length / 2; i++)
                {
                    s += sData.Substring(sData.Length - (i + 1) * 2, 2);
                }
            }
            else
            {
                s = sData;
            }

            int result = 0;
            result = System.Int32.Parse(s, System.Globalization.NumberStyles.HexNumber); ;
            return result;

        }

        /// <summary>
        /// 2位16进制字符串转10进制字符，例如01->01,0b->11，保留两位字符串
        /// </summary>
        /// <param name="sData"></param>
        /// <returns></returns>
        public static string HexToChar(string sData)
        {
            string result = null;
            if (sData.Length == 2)
            {
                result = "00" + int.Parse(sData, System.Globalization.NumberStyles.HexNumber).ToString(); ;
                result = result.Substring(result.Length - 2);
            }

            return result;

        }
        /// <summary>
        /// 16进制字符串转2进制字符串，例如“F401”转为“1111010000000001”,
        /// bStrOrder,字符串顺序，true-大端模式，高位在前；false-小端模式，低位在前，例如"OX123456",True-123456,false-563412
        /// bByteOrder,单字节顺序，true-大端模式，高位在前；false-小端模式，低位在前,例如"0x12",True-12,false-21
        /// </summary>
        /// <param name="hexString"></param>
        /// <param name="bStrOrder">字符串顺序</param>
        /// <param name="bByteOrder">单字节顺序</param>
        /// <returns></returns>
        public static string HexString2BinString(string hexString,bool bStrOrder,bool bByteOrder)
        {
            string result = string.Empty;
            hexString = Regex.Replace(hexString, @"\s", "");
            if (hexString.Length % 2 != 0)
            {
                return null;
            }
            string tempStr1 = null;
            string tempStr2 = null;
            if (!bStrOrder)
            {
                for (int i = 0; 2*i < hexString.Length ; i++)
                {
                    tempStr1 += hexString.Substring(hexString.Length - 2 * (i + 1), 2);
                }
            }
            else
            {
                tempStr1 = hexString;
            }
            if (!bByteOrder)
            {
                for (int i = 0; 2*i < tempStr1.Length ; i++)
                {
                    
                    tempStr2 += tempStr1.Substring(2 * i+1, 1) + tempStr1.Substring(2 * i, 1);
                }
            }
            else
            {
                tempStr2 = tempStr1;
            }


            foreach (char c in tempStr2)
            {
                int v = Convert.ToInt32(c.ToString(), 16);
                int v2 = int.Parse(Convert.ToString(v, 2));
                // 去掉格式串中的空格，即可去掉每个4位二进制数之间的空格，
                result += string.Format("{0:d4}", v2);
            }
            return result;
        }
        /// <summary>
        /// bitnum是多少位转1个十进制数字
        /// </summary>
        /// <param name="binstr"></param>
        /// <param name="bitnum">位数</param>
        /// <returns></returns>

        public static List<string> BinByteToDecString(string binstr, int bitnum)
        {
            string b = null;
            List<string> c = new List<string>();
            for (int i = 0; i < binstr.Length / bitnum; i++)
            {
                b = "0000" + binstr.Substring(i * bitnum, bitnum);
                b = b.Substring(b.Length - 4);
                c.Add(Convert.ToInt32(b, 2).ToString());
            }
            return c;

        }

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        /// <returns></returns>
        public static int getTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt32(ts.TotalSeconds);
        }

        /// <summary>
        /// 字符串逆序
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string reverseString(string text)
        {
            char[] charArray = text.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
        ///

        public static string rightSub(string text,int num)
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
        /// <summary>
        /// 16进制字符串转浮点数
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static float hextofloat(string s)
        {
            MatchCollection matches = Regex.Matches(s, @"[0-9A-Fa-f]{2}");
            byte[] bytes = new byte[matches.Count];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(matches[i].Value, System.Globalization.NumberStyles.AllowHexSpecifier);
            float m = BitConverter.ToSingle(bytes.Reverse().ToArray(), 0);
            return m;

            //uint num = uint.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);
            //byte[] floatVals = BitConverter.GetBytes(num);
            //float f = BitConverter.ToSingle(floatVals, 0);

        }

        /// <summary>
        /// 字典转json字符串
        /// </summary>
        /// <param name="myDic"></param>
        /// <returns></returns>
        public static string DictionaryToJson(Dictionary<string, string> myDic)
        {
            string jsonStr = JsonConvert.SerializeObject(myDic);
            return jsonStr;
        }

        /// <summary>
        /// json转字典
        /// </summary>
        /// <param name="jsonStr"></param>
        /// <returns></returns>
        public static Dictionary<string, string> JsonToDictionary(string jsonStr)
        {
            Dictionary<string, string> dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonStr);
            return dic;
        }
    }


}
