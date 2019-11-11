﻿using Application.Enums;
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
        private string _dataDirectory;

        public ChatParserFactory(ILogger logger, string dataDirectory)
        {
            _logger = logger;
            _dataDirectory = dataDirectory;
        }

        public IChatParser CreateChatParser(ClientLanguage clientLanguage)
        {
            if (clientLanguage == ClientLanguage.English)
                return new ChatParser(_logger, _dataDirectory);
            else
                throw new NotImplementedException();
        }
    }
}
