using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CornerChatParser.Training
{
    public static class GlyphTrainer
    {
        public static Glyph CombineExtractedGlyphs(char character, IEnumerable<ExtractedGlyph> glyphs)
        {
            var glyphRect = new Rectangle(0, 0, glyphs.Select(g => g.Width).Max(),
                                                      glyphs.Select(g => g.Height).Max());
            var pixelCounts = new Dictionary<Point, int>();
            var emptyCounts = new Dictionary<Point, int>();
            foreach (var glyph in glyphs)
            {
                foreach (var pixel in glyph.RelativePixelLocations)
                {
                    if (!pixelCounts.ContainsKey(pixel))
                        pixelCounts[pixel] = 0;
                    pixelCounts[pixel]++;
                }
                foreach (var pixel in glyph.RelativeEmptyLocations)
                {
                    if (!emptyCounts.ContainsKey(pixel))
                        emptyCounts[pixel] = 0;
                    emptyCounts[pixel]++;
                }
            }

            var pixelCountsAverage = pixelCounts.Values.Average();
            var finalRelPixels = pixelCounts.Where(kvp => kvp.Value > pixelCountsAverage).Select(kvp => kvp.Key);
            var emptyCountsAverage = emptyCounts.Values.Average();
            var finalRelEmpties = emptyCounts.Where(kvp => kvp.Value > emptyCountsAverage).Select(kvp => kvp.Key);

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
                Character = character,
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
