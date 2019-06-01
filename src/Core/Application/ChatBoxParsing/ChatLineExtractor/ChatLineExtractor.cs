using Application.ChatLineExtractor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
                Bitmap newLine = new Bitmap(endX - startX, endY - startY);
                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        var v = (int)((1 - cache[x, y]) * 255);
                        newLine.SetPixel(x-startX, y-startY, Color.FromArgb(v, v, v));
                    }
                }

                using (var mem = new MemoryStream())
                {
                    newLine.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    mem.Seek(0, SeekOrigin.Begin);
                    using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load(mem))
                    {
                        var scale = 70f / image.Height;
                        var width = (int)Math.Round(scale * image.Width);
                        var height = (int)Math.Round(scale * image.Height);
                        image.Mutate(m => m.Resize(width, height).Pad(width + 40, height + 40).BackgroundColor(Rgba32.White));


                        mem.Seek(0, SeekOrigin.Begin);
                        mem.SetLength(0);
                        image.SaveAsPng(mem);
                        newLine.Dispose();
                        newLine = new Bitmap(mem);
                    }
                }
                lines.Add(newLine);
            }


            return lines.ToArray();
        }
    }
}
