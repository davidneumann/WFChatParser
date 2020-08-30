using Application.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Application.Logger
{
    public class Logger : ILogger
    {
        private IDataSender _dataSender;
        private StreamWriter _streamWriter;
        private CancellationToken _token;
        private ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();

        public Logger(IDataSender dataSender, CancellationToken c)
        {
            _dataSender = dataSender;
            _streamWriter = new System.IO.StreamWriter("log.txt", false);
            _token = c;
            var t = new Thread(new ThreadStart(DataSenderMessageSender));
            t.Start();            
        }

        private void DataSenderMessageSender()
        {
            var processingSw = new System.Diagnostics.Stopwatch();
            while (!_token.IsCancellationRequested)
            {
                processingSw.Restart();
                var messageSb = new StringBuilder();
                string message = null;
                var messageCount = 0;
                do
                {
                    if (_messages.TryDequeue(out message))
                    {
                        messageCount++;
                        messageSb.Insert(0, message + "\n");
                        _streamWriter.WriteLine(message);
                    }
                    else
                        break;
                } while (messageCount < 1000 && message != null);
                if (messageCount > 0)
                {
                    _dataSender.AsyncSendLogMessage(messageSb.ToString());
                    _streamWriter.Flush();
                }
                processingSw.Stop();
                Thread.Sleep((int)Math.Max(0, 1000 - processingSw.ElapsedMilliseconds));
            }
        }

        public void Log(string message, bool writeToConsole = true)
        {
            message = $"[{DateTime.Now.ToString("HH:mm:ss.f")}] {message}";
            if (writeToConsole)
            {
                var consoleMessage = message;
                if (consoleMessage.Length > Console.BufferWidth)
                    consoleMessage = consoleMessage.Substring(0, Console.BufferWidth - 1);
                Console.WriteLine(consoleMessage);
            }
            _messages.Enqueue(message);
        }
    }
}
