using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Numerics;

namespace CornerChatParser
{
    public class Glyph
    {
        public char Character;
        public Vector2[] Corners;
        [JsonIgnore]
        public bool[,] ReferenceCorners;
        public float AspectRatio;
        public int ReferenceWidth;
        public int ReferenceHeight;
        public float ReferenceGapFromLineTop;
        public float VerticalWeight;
        public float HorizontalWeight;
        internal int CenterLines;

        public Glyph()
        {
        }
    }
}
