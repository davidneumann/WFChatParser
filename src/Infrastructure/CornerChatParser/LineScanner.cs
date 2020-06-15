using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using WebSocketSharp;

namespace CornerChatParser
{
    public static class LineScanner
    {
        private static int[] _lineOffsets = new int[] { 768, 818, 868, 917, 967, 1016, 1066, 1115, 1165, 1215, 1264, 1314, 1363, 1413, 1463, 1512, 1562, 1611, 1661, 1711, 1760, 1810, 1859, 1909, 1958, 2008, 2058 };

        public static ExtractedGlyph[] ExtractGlyphsFromLine(ImageCache image, Rectangle lineRect)
        {
            var results = new List<ExtractedGlyph>();
            var lastGlobalX = lineRect.Left;
            var localBlacklist = new bool[lineRect.Width, lineRect.Height];
            for (int globalX = lastGlobalX; globalX < lineRect.Right; globalX++)
            {
                var nextPoint = FindNextPoint(image, lineRect, localBlacklist, lastGlobalX);
                if (nextPoint == Point.Empty)
                    break;

                var validPixels = new List<Point>();
                var newValidPixels = GlyphExtractor.GetValidPixels(image, localBlacklist, nextPoint, lineRect);
                //Gotta keep scanning down for things like the dot in ! or the bits of a %
                while(newValidPixels != null && newValidPixels.Count > 0)
                {
                    validPixels.AddRange(newValidPixels);
                    BlacklistPixels(localBlacklist, newValidPixels, lineRect);
                    var leftmostX = validPixels.Min(p => p.X);
                    var rightmostX = validPixels.Max(p => p.X);
                    var bototmMost = validPixels.Where(p => p.X == leftmostX).Max(p => p.Y);
                    nextPoint = FindNextPoint(image, lineRect, localBlacklist, leftmostX);
                    if (nextPoint.X > rightmostX)
                        break;
                    newValidPixels = GlyphExtractor.GetValidPixels(image, localBlacklist, nextPoint, lineRect);
                }

                var newGlyph = GlyphExtractor.ExtractGlyphFromPixels(validPixels, lineRect);
                newGlyph.CenterLines = GlyphIdentifier.GetVerticalLines(image, newGlyph, newGlyph.GlobalGlpyhRect.Left + newGlyph.GlobalGlpyhRect.Width / 2 + 1);
                results.Add(newGlyph);

                globalX = lastGlobalX = newGlyph.GlobalGlpyhRect.Left;

                BlacklistGlyph(localBlacklist, newGlyph, lineRect);
            }

            return results.ToArray();
        }

        private static void BlacklistPixels(bool[,] localBlacklist, List<Point> newValidPixels, Rectangle lineRect)
        {
            foreach (var p in newValidPixels)
            {
                localBlacklist[p.X - lineRect.Left, p.Y - lineRect.Top] = true;
            }
        }

        public static ExtractedGlyph[] ExtractGlyphsFromLine(ImageCache image, int lineIndex)
        {
            return ExtractGlyphsFromLine(image, new Rectangle(4, _lineOffsets[lineIndex], 3236, 34));
        }

        public static void SaveExtractedGlyphs(ImageCache image, string outputDir, ExtractedGlyph[] glyphs)
        {
            var glyphsSaved = 0;

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            foreach (var glyph in glyphs)
            {
                var bitmap = new Bitmap(glyph.GlobalGlpyhRect.Width, glyph.GlobalGlpyhRect.Height);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        var v = (int)(image[x + glyph.GlobalGlpyhRect.Left, y + glyph.GlobalGlpyhRect.Top] * 255);
                        var c = Color.FromArgb(v, v, v);
                        bitmap.SetPixel(x, y, c);
                    }
                }

                var output = System.IO.Path.Combine(outputDir, $"glyph_{glyphsSaved++}.png");
                bitmap.Save(output);
                bitmap.Dispose();
            }
        }

        private static void BlacklistGlyph(bool[,] localBlacklist, ExtractedGlyph extractedGlyph, Rectangle lineRect)
        {
            for (int glyphX = 0; glyphX < extractedGlyph.GlobalGlpyhRect.Width; glyphX++)
            {
                for (int glyphY = 0; glyphY < extractedGlyph.GlobalGlpyhRect.Height; glyphY++)
                {
                    if (extractedGlyph.LocalDetectedCorners[glyphX, glyphY])
                    {
                        localBlacklist[extractedGlyph.GlobalGlpyhRect.Left - lineRect.Left,
                                       extractedGlyph.GlobalGlpyhRect.Top - lineRect.Top] = true;
                    }
                }
            }
        }

        private static Point FindNextPoint(ImageCache image, Rectangle globalLineRect, bool[,] localBlacklist, int startingGlobalX)
        {
            for (int globalX = startingGlobalX; globalX < globalLineRect.Right; globalX++)
            {
                for (int globalY = globalLineRect.Top; globalY < globalLineRect.Bottom; globalY++)
                {
                    if (image[globalX, globalY] > 0 &&
                        !localBlacklist[globalX - globalLineRect.Left, globalY - globalLineRect.Top])
                        return new Point(globalX, globalY);
                }
            }

            return Point.Empty;
        }
    }
}
