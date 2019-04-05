using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WFImageParser
{
    public class ImageCleaner
    {
        /// <summary>
        /// Converts the full color game window into a image of the chat window in grayscale.
        /// </summary>
        /// <param name="imagePath">The path to the game screenshot</param>
        /// <param name="outputDirectory">The directory to save the processed image</param>
        /// <returns>The full path to the processed image</returns>
        public string ProcessChatImage(string imagePath, string outputDirectory)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 893, 3236, 1350);
            //Image.Load(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screenshot (7).png")
            //@"C:\Users\david\OneDrive\Documents\WFChatParser\friday_last_moved.png"
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                if (rgbImage.Height == 1440)
                    chatRect = new Rectangle(4, 530, 2022, 850);
                else if (rgbImage.Height == 1080)
                    chatRect = new Rectangle(4, 370, 1507, 650);

                rgbImage.Mutate(x => x.Crop(chatRect));

                var minV = 0.29;

                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                var file = new FileInfo(imagePath);
                var outputPath = Path.Combine(outputDirectory, file.Name);
                rgbImage.Save(outputPath);
                return outputPath;
            }
        }

        public void SaveSoftMask(string input, string outputPath)
        {
            using (var rgbImage = Image.Load(input))
            {
                using (var output = new Image<Rgba32>(rgbImage.Width, rgbImage.Height))
                {
                    var cache = new ImageCache(rgbImage);
                    for (int x = 0; x < rgbImage.Width; x++)
                    {
                        for (int y = 0; y < rgbImage.Height; y++)
                        {
                            output[x, y] = new Rgba32(cache[x, y], cache[x, y], cache[x, y]);
                        }
                    }
                    output.Save(outputPath);
                }
            }
        }

        public void MakeGreyscaleImageFromArray(string outputDir, char character, byte[,] pixels)
        {
            var name = character.ToString();
            if (name.ToUpper() != name.ToLower() && name.ToUpper() == name)
                name = name + "_upper";
            else if (name.ToUpper() != name.ToLower() && name.ToLower() == name)
                name = name + "_lower";
            if (name == ":")
                name = "colon";
            else if (name == "*")
                name = "asterix";
            else if (name == ">")
                name = "gt";
            else if (name == "<")
                name = "lt";
            else if (name == "\\")
                name = "backSlash";
            else if (name == "?")
                name = "question";
            else if (name == "/")
                name = "forwardSlash";
            else if (name == "|")
                name = "pipe";
            else if (name == "," || name[0] == ',')
                name = "comma";
            using (var image = new Image<Rgba32>(pixels.GetLength(0), pixels.GetLength(1)))
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        image[x, y] = new Rgba32(pixels[x, y], pixels[x, y], pixels[x, y]);
                    }
                }

                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
                image.Save(Path.Combine(outputDir, name + ".png"));
            }
        }

        public void SaveGreyscaleImage(string imagePath, string outputPath, float minV = 0.44f)
        {
            var converter = new ColorSpaceConverter();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                rgbImage.Save(outputPath);
            }
        }

        internal bool IsChatColor(Hsv hsvPixel)
        {
            if ((hsvPixel.H >= 175 && hsvPixel.H <= 190)
                && hsvPixel.S > 0.1) //green
                return true;
            if (hsvPixel.S < 0.3) //white
                return true;
            if (hsvPixel.H >= 190 && hsvPixel.H <= 210 && hsvPixel.V >= 0.25) // blue
                return true;
            if ((hsvPixel.H <= 1 || hsvPixel.H >= 359) && hsvPixel.S >= 0.7f && hsvPixel.S <= 0.8f)
                return true;

            return false;
        }

        public void SaveChatColors(string imagePath, string outputPath)
        {
            var converter = new ColorSpaceConverter();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    var v = (hsvPixel.V - 0.21f) / (1f - 0.21f);
                    //if (x == 369 && y == 1134)
                    //    System.Diagnostics.Debugger.Break();
                    if (IsChatColor(hsvPixel))
                    {
                        rgbImage[x, y] = new Rgba32(v, v, v);
                    }
                    else
                        rgbImage[x, y] = Rgba32.Black;
                }

                rgbImage.Save(outputPath);
            }
        }

        public void SaveGreyscaleImage(string imagePath, string outputPath, float minV, float maxV)
        {
            var converter = new ColorSpaceConverter();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV && hsvPixel.V < maxV)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                rgbImage.Save(outputPath);
            }
        }

        public void SaveClickMarkers(string inputImage, string outputPath, CoordinateList clickPoints)
        {
            using (Image<Rgba32> image = Image.Load(inputImage))
            {
                foreach (var point in clickPoints)
                {
                    for (int x = -4; x <= 4; x++)
                    {
                        for (int y = -4; y <= 4; y++)
                        {
                            image[point.X + x, point.Y + y] = Rgba32.Red;
                        }
                    }
                }

                image.Save(outputPath);
            }
        }

    }
}