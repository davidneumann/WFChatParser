using Application.Interfaces;
using Application.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace WFImageParser
{
    public class RivenCleaner : IRivenCleaner
    {
        private System.Drawing.Point[] _middleLockPixels = new System.Drawing.Point[] { new System.Drawing.Point(176, 725), new System.Drawing.Point(189, 720), new System.Drawing.Point(247, 725) };

        public void FastPrepareRiven(string imagePath, string outputPath)
        {

            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load(imagePath))
            {
                image.Mutate(i => i.Crop(new SixLabors.Primitives.Rectangle(1804, 464, 488, 746)).Resize(232, 373));
                image.Save(outputPath);
            }
        }

        private bool IsPurple(Rgba32 p)
        {
            var color = Color.FromArgb(p.R, p.G, p.B);
            var hsv = color.ToHsv();
            if (hsv.Hue >= 240 && hsv.Hue <= 280
                && hsv.Value >= 0.4)
                return true;
            return false;
        }
        public Bitmap CleanRiven(Bitmap croppedRiven)
        {
            Bitmap result = null;
            Rgba32 background = Rgba32.Black;
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 540, 740, background))
            {
                var croppedAsMemory = new MemoryStream();
                croppedRiven.Save(croppedAsMemory, System.Drawing.Imaging.ImageFormat.Bmp);
                croppedAsMemory.Seek(0, SeekOrigin.Begin);
                using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load(croppedAsMemory))
                {
                    var refX = 21;
                    var refY = 63;
                    //Copy title/modis
                    var pastBackground = false;
                    var converter = new ColorSpaceConverter();
                    Rgba32 foreground = Rgba32.White;
                    for (int y = 0; y < 630; y++)
                    {
                        //Check if this line is still image
                        if (!pastBackground)
                        {
                            var Vs = new float[30];
                            for (int x = 0; x < 15; x++)
                            {
                                Vs[x] = converter.ToHsv(image[refX + x, refY + y]).V;
                            }
                            for (int x = 540-15; x < 540; x++)
                            {
                                Vs[x - (540-15) + 15] = converter.ToHsv(image[refX + x, refY + y]).V;
                            }
                            if (Vs.Average() >= 0.165)
                                continue;
                            else
                                pastBackground = true;
                        }
                        for (int x = 0; x < 540; x++)
                        {
                            var p = image[refX + x, refY + y];
                            if (IsPurple(p))
                                outputImage[x, y] = foreground;
                        }
                    }
                    refX = 469;
                    refY = 2;
                    //Copy Drain
                    for (int x = 0; x < 60; x++)
                    {
                        for (int y = 0; y < 45; y++)
                        {
                            var p = image[refX + x, refY + y];
                            if (IsPurple(p))
                                outputImage[x, y + 640] = foreground;
                        }
                    }
                    refX = 65;
                    refY = 704;
                    //Copy MR & rerolls
                    for (int x = 0; x < 450; x++)
                    {
                        for (int y = 0; y < 42; y++)
                        {
                            var p = image[refX + x, refY + y];
                            if (IsPurple(p))
                                outputImage[x, y + 640 + 45 + 10] = foreground;
                        }
                    }

                    //Clean up bottom corners
                    for (int x = 0; x < 25; x++)
                    {
                        for (int y = 587; y < 587 + 48; y++)
                        {
                            outputImage[x, y] = background;
                        }
                    }
                    for (int x = outputImage.Width - 25; x < outputImage.Width; x++)
                    {
                        for (int y = 587; y < 587 + 48; y++)
                        {
                            outputImage[x, y] = background;
                        }
                    }

                    //Remove lock icon
                    var middleLockPresent = _middleLockPixels.Any(p => outputImage[p.X, p.Y].R > 128);
                    if (middleLockPresent)
                    {
                        for (int x = 216; x < 226 + 29; x++)
                        {
                            for (int y = outputImage.Height - 40; y < outputImage.Height; y++)
                            {
                                outputImage[x, y] = background;
                            }
                        }
                    }
                    else
                    {
                        //Lock & roll
                        for (int x = 80; x < 80 + 30; x++)
                        {
                            for (int y = outputImage.Height - 40; y < outputImage.Height; y++)
                            {
                                outputImage[x, y] = background;
                            }
                        }
                        var startX = -1;
                        for (int x = outputImage.Width; x < outputImage.Width; x++)
                        {
                            if (startX >= 0)
                                break;
                            for (int y = outputImage.Height - 40; y < outputImage.Height; y++)
                            {
                                if(outputImage[x,y].R > 128)
                                {
                                    startX = x;
                                    break;
                                }
                            }
                        }
                        if (startX < 0)
                            startX = 279;
                        for (int x = 379; x < 379 + 35; x++)
                        {
                            for (int y = outputImage.Height - 40; y < outputImage.Height; y++)
                            {
                                outputImage[x, y] = background;
                            }
                        }
                    }
                }

                outputImage.Mutate(i => i.Pad(outputImage.Width + 20, outputImage.Height + 20).BackgroundColor(background));

                var mem = new MemoryStream();
                outputImage.Save(mem, new PngEncoder());
                result = new Bitmap(mem);
                croppedAsMemory.Dispose();
                mem.Dispose();
            }

            var smallCropped = result.Clone(new System.Drawing.Rectangle(0, 0, result.Width, result.Height - 1), System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            result.Dispose();
            return smallCropped;
        }

        public void PrepareRivenFromFullscreenImage(string imagePath, string outputPath)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 500, 765, Rgba32.White))
            {
                using (Image<Rgba32> image = SixLabors.ImageSharp.Image.Load(imagePath))
                {
                    //Copy title/modis
                    for (int x = 1800; x < 2300; x++)
                    {
                        for (int y = 525; y < 1155; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 1800, y - 525] = Rgba32.Black;
                            else
                                outputImage[x - 1800, y - 525] = Rgba32.White;
                        }
                    }
                    //Copy Drain
                    for (int x = 2225; x < 2285; x++)
                    {
                        for (int y = 465; y < 510; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 2225, y - 510 + 630 + 45] = Rgba32.Black;
                            else
                                outputImage[x - 2225, y - 510 + 630 + 45] = Rgba32.White;
                        }
                    }
                    //Copy MR & rerolls
                    for (int x = 1820; x < 2270; x++)
                    {
                        for (int y = 1165; y < 1210; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 1820, y - 1165 + 630 + 45] = Rgba32.Black;
                            else
                                outputImage[x - 1820, y - 1165 + 630 + 45] = Rgba32.White;
                        }
                    }
                    ////Copy rerolls
                    //for (int x = 2212; x < 2270; x++)
                    //{
                    //    for (int y = 1166; y < 1211; y++)
                    //    {
                    //        if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                    //            outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = Rgba32.Black;
                    //        else
                    //            outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = Rgba32.White;
                    //    }
                    //}
                }
                outputImage.Save(outputPath);
            }
        }
    }
}
