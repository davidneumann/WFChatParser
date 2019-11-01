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

            var allPossiblePairs = new List<SimpleGapPair>();
            for (int i = 0; i < expectedDistinctChars.Length; i++)
            {
                var prefix = ImageCleaner.ConvertCharacterToName(expectedDistinctChars[i]);
                for (int j = 0; j < expectedDistinctChars.Length; j++)
                {
                    var suffix = ImageCleaner.ConvertCharacterToName(expectedDistinctChars[j]);
                    allPossiblePairs.Add(new SimpleGapPair() { Left = prefix, Right = suffix });
                }
            }
            //Add support for ] [
            allPossiblePairs.Add(new SimpleGapPair() { Left = ImageCleaner.ConvertCharacterToName(']'), Right = ImageCleaner.ConvertCharacterToName(']') });
            //Add support for (character) [
            for (int i = 0; i < expectedDistinctChars.Length; i++)
            {
                if (expectedDistinctChars[i] == ']')
                    continue;
                allPossiblePairs.Add(new SimpleGapPair() { Left = ImageCleaner.ConvertCharacterToName(expectedDistinctChars[i]), Right = ImageCleaner.ConvertCharacterToName('[') });
            }
            //Add support for [ (char)
            for (int i = 0; i < expectedDistinctChars.Length; i++)
            {
                if (expectedDistinctChars[i] == ']')
                    continue;
                allPossiblePairs.Add(new SimpleGapPair() { Left = ImageCleaner.ConvertCharacterToName('['), Right = ImageCleaner.ConvertCharacterToName(expectedDistinctChars[i]) });
            }

            var expectedResults = allPossiblePairs.Count;

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
                var errorSb = new StringBuilder();
                foreach (var result in results)
                {
                    if (!allPossiblePairs.Exists(pair => pair.Left == result.Left && pair.Right == result.Right))
                    {
                        errorSb.AppendLine(string.Format("Unexpected pair found: {0} {1}", result.Left, result.Right));
                        missing.Add(result);
                    }
                }
                foreach (var expectedPair in allPossiblePairs)
                {
                    if (!results.Exists(pair => pair.Left == expectedPair.Left && pair.Right == expectedPair.Right))
                    {
                        errorSb.AppendLine(string.Format("Expected pair not found: {0} {1}", expectedPair.Left, expectedPair.Right));
                        missing.Add(expectedPair);
                    }
                }

                throw new Exception("Invalid pairs detected\r\n " + errorSb.ToString());
            }

            var outputDir = new DirectoryInfo(outputPath);
            File.WriteAllText(Path.Combine(outputDir.FullName, "gaps.json"), JsonConvert.SerializeObject(results.ToArray()));
        }

        private void TrainOnImage(string imagePath, string textPath, List<SimpleGapPair> results)
        {
            Console.WriteLine("Training on image {0}. Total results: {1}", imagePath, results.Count);
            var ic = new ImageCleaner();
            ic.SaveChatColors(imagePath, "debug.png");
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
            var lineHeight = 34;

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
                    gapPair.Gap = targetRight.MinX - (targetLeft.MaxX+1);
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
