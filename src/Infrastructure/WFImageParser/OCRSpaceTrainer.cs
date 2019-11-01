using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WFImageParser
{
    public class OCRSpaceTrainer
    {
        public void TrainOnImages(string trainingDataPath, string outputDir)
        {
            //Train on a single image
            var trainingImagePaths = Directory.GetFiles(trainingDataPath).Where(f => f.EndsWith(".png")).ToArray();
            var trainingTextPaths = Directory.GetFiles(trainingDataPath).Where(f => f.EndsWith(".txt")).ToArray();

            if (trainingImagePaths.Length != trainingTextPaths.Length)
                throw new Exception("Unmatched training images and text files");

            for (int i = 0; i < trainingImagePaths.Length; i++)
            {
                TrainOnImage(trainingImagePaths[i], trainingTextPaths[i]);
            }
        }

        private void TrainOnImage(string imagePath, string textPath)
        {
            var expectedLines = File.ReadAllLines(textPath).Select(line => line.Replace(" ", "")).ToArray();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var cache = new ImageCache(rgbImage);
                var offsets = OCRHelpers.LineOffsets;
                for (int i = 0; i < offsets.Length && i < expectedLines.Length; i++)
                {
                    TrainOnLine(cache, i, expectedLines[i], offsets[i]);
                }
            }
        }

        private void TrainOnLine(ImageCache cache, int lineIndex, string expectedCharacters, int lineVertOffset)
        {
            //TODO: Get characters from line
        }
    }
}
