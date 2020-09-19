using Newtonsoft.Json.Schema;
using RelativeChatParser.Database;
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
    public static class FastGlyphTrainer
    {
        public static FastFuzzyGlyph CombineExtractedGlyphs(char character, IEnumerable<FastExtractedGlyph> glyphs)
        {
            var maxWidth = 0;
            var maxHeight = 0;
            var minWidth = int.MaxValue;
            var minHeight = int.MaxValue;
            foreach (var glyph in glyphs)
            {
                maxWidth = Math.Max(maxWidth, glyph.Width);
                maxHeight = Math.Max(maxHeight, glyph.Height);
                minWidth = Math.Min(minWidth, glyph.Width);
                minHeight = Math.Min(minHeight, glyph.Height);
            }

            var glyphRect = new Rectangle(0, 0, maxWidth, maxHeight);
            var pixelCounts = new int[maxWidth, maxHeight];
            var pixelValues = new float[maxWidth, maxHeight];
            var emptyCounts = new int[maxWidth, maxHeight];
            foreach (var glyph in glyphs)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    for (int y = 0; y < glyph.Height; y++)
                    {
                        var value = glyph.RelativePixels[x, y];
                        if (value > 0)
                        {
                            pixelCounts[x, y]++;
                            pixelValues[x, y] += value;
                        }
                        else
                        {
                            emptyCounts[x, y]++;
                        }
                    }
                }
            }

            var averagePixelValues = new float[maxWidth, maxHeight];
            var averageBrightValues = new float[maxWidth, maxHeight];
            var totalEmpties = 0;
            var totalPixelCount = 0;
            var relativePixelCount = 0;
            var relativeBrightCount = 0;
            for (int x = 0; x < maxWidth; x++)
            {
                for (int y = 0; y < maxHeight; y++)
                {
                    totalPixelCount++;
                    totalEmpties += emptyCounts[x, y];

                    if (pixelCounts[x, y] > 0)
                        averagePixelValues[x, y] = pixelValues[x, y] / pixelCounts[x, y];

                    if (averagePixelValues[x, y] > 0)
                        relativePixelCount++;
                    if (averagePixelValues[x, y] >= FastGlyphDatabase.BrightMinV)
                    {
                        averageBrightValues[x, y] = averagePixelValues[x, y];
                        relativeBrightCount++;
                    }
                }
            }
            var emptyThreshold = (float)totalEmpties / totalPixelCount;
            var averageEmpties = new bool[maxWidth, maxHeight];
            var relativeEmptyCount = 0;
            for (int x = 0; x < maxWidth; x++)
            {
                for (int y = 0; y < maxHeight; y++)
                {
                    if (emptyCounts[x, y] > emptyThreshold)
                    {
                        averageEmpties[x, y] = true;
                        relativeEmptyCount++;
                    }
                }
            }

            return new FastFuzzyGlyph()
            {
                AspectRatio = (float)maxWidth / maxHeight,
                ReferenceMaxWidth = maxWidth,
                ReferenceMaxHeight = maxHeight,
                ReferenceGapFromLineTop = (int)Math.Round(glyphs.Select(g => g.Top - g.LineOffset).Average()),
                Character = character.ToString(),
                RelativePixels = averagePixelValues,
                RelativePixelsCount = relativePixelCount,
                RelativeEmpties = averageEmpties,
                RelativeEmptiesCount = relativeEmptyCount,
                RelativeBrights = averageBrightValues,
                RelativeBrightsCount = relativeBrightCount,
                ReferenceMinWidth = minWidth,
                ReferenceMinHeight = minHeight
            };
        }
    }
}
