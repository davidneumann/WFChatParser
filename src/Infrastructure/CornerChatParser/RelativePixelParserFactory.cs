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

        public RelativePixelParserFactory(ILogger logger)
        {
            _logger = logger;
        }
        public IChatParser CreateChatParser(ClientLanguage clientLanguage)
        {
            return new RelativePixelParser(_logger);
        }
    }
}
