using Application.ChatLineExtractor;
using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using WebSocketSharp;

namespace CornerChatParser.Extraction
{
    public static class LineScanner
    {
        public static readonly int[] LineOffsets = new int[] { 767, 816, 866, 915, 965, 1015, 1064, 1114, 1163, 1213, 1262, 1312, 1362, 1411, 1461, 1510, 1560, 1610, 1659, 1709, 1758, 1808, 1858, 1907, 1957, 2006, 2056 };
        public static readonly int ChatLeftX = 4;
        public static readonly int ChatWidth = 3236;
        public static readonly int Lineheight = 35;
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
                    if (nextPoint.X > rightmostX || nextPoint == Point.Empty)
                        break;
                    newValidPixels = GlyphExtractor.GetValidPixels(image, localBlacklist, nextPoint, lineRect);
                }

                var newGlyph = GlyphExtractor.ExtractGlyphFromPixels(validPixels, lineRect, image);
                results.Add(newGlyph);

                globalX = lastGlobalX = newGlyph.Left;

                //BlacklistGlyph(localBlacklist, newGlyph, lineRect);
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
            return ExtractGlyphsFromLine(image, new Rectangle(ChatLeftX, LineOffsets[lineIndex], ChatWidth, Lineheight));
        }

        public static void SaveExtractedGlyphs(ImageCache image, string outputDir, ExtractedGlyph[] glyphs)
        {
            var glyphsSaved = 0;

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            foreach (var glyph in glyphs)
            {
                var bitmap = new Bitmap(glyph.Width, glyph.Height);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        var v = (int)(image[x + glyph.Left, y + glyph.Top] * 255);
                        var c = Color.FromArgb(v, v, v);
                        bitmap.SetPixel(x, y, c);
                    }
                }

                var output = System.IO.Path.Combine(outputDir, $"glyph_{glyphsSaved++}.png");
                bitmap.Save(output);
                bitmap.Dispose();
            }
        }

        public static void SaveLines(Bitmap b, string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            var count = 0;
            foreach (var lineOffset in LineOffsets)
            {
                var rect = new Rectangle(ChatLeftX, lineOffset, ChatWidth, Lineheight);
                using (var clone = b.Clone(rect, b.PixelFormat))
                {
                    clone.Save(Path.Combine(outputDir, (count++) + ".png"));
                }
            }
        }

        private static void BlacklistGlyph(bool[,] localBlacklist, ExtractedGlyph extractedGlyph, Rectangle lineRect)
        {
            foreach (var p in extractedGlyph.RelativePixelLocations)
            {
                localBlacklist[p.X + extractedGlyph.Left - lineRect.Left,
                               p.Y + extractedGlyph.Top - lineRect.Top] = true;
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
