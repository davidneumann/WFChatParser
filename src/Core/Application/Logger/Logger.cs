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
            var t = new Thread(new ThreadStart(ProcessMessages));
            t.Start();            
        }

        private void ProcessMessages()
        {
            while(!_token.IsCancellationRequested)
            {
                string message = null;
                if (_messages.TryDequeue(out message) || message != null)
                {
                    message = $"[{DateTime.Now.ToString("HH:mm:ss.f")}] {message}";
                    _dataSender.AsyncSendLogMessage(message);
                    _streamWriter.WriteLine(message);
                    if (message.Length > Console.BufferWidth)
                        message = message.Substring(0, Console.BufferWidth - 1);
                    Console.WriteLine(message);
                }
            }
        }

        public void Log(string message)
        {
            _messages.Enqueue(message);
        }
    }
}
