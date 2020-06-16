using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;

namespace CornerChatParser.Models
{
    public class ExtractedGlyph
    {
        public bool[,] LocalDetectedCorners;
        public Vector2[] NormalizedCorners;
        public Vector2[] NormalizedPixelLocations;
        public Vector2[] NormalizedEmptyLocations;
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
    }
}
