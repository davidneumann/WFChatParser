using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WFImageParser
{
    internal static class OCRHelpers
    {
        internal static int[] LineOffsets = new int[] { 768, 818, 868, 917, 967, 1016, 1066, 1115, 1165, 1215, 1264, 1314, 1363, 1413, 1463, 1512, 1562, 1611, 1661, 1711, 1760, 1810, 1859, 1909, 1958, 2008, 2058 };

        internal static List<Point> FindCharacterPixelPoints(Point firstPixel, VCache image, List<Point> blacklistedPoints, float minV, int minX, int maxX, int minY, int maxY)
        {
            var characterPoints = new List<Point>();
            AddConnectedPoints(characterPoints, firstPixel, image, blacklistedPoints, minV, minX, maxX, minY, maxY);
            
            var midX = (int)characterPoints.Average(p => p.X);

            //Scan down from the initial midpoint for gap characters
            //Account for gaps such as in i or j
            for (int y = characterPoints.Where(p => p.X == midX).Max(p => p.Y)+1; y < maxY; y++)
            {
                AddConnectedPoints(characterPoints, new Point(midX, y), image, blacklistedPoints, minV, minX, maxX, minY, maxY);
            }
            //Scan up from new midpoint for gap characters that have a bit sticking out the front. Account for gaps like in ;
            midX = (int)characterPoints.Average(p => p.X);
            var highestY = characterPoints.Min(p => p.X == midX ? p.Y : maxY);
            for (int i = highestY; i >= minY; i--)
            {
                AddConnectedPoints(characterPoints, new Point(midX, i), image, blacklistedPoints, minV, minX, maxX, minY, maxY);
            }
            //Account for crazy gaps such as in %
            var foundNewPixels = false;
            do
            {
                foundNewPixels = false;
                var maxCharX = characterPoints.Max(p => p.X);
                for (int y = characterPoints.Where(p => p.X == maxCharX).Min(p => p.Y)+1; y < minY + ((maxY - minY) * 0.75f); y++)
                {
                    var origCount = characterPoints.Count;
                    AddConnectedPoints(characterPoints, new Point(maxCharX, y), image, blacklistedPoints, minV, minX, maxX, minY, maxY);
                    if (characterPoints.Count > origCount)
                        foundNewPixels = true;
                }
            } while (foundNewPixels);

            return characterPoints;
        }

        private static void AddConnectedPoints(List<Point> existingPoints, Point firstPixel, VCache image, List<Point> blacklistedPoints, float minV, int minX, int maxX, int minY, int maxY)
        {
            if (image[firstPixel.X, firstPixel.Y] < minV)
                return;
            var q = new Queue<Point>();
            if (!existingPoints.Any(p => p.X == firstPixel.X && p.Y == firstPixel.Y))
            {
                existingPoints.Add(firstPixel);
                q.Enqueue(firstPixel);
            }
            while(q.Count > 0)
            {
                var n = q.Dequeue();
                if(n.X + 1 <= maxX &&
                    image[n.X + 1, n.Y] >= minV && 
                    !existingPoints.Any(p => p.X == n.X + 1 && p.Y == n.Y) && 
                    (blacklistedPoints == null || (blacklistedPoints != null && !blacklistedPoints.Any(p => p.X == n.X + 1 && p.Y == n.Y))) )
                {
                    var np = new Point(n.X + 1, n.Y);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.X - 1 >= minX &&
                    image[n.X - 1, n.Y] >= minV &&
                    !existingPoints.Any(p => p.X == n.X - 1 && p.Y == n.Y) &&
                    (blacklistedPoints == null || (blacklistedPoints != null && !blacklistedPoints.Any(p => p.X == n.X - 1 && p.Y == n.Y))))
                {
                    Point np = new Point(n.X - 1, n.Y);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.Y - 1 >= minY &&
                     image[n.X, n.Y - 1] >= minV &&
                     !existingPoints.Any(p => p.X == n.X && p.Y == n.Y - 1) &&
                     (blacklistedPoints == null || (blacklistedPoints != null && !blacklistedPoints.Any(p => p.X == n.X && p.Y == n.Y - 1))))
                {
                    Point np = new Point(n.X, n.Y - 1);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.Y + 1 <= maxY &&
                     image[n.X, n.Y + 1] >= minV &&
                     !existingPoints.Any(p => p.X == n.X && p.Y == n.Y + 1) &&
                     (blacklistedPoints == null || (blacklistedPoints != null && !blacklistedPoints.Any(p => p.X == n.X && p.Y == n.Y + 1))))
                {
                    Point np = new Point(n.X, n.Y + 1);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
            }
        }

        internal static TargetMask FindCharacterMask(Point firstPixel, VCache image, List<Point> blacklistedPoints, float minV, int minX, int maxX, int minY, int maxY)
        {
            var points = FindCharacterPixelPoints(firstPixel, image, blacklistedPoints, minV, minX, maxX, minY, maxY);
            var minPointX = points.Min(p => p.X);
            var mask = new bool[points.Max(p => p.X) - points.Min(p => p.X)+1, maxY - minY];
            var pixelCount = 0;
            foreach (var p in points)
            {
                mask[p.X - minPointX, p.Y - minY] = true;
                pixelCount++;
            }
            return new TargetMask(mask, points.Max(p => p.X), minPointX, points.Max(p => p.X) - minPointX + 1, pixelCount);
        }

        internal static int NeighborCount(TargetMask prevTargetMask, int x, int y)
        {
            var width = prevTargetMask.Mask.GetLength(0);
            var height = prevTargetMask.Mask.GetLength(1);
            var mask = prevTargetMask.Mask;
            if (!prevTargetMask.Mask[x, y])
                return 0;
            var neighborCount = 0;
            if (x - 1 > 0 && mask[x - 1, y])
                neighborCount++;
            if (x + 1 < width && mask[x + 1, y])
                neighborCount++;
            if (y - 1 > 0 && mask[x, y - 1])
                neighborCount++;
            if (y + 1 < height && mask[x, y + 1])
                neighborCount++;
            return neighborCount;
        }
    }
}
