using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace RelativeChatParser.Models
{
    [DebuggerDisplay("{Character}")]
    public class FastFuzzyGlyph
    {
        public string Character { get; set;}
        public float[,] RelativePixels { get; set;}
        public float[,] RelativeBrights { get; set;}
        public bool[,] RelativeEmpties { get; set;}
        public float AspectRatio { get; set;}
        public int ReferenceMaxWidth { get; set;}
        public int ReferenceMaxHeight { get; set;}
        public int ReferenceMinWidth { get; set;}
        public int ReferenceMinHeight { get; set;}
        public float ReferenceGapFromLineTop { get; set;}
        public bool IsOverlap { get; set;} = false;
        public float[,] RelativeCombinedLocations { get; set;}
        public int RelativePixelsCount { get; set; }
        public int RelativeEmptiesCount { get; set; }
        public int RelativeBrightsCount { get; set; }

        public FastFuzzyGlyph()
        {

        }

        public FastFuzzyGlyph Clone()
        {
            var clone = (FastFuzzyGlyph)this.MemberwiseClone();
            clone.Character = string.Copy(Character);
            clone.RelativePixels = (float[,]) RelativePixels.Clone();
            clone.RelativeBrights = (float[,])RelativeBrights.Clone();
            clone.RelativeEmpties = (bool[,])RelativeEmpties.Clone();
            clone.RelativeCombinedLocations = (float[,])RelativeCombinedLocations.Clone();

            return clone;
        }

        public void SaveVisualization(string fileName, bool brightsOnly)
        {
            var b = new Bitmap(ReferenceMaxWidth, ReferenceMaxHeight);
            var pixelColor = Color.White;
            var emptyColor = Color.Black;
            var missingColor = Color.Magenta;
            var bothColor = Color.CornflowerBlue;
            var pixels = brightsOnly ? RelativeBrights : RelativePixels;
            for (int x = 0; x < b.Width; x++)
            {
                for (int y = 0; y < b.Height; y++)
                {
                    bool isPixel = pixels[x, y] > 0;
                    bool isEmpty = RelativeEmpties[x, y];
                    if (isPixel)
                    {
                        var pixel = pixels[x, y];
                        var v = (int)(pixel * byte.MaxValue);
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
}
