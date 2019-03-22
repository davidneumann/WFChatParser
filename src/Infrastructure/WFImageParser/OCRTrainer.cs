using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Drawing;
using System.Text;

namespace WFImageParser
{
    public class OCRTrainer
    {
        public class TrainingSampleCharacter
        {
            internal float[,] Mask;
            //public List<Point> Pixels;
            public int Width;
            public char Character;
        }
        public List<List<TrainingSampleCharacter>> TrainOnImage(string imagePath, List<char[]> referenceLines, int xOffset = 4, float minV = 0.44f)
        {
            var results = new List<List<TrainingSampleCharacter>>();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var cache = new VCache(rgbImage);
                var chatRect = new Rectangle(4, 763, 3236, 1350);
                var offsets = OCRHelpers.LineOffsets;
                var lineHeight = 36;
                var refLineIndex = 0;
                for (int i = 0; i < offsets.Length; i++)
                {
                    var line = TrainOnLine(minV, referenceLines[refLineIndex], xOffset, chatRect, cache, lineHeight, offsets[i]);
                    if (line.Count > 0 && line.Count == referenceLines[i].Length)
                    {
                        results.Add(line);
                        refLineIndex++;
                    }
                    else if (line.Count == referenceLines[i].Length)
                    {
                        throw new Exception("Reference lines do not match up with found characters");
                    }
                }
            }

            return results;
        }

        private List<TrainingSampleCharacter> TrainOnLine(float minV, char[] referenceCharacters, int xOffset, Rectangle chatRect, VCache image, int lineHeight, int lineOffset)
        {
            var startX = xOffset;
            var endX = xOffset;
            List<Point> targetCharacterPixels = null;
            var refIndex = 0;
            var results = new List<TrainingSampleCharacter>();
            for (int x = xOffset; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                var firstPixel = Point.Empty;
                for (int i = endX; i < chatRect.Right; i++)
                {
                    var pixelFound = false;
                    for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                    {
                        if (image[i, y] > minV)
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
                targetCharacterPixels = OCRHelpers.FindCharacterPixelPoints(firstPixel, image, null, minV, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                var target = OCRHelpers.FindCharacterMask(firstPixel, image, null, minV, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                var mask = new float[target.Width, lineHeight];
                for (int x2 = 0; x2 < target.Width; x2++)
                {
                    for (int y2 = 0; y2 < lineHeight; y2++)
                    {
                        mask[x2, y2] = Math.Max(0, (image[target.MinX + x2, y2+lineOffset] - minV) / (1 - minV));
                    }
                }

                startX = Math.Min(startX, targetCharacterPixels.Min(p => p.X));
                endX = Math.Max(endX, targetCharacterPixels.Max(p => p.X + 1));

                results.Add(new TrainingSampleCharacter() { Mask = mask, Width = endX - startX, Character = referenceCharacters[refIndex++] });
                x = endX;
            }
            if (referenceCharacters.Length > refIndex)
            {
                throw new Exception("Length missmatch on training line");
            }

            return results;
        }

        public void TrainOnImages(string trainingDir, string outputDir, int xOffset = 253)
        {
            var trainingImagePaths = Directory.GetFiles(trainingDir).Where(f => f.EndsWith(".png")).ToArray();
            var trainingTextPaths = Directory.GetFiles(trainingDir).Where(f => f.EndsWith(".txt")).ToArray();

            var characters = new List<TrainingSampleCharacter>();

            Console.WriteLine("Looking at training images");
            for (int i = 0; i < trainingImagePaths.Length; i++)
            {
                var correctText = File.ReadAllLines(trainingTextPaths[i]).Select(line => line.Replace(" ", "").ToArray()).ToList();
                var results = TrainOnImage(trainingImagePaths[i], correctText, xOffset, minV:0.3f);
                results.SelectMany(list => list).ToList().ForEach(t => characters.Add(t));
            }
            var groupedChars = characters.GroupBy(t => t.Character);
            //Get max widths
            Console.WriteLine("Finding max widths");
            var maxWidths = new Dictionary<char, int>();
            groupedChars.ToList().ForEach(g => maxWidths[g.Key] = g.Max(t => t.Width));
            //Turn all pixel lists into arrays of max width
            Console.WriteLine("Converting lists to padded arrays");
            var groupedCharArrays = groupedChars.Select(g =>
            {
                Console.Write("\rLooking at: " + g.Key);
                return g.Select(t =>
                {
                    var arr = new float[maxWidths[t.Character], 36];
                    for (int x = 0; x < maxWidths[t.Character] && x < t.Width; x++)
                    {
                        for (int y = 0; y < 36; y++)
                        {
                            arr[x, y] = t.Mask[x, y];
                        }
                    }
                    return new { t.Character, Mask = arr };
                });
            }).Select(i => i).SelectMany(i => i).GroupBy(t => t.Character).ToList();
            //Get pixel counts
            Console.WriteLine("\nDeterming pixel counts");
            var pixelCounts = groupedCharArrays.Select(g =>
            {
                Console.Write("\rLooking at: " + g.Key);
                var arr = new float[maxWidths[g.Key], 36];
                g.ToList().ForEach(t =>
                {
                    for (int x = 0; x < maxWidths[g.Key]; x++)
                    {
                        for (int y = 0; y < 36; y++)
                        {
                            arr[x, y] += t.Mask[x, y];
                        }
                    }
                });
                return new { Character = g.Key, PixelCounts = arr, SampleCount = g.Count() };
            }).ToList();
            Console.WriteLine("\nConverting pixels into visualization");
            var grayscalePixels = pixelCounts.Select(i =>
            {
                var arr = new byte[i.PixelCounts.GetLength(0), i.PixelCounts.GetLength(1)];
                for (int x = 0; x < arr.GetLength(0); x++)
                {
                    for (int y = 0; y < arr.GetLength(1); y++)
                    {
                        arr[x, y] = (byte)(((float)i.PixelCounts[x, y] / (float)i.SampleCount) * byte.MaxValue);
                    }
                }
                return new { i.Character, Pixels = arr };
            }).ToList();
            var c = new ImageCleaner();
            grayscalePixels.ForEach(i => c.MakeGreyscaleImageFromArray(outputDir, i.Character, i.Pixels));
        }
    }
}
