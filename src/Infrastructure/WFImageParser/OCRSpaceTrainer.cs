using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static WFImageParser.ChatParser;

namespace WFImageParser
{
    public class OCRSpaceTrainer
    {
        public void TrainOnImages(string trainingDataPath, string outputPath, char[] expectedDistinctChars)
        {
            Console.WriteLine("Training on images in {0} and saving gaps.json to {1}", trainingDataPath, outputPath);
            //Train on a single image
            var trainingImagePaths = Directory.GetFiles(trainingDataPath).Where(f => f.EndsWith(".png")).ToArray();
            var trainingTextPaths = Directory.GetFiles(trainingDataPath).Where(f => f.EndsWith(".txt")).ToArray();

            expectedDistinctChars = expectedDistinctChars.Distinct().ToArray();

            var expectedResults = expectedDistinctChars.Distinct().Count();
            expectedResults = expectedResults * expectedResults;

            if (trainingImagePaths.Length != trainingTextPaths.Length)
                throw new Exception("Unmatched training images and text files");

            var results = new List<SimpleGapPair>();
            for (int i = 0; i < trainingImagePaths.Length; i++)
            {
                TrainOnImage(trainingImagePaths[i], trainingTextPaths[i], results);
            }

            if (results.Count == 0)
                throw new Exception("No pairs found!");
            if (results.Count != expectedResults)
            {
                var missing = new List<SimpleGapPair>();
                for (int i = 0; i < expectedDistinctChars.Length; i++)
                {
                    var left = expectedDistinctChars[i];
                    string leftName = ImageCleaner.ConvertCharacterToName(left);
                    for (int j = 0; j < expectedDistinctChars.Length; j++)
                    {
                        var right = expectedDistinctChars[j];
                        string rightName = ImageCleaner.ConvertCharacterToName(right);
                        if (!results.Exists(pair => pair.Left == leftName && pair.Right == rightName))
                        {
                            missing.Add(new SimpleGapPair() { Left = leftName, Right = rightName });
                        }
                    }
                }
                throw new Exception(string.Format("Not enough gaps found! Missing {0}", missing.Select(pair => string.Format("{0} {1}", pair.Left, pair.Right))));
            }

            var outputDir = new DirectoryInfo(outputPath);
            File.WriteAllText(Path.Combine(outputDir.FullName, "gaps.json"), JsonConvert.SerializeObject(results.ToArray()));
        }

        private void TrainOnImage(string imagePath, string textPath, List<SimpleGapPair> results)
        {
            Console.WriteLine("Training on image {0}. Total results: {1}", imagePath, results.Count);
            var expectedLines = File.ReadAllLines(textPath).Select(line => line.Replace(" ", "")).ToArray();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var cache = new ImageCache(rgbImage);
                var offsets = OCRHelpers.LineOffsets;
                for (int i = 0; i < offsets.Length && i < expectedLines.Length; i++)
                {
                    TrainOnLine(cache, i, expectedLines[i], offsets[i], results);
                }
            }

            Console.Write("\r\n");
        }

        private void TrainOnLine(ImageCache cache, int lineIndex, string expectedCharacters, int lineVertOffset, List<SimpleGapPair> results)
        {
            Console.Write("\rTraining on line {0,2}. Results {1}", lineIndex, results.Count);
            var addedPairs = 0;

            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var startX = chatRect.Left;
            var endX = chatRect.Left;
            var lineHeight = 36;

            TargetMask targetLeft = null;
            var charIndex = 0;
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

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, null, chatRect.Left, chatRect.Right, lineVertOffset, lineVertOffset + lineHeight);

                endX = Math.Max(endX, targetMask.MaxX + 1);

                if (targetLeft == null)
                    targetLeft = targetMask;
                else
                {
                    targetRight = targetMask;

                    var gapPair = new SimpleGapPair();
                    gapPair.Left = ImageCleaner.ConvertCharacterToName(expectedCharacters[charIndex]);
                    gapPair.Right = ImageCleaner.ConvertCharacterToName(expectedCharacters[charIndex+1]);
                    gapPair.Gap = targetRight.MinX - targetLeft.MaxX;
                    charIndex += 2;

                    if (!results.Exists(existingPair => existingPair.Left == gapPair.Left && existingPair.Right == gapPair.Right))
                    {
                        addedPairs++;
                        results.Add(gapPair);
                    }
                    else
                        throw new Exception("Redudant pair detected");

                    targetRight = null;
                    targetLeft = null;
                }

                x = endX;
            }

            if (addedPairs != expectedCharacters.Length / 2)
                throw new Exception("Could not find enough gap pairs for line");
        }
    }
}
