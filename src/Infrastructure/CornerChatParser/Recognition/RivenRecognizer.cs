using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace RelativeChatParser.Recognition
{
    public static class RivenRecognizer
    {
        private static ImmutableSortedSet<string> _suffixes = null;

        private static string[] GetSuffixFromSemlar(List<string> existingSuffixes)
        {
            var results = new List<string>();
            try
            {
                using (WebClient rc = new WebClient())
                {
                    var json = rc.DownloadString(@"https://10o.io/rivens/affixes.json");
                    var content = JsonConvert.DeserializeObject<IEnumerable<string>>(json)
                        .Select(str => str.Trim() + "]");
                    //_suffixes.Add(line.Trim() + ']');
                    foreach (var item in content)
                    {
                        if (!existingSuffixes.Contains(item))
                            results.Add(item);
                    }
                }
            }
            catch { }
            return results.ToArray();
        }

        static RivenRecognizer()
        {
            //Load suffixes
            if (_suffixes == null)
            {
                var suffixes = new List<string>();

                string affixFile = Path.Combine(Application.Data.DataHelper.RivenDataPath, "affixcombos.txt");
                if (Directory.Exists(Application.Data.DataHelper.RivenDataPath) &&
                    File.Exists(affixFile))
                {
                    foreach (var line in File.ReadAllLines(affixFile))
                    {
                        suffixes.Add(line.Trim() + ']');
                    }
                }

                suffixes.AddRange(GetSuffixFromSemlar(suffixes));
                _suffixes = ImmutableSortedSet.Create(suffixes.ToArray());
                //_suffixes.Sort();
            }
        }

        public static bool StringContainsRiven(string word)
        {
            var rivenBit = word.Substring(0, word.IndexOf(']') + 1).ToLower().Trim();
            if (rivenBit.IndexOf('-') > 0)
                rivenBit = rivenBit.Substring(rivenBit.IndexOf('-') + 1);
            return _suffixes.Contains(rivenBit);
        }
    }
}
