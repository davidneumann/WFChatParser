using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace WFImageParser
{
    public static class ChatLineExtractor
    {
        public static Bitmap[] ExtractChatLines(Bitmap image, Rectangle messageRectangle)
        {
            var lineOffsets = new List<int>();
            for (int y = messageRectangle.Top; y < messageRectangle.Bottom; y++)
            {
                for (int i = 0; i < OCRHelpers.LineOffsets.Length; i++)
                {
                    if (OCRHelpers.LineOffsets[i] == y)
                    {
                        lineOffsets.Add(y);
                        break;
                    }
                }
            }

            var results = new Bitmap[lineOffsets.Count];
            var cache = new ImageCache(image);
            for (int i = 0; i < lineOffsets.Count; i++)
            {
                results[i] = new Bitmap(messageRectangle.Width, OCRHelpers.LINEHEIGHT);
                for (int x = 0; x < results[i].Width; x++)
                {
                    for (int y = 0; y < OCRHelpers.LINEHEIGHT; y++)
                    {
                        int cacheX = messageRectangle.Left + x;
                        int cacheY = lineOffsets[i] + y;
                        var cacheColor = cache.GetColor(cacheX, cacheY);
                        if (cacheColor == ImageCache.ChatColor.ChatTimestampName)
                            results[i].SetPixel(x, y, Color.White);
                        else
                        {
                            var v = cache[cacheX, cacheY];
                            var channel = byte.MaxValue - (byte)(byte.MaxValue * v);
                            var color = Color.FromArgb(channel, channel, channel);
                            results[i].SetPixel(x, y, color);
                        }
                    }
                }
            }

            return results;
        }
    }
}
