using Application.ChatLineExtractor;
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
        public static int distanceThreshold = 2;

        public Rectangle GetCorePixelsRect(ImageCache image, ref bool[,] localBlacklist, Point firstPixel, Rectangle lineRect)
        {
            if (image[firstPixel.X, firstPixel.Y] <= 0
                || localBlacklist[firstPixel.X - lineRect.Left, firstPixel.Y - lineRect.Top])
                return Rectangle.Empty;

            var checkQueue = new Queue<Point>();
            var minX = lineRect.Right;
            var maxX = lineRect.Left;
            var topY = lineRect.Bottom;
            var bottomY = lineRect.Top;
            checkQueue.Enqueue(firstPixel);
            //Find all points within 2 pixels of current pixels
            while (checkQueue.Count > 0)
            {
                var pixel = checkQueue.Dequeue();
                minX = Math.Min(minX, pixel.X);
                maxX = Math.Max(maxX, pixel.X);
                topY = Math.Min(topY, pixel.Y);
                bottomY = Math.Max(bottomY, pixel.Y);

                for (int globalX = Math.Max(lineRect.Left, pixel.X - distanceThreshold); globalX <= Math.Min(lineRect.Right - 1, pixel.X + distanceThreshold); globalX++)
                {
                    for (int globalY = Math.Max(lineRect.Top, pixel.Y - distanceThreshold); globalY <= Math.Min(lineRect.Bottom - 1, pixel.Y + distanceThreshold); globalY++)
                    {
                        if (!localBlacklist[globalX - lineRect.Left, globalY - lineRect.Top] &&
                            image[globalX, globalY] >= GlyphDatabase.BrightMinV &&
                            PointsInRange(pixel, globalX, globalY))
                        {
                            checkQueue.Enqueue(new Point(globalX, globalY));
                            localBlacklist[globalX - lineRect.Left, globalY - lineRect.Top] = true;
                        }
                    }
                }
            }

            return new Rectangle(minX, topY, maxX - minX + 1, bottomY - topY + 1);
        }

        public ExtractedGlyph ExtractGlyphFromCorePixels(Rectangle lineRect, ImageCache image, Rectangle coreRect)
        {
            var minX = Math.Max(lineRect.Left, coreRect.Left - 2);
            var maxX = Math.Min(lineRect.Right, coreRect.Right + 2);
            var topY = Math.Max(lineRect.Top, coreRect.Top - 2);
            var bottomY = Math.Min(lineRect.Bottom, coreRect.Bottom + 2);

            var widthChange = (coreRect.Left - minX) + (maxX - coreRect.Right);
            var heightchange = (coreRect.Top - topY) + (bottomY - coreRect.Bottom);

            var glyphRect = new Rectangle(minX, topY, coreRect.Width + widthChange, coreRect.Height + heightchange);

            var localEmpties = new bool[glyphRect.Width, glyphRect.Height];
            var emptiesCount = 0;
            var localValdidPixels = new float[glyphRect.Width, glyphRect.Height];
            var validCount = 0;
            var localBrights = new float[glyphRect.Width, glyphRect.Height];
            var brightsCount = 0;
            var localCombined = new bool[glyphRect.Width, glyphRect.Height];
            for (int x = minX; x < maxX; x++)
            {
                for (int y = topY; y < bottomY; y++)
                {
                    var localX = x - minX;
                    var localY = y - topY;
                    localCombined[localX, localY] = image[x, y] > 0 || localEmpties[localX, localY];
                    if (image[x, y] > 0)
                    {
                        localValdidPixels[localX, localY] = image[x, y];
                        validCount++;
                        if (image[x, y] >= GlyphDatabase.BrightMinV)
                        {
                            localBrights[localX, localY] = image[x, y];
                            brightsCount++;
                        }
                    }
                    else
                    {
                        localEmpties[localX, localY] = true;
                        emptiesCount++;
                    }
                }
            }

            var result = new ExtractedGlyph()
            {
                PixelsFromTopOfLine = topY - lineRect.Top,
                Left = glyphRect.Left,
                Bottom = glyphRect.Bottom,
                Height = glyphRect.Height,
                Right = glyphRect.Right,
                Top = glyphRect.Top,
                Width = glyphRect.Width,
                LineOffset = lineRect.Top,
                AspectRatio = (float)glyphRect.Width / glyphRect.Height,
                RelativeEmpties = localEmpties,
                RelativeEmptiesCount = emptiesCount,
                RelativePixels = localValdidPixels,
                RelativePixelsCount = validCount,
                FromFile = image.DebugFilename,
                RelativeBrights = localBrights,
                RelativeBrightsCount = brightsCount,
                RelativeCombinedMask = localCombined
            };
            TrimGlyph(result);
            return result;
        }

        private void TrimGlyph(ExtractedGlyph extracted)
        {
            var minX = extracted.Width;
            var maxX = 0;
            var minY = extracted.Height;
            var maxY = 0;

            for (int x = 0; x < extracted.Width; x++)
            {
                for (int y = 0; y < extracted.Height; y++)
                {
                    if(extracted.RelativePixels[x, y] > 0)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            var pixels = new float[width, height];
            var empties = new bool[width, height];
            var brights = new float[width, height];
            var combined = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    pixels[x, y]  = extracted.RelativePixels [x + minX, y + minY];
                    empties[x, y] = extracted.RelativeEmpties[x + minX, y + minY];
                    brights[x, y] = extracted.RelativeBrights[x + minX, y + minY];
                    combined[x, y] = extracted.RelativeCombinedMask[x + minX, y + minY];
                }
            }
            extracted.Width = width;
            extracted.Height = height;
            extracted.RelativeBrights = brights;
            extracted.RelativePixels = pixels;
            extracted.RelativeEmpties = empties;
            extracted.AspectRatio = (float)width / height;
            extracted.Top += minY;
            extracted.Bottom -= minY;
            extracted.Left += minX;
            extracted.PixelsFromTopOfLine += minY;
            extracted.Right -= minX;
        }

        private bool PointsInRange(Point point, int x2, int y2)
        {
            var xDistance = Math.Abs(point.X - x2);
            var yDistance = Math.Abs(point.Y - y2);
            return xDistance <= distanceThreshold && yDistance <= distanceThreshold;
        }
    }
}
