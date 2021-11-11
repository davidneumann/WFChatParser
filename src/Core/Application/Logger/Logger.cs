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
        private IDataTxRx _dataSender;
        private StreamWriter _streamWriter;
        private CancellationToken _token;
        private ConcurrentQueue<LogMessage> _messages = new ConcurrentQueue<LogMessage>();

        public IDataTxRx DataSender { get => _dataSender; set => _dataSender = value; }

        public Logger(CancellationToken c)
        {
            var existingLog = new FileInfo("log.txt");
            if (existingLog.Exists)
            {
                File.Delete("log.old.txt");
                existingLog.MoveTo("log.old.txt");
            }
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
                var socketMessageSb = new StringBuilder();
                LogMessage message = null;
                var messageCount = 0;
                do
                {
                    if (_messages.TryDequeue(out message))
                    {
                        messageCount++;
                        if (message.WriteToFile)
                        {
                            _streamWriter.WriteLine(message.Message);
                        }
                        if(message.SendToSocket)
                            messageSb.Insert(0, message.Message + "\n");
                        if(message.WriteToConsole)
                        {
                            var consoleMessage = message.Message;
                            if (consoleMessage.Length > Console.BufferWidth)
                                consoleMessage = consoleMessage.Substring(0, Console.BufferWidth - 1);
                            Console.WriteLine(consoleMessage);
                        }
                    }
                    else
                        break;
                } while (messageCount < 1000 && message != null);
                if (messageCount > 0)
                {
                    if(_dataSender != null)
                        _dataSender.AsyncSendLogMessage(messageSb.ToString());
                    _streamWriter.Flush();
                }
                processingSw.Stop();
                Thread.Sleep((int)Math.Max(0, 1000 - processingSw.ElapsedMilliseconds));
            }
        }

        public void Log(string message, bool writeToConsole = true, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            bool sendToSocket = true)
        {
            message = $"[{DateTime.Now.ToString("HH:mm:ss.f")}] {memberName} -> {message}";
            _messages.Enqueue(new LogMessage() { Message = message, SendToSocket = sendToSocket, WriteToConsole = writeToConsole });
        }

        private class LogMessage
        {
            public bool WriteToFile = true;
            public bool SendToSocket = true;
            public bool WriteToConsole = true;
            public string Message = string.Empty;
            public override string ToString()
            {
                return Message;
            }
        }
    }
}
