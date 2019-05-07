using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    public class ChatParserFactory : IChatParserFactory
    {
        private ILogger _logger;

        public ChatParserFactory(ILogger logger)
        {
            _logger = logger;
        }

        public IChatParser CreateChatParser()
        {
            return new ChatParser(_logger);
        }
    }
}
