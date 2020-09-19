using Application.ChatLineExtractor;
using RelativeChatParser.Database;
using RelativeChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using WebSocketSharp;

namespace RelativeChatParser.Extraction
{
    public static class FastLineScanner
    {
        /*
         * Unused data for small text rendering at 4K
        */
        //public static readonly int[] LineOffsets = new int[] { 710, 729, 749, 768, 787, 807, 826, 846, 865, 885, 904, 923, 943, 962, 982, 1001, 1021, 1040, 1059, 1079, 1098, 1118, 1137, 1157, 1176, 1195, 1215, 1234, 1254, 1273, 1293, 1312, 1331, 1351, 1370, 1390, 1409, 1429, 1448, 1468, 1487, 1506, 1526, 1545, 1565, 1584, 1604, 1623, 1642, 1662, 1681, 1701, 1720, 1740, 1759, 1778, 1798, 1817, 1837, 1856, 1876, 1895, 1914, 1934, 1953, 1973, 1992, 2012, 2031, 2050, 2070, 2089, 2109 };
        //public static readonly int ChatWidth = 1722;
        //public static readonly int Lineheight = 14;
        public static readonly int[] LineOffsets = new int[] { 768, 817, 867, 916, 966, 1016, 1065, 1115, 1164, 1214, 1264, 1313, 1363, 1412, 1462, 1511, 1561, 1611, 1660, 1710, 1759, 1809, 1859, 1908, 1958, 2007, 2057 };
        public static readonly int ChatWidth = 3236;
        public static readonly int Lineheight = 35;

        public static readonly int ChatLeftX = 4;

        public static FastExtractedGlyph[] ExtractGlyphsFromLine(ImageCache image, Rectangle lineRect, bool abortAfterUsername = false)
        {
            var ge = new FastGlyphExtractor();

            var results = new List<FastExtractedGlyph>();
            var lastGlobalX = lineRect.Left;
            var localBlacklist = new bool[lineRect.Width, lineRect.Height];
            for (int globalX = lastGlobalX; globalX < lineRect.Right; globalX++)
            {
                var nextPoint = FindNextPoint(image, lineRect, localBlacklist, lastGlobalX);
                if (nextPoint == Point.Empty)
                    break;

                if (abortAfterUsername && image.GetColor(nextPoint.X, nextPoint.Y) != ChatColor.ChatTimestampName)
                    break;

                var chatColor = image.GetColor(nextPoint.X, nextPoint.Y);

                var newValidCoreRect = ge.GetCorePixelsRect(image, ref localBlacklist, nextPoint, lineRect);
                //Gotta keep scanning down for things like the dot in ! or the bits of a %

                while (newValidCoreRect != null && newValidCoreRect != Rectangle.Empty)
                {
                    //BlacklistPixels(localBlacklist, newValidPixels, lineRect);
                    var leftmostX = newValidCoreRect.Left;
                    var rightmostX = newValidCoreRect.Right;
                    //var bottomMost = validPixels.Where(p => p.X == leftmostX).Max(p => p.Y);
                    nextPoint = FindNextPoint(image, lineRect, localBlacklist, leftmostX);
                    if (nextPoint.X > rightmostX || nextPoint == Point.Empty)
                        break;
                    var additionalRect = ge.GetCorePixelsRect(image, ref localBlacklist, nextPoint, lineRect);
                    if (additionalRect == Rectangle.Empty)
                        break;
                    var left = Math.Min(newValidCoreRect.Left, additionalRect.Left);
                    var top = Math.Min(newValidCoreRect.Top, additionalRect.Top);
                    var right = Math.Max(newValidCoreRect.Right, additionalRect.Right);
                    var bottom = Math.Max(newValidCoreRect.Bottom, additionalRect.Bottom);
                    newValidCoreRect = new Rectangle(left, top, right - left, bottom - top);
                }

                var newGlyph = ge.ExtractGlyphFromCorePixels(lineRect, image, newValidCoreRect);
                newGlyph.FirstPixelColor = chatColor;
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

        public static ExtractedGlyph[] ExtractGlyphsFromLine(ImageCache image, int lineIndex, bool abortAfterUsername = false, int startX = 0)
        {
            var rect = new Rectangle(ChatLeftX, LineOffsets[lineIndex], ChatWidth, Lineheight);
            if (startX > 0)
            {
                var left = startX;
                var width = ChatWidth - (startX - ChatLeftX);
                rect = new Rectangle(left, LineOffsets[lineIndex], width, Lineheight);
            }
            //todo: add this back
            throw new NotImplementedException();
            //return ExtractGlyphsFromLine(image, rect, abortAfterUsername: abortAfterUsername);
        }

        public static FastExtractedGlyph[] ExtractGlyphsFromLineShim(ImageCache image, int lineIndex, bool abortAfterUsername = false, int startX = 0)
        {
            var rect = new Rectangle(ChatLeftX, LineOffsets[lineIndex], ChatWidth, Lineheight);
            if (startX > 0)
            {
                var left = startX;
                var width = ChatWidth - (startX - ChatLeftX);
                rect = new Rectangle(left, LineOffsets[lineIndex], width, Lineheight);
            }
            return ExtractGlyphsFromLine(image, rect, abortAfterUsername: abortAfterUsername);
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
        public static void SaveExtractedGlyphs(ImageCache image, string outputDir, FastExtractedGlyph[] glyphs)
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
                    if (image[globalX, globalY] >= GlyphDatabase.BrightMinV &&
                        !localBlacklist[globalX - globalLineRect.Left, globalY - globalLineRect.Top])
                        return new Point(globalX, globalY);
                }
            }

            return Point.Empty;
        }
    }
}
