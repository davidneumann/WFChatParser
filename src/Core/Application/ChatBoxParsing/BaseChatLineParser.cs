using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Application.Data;
using Application.LineParseResult;

namespace Application.ChatBoxParsing
{
    public abstract class BaseChatLineParser
    {
        protected static readonly List<string> _affixes = new List<string>();

        public BaseChatLineParser()
        {
            if(_affixes.Count == 0)
            {
                //Load suffixes
                var dir = DataHelper.RivenDataPath;
                var file = Path.Combine(dir, "affixcombos.txt");
                if (Directory.Exists(dir) && File.Exists(file))
                {
                    foreach (var line in File.ReadAllLines(file))
                    {
                        _affixes.Add(line.Trim() + ']');
                    }
                }
            }
        }

        public static void ReplaxeAffixes(IEnumerable<string> newAffixes)
        {
            _affixes.Clear();
            _affixes.AddRange(newAffixes);
        }
    }
}
