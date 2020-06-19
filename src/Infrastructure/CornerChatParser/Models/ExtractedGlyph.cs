﻿using System;
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
    }
}
