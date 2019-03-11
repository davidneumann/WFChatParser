using Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WFImageParser
{
    public class ChatImageCleaner : IChatImageProcessor
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
            var chatRect = new Rectangle(5, 750, 3249, 1350);
            //Image.Load(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screenshot (7).png")
            //@"C:\Users\david\OneDrive\Documents\WFChatParser\friday_last_moved.png"
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                if (rgbImage.Height == 1440)
                    chatRect = new Rectangle(4, 530, 2022, 850);

                //chatRect.Width = (int)(rgbImage.Width * 0.799f);

                rgbImage.Mutate(x => x.Crop(chatRect));

                var maxHsv = 0.29;
                
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > maxHsv)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                        ////On middle of line check for click points
                        ///THIS HAS BEEN KILLED AS IT IS BROKEN BY MULTIPLE ITEMS IN A ROW
                        //if ((y - 25) % 50 == 0)
                        //{
                        //    if (hsvPixel.H >= 185 && hsvPixel.S >= 0.5 && !prevCharBlue)
                        //    {
                        //        result.ClickPoints.Add(new Point(x, y));
                        //        prevCharBlue = true;
                        //        rgbImage[x, y] = Rgba32.Red;
                        //    }
                        //    else if (hsvPixel.H < 185 && hsvPixel.S < 0.5 && prevCharBlue)
                        //        prevCharBlue = false;
                        //}
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                var file = new FileInfo(imagePath);
                var outputPath = Path.Combine(outputDirectory, file.Name);
                rgbImage.Save(outputPath);
                return outputPath;
            }

            //Image<Rgba32> image = Image.Load("test.jpg");
            //image.Crop()
            //using (var cropped = image.Clone()
            //{

            //}
            //Parallel.For(0, image.Width * image.Height, i =>
            //{
            //    var pixel = image[i % image.Width, i / image.Height];

            //});
        }
    }
}
