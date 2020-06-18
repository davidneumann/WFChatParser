﻿using Application.ChatLineExtractor;
using Application.Interfaces;
using Application.LineParseResult;
using CornerChatParser.Extraction;
using CornerChatParser.Recognition;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CornerChatParser
{
    public class RelativePixelParser : IChatParser
    {
        public void InvalidateCache(string key)
        {
            throw new NotImplementedException();
        }

        public bool IsChatFocused(Bitmap chatIconBitmap)
        {
            throw new NotImplementedException();
        }

        public bool IsScrollbarPresent(Bitmap fullScreenBitmap)
        {
            throw new NotImplementedException();
        }

        public ChatMessageLineResult[] ParseChatImage(Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            var imageCache = new ImageCache(image);

            var result = new ChatMessageLineResult[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                var glyphs = LineScanner.ExtractGlyphsFromLine(imageCache, i);
                Console.Write("\r");
                result[i] = new ChatMessageLineResult()
                {
                    RawMessage = new String(glyphs.Select(g => RelativePIxelIdentifier.IdentifyGlyph(g).Character).ToArray())
                };
            }

            return result;
        }
    }
}