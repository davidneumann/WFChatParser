using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Application.Logger
{
    public class Logger : ILogger
    {
        private IDataSender _dataSender;
        private StreamWriter _streamWriter;

        public Logger(IDataSender dataSender)
        {
            _dataSender = dataSender;
            _streamWriter = new System.IO.StreamWriter("log.txt", false);
        }
        public void Log(string message)
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
