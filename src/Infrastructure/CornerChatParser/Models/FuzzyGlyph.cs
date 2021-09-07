using Newtonsoft.Json;
using ParsingModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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

        public void SaveVisualization(string fileName, bool brightsOnly)
        {
            var b = new Bitmap(ReferenceMaxWidth, ReferenceMaxHeight);
            var pixelColor = Color.White;
            var emptyColor = Color.Black;
            var missingColor = Color.Magenta;
            var bothColor = Color.CornflowerBlue;
            var pixels = brightsOnly ? RelativeBrights : RelativePixelLocations;
            for (int x = 0; x < b.Width; x++)
            {
                for (int y = 0; y < b.Height; y++)
                {
                    bool isPixel = pixels.Any(p => p.X == x && p.Y == y);
                    bool isEmpty = RelativeEmptyLocations.Any(p => p.X == x && p.Y == y);
                    if (isPixel)
                    {
                        var pixel = pixels.First(p => p.X == x && p.Y == y);
                        var v = (int)(pixel.Z * byte.MaxValue);
                        if (isPixel && !isEmpty)
                        {
                            var c = Color.FromArgb(v, v, v);
                            b.SetPixel(x, y, c);
                        }
                        else if (isEmpty && isPixel)
                        {
                            var c = Color.FromArgb(0, 0, v);
                            b.SetPixel(x, y, c);
                        }
                    }
                    else if (isEmpty && !isPixel)
                        b.SetPixel(x, y, emptyColor);
                    else
                        b.SetPixel(x, y, missingColor);
                }
            }

            b.Save(fileName);
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
