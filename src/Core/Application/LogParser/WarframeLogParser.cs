using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Application.LogParser
{
    public class WarframeLogParser : IDisposable
    {
        private static readonly Regex LogLineRegex = new Regex(@"^(?<timer>\d+\.\d+)? ?(?<category>[a-zA-Z0-9]+) \[(?<level>[a-zA-Z0-9]+)\]: (?<message>.*)$", RegexOptions.Compiled);

        private readonly Timer _timer;
        private readonly string _logFilePath;
        private StreamReader _reader;
        private long _pollingInterval = 1000L;
        private long _previousPosition = 0L;
        private bool _disposed = false;
        private DateTimeOffset _logStartTime = DateTimeOffset.UtcNow;
        private bool _foundStartTime = false;

        public event Action<LogMessage> OnNewMessage;

        public WarframeLogParser() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Warframe", "EE.log"))
        {
        }

        public WarframeLogParser(string path)
        {
            _logFilePath = path;

            _timer = new Timer(TimerHandler);
            _timer.Change(_pollingInterval, Timeout.Infinite);
        }

        private LogMessage ParseLine(string line)
        {
            var match = LogLineRegex.Match(line);
            if (!match.Success)
                return null;

            double? timer = null;
            var actualTime = DateTimeOffset.MinValue;
            if (double.TryParse(match.Groups["timer"].Value, out var temp))
            {
                timer = temp;
                actualTime = _logStartTime.AddSeconds(temp);
            }

            return new LogMessage
            {
                Timer = timer,
                Category = match.Groups["category"].Value,
                Level = match.Groups["level"].Value,
                Message = match.Groups["message"].Value,
                ActualTime = actualTime
            };
        }

        private void TimerHandler(object state)
        {
            var fi = new FileInfo(_logFilePath);

            if (fi.Exists)
            {

                if (_reader == null)
                {
                    _reader = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                }

                // Assume the file got truncated if it shrinks in size.
                if (fi.Length < _previousPosition)
                {
                    _previousPosition = 0;
                    _reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    _foundStartTime = false;
                }

                string line;
                while ((line = _reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;
                    var message = ParseLine(line);
                    if (message != null)
                    {
                        if (!_foundStartTime)
                        {
                            FindLogStartTime(message);
                        }

                        OnNewMessage?.Invoke(message);
                    }
                }

                _previousPosition = _reader.BaseStream.Position;
            }

            if (!_disposed)
                _timer.Change(_pollingInterval, Timeout.Infinite);
        }

        private void FindLogStartTime(LogMessage message)
        {
            var msg = message.Message;

            if (!msg.StartsWith("Current time:"))
                return;

            var m = Regex.Match(msg, @"Current time: [^\[]+ \[UTC: ([^\]]+)");
            if (!m.Success)
                return;

            if (DateTimeOffset.TryParseExact(m.Groups[1].Value, "ddd MMM d H:mm:ss yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                _foundStartTime = true;
                _logStartTime = dateTime.AddSeconds(-message.Timer.GetValueOrDefault());
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
