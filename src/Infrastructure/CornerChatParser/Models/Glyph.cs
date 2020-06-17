using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Numerics;

namespace CornerChatParser.Models
{
    public class Glyph
    {
        public char Character;
        public Vector2[] Corners;
        public Point[] RelativePixelLocations;
        public Point[] RelativeEmptyLocations;
        [JsonIgnore]
        public bool[,] ReferenceCorners;
        public float AspectRatio;
        public int ReferenceMaxWidth;
        public int ReferenceMaxHeight;
        public int ReferenceMinWidth;
        public int ReferenceMinHeight;
        public float ReferenceGapFromLineTop;
        public float VerticalWeight;
        public float HorizontalWeight;

        public Glyph()
        {
        }
    }
}
