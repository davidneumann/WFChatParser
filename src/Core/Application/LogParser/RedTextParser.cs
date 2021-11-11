using System;
using System.Collections.Generic;
using System.Linq;

namespace Application.LogParser
{
    public class RedTextParser : IDisposable
    {
        public WarframeLogParser LogParser { get; set; }

        public event Action<RedTextMessage> OnRedText;

        public RedTextParser(WarframeLogParser logParser)
        {
            LogParser = logParser;
            LogParser.OnNewMessage += OnLogMessage;
        }

        //public RedTextParser() : this(new WarframeLogParser())
        //{
        //}

        private void OnLogMessage(LogMessage entry)
        {
            if (!entry.Message.StartsWith("IRC in:"))
                return;

            try
            {
                var (prefix, command, args) = Parse(entry.Message.Substring(8));
                if (command == "WALLOPS")
                {
                    var name = prefix.Substring(0, prefix.IndexOf('!'));
                    OnRedText?.Invoke(new RedTextMessage
                    {
                        SentTime = entry.ActualTime,
                        Name = name,
                        Message = args.Last()
                    });
                }
            }
            catch
            {
                // ignored
            }
        }

        // This could probably be cleaned up a bit
        private static (string prefix, string command, List<string> args) Parse(string data)
        {
            var prefix = "";
            var args = new List<string>();

            if (data[0] == ':')
            {
                var temp = data.Substring(1).Split(new []{' '}, 2);
                prefix = temp[0];
                data = temp[1];
            }

            if (data.Contains(" :"))
            {
                var temp = data.Split(new []{" :"}, 2, StringSplitOptions.None);
                data = temp[0];
                args.AddRange(data.Split());
                args.Add(temp[1]);
            }
            else
            {
                args.AddRange(data.Split());
            }

            var cmd = args[0];
            args.RemoveAt(0);
            return (prefix, cmd, args);
        }

        public void Dispose()
        {
            LogParser?.Dispose();
        }
    }
}
