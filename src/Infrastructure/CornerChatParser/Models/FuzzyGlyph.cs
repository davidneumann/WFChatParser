using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace CornerChatParser.Models
{
    [DebuggerDisplay("{Character}")]
    public class FuzzyGlyph
    {
        public string Character;
        public Point3[] RelativePixelLocations;
        public Point[] RelativeEmptyLocations;
        public float AspectRatio;
        public int ReferenceMaxWidth;
        public int ReferenceMaxHeight;
        public int ReferenceMinWidth;
        public int ReferenceMinHeight;
        public float ReferenceGapFromLineTop;
        public bool IsOverlap = false;

        public FuzzyGlyph()
        {
        }
    }

    public class Glyph
    {
        public string Character { get; set; }
        public Dictionary<Point, float> Pixels = new Dictionary<Point, float>();
        public HashSet<Point> Empties = new HashSet<Point>();
        public int Width { get; set; }
        public int Height { get; set; }
        public int GapFromTopOfLine { get; set; }
        public bool IsOverlap { get; set; }
    }
}
