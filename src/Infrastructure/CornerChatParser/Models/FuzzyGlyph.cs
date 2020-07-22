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
        public string Character { get; set;}
        public Point3[] RelativePixelLocations { get; set;}
        [JsonIgnore] public Point3[] RelativeBrights { get; set;}
        public Point[] RelativeEmptyLocations { get; set;}
        public float AspectRatio { get; set;}
        public int ReferenceMaxWidth { get; set;}
        public int ReferenceMaxHeight { get; set;}
        public int ReferenceMinWidth { get; set;}
        public int ReferenceMinHeight { get; set;}
        public float ReferenceGapFromLineTop { get; set;}
        public bool IsOverlap { get; set;} = false;
        public Point[] RelativeCombinedLocations { get; set;}

        //public FuzzyGlyph(string character, Point3[] relativePixelLocations, Point[] relativeEmptyLocations, float aspectRatio, int referenceMaxWidth, int referenceMaxHeight, int referenceMinWidth, int referenceMinHeight, 
        //    float referenceGapFromLineTop, bool isOverlap)
        //{
        //    this.Character = character;
        //    this.RelativePixelLocations = relativePixelLocations;
        //    this.RelativeEmptyLocations = relativeEmptyLocations;
        //    this.AspectRatio = aspectRatio;
        //    this.ReferenceMaxWidth = referenceMaxWidth;
        //    this.ReferenceMaxHeight = referenceMaxHeight;
        //    this.ReferenceMinWidth = referenceMinWidth;
        //    this.ReferenceMinHeight = referenceMinHeight;
        //    this.ReferenceGapFromLineTop = referenceGapFromLineTop;
        //    this.IsOverlap = isOverlap;
        //}
        public FuzzyGlyph()
        {

        }

        public FuzzyGlyph Clone()
        {
            var clone = (FuzzyGlyph)this.MemberwiseClone();
            clone.Character = string.Copy(Character);
            clone.RelativePixelLocations = (Point3[]) RelativePixelLocations.Clone();
            clone.RelativeBrights = (Point3[])RelativeBrights.Clone();
            clone.RelativeEmptyLocations = (Point[])RelativeEmptyLocations.Clone();
            clone.RelativeCombinedLocations = (Point[])RelativeCombinedLocations.Clone();

            return clone;
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
