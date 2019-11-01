using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
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

            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var startX = chatRect.Left;
            var endX = chatRect.Left;
            var lineHeight = 36;

            TargetMask targetLeft = null;
            TargetMask targetRight = null;
            for (int x = chatRect.Left; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                var firstPixel = Point.Empty;
                for (int i = endX; i < chatRect.Right; i++)
                {
                    var pixelFound = false;
                    for (int y = lineVertOffset; y < lineVertOffset + lineHeight; y++)
                    {
                        if (cache[i, y] > 0.3f)
                        {
                            x = i;
                            pixelFound = true;
                            firstPixel = new Point(i, y);
                            break;
                        }
                    }

                    if (pixelFound)
                    {
                        break;
                    }
                }

                //Make sure we didn't escape
                if (x >= chatRect.Right || firstPixel == Point.Empty)
                    break;

                startX = chatRect.Right;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, null, chatRect.Left, chatRect.Right, lineVertOffset, lineVertOffset + lineHeight);

                startX = Math.Min(startX, targetMask.MinX);
                endX = Math.Max(endX, targetMask.MaxX + 1);

                if (targetLeft == null)
                    targetLeft = targetMask;
                else
                {
                    targetRight = targetMask;

                    //TODO: do something with these 2 masks to make a gap pair

                    targetRight = null;
                    targetLeft = null;
                }

                //results.Add(new TrainingSampleCharacter() { Mask = mask, Width = endX - startX, Character = referenceCharacters[refIndex++] });
                //TODO: maybe store these results??
                x = endX;
            }
        }
    }
}
