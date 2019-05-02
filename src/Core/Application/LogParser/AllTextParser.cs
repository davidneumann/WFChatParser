using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LogParser
{
    public class AllTextParser : WarframeLogParser
    {
        private IDataSender _dataSender;
        public AllTextParser(IDataSender dataSender)
        {
            _dataSender = dataSender;
            this.OnNewMessage += AllTextParser_OnNewMessage;
        }

        private void AllTextParser_OnNewMessage(LogMessage message)
        {
            _dataSender.AsyncSendLogLine(message).Wait();
        }
    }
}
