using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Utils
{
    public static class ImageCleaner
    {
        public static void SaveSoftMask(string input, string outputPath)
        {
            using (var rgbImage = new Bitmap(input))
            {
                using (var output = new Bitmap(rgbImage.Width, rgbImage.Height))
                {
                    var cache = new ImageCache(rgbImage);
                    for (int x = 0; x < rgbImage.Width; x++)
                    {
                        for (int y = 0; y < rgbImage.Height; y++)
                        {
                            var v = (int)(cache[x, y] * byte.MaxValue);
                            output.SetPixel(x, y, Color.FromArgb(v, v, v));
                        }
                    }
                    output.Save(outputPath);
                }
            }
        }
    }
}
