using Application.ChatLineExtractor;
using RelativeChatParser.Database;
using RelativeChatParser.Recognition;
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
        public Point[] CombinedLocations;

        public Point3[] RelativeBrights { get; set; }

        public ExtractedGlyph Clone()
        {
            var clone = (ExtractedGlyph)(this.MemberwiseClone());
            clone.RelativePixelLocations = (Point3[])this.RelativePixelLocations.Clone();
            clone.RelativeEmptyLocations = (Point[])this.RelativeEmptyLocations.Clone();
            clone.CombinedLocations = (Point[])this.CombinedLocations.Clone();
            return clone;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="glyph"></param>
        /// <param name="vOffset">The amount the glyph needs to be moved down</param>
        /// <returns></returns>
        public ExtractedGlyph Subtract(FuzzyGlyph glyph, float vOffset = 0f)
        {
            if (this.Width <= glyph.ReferenceMaxWidth)
                return null;

            //Wipe out any brights that match any location on the known glyph
            const float maxDistance = 2.83f;
            //var survivingBrights = RelativeBrights.Where(p => !glyph.RelativePixelLocations.Any(p2 => p2.X == p.X && p2.Y + vOffset == p.Y)).ToArray();
            var survivingBrights = RelativeBrights.Where(p1 => !glyph.RelativeBrights.Any(p2 =>
            {
                var p3 = new Point3(p2.X, (int)Math.Round(p2.Y + vOffset), p2.Z);
                return p1.Distance(p3, 2) <= maxDistance;
            })).ToArray();

            if (survivingBrights.Length == 0)
                return null;

            var localXMin = Math.Max(1, Math.Min(glyph.ReferenceMaxWidth, survivingBrights.Min(p => p.X) - 1));

            ////Wipe out anything to the left of the remaining leftmost bright. Allow an allowance of 1 column
            ////At least advance 1 pixel
            //var localXMin = Math.Max(1, Math.Max(glyph.ReferenceMinWidth - 1,  survivingBrights.Min(p => p.X) - 1));

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
            var localBottom = survivingPixels.Max(p => p.Y);
            var width = right - left;
            var height = bottom - top;

            var relEmpties = this.RelativeEmptyLocations.Where(p => p.X >= localXMin && p.Y >= survivingLocalTop && p.Y <= localBottom)
                                                        .Select(p => new Point(p.X - localXMin, p.Y - survivingLocalTop)).ToArray();

            //With empties and rels we can make combined
            var combined = relPixels.Select(p => new Point(p.X, p.Y)).Union(relEmpties).ToArray();
            var brights = relPixels.Where(p => p.Z >= GlyphDatabase.BrightMinV).ToArray();

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
                AspectRatio = (float)width / (float)height,
                CombinedLocations = combined,
                RelativeBrights = brights,
                FirstPixelColor = FirstPixelColor //This is technicially a bad way to do this
            };

            return result;
        }

        internal void Save(string filename, bool brightsOnly = false)
        {
            var pixels = brightsOnly ? RelativeBrights : RelativePixelLocations;
            using (var b = new Bitmap(Width, Height))
            {
                for (int x = 0; x < b.Width; x++)
                {
                    for (int y = 0; y < b.Height; y++)
                    {
                        var pixel = pixels.FirstOrDefault(p => p.X == x && p.Y == y);
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
