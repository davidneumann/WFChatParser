using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace RelativeChatParser
{
    public class RelativePixelParserFactory : IChatParserFactory
    {
        private ILogger _logger;
        private readonly IDataTxRx _sender;

        public RelativePixelParserFactory(ILogger logger, IDataTxRx sender)
        {
            _logger = logger;
            this._sender = sender;
        }
        public IChatParser CreateChatParser(ClientLanguage clientLanguage)
        {
            return new RelativePixelParser(_logger, _sender);
        }
    }
}
