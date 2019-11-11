using Application.Data;
using Application.Interfaces;
using Application.LineParseResult;
using Application.Logger;
using System;
using System.Drawing;
using WFImageParser;

namespace ChineseChatParser
{
    public class ChineseChatParser : IChatParser
    {
        private ILogger _logger;
        private ChatParser _backingCP;

        public ChineseChatParser(ILogger logger)
        {
            _logger = logger;
            _backingCP = new ChatParser(logger, DataHelper.OcrDataPathChinese);
        }
        public void InvalidateCache(string key)
        {
            _backingCP.InvalidateCache(key);
        }

        public bool IsChatFocused(Bitmap chatIconBitmap)
        {
            return _backingCP.IsChatFocused(chatIconBitmap);        }

        public bool IsScrollbarPresent(Bitmap fullScreenBitmap)
        {
            return _backingCP.IsScrollbarPresent(fullScreenBitmap);
        }

        public ChatMessageLineResult[] ParseChatImage(Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            var firstResult = _backingCP.ParseChatImage(image, useCache, isScrolledUp, lineParseCount);

            return firstResult;
        }
    }
}
