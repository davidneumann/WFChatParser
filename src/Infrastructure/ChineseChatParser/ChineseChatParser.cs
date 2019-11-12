using Application.ChatBoxParsing;
using Application.Data;
using Application.Enums;
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
        private ClientLanguage _language;
        private ILogger _logger;
        private ChatParser _backingCP;
        private TessChatLineParser _lp;

        public ChineseChatParser(ILogger logger, ClientLanguage clientLanguage)
        {
            _language = clientLanguage;
            _logger = logger;
            _backingCP = new ChatParser(logger, DataHelper.OcrDataPathChinese);
            _lp = new TessChatLineParser(_language);
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
            _logger.Log("Getting initial result");
            var lines = _backingCP.ParseChatImage(image, useCache, isScrolledUp, lineParseCount);
            _logger.Log("Retrieved " + lines.Length + " lines of \"text\"");
            //for (int i = 0; i < lines.Length; i++)
            //{
            //    Rectangle rect = lines[i].MessageBounds;
            //    using (var lineBitmap = new Bitmap(rect.Width, rect.Height))
            //    {
            //        for (int x = 0; x < lineBitmap.Width; x++)
            //        {
            //            for (int y = 0; y < lineBitmap.Height; y++)
            //            {
            //                lineBitmap.SetPixel(x, y, image.GetPixel(rect.Left + x, rect.Top + y));
            //            }
            //        }
            //        lineBitmap.Save("line_" + i + ".png");
            //    }

            //    _logger.Log("Getting real text for line: " + i);
            //    var tessLines = WFImageParser.ChatLineExtractor.ExtractChatLines(image, rect);
            //    ChatMessageLineResult fullMessage = null;
            //    for (int j = 0; j < tessLines.Length; j++)
            //    {
            //        tessLines[j].Save("line_" + i + "_" + j + ".png");
            //        var parsedLine = _lp.ParseLine(tessLines[j]) as ChatMessageLineResult;
            //        if (fullMessage == null)
            //        {
            //            fullMessage = parsedLine;
            //            fullMessage.Username = lines[i].Username;
            //            fullMessage.Timestamp = lines[i].Timestamp;
            //            fullMessage.RawMessage = $"{fullMessage.Timestamp} {fullMessage.Username}{fullMessage.RawMessage}";
            //            fullMessage.EnhancedMessage = $"{fullMessage.Timestamp} {fullMessage.Username}{fullMessage.EnhancedMessage}";
            //        }
            //        else
            //            fullMessage.Append(parsedLine, 0, 0);
            //    }
            //    fullMessage.MessageBounds = rect;
            //    lines[i] = fullMessage;
            //}

            _logger.Log("All lines parsed");
            return lines;
        }
    }
}
