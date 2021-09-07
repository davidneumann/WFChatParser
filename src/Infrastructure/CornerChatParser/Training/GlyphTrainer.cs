using ParsingModel;
using RelativeChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

namespace RelativeChatParser.Training
{
    public static class GlyphTrainer
    {
        public static FuzzyGlyph CombineExtractedGlyphs(char character, IEnumerable<ExtractedGlyph> glyphs)
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

            return new FuzzyGlyph()
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

        public static FuzzyGlyph[] CombineExtractedGlyphsByRects(char character, IEnumerable<ExtractedGlyph> glyphs)
        {
            var rectDict = new Dictionary<Rectangle, List<ExtractedGlyph>>();
            foreach (var glyph in glyphs)
            {
                var rect = new Rectangle(0, 0, glyph.Width, glyph.Height);
                if (!rectDict.ContainsKey(rect))
                    rectDict[rect] = new List<ExtractedGlyph>();
                rectDict[rect].Add(glyph);
            }
            return rectDict.Select(kvp => CombineExtractedGlyphs(character, kvp.Value)).ToArray();
        }

        private static Vector2 PointToV2(Point p, int width, int height)
        {
            return new Vector2((float)p.X / (width - 1), (float)p.Y / (height - 1));
        }

        public static Glyph[] ExtractGlyphsFromSamples(string character, IEnumerable<ExtractedGlyph> glyphs)
        {
            var baseGlyph = CombineExtractedGlyphs(character[0], glyphs);
            var results = new List<Glyph>();
            for (int width = baseGlyph.ReferenceMinWidth - 1; width <= baseGlyph.ReferenceMaxWidth; width++)
            {
                for (int height = baseGlyph.ReferenceMinHeight - 1; height <= baseGlyph.ReferenceMaxHeight; height++)
                {
                    var resizedGlyph = ResizeGlyph(baseGlyph, width, height);
                    results.Add(resizedGlyph);
                }
            }

            return results.ToArray();
        }

        private static Glyph ResizeGlyph(FuzzyGlyph baseGlyph, int width, int height)
        {
            var sideTrimLeft = baseGlyph.ReferenceMaxWidth - width;
            var topTrimLeft = baseGlyph.ReferenceMaxHeight - height;
            var remainingPixels = new List<Point3>();
            remainingPixels.AddRange(baseGlyph.RelativePixelLocations);
            var remainingEmpties = new List<Point>();
            remainingEmpties.AddRange(baseGlyph.RelativeEmptyLocations);

            var topChange = 0;
            while (sideTrimLeft > 0 || topTrimLeft > 0)
            {
                var bottomY = remainingPixels.Max(p => p.Y);
                var rightX = remainingPixels.Max(p => p.X);
                var topLeft = remainingPixels.Where(p => p.Y == 0).Aggregate(0f, (acc, p) => acc + p.Z);
                var bottomLeft = remainingPixels.Where(p => p.Y == bottomY).Aggregate(0f, (acc, p) => acc + p.Z);
                var leftLeft = remainingPixels.Where(p => p.X == 0).Aggregate(0f, (acc, p) => acc + p.Z);
                var rightLeft = remainingPixels.Where(p => p.X == rightX).Aggregate(0f, (acc, p) => acc + p.Z);

                //Always cut the lowest row / column first
                //Trim top or bottom if we still have top/bottom to trim AND they are the lowest thing
                var shouldRemoveTopOrBottom = false;
                if(topTrimLeft > 0)
                {
                    if (sideTrimLeft <= 0)
                        shouldRemoveTopOrBottom = true;
                    else
                    {
                        if(topLeft <= leftLeft && topLeft <= rightLeft ||
                            bottomLeft <= leftLeft && bottomLeft <= rightLeft)
                        {
                            shouldRemoveTopOrBottom = true;
                        }
                    }
                }
                if (shouldRemoveTopOrBottom)
                {
                    if (topLeft <= bottomLeft)
                    {
                        topChange++;
                        remainingPixels.RemoveAll(p => p.Y == 0);
                        remainingEmpties.RemoveAll(p => p.Y == 0);
                    }
                    else
                    {
                        remainingPixels.RemoveAll(p => p.Y == bottomY);
                        remainingEmpties.RemoveAll(p => p.Y == bottomY);
                    }
                    topTrimLeft--;
                }
                else
                {
                    if (leftLeft <= rightLeft)
                    {
                        remainingPixels.RemoveAll(p => p.X == 0);
                        remainingEmpties.RemoveAll(p => p.X == 0);
                    }
                    else
                    {
                        remainingPixels.RemoveAll(p => p.X == rightX);
                        remainingEmpties.RemoveAll(p => p.X == rightX);
                    }
                    sideTrimLeft--;
                }

                //Slide all pixels to account for changes
                var minX = remainingPixels.Min(p => p.X);
                var minY = remainingPixels.Min(p => p.Y);
                var adjustedPixels = remainingPixels.Select(p => new Point3(p.X - minX, p.Y - minY, p.Z)).ToArray();
                remainingPixels.Clear();
                remainingPixels.AddRange(adjustedPixels);

                var adjustedEmpties = remainingEmpties.Where(p => p.X >= minX && p.Y >= minY).Select(p => new Point(p.X - minX, p.Y - minY)).ToArray();
                remainingEmpties.Clear();
                remainingEmpties.AddRange(adjustedEmpties);
            }

            var resultingEmpties = new HashSet<(int, int)>();
            foreach (var empty in remainingEmpties)
            {
                resultingEmpties.Add((empty.X, empty.Y));
            }

            var resultingPixels = new Dictionary<(int, int), float>();
            foreach (var pixel in remainingPixels)
            {
                resultingPixels[(pixel.X, pixel.Y)] = pixel.Z;
            }

            var result = new Glyph()
            {
                Character = baseGlyph.Character,
                Empties = resultingEmpties,
                Pixels = resultingPixels,
                Width = width,
                Height = height,
                IsOverlap = baseGlyph.IsOverlap,
                GapFromTopOfLine = (int)(Math.Round(baseGlyph.ReferenceGapFromLineTop)) + topChange
            };
            return result;
        }
    }
}
