using Application.ChatLineExtractor;
using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using Tesseract;

namespace CornerChatParser.Extraction
{
    public static class GlyphExtractor
    {
        private static bool[,] cache;
        private static int currentCacheMinX = -1;
        private static int currentCacheMaxX = 0;
        private static int distanceThresholdSquared = 2 * 2;
        private static int lastLineTop = 0;

        private static void ClearCacheSubregion(Rectangle lineRect)
        {
            for (int localX = currentCacheMinX; localX <= currentCacheMaxX; localX++)
            {
                for (int localY = 0; localY < lineRect.Height; localY++)
                {
                    cache[localX, localY] = false;
                }
            }
            currentCacheMinX = currentCacheMaxX;
        }

        public static List<Point> GetValidPixels(ImageCache image, bool[,] localBlacklist, Point firstPixel, Rectangle lineRect)
        {
            if (cache == null || cache.GetLength(0) != lineRect.Width || cache.GetLength(1) != lineRect.Height || lineRect.Top != lastLineTop)
            {
                cache = new bool[lineRect.Width, lineRect.Height];
                lastLineTop = lineRect.Top;
                currentCacheMinX = firstPixel.X;
                currentCacheMaxX = firstPixel.X;
            }

            ClearCacheSubregion(lineRect);

            if (image[firstPixel.X, firstPixel.Y] <= 0
                || localBlacklist[firstPixel.X - lineRect.Left, firstPixel.Y - lineRect.Top])
                return null;

            var checkQueue = new Queue<Point>();
            var validPixels = new List<Point>();
            checkQueue.Enqueue(firstPixel);
            //Find all points within 2 pixels of current pixels
            while (checkQueue.Count > 0)
            {
                var pixel = checkQueue.Dequeue();
                validPixels.Add(pixel);
                cache[pixel.X - lineRect.Left, pixel.Y - lineRect.Top] = true;
                currentCacheMaxX = Math.Max(currentCacheMaxX, pixel.X);

                for (int globalX = Math.Max(lineRect.Left, pixel.X - 2); globalX <= Math.Min(lineRect.Right - 1, pixel.X + 2); globalX++)
                {
                    for (int globalY = Math.Max(lineRect.Top, pixel.Y - 2); globalY <= Math.Min(lineRect.Bottom - 1, pixel.Y + 2); globalY++)
                    {
                        if (!cache[globalX - lineRect.Left, globalY - lineRect.Top] &&
                            !localBlacklist[globalX - lineRect.Left, globalY - lineRect.Top] &&
                            image[globalX, globalY] > 0f &&
                            PointsInRange(pixel, globalX, globalY))
                        {
                            cache[globalX - lineRect.Left, globalY - lineRect.Top] = true;
                            checkQueue.Enqueue(new Point(globalX, globalY));
                        }
                    }
                }
            }

            return validPixels;
        }

        public static ExtractedGlyph ExtractGlyphFromPixels(List<Point> validPixels, Rectangle lineRect)
        {
            ClearCacheSubregion(lineRect);

            foreach (var p in validPixels)
            {
                cache[p.X - lineRect.Left, p.Y - lineRect.Top] = true;
            }

            var minGlobalX = lineRect.Right;
            var maxGlobalX = lineRect.Left;
            var minGlobalY = lineRect.Bottom;
            var maxGlobalY = lineRect.Top;
            foreach (var p in validPixels)
            {
                minGlobalX = Math.Min(p.X, minGlobalX);
                maxGlobalX = Math.Max(p.X, maxGlobalX);
                minGlobalY = Math.Min(p.Y, minGlobalY);
                maxGlobalY = Math.Max(p.Y, maxGlobalY);
            }
            List<Point> corners = GetCorners(lineRect, minGlobalX, maxGlobalX, minGlobalY, maxGlobalY);
            List<Point> emptyPixels = GetEmpties(lineRect, minGlobalX, maxGlobalX, minGlobalY, maxGlobalY);

            var extractedGlobalMinX = maxGlobalX;
            var extractedGlobalMaxX = minGlobalX;
            var extractedGlobalMinY = maxGlobalY;
            var extractedGlobalMaxY = minGlobalY;
            foreach (var p in corners)
            {
                extractedGlobalMinX = Math.Min(p.X, extractedGlobalMinX);
                extractedGlobalMaxX = Math.Max(p.X, extractedGlobalMaxX);
                extractedGlobalMinY = Math.Min(p.Y, extractedGlobalMinY);
                extractedGlobalMaxY = Math.Max(p.Y, extractedGlobalMaxY);
            }
            var width = extractedGlobalMaxX - extractedGlobalMinX + 1;
            var height = extractedGlobalMaxY - extractedGlobalMinY + 1;

            var normalizedCorners = corners.Select(p => new Vector2((p.X - extractedGlobalMinX) / (float)(width - 1),
                                                                    (p.Y - extractedGlobalMinY) / (float)(height - 1)));
            var normalizedPixels = validPixels.Select(p => new Vector2((p.X - extractedGlobalMinX) / (float)(width - 1),
                                                                    (p.Y - extractedGlobalMinY) / (float)(height - 1)));
            var normalizedEmpties = emptyPixels.Select(p => new Vector2((p.X - extractedGlobalMinX) / (float)(width - 1),
                                                                    (p.Y - extractedGlobalMinY) / (float)(height - 1)));

            var localCorners = new bool[width, height];
            foreach (var p in corners)
            {
                localCorners[p.X - extractedGlobalMinX, p.Y - extractedGlobalMinY] = true;
            }

            var glyphRect = new Rectangle(extractedGlobalMinX, extractedGlobalMinY, width, height);
            var result = new ExtractedGlyph()
            {
                LocalDetectedCorners = localCorners,
                NormalizedCorners = normalizedCorners.ToArray(),
                NormalizedEmptyLocations = normalizedEmpties.ToArray(),
                NormalizedPixelLocations = normalizedPixels.ToArray(),
                PixelsFromTopOfLine = minGlobalY - lineRect.Top,
                Left = glyphRect.Left,
                Bottom = glyphRect.Bottom,
                Height = glyphRect.Height,
                Right = glyphRect.Right,
                Top = glyphRect.Top,
                Width = glyphRect.Width,
                LineOffset = lineRect.Top,
                AspectRatio = (float)width / height
            };

            ClearCacheSubregion(lineRect);

            return result;
        }

        private static List<Point> GetEmpties(Rectangle lineRect, int minGlobalX, int maxGlobalX, int minGlobalY, int maxGlobalY)
        {
            var empties = new List<Point>();
            for (int globalX = minGlobalX; globalX <= maxGlobalX; globalX++)
            {
                for (int globalY = minGlobalY; globalY <= maxGlobalY; globalY++)
                {
                    if (!cache[globalX - lineRect.Left, globalY - lineRect.Top])
                    {
                        empties.Add(new Point(globalX, globalY));
                    }
                }
            }

            return empties;
        }

        private static List<Point> GetCorners(Rectangle lineRect, int minGlobalX, int maxGlobalX, int minGlobalY, int maxGlobalY)
        {
            var corners = new List<Point>();
            for (int globalX = minGlobalX; globalX <= maxGlobalX; globalX++)
            {
                for (int globalY = minGlobalY; globalY <= maxGlobalY; globalY++)
                {
                    if (cache[globalX - lineRect.Left, globalY - lineRect.Top])
                    {
                        var cacheX = globalX - lineRect.Left;
                        var cacheY = globalY - lineRect.Top;
                        // Valid only if no neighbor directly across
                        var aboveEmpty = cacheY - 1 < 0 || !cache[cacheX, cacheY - 1];
                        var belowEmpty = cacheY + 1 >= lineRect.Height || !cache[cacheX, cacheY + 1];
                        var leftEmpty = cacheX - 1 < 0 || !cache[cacheX - 1, cacheY];
                        var rightEmpty = globalX + 1 > maxGlobalX || !cache[cacheX + 1, cacheY];

                        var oneOrLessVertNeighbors = aboveEmpty || belowEmpty;
                        var oneOrLessHorizNeighbors = leftEmpty || rightEmpty;

                        if (oneOrLessHorizNeighbors && oneOrLessVertNeighbors)
                            corners.Add(new Point(globalX, globalY));
                    }
                }
            }

            return corners;
        }

        private static bool PointsInRange(Point point, int x2, int y2)
        {
            var xDistance = Math.Abs(point.X - x2);
            var yDistance = Math.Abs(point.Y - y2);
            return xDistance <= 2 && yDistance <= 2;
            //var x = (x2 - point.X);
            //var y = (y2 - point.Y);
            //var d = (x * x) + (y * y);
            //return d < distanceThresholdSquared;
        }
    }
}
