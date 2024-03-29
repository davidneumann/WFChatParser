﻿using Application.ChatLineExtractor;
using ParsingModel;
using RelativeChatParser.Database;
using RelativeChatParser.Models;
using RelativeChatParser.Recognition;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Tesseract;

namespace RelativeChatParser.Extraction
{
    public class GlyphExtractor
    {
        public static int distanceThreshold = 1;
        private bool[,] cache;
        private int currentCacheGlobalMinX = -1;
        private int currentCachGlobaleMaxX = 0;
        private int lastLineTop = 0;

        private void ClearCacheSubregion(Rectangle lineRect)
        {
            for (int localX = currentCacheGlobalMinX - lineRect.Left; localX <= currentCachGlobaleMaxX - lineRect.Left; localX++)
            {
                for (int localY = 0; localY < lineRect.Height; localY++)
                {
                    cache[localX, localY] = false;
                }
            }
            currentCacheGlobalMinX = currentCachGlobaleMaxX;
        }

        public List<Point> GetValidPixels(ImageCache image, bool[,] localBlacklist, Point firstPixel, Rectangle lineRect)
        {
            if (cache == null || cache.GetLength(0) != lineRect.Width || cache.GetLength(1) != lineRect.Height || lineRect.Top != lastLineTop)
            {
                cache = new bool[lineRect.Width, lineRect.Height];
                lastLineTop = lineRect.Top;
                currentCacheGlobalMinX = firstPixel.X;
                currentCachGlobaleMaxX = firstPixel.X;
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
                currentCachGlobaleMaxX = Math.Max(currentCachGlobaleMaxX, pixel.X);

                for (int globalX = Math.Max(lineRect.Left, pixel.X - distanceThreshold); globalX <= Math.Min(lineRect.Right - 1, pixel.X + distanceThreshold); globalX++)
                {
                    for (int globalY = Math.Max(lineRect.Top, pixel.Y - distanceThreshold); globalY <= Math.Min(lineRect.Bottom - 1, pixel.Y + distanceThreshold); globalY++)
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

        public ExtractedGlyph ExtractGlyphFromPixels(List<Point> validPixels, Rectangle lineRect, ImageCache image)
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

            
            var relativePixels = validPixels.Select(p =>
            {
                int x = p.X - extractedGlobalMinX;
                int y = p.Y - extractedGlobalMinY;
                return new Point3(x, y, image[p.X, p.Y]);
            });
            var relativeBrights = relativePixels.Where(p => p.Z >= GlyphDatabase.BrightMinV).ToArray();
            var relativeEmpties = emptyPixels.Select(p => new Point(p.X - extractedGlobalMinX, p.Y - extractedGlobalMinY));

            var glyphRect = new Rectangle(extractedGlobalMinX, extractedGlobalMinY, width, height);
            var extracedCombinedEmpties = relativePixels.Select(p => new Point(p.X, p.Y)).Union(relativeEmpties).ToArray();

            var result = new ExtractedGlyph()
            {
                PixelsFromTopOfLine = minGlobalY - lineRect.Top,
                Left = glyphRect.Left,
                Bottom = glyphRect.Bottom,
                Height = glyphRect.Height,
                Right = glyphRect.Right,
                Top = glyphRect.Top,
                Width = glyphRect.Width,
                LineOffset = lineRect.Top,
                AspectRatio = (float)width / height,
                RelativeEmptyLocations = relativeEmpties.ToArray(),
                RelativePixelLocations = relativePixels.ToArray(),
                FromFile = image.DebugFilename,
                RelativeBrights = relativeBrights,
                CombinedLocations = extracedCombinedEmpties
            };

            ClearCacheSubregion(lineRect);

            return result;
        }

        private List<Point> GetEmpties(Rectangle lineRect, int minGlobalX, int maxGlobalX, int minGlobalY, int maxGlobalY)
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

            var validEmpties = new List<Point>();
            foreach (var empty in empties)
            {
                var neighborCount = empties.Where(p => p != empty ? p.Distance(empty, 1) <= 1 : false).Count();
                if (neighborCount != 0)
                    validEmpties.Add(empty);
            }
            empties = validEmpties;

            return empties;
        }

        private List<Point> GetCorners(Rectangle lineRect, int minGlobalX, int maxGlobalX, int minGlobalY, int maxGlobalY)
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

        private bool PointsInRange(Point point, int x2, int y2)
        {
            var xDistance = Math.Abs(point.X - x2);
            var yDistance = Math.Abs(point.Y - y2);
            return xDistance <= distanceThreshold && yDistance <= distanceThreshold;
            //var x = (x2 - point.X);
            //var y = (y2 - point.Y);
            //var d = (x * x) + (y * y);
            //return d < distanceThresholdSquared;
        }
    }
}
