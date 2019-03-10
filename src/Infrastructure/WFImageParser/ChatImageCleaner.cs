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
    public class ChatImageCleaner
    {
        //This should return the messages
        //This hsould also take the image for now I'm hard coding it
        public string[] CleanImage(string imagePath, string outputDirectory)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(5, 750, 3249, 1350);
            //Image.Load(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screenshot (7).png")
            //@"C:\Users\david\OneDrive\Documents\WFChatParser\friday_last_moved.png"
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                rgbImage.Mutate(x => x.Crop(chatRect));//.Resize(1526, 654).Pad(1546, 674));

                var maxHsv = 0.29;
                
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var pixel = rgbImage[i % rgbImage.Width, i / rgbImage.Width];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > maxHsv)
                        rgbImage[i % rgbImage.Width, i / rgbImage.Width] = Rgba32.Black;
                    else
                        rgbImage[i % rgbImage.Width, i / rgbImage.Width] = Rgba32.White;
                }

                var file = new FileInfo(imagePath);
                var output = Path.Combine(outputDirectory, file.Name);
                rgbImage.Save(output);

                var images = new List<string>();
                for (int i = 0; i < 27; i++)
                {
                    using (Image<Rgba32> clone = rgbImage.Clone())
                    {
                        clone.Mutate(x => x.Crop(new Rectangle(0, 50 * i, chatRect.Width, 50)).Pad(chatRect.Width + 100, 150).BackgroundColor(Rgba32.White));
                        clone.Save(output + i + ".png");
                        images.Add(output + i + ".png");
                    }
                }
                return images.ToArray();
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
