using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LogParser
{
    public class AllTextParser
    {
        private IDataTxRx _dataSender;
        private WarframeLogParser _warframeLogParser;

        public AllTextParser(IDataTxRx dataSender, WarframeLogParser warframeLogParser)
        {
            _dataSender = dataSender;
            _warframeLogParser = warframeLogParser;
            _warframeLogParser.OnNewMessage += AllTextParser_OnNewMessage;
        }

        private void AllTextParser_OnNewMessage(LogMessage message)
        {

            _dataSender.AsyncSendLogLine(message).Wait();
        }
    }
}
