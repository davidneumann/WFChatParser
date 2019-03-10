using Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    public static class ClickPointVisualizer
    {
        public static void DrawClickPointsOnImage(string imagePath, ClickPoint[] clickPoints)
        {
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                foreach (var clickPoint in clickPoints)
                {
                    for (int x = clickPoint.X - 5; x < clickPoint.X + 6; x++)
                    {
                        rgbImage[x, clickPoint.Y] = Rgba32.Red;
                    }

                    for (int y = clickPoint.Y - 5; y < clickPoint.Y + 6; y++)
                    {
                        rgbImage[clickPoint.X, y] = Rgba32.Red;
                    }
                }

                rgbImage.Save(imagePath);
            }
        }
    }
}
