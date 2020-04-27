using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace dayLogFiles

{
    public class EveryDayLog
    {
        #region   当前文件输出路径
        public static string SYSTEM_OUTLOG_FILEDIRECTORY = Directory.GetCurrentDirectory() + "/EveryDayLog/";

        #endregion

        #region   公有方法

        /// <summary>
        /// 给日志文件写入信息
        /// </summary>
        /// <param name="info">需要写入的信息</param>
        /// <returns>true:表示写入成功</returns>
        public static bool Write(string needWriteInfo)
        {
            bool success = false;
            if (File.Exists(LogPath))
            {
                //追加文件
                using (FileStream fs = new FileStream(LogPath, FileMode.Append, System.IO.FileAccess.Write, FileShare.ReadWrite))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        //开始写入
                        sw.WriteLine(DateTime.Now + ": " + needWriteInfo);
                        success = true;
                    }
                }
            }
            else
            {
                using (FileStream fs = new FileStream(LogPath, FileMode.CreateNew, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        //开始写入
                        sw.WriteLine(DateTime.Now + ": " + needWriteInfo);
                        success = true;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// 读取日志文件的信息
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>返回读取的信息</returns>
        public static string Read(string path)
        {
            string contents = string.Empty;
            if (File.Exists(path))
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        //开始读取
                        contents = sr.ReadToEnd();
                    }
                }
            }
            return contents;
        }

        /// <summary>
        /// 清除日志文件的信息
        /// </summary>
        /// <param name="path">文件的路径</param>
        /// <returns>true:表示清空成功</returns>
        public static bool Clear(string path)
        {
            bool success = false;
            if (File.Exists(path))
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.SetLength(0);
                    success = true;
                }
            }
            return success;
        }

        /// <summary>
        /// 删除日志文件
        /// </summary>
        /// <param name="path">文件的路径</param>
        /// <returns>true:表示删除成功</returns>
        public static bool Delete(string path)
        {
            bool success = false;
            if (File.Exists(path))
            {
                File.Delete(path);
                success = true;
            }
            return success;
        }

        #endregion

        #region   私有方法

        /// <summary>
        /// 日志文件存放路径
        /// </summary>
        private static string LogPath
        {
            get
            {
                string OutPath = SYSTEM_OUTLOG_FILEDIRECTORY;

                if (!Directory.Exists(OutPath))
                {
                    Directory.CreateDirectory(OutPath);
                }

                return OutPath + DateTime.Today.ToString("yyyy-MM-dd") + ".txt";
            }
        }

        #endregion

    }//Class_end
}