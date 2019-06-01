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
                return Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data");
            }
        }

        public static string RivenDataPath
        {
            get
            {
                return Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data", "rivendata");
            }
        }

        public static string TessDataPath
        {
            get
            {
                return Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data", "tessdata");
            }
        }
    }
}
