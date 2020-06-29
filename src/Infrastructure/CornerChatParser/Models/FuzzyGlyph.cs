using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace RelativeChatParser.Models
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
        public Dictionary<(int, int), float> Pixels = new Dictionary<(int, int), float>();
        public HashSet<(int, int)> Empties = new HashSet<(int, int)>();
        public int Width { get; set; }
        public int Height { get; set; }
        public int GapFromTopOfLine { get; set; }
        public bool IsOverlap { get; set; }
    }
}
