using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Application.LineParseResult;

namespace Application.ChatBoxParsing
{
    public abstract class BaseChatLineParser : IChatLineParser
    {
        protected static readonly List<string> _suffixes = new List<string>();

        public BaseChatLineParser()
        {
            if(_suffixes.Count == 0)
            {
                //Load suffixes
                var dir = Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase), "data", "rivendata");
                var file = Path.Combine(dir, "affixcombos.txt");
                if (Directory.Exists(dir) && File.Exists(file))
                {
                    foreach (var line in File.ReadAllLines(file))
                    {
                        _suffixes.Add(line.Trim() + ']');
                    }
                }
            }
        }

        public abstract BaseLineParseResult ParseLine(Bitmap lineImage);
    }
}
