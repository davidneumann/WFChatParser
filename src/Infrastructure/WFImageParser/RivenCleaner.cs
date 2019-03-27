﻿using Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    public class RivenCleaner : IRivenCleaner
    {
        public void FastPrepareRiven(string imagePath, string outputPath)
        {

            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> image = Image.Load(imagePath))
            {
                image.Mutate(i => i.Crop(new Rectangle(1804, 464, 488, 746)).Resize(232, 373));
                image.Save(outputPath);
            }
        }

        public void CleanRiven(string imagePath)
        {
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 540, 720, Rgba32.White))
            {
                using (Image<Rgba32> image = Image.Load(imagePath))
                {
                    var refX = 21;
                    var refY = 63;
                    //Copy title/modis
                    for (int x = 0; x < 540; x++)
                    {
                        for (int y = 0; y < 630; y++)
                        {
                            var p = image[refX + x, refY + y];
                            if (p.R == 172 && p.G == 131 && p.B == 213)
                                outputImage[x, y] = Rgba32.Black;
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
                            if (p.R == 172 && p.G == 131 && p.B == 213)
                                outputImage[x, y+630] = Rgba32.Black;
                        }
                    }
                    refX = 65;
                    refY = 704;
                    //Copy MR & rerolls
                    for (int x = 0; x < 450; x++)
                    {
                        for (int y = 0; y < 45; y++)
                        {
                            var p = image[refX + x, refY + y];
                            if (p.R == 172 && p.G == 131 && p.B == 213)
                                outputImage[x, y+630+45] = Rgba32.Black;
                        }
                    }
                }
                outputImage.Mutate(i => i.Pad(outputImage.Width + 20, outputImage.Height + 20).BackgroundColor(Rgba32.White));
                outputImage.Save(imagePath);
            }
        }

        public void PrepareRivenFromFullscreenImage(string imagePath, string outputPath)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> outputImage = new Image<Rgba32>(null, 500, 765, Rgba32.White))
            {
                using (Image<Rgba32> image = Image.Load(imagePath))
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