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
        public ProcessedImageResult CleanImage(string imagePath, string outputDirectory)
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

                var result = new ProcessedImageResult();
                bool prevCharBlue = false;
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
                result.OutputPath = Path.Combine(outputDirectory, file.Name);
                rgbImage.Save(result.OutputPath);
                return result;
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

        public class ProcessedImageResult
        {
            public string OutputPath { get; set; }
            public List<Point> ClickPoints { get; set; } = new List<Point>();
        }
    }
}
