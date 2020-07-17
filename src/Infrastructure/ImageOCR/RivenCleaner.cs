using Application.Interfaces;
using Application.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ImageOCR
{
    public class RivenCleaner : IRivenCleaner
    {
        private static Rgba32 _white = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        private static Rgba32 _black = new Rgba32(0, 0, 0);

        public void FastPrepareRiven(string imagePath, string outputPath)
        {

            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(imagePath))
            {
                image.Mutate(i => i.Crop(new SixLabors.ImageSharp.Rectangle(1804, 464, 488, 746)).Resize(232, 373));
                image.Save(outputPath);
            }
        }

        private bool IsPurple(Rgba32 p)
        {
            var color = System.Drawing.Color.FromArgb(p.R, p.G, p.B);
            var hsv = color.ToHsv();
            if (hsv.Hue >= 240 && hsv.Hue <= 280
                && hsv.Value >= 0.4)
                return true;
            return false;
        }
        public Bitmap CleanRiven(Bitmap croppedRiven)
        {
            Bitmap result = null;
            Rgba32 background = _white;
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 540, 780, background))
            {
                var croppedAsMemory = new MemoryStream();
                croppedRiven.Save(croppedAsMemory, System.Drawing.Imaging.ImageFormat.Bmp);
                croppedAsMemory.Seek(0, SeekOrigin.Begin);
                using (Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(croppedAsMemory))
                {
                    if (image.Width != 582)
                        image.Mutate(i => i.Resize(582, 831));

                    var refX = 21;
                    var refY = 63;
                    //Copy title/modis
                    var pastBackground = false;
                    var converter = new ColorSpaceConverter();
                    Rgba32 foreground = _black;
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
                            for (int x = 540 - 15; x < 540; x++)
                            {
                                Vs[x - (540 - 15) + 15] = converter.ToHsv(image[refX + x, refY + y]).V;
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
                                outputImage[outputImage.Width / 2 - 30 + x, y + 650] = foreground;
                        }
                    }

                    //MR and Rerolls
                    CopyMRAndRerolls(outputImage, image, out refX, out refY, foreground);


                    //Clean up bottom corners
                    for (int x = 0; x < 18; x++)
                    {
                        for (int y = 587; y < 587 + 48; y++)
                        {
                            outputImage[x, y] = background;
                        }
                    }
                    for (int x = outputImage.Width - 18; x < outputImage.Width; x++)
                    {
                        for (int y = 587; y < 587 + 48; y++)
                        {
                            outputImage[x, y] = background;
                        }
                    }

                    ////Remove centered lock icon
                    //for (int x = outputImage.Width / 2 - 12; x < outputImage.Width / 2 - 12 + 48; x++)
                    //{
                    //    for (int y = outputImage.Height - 50; y < outputImage.Height; y++)
                    //    {
                    //        outputImage[x, y] = background;
                    //    }
                    //}
                    ////Remove left lock icon
                    //for (int x = 122; x < 122 + 30; x++)
                    //{
                    //    for (int y = outputImage.Height - 50; y < outputImage.Height; y++)
                    //    {
                    //        outputImage[x, y] = background;
                    //    }
                    //}
                    ////Remove right roll icon
                    //var startX = -1;
                    //for (int x = (int)(outputImage.Width * 0.68); x < outputImage.Width; x++)
                    //{
                    //    if (startX >= 0)
                    //        break;
                    //    for (int y = outputImage.Height - 50; y < outputImage.Height; y++)
                    //    {
                    //        if (outputImage[x, y].R < 128)
                    //        {
                    //            startX = x;
                    //            break;
                    //        }
                    //    }
                    //}
                    //if (startX > 0)
                    //{
                    //    var endX = Math.Min(startX + 42, outputImage.Width);
                    //    for (int x = startX; x < endX; x++)
                    //    {
                    //        for (int y = outputImage.Height - 50; y < outputImage.Height; y++)
                    //        {
                    //            outputImage[x, y] = background;
                    //        }
                    //    }
                    //}
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

        private void CopyMRAndRerolls(Image<Rgba32> outputImage, Image<Rgba32> image, out int refX, out int refY, Rgba32 foreground)
        {
            refX = 65;
            refY = 704;
            //Identify all the characters
            var charRects = new List<System.Drawing.Rectangle>();
            var onChar = false;
            var charStartX = 450;
            var charEndX = 450;
            //This footer is between 65,704 and is 450 wide 42 tall
            for (int x = 65 + 450 - 1; x >= 65; x--)
            {
                var foundPixel = false;
                for (int y = 704; y < 704 + 42; y++)
                {
                    var p = image[x, y];
                    if (IsPurple(p))
                    {
                        if (!onChar)
                            charEndX = x;
                        onChar = true;
                        foundPixel = true;
                        break;
                    }
                }
                if (!foundPixel && onChar)
                {
                    charStartX = x + 1;
                    onChar = false;
                    charRects.Add(new System.Drawing.Rectangle(charStartX, 704, charEndX - charStartX + 1, 42));
                }
            }
            //Remove all characters who are invalid.
            //Copy MR & rerolls
            //Riven footer will be either
            //MR (lock) X         (reroll) X
            //          MR (lock) X

            //If we have any thing with an x beyond 388 then it's MR (lock) x     (reroll) x
            var rerolls = charRects.Where(r => r.Left > 388).OrderBy(r => r.Left).Skip(1).ToArray();
            //MR will always be the 3rd+ character on the left side
            var mr = charRects.Where(r => r.Left < 388).OrderBy(r => r.Left).Skip(3);
            foreach (var item in rerolls)
                CopyCharToOutput(outputImage, image, foreground, item);
            foreach (var item in mr)
                CopyCharToOutput(outputImage, image, foreground, item);
        }

        private void CopyCharToOutput(Image<Rgba32> outputImage, Image<Rgba32> image, Rgba32 foreground, System.Drawing.Rectangle charRect)
        {
            for (int x = charRect.Left; x <= charRect.Right; x++)
            {
                for (int y = charRect.Top; y < charRect.Bottom; y++)
                {
                    var p = image[x, y];
                    if (IsPurple(p))
                    {
                        //outputImage[outputImage.Width / 2 - (450 / 2) + (x - 450 - 65), (y - charRect.Top) + 650 + 55 + 20] = foreground;
                        outputImage[x - 85, (y - charRect.Top) + 650 + 55 + 20] = foreground;
                    }
                }
            }
        }

        public void PrepareRivenFromFullscreenImage(string imagePath, string outputPath)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 500, 765, _white))
            {
                using (Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(imagePath))
                {
                    //Copy title/modis
                    for (int x = 1800; x < 2300; x++)
                    {
                        for (int y = 525; y < 1155; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 1800, y - 525] = _black;
                            else
                                outputImage[x - 1800, y - 525] = _white;
                        }
                    }
                    //Copy Drain
                    for (int x = 2225; x < 2285; x++)
                    {
                        for (int y = 465; y < 510; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 2225, y - 510 + 630 + 45] = _black;
                            else
                                outputImage[x - 2225, y - 510 + 630 + 45] = _white;
                        }
                    }
                    //Copy MR & rerolls
                    for (int x = 1820; x < 2270; x++)
                    {
                        for (int y = 1165; y < 1210; y++)
                        {
                            if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                                outputImage[x - 1820, y - 1165 + 630 + 45] = _black;
                            else
                                outputImage[x - 1820, y - 1165 + 630 + 45] = _white;
                        }
                    }
                    ////Copy rerolls
                    //for (int x = 2212; x < 2270; x++)
                    //{
                    //    for (int y = 1166; y < 1211; y++)
                    //    {
                    //        if (image[x, y].R == 172 && image[x, y].G == 131 && image[x, y].B == 213)
                    //            outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = _black;
                    //        else
                    //            outputImage[x - 2212, y - 1166 + 630 + 45 + 45] = _white;
                    //    }
                    //}
                }
                outputImage.Save(outputPath);
            }
        }
    }
}
