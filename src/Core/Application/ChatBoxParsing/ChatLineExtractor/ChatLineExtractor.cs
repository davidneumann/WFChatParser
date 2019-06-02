using Application.ChatLineExtractor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Application.ChatBoxParsing.ChatLineExtractor
{
    public class ChatLineExtractor : IChatLineExtractor
    {
        
        public Bitmap[] ExtractChatLines(Bitmap screenshot)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var lineYStarts = new List<int>();
            var cache = new ImageCache(screenshot);
            var lines = new List<Bitmap>();
            for (int y = chatRect.Top; y < chatRect.Bottom; y++)
            {
                for (int x = chatRect.Left; x < chatRect.Right; x++)
                {
                    var v = cache[x, y];
                    if (v > 0f)
                    {
                        lineYStarts.Add(y);
                        y += 40;
                        break;
                    }
                }
            }
            lineYStarts.Add(lineYStarts.Last() + 50);

            for (int i = 0; i < lineYStarts.Count - 1; i++)
            {
                var startX = chatRect.Left;
                var endX = chatRect.Right;
                var startY = lineYStarts[i];
                var endY = lineYStarts[i + 1] - 1;
                //MinX
                for (int x = startX; x < endX; x++)
                {
                    var pixelFound = false;
                    for (int y = startY; y < endY; y++)
                    {
                        if(cache[x,y] > 0)
                        {
                            pixelFound = true;
                            break;
                        }
                    }
                    if (!pixelFound)
                        startX = x;
                    else
                        break;
                }

                //MaxX
                for (int x = endX; x > startX; x--)
                {
                    var pixelFound = false;
                    for (int y = startY; y < endY; y++)
                    {
                        if (cache[x, y] > 0)
                        {
                            pixelFound = true;
                            break;
                        }
                    }
                    if (!pixelFound)
                        endX = x;
                    else
                        break;
                }

                //MaxY
                for (int y = endY; y > startY; y--)
                {
                    var pixelFound = false;
                    for (int x = startX; x < endX; x++)
                    {
                        if (cache[x, y] > 0)
                        {
                            pixelFound = true;
                            break;
                        }
                    }
                    if (!pixelFound)
                        endY = y;
                    else
                        break;
                }

                //Make new bitmap
                Bitmap newLine = new Bitmap(endX - startX, endY - startY, PixelFormat.Format24bppRgb);
                var data = newLine.LockBits(new Rectangle(0, 0, newLine.Width, newLine.Height), ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
                var bytes = new byte[data.Stride * newLine.Height];
                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        var pos = (y-startY) * data.Stride + (x - startX) * 3;
                        var v = (byte)((1 - cache[x, y]) * 255);
                        bytes[pos] = v;
                        bytes[pos + 1] = v;
                        bytes[pos + 2] = v;
                    }
                }
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
                newLine.UnlockBits(data);

                lines.Add(newLine);
            }


            return lines.ToArray();
        }
    }
}
