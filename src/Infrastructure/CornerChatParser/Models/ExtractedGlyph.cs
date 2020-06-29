﻿using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace RelativeChatParser.Models
{
    public class ExtractedGlyph
    {
        public Point3[] RelativePixelLocations;
        public Point[] RelativeEmptyLocations;
        //public Rectangle GlobalGlpyhRect;
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
        public int Width;
        public int Height;
        //public int InterLineOffset;
        public int LineOffset;
        public int PixelsFromTopOfLine;
        //public Point GlobalTopLeft;
        public float AspectRatio;
        public string FromFile;
        public ChatColor FirstPixelColor;

        public ExtractedGlyph Subtract(FuzzyGlyph glyph)
        {
            if (this.Width <= glyph.ReferenceMaxWidth)
                return null;
            // The new system allows for small gaps between characters. We need to scan for the
            // true new left
            var localXMin = this.RelativePixelLocations.Where(p => p.X >= glyph.ReferenceMaxWidth)
                                                       .Min(p => p.X);

            // Only keep points beyond the new true min x
            var survivingPixels = this.RelativePixelLocations.Where(p => p.X >= localXMin).ToArray();

            // With an overlap like &_ the new top will be way lower
            // and with other overlaps the bottom may be higher
            var survivingLocalTop = survivingPixels.Select(p => p.Y).Min();

            //Use the width of the glyph and the new top to get the real pixels and emties
            var relPixels = survivingPixels.Select(p => new Point3(p.X - localXMin,
                                                                   p.Y - survivingLocalTop,
                                                                   p.Z)).ToArray();

            var left = survivingPixels.Min(p => p.X) + this.Left;
            var top = survivingLocalTop + this.LineOffset + this.PixelsFromTopOfLine;
            var right = survivingPixels.Max(p => p.X) + 1 + this.Left;
            var bottom = survivingPixels.Max(p => p.Y) + 1 + this.LineOffset + this.PixelsFromTopOfLine;
            var width = right - left;
            var height = bottom - top;

            var relEmpties = this.RelativeEmptyLocations.Where(p => p.X >= left && p.Y >= survivingLocalTop && p.X < right && p.Y < bottom)
                                                        .Select(p => new Point(p.X - left, p.Y - top)).ToArray();


            var result = new ExtractedGlyph()
            {
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                Width = width,
                Height = height,
                RelativePixelLocations = relPixels,
                RelativeEmptyLocations = relEmpties,
                LineOffset = this.LineOffset,
                PixelsFromTopOfLine = top - this.LineOffset,
                AspectRatio = (float)width / (float)height
            };

            return result;
        }

        internal void Save(string filename)
        {
            using (var b = new Bitmap(Width, Height))
            {
                for (int x = 0; x < b.Width; x++)
                {
                    for (int y = 0; y < b.Height; y++)
                    {
                        var pixel = RelativePixelLocations.FirstOrDefault(p => p.X == x && p.Y == y);
                        if (pixel != null)
                        {
                            var v = (int)(pixel.Z * byte.MaxValue);
                            var c = Color.FromArgb(v, v, v);
                            b.SetPixel(x, y, c);
                        }
                        else
                            b.SetPixel(x, y, Color.Black);
                    }
                }
                b.Save(filename);
            }
        }

        internal void Save(string v, Bitmap b)
        {
            var fileInfo = new FileInfo(v);
            if (!Directory.Exists(fileInfo.Directory.FullName))
            {
                Directory.CreateDirectory(fileInfo.Directory.FullName);
                Thread.Sleep(1000);
            }
            while (true)
            {
                try
                {
                    using (var clone = b.Clone(new Rectangle(Left, Top, Width, Height), b.PixelFormat))
                    {
                        clone.Save(v);
                    }
                    break;
                }
                catch
                {
                    var random = new Random();
                    Thread.Sleep(random.Next(100, 1000));
                }
            }
        }
    }
}
