using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Application.Data
{
    public static class DataHelper
    {
        public static string DataPath
        {
            get
            {
                //var debug = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data");
                //Console.WriteLine(debug);
                //Console.WriteLine(Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data"));
                //var data = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                //if (Environment.OSVersion.Platform.)
                //{
                //}
                return (new Uri(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data"))).LocalPath;
                //return (new DirectoryInfo("data")).FullName;
            }
        }

        public static string RivenDataPath
        {
            get
            {
                return Path.Combine(DataPath, "rivendata");
            }
        }

        public static string TessDataPath
        {
            get
            {
                return Path.Combine(DataPath, "tessdata");
            }
        }

        public static string OcrDataPathEnglish
        {
            get
            {
                return Path.Combine(DataPath, "ocrdata", "english");
            }
        }

        public static string OcrDataPathChinese
        {
            get
            {
                return Path.Combine(DataPath, "ocrdata", "chinese");
            }
        }
    }
}
