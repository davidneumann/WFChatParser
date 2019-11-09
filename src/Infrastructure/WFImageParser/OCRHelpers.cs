using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
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

        public static int LINEHEIGHT = 34;

        internal static List<Point> FindCharacterPixelPoints(System.Drawing.Point firstPixel, ImageCache image, CoordinateList blacklistedPoints, int minX, int maxX, int minY, int maxY)
        {
            var characterPoints = new CoordinateList();
            AddConnectedPoints(characterPoints, firstPixel, image, blacklistedPoints, minX, maxX, minY, maxY);
            
            var midX = (int)characterPoints.Average(p => p.X);

            //Scan down from the initial midpoint for gap characters
            //Account for gaps such as in i or j
            for (int y = characterPoints.Where(p => p.X == midX).Max(p => p.Y)+1; y < maxY; y++)
            {
                AddConnectedPoints(characterPoints, new System.Drawing.Point(midX, y), image, blacklistedPoints, minX, maxX, minY, maxY);
            }
            //Scan up from new midpoint for gap characters that have a bit sticking out the front. Account for gaps like in ;
            midX = (int)characterPoints.Average(p => p.X);
            var highestY = characterPoints.Min(p => p.X == midX ? p.Y : maxY);
            for (int i = highestY; i >= minY; i--)
            {
                AddConnectedPoints(characterPoints, new System.Drawing.Point(midX, i), image, blacklistedPoints, minX, maxX, minY, maxY);
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
                    AddConnectedPoints(characterPoints, new System.Drawing.Point(maxCharX, y), image, blacklistedPoints, minX, maxX, minY, maxY);
                    if (characterPoints.Count > origCount)
                        foundNewPixels = true;
                }
            } while (foundNewPixels);

            return new List<Point>(characterPoints);
        }
        
        public static float PixelValue(Hsv hsvPixel)
        {
            var v = (hsvPixel.V - 0.15f) / (1f - 0.15f);
            if (hsvPixel.H >= 175 && hsvPixel.H <= 185 //green
                        || hsvPixel.S < 0.3 //white
                        || hsvPixel.H >= 190 && hsvPixel.H <= 210) //blue
                return v;
            else
                return 0f;
        }
        private static void AddConnectedPoints(CoordinateList existingPoints, System.Drawing.Point firstPixel, ImageCache image, CoordinateList blacklistedPoints, int minX, int maxX, int minY, int maxY)
        {
            if (image[firstPixel.X, firstPixel.Y] <= 0 && firstPixel.X >= minX && firstPixel.X < maxX && firstPixel.Y >= minY && firstPixel.Y < maxY)
                return;
            var q = new Queue<System.Drawing.Point>();
            if (!existingPoints.Exists(firstPixel))
            {
                existingPoints.Add(firstPixel);
                q.Enqueue(firstPixel);
            }
            while(q.Count > 0)
            {
                var n = q.Dequeue();
                if(n.X + 1 <= maxX &&
                    image[n.X + 1, n.Y] > 0 && 
                    !existingPoints.Exists(n.X + 1, n.Y) && 
                    (blacklistedPoints == null || !blacklistedPoints.Exists(n.X + 1 , n.Y)))
                {
                    var np = new System.Drawing.Point(n.X + 1, n.Y);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.X - 1 >= minX &&
                    image[n.X - 1, n.Y] > 0 &&
                    !existingPoints.Exists(n.X - 1, n.Y) &&
                    (blacklistedPoints == null || !blacklistedPoints.Exists(n.X - 1, n.Y)))
                {
                    var np = new System.Drawing.Point(n.X - 1, n.Y);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.Y - 1 >= minY &&
                     image[n.X, n.Y - 1] > 0 &&
                     !existingPoints.Exists(n.X, n.Y - 1) &&
                     (blacklistedPoints == null || !blacklistedPoints.Exists(n.X, n.Y - 1)))
                {
                    var np = new System.Drawing.Point(n.X, n.Y - 1);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
                if (n.Y + 1 < maxY &&
                     image[n.X, n.Y + 1] > 0 &&
                     !existingPoints.Exists(n.X, n.Y + 1) &&
                     (blacklistedPoints == null || !blacklistedPoints.Exists(n.X , n.Y + 1)))
                {
                    var np = new System.Drawing.Point(n.X, n.Y + 1);
                    existingPoints.Add(np);
                    q.Enqueue(np);
                }
            }
        }

        private static int PixelValue(object p)
        {
            throw new NotImplementedException();
        }

        internal static TargetMask FindCharacterMask(System.Drawing.Point firstPixel, ImageCache image, CoordinateList blacklistedPoints, int minX, int maxX, int minY, int maxY)
        {
            var points = FindCharacterPixelPoints(firstPixel, image, blacklistedPoints, minX, maxX, minY, maxY);
            var minPointX = points.Min(p => p.X);
            var mask = new bool[points.Max(p => p.X) - points.Min(p => p.X)+1, maxY - minY];
            var softMask = new float[points.Max(p => p.X) - points.Min(p => p.X) + 1, maxY - minY];
            var pixelCount = 0;
            float softPixelCount = 0;
            foreach (var p in points)
            {
                mask[p.X - minPointX, p.Y - minY] = true;
                softMask[p.X - minPointX, p.Y - minY] = image[p.X, p.Y];
                softPixelCount += image[p.X, p.Y];
                pixelCount++;
            }
            return new TargetMask(mask, points.Max(p => p.X), minPointX, points.Max(p => p.X) - minPointX + 1, pixelCount, softPixelCount, softMask);
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
