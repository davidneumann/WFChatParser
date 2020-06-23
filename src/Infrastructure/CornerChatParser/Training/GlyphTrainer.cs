using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace CornerChatParser.Training
{
    public static class GlyphTrainer
    {
        public static Glyph CombineExtractedGlyphs(char character, IEnumerable<ExtractedGlyph> glyphs)
        {
            var glyphRect = new Rectangle(0, 0, glyphs.Select(g => g.Width).Max(),
                                                      glyphs.Select(g => g.Height).Max());
            var pixelCounts = new Dictionary<Point, int>();
            var pixelValues = new Dictionary<Point, float>();
            var emptyCounts = new Dictionary<Point, int>();
            foreach (var glyph in glyphs)
            {
                foreach (var pixel3 in glyph.RelativePixelLocations)
                {
                    var pixel = new Point(pixel3.X, pixel3.Y);
                    if (!pixelCounts.ContainsKey(pixel))
                    {
                        pixelCounts[pixel] = 0;
                        pixelValues[pixel] = 0f;
                    }
                    pixelCounts[pixel]++;
                    pixelValues[pixel] += pixel3.Z;
                }
                foreach (var pixel in glyph.RelativeEmptyLocations)
                {
                    if (!emptyCounts.ContainsKey(pixel))
                        emptyCounts[pixel] = 0;
                    emptyCounts[pixel]++;
                }
            }

            if(character == ']')
            {
                var i = 0;
                if(Directory.Exists("debug_glyphs"))
                {
                    Directory.Delete("debug_glyphs", true);
                    Thread.Sleep(1000);
                }
                Directory.CreateDirectory("debug_glyphs");
                foreach (var g in glyphs)
                {
                    g.Save(Path.Combine("debug_glyphs", (i++) + ".png"));
                }
            }

            var pixelCountsAverage = pixelCounts.Values.Count > 0 ? pixelCounts.Values.Average() : 0;
            foreach (var item in pixelValues.ToArray())
            {
                pixelValues[item.Key] = item.Value / pixelCounts[item.Key];
            }
            var finalRelPixels = pixelCounts.Where(kvp => kvp.Value >= 0)//pixelCountsAverage)
                .Select(kvp => new Point3(kvp.Key.X, kvp.Key.Y, pixelValues[kvp.Key]));
            var emptyCountsAverage = emptyCounts.Values.Count > 0 ? emptyCounts.Values.Average() : 0;
            var finalRelEmpties = emptyCounts.Where(kvp => kvp.Value >= emptyCountsAverage).Select(kvp => kvp.Key);

            var masterEGlyph =
                new ExtractedGlyph()
                {
                    AspectRatio = glyphs.Average(glyph => glyph.AspectRatio),
                    Left = glyphRect.Left,
                    Bottom = glyphRect.Bottom,
                    Height = glyphRect.Height,
                    Right = glyphRect.Right,
                    Top = glyphRect.Top,
                    Width = glyphRect.Width,
                    LineOffset = glyphs.Select(g => g.Top - g.LineOffset).Min(),
                    PixelsFromTopOfLine = (int)Math.Round(glyphs.Select(g => g.Top - g.LineOffset).Average()),
                    RelativeEmptyLocations = finalRelEmpties.ToArray(),
                    RelativePixelLocations = finalRelPixels.ToArray()
                };

            return new Glyph()
            {
                AspectRatio = masterEGlyph.AspectRatio,
                ReferenceMaxWidth = masterEGlyph.Width,
                ReferenceMaxHeight = masterEGlyph.Height,
                ReferenceGapFromLineTop = masterEGlyph.PixelsFromTopOfLine,
                Character = character.ToString(),
                RelativePixelLocations = masterEGlyph.RelativePixelLocations,
                RelativeEmptyLocations = masterEGlyph.RelativeEmptyLocations,
                ReferenceMinWidth = glyphs.Min(g => g.Width),
                ReferenceMinHeight = glyphs.Min(g => g.Height)
            };
        }

        private static Vector2 PointToV2(Point p, int width, int height)
        {
            return new Vector2((float)p.X / (width - 1), (float)p.Y / (height - 1));
        }
    }
}
