using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CornerChatParser.Models
{
    public class ExtractedGlyph
    {
        public Point[] RelativePixelLocations;
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

        public ExtractedGlyph Subtract(Glyph glyph)
        {
            // The new system allows for small gaps between characters. We need to scan for the
            // true new left
            var trueXMin = this.RelativePixelLocations.Where(p => p.X >= glyph.ReferenceMaxWidth)
                                                      .Min(p => p.X);

            // Only keep points beyond the new true min x
            var survivingPixels = this.RelativePixelLocations.Where(p => p.X >= trueXMin).ToArray();

            // With an overlap like &_ the new top will be way lower
            // and with other overlaps the bottom may be higher
            var survivingTop = survivingPixels.Select(p => p.Y).Min();

            //Use the width of the glyph and the new top to get the real pixels and emties
            var relPixels = survivingPixels.Select(p => new Point(p.X - trueXMin,
                                                                  p.Y - survivingTop)).ToArray();

            var left = survivingPixels.Min(p => p.X);
            var top = survivingTop;
            var right = survivingPixels.Max(p => p.X) + 1;
            var bottom = survivingPixels.Max(p => p.Y) + 1;
            var width = right - left;
            var height = bottom - top;

            var relEmpties = this.RelativeEmptyLocations.Where(p => p.X >= left && p.Y >= top && p.X < right && p.Y < bottom)
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
    }
}
