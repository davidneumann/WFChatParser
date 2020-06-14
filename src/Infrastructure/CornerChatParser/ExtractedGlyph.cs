using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;

namespace CornerChatParser
{
    public class ExtractedGlyph
    {
        public bool[,] LocalDetectedCorners;
        public Vector2[] NormalizedCorners;
        public Rectangle GlobalGlpyhRect;
        //public int Width;
        //public int Height;
        //public int InterLineOffset;
        public int LineOffset;
        //public Point GlobalTopLeft;
        public float AspectRatio;
    }
}
