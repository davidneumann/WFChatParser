using Application.Enums;
using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace RelativeChatParser
{
    public class RelativePixelParserFactory : IChatParserFactory
    {
        public IChatParser CreateChatParser(ClientLanguage clientLanguage)
        {
            return new RelativePixelParser();
        }
    }
}
