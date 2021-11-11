using Application.Logger;
using System;
using System.Globalization;
using System.IO;
using System.Text;
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
        private long _previousPosition;
        private bool _disposed = false;
        private DateTimeOffset _logStartTime = DateTimeOffset.UtcNow;
        private readonly StringBuilder _sb = new StringBuilder();
        private double _previousTime;
        private LogMessage _prevEntry;
        private readonly StringBuilder _prevMessage = new StringBuilder();

        public event Action<LogMessage> OnNewMessage;

        private ILogger _logger;

        public WarframeLogParser(ILogger logger) : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Warframe", "EE.log"), logger)
        {
        }

        public WarframeLogParser(string path, ILogger logger)
        {
            _logFilePath = path;

            _logger = logger;
            _timer = new Timer(TimerHandler);
            _timer.Change(_pollingInterval, Timeout.Infinite);

            try
            {
                _logger.Log("Warframe log parser started");
            }
            catch { }
        }

        private LogMessage ParseLine(string line)
        {
            var match = LogLineRegex.Match(line);
            if (!match.Success)
                return null;

            double timer;
            var actualTime = DateTimeOffset.Now;
            if (double.TryParse(match.Groups["timer"].Value, out var temp))
            {
                timer = temp;
                actualTime = _logStartTime.AddSeconds(temp);
                _previousTime = temp;
            }
            else
            {
                timer = _previousTime;
                actualTime = _logStartTime.AddSeconds(_previousTime);
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

        private void SearchForDate()
        {
            // Loops until we get to the date
            Match m = null;
            do
            {
                var line = ReadLine();
                if (line == null) continue;
                m = Regex.Match(line ?? "", @"Current time: [^\[]+ \[UTC: ([^\]]+)");
                if (m.Success)
                {
                    var message = ParseLine(line);
                    if (DateTimeOffset.TryParseExact(m.Groups[1].Value, "ddd MMM d H:mm:ss yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                        out var dateTime))
                    {
                        _logStartTime = dateTime.AddSeconds(-message.Timer);
                    }
                }
            } while (m == null || !m.Success);

            _reader.BaseStream.Seek(0, SeekOrigin.End);
            _reader.DiscardBufferedData();
            _previousPosition = _reader.BaseStream.Position;
        }

        private string ReadLine(bool wait = false)
        {
            while (true)
            {
                var c = _reader.Read();
                if (c == -1)
                {
                    if (wait) Thread.Sleep(25);
                    else break;
                }
                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && _reader.Peek() == '\n')
                    {
                        _reader.Read();
                    }

                    var line = _sb.ToString();
                    _sb.Clear();
                    return line;
                }

                _sb.Append((char)c);
            }

            return null;
        }

        private void TimerHandler(object state)
        {
            var fi = new FileInfo(_logFilePath);

            if (fi.Exists)
            {

                if (_reader == null)
                {
                    _reader = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    _sb.Clear();
                    SearchForDate();
                }

                // Assume the file got truncated if it shrinks in size.
                if (fi.Length < _previousPosition)
                {
                    Console.WriteLine("Log file truncated, starting over");
                    _previousPosition = 0;
                    _reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    SearchForDate();
                }

                while (true)
                {
                    var line = ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    var msg = ParseLine(line);

                    if (msg != null)
                    {
                        if (_prevEntry != null)
                        {
                            try
                            {
                                _logger.Log("Trying to send new log message");
                            }
                            catch { }
                            _prevEntry.Message = _prevMessage.ToString();
                            OnNewMessage?.Invoke(_prevEntry);
                            _prevMessage.Clear();
                        }

                        _prevEntry = msg;
                        _prevMessage.Append(msg.Message);
                    }
                    else
                    {
                        // Append to the previous entry's message
                        if (_prevEntry == null)
                            continue;
                        if (!string.IsNullOrEmpty(line))
                        {
                            _prevMessage.Append("\n");
                            _prevMessage.Append(line);
                        }
                    }
                }

                _previousPosition = _reader.BaseStream.Position;
            }

            if (!_disposed)
                _timer.Change(_pollingInterval, Timeout.Infinite);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
