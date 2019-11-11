using Application.Data;
using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using WFImageParser;

namespace ChineseChatParser
{
    public class ChineseChatParserFactory : IChatParserFactory
    {
        private ILogger _logger;

        public ChineseChatParserFactory(ILogger logger)
        {
            _logger = logger;
        }
        public IChatParser CreateChatParser(ClientLanguage clientLanguage)
        {
            if (clientLanguage == ClientLanguage.English)
            {
                _logger.Log("Creating english chat parser");
                return new ChatParser(_logger, DataHelper.OcrDataPathEnglish);
            }
            else
            {
                _logger.Log("Creating chinese chat parser");
                return new ChineseChatParser(_logger);
            }
        }
    }
}
