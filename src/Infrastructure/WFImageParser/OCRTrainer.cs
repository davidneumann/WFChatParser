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
        public List<List<TrainingSampleCharacter>> TrainOnImage(string imagePath, List<char[]> referenceLines, int xOffset = 4)
        {
            var cleaner = new ImageCleaner();
            //cleaner.SaveChatColors(imagePath, "debug.png");

            var results = new List<List<TrainingSampleCharacter>>();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var cache = new ImageCache(rgbImage);
                var chatRect = new Rectangle(4, 763, 3236, 1350);
                var offsets = OCRHelpers.LineOffsets;
                var lineHeight = 34;
                var refLineIndex = 0;
                for (int i = 0; i < offsets.Length && refLineIndex < referenceLines.Count; i++)
                {
                    var line = TrainOnLine(referenceLines[refLineIndex], xOffset, chatRect, cache, lineHeight, offsets[i]);
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

        private List<TrainingSampleCharacter> TrainOnLine(char[] referenceCharacters, int xOffset, Rectangle chatRect, ImageCache image, int lineHeight, int lineOffset)
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
                        if (image[i, y] > 0f)
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
                //targetCharacterPixels = OCRHelpers.FindCharacterPixelPoints(firstPixel, image, null, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                //var minX = targetCharacterPixels.Min(p => p.X);
                //var minY = targetCharacterPixels.Min(p => p.Y);
                //using (var image2 = new Image<Rgba32>(targetCharacterPixels.Max(p => p.X) - targetCharacterPixels.Min(p => p.X) + 1, targetCharacterPixels.Max(p => p.Y) - targetCharacterPixels.Min(p => p.Y) + 1))
                //{
                //    for (int x2 = 0; x2 < image2.Width; x2++)
                //    {
                //        for (int y2 = 0; y2 < image2.Height; y2++)
                //        {
                //            var p = targetCharacterPixels.FirstOrDefault(p2 => p2.X == x2 + minX  && p2.Y == y2 + minY);
                //            if (p != null)
                //                image2[x2, y2] = new Rgba32(image[p.X, p.Y], image[p.X, p.Y], image[p.X, p.Y]);
                //            else
                //                image2[x2, y2] = Rgba32.Black;
                //        }
                //    }
                //    image2.Save("debug_target.png");
                //}
                var target = OCRHelpers.FindCharacterMask(firstPixel, image, null, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                var mask = new float[target.Width, lineHeight];
                for (int x2 = 0; x2 < target.Width; x2++)
                {
                    for (int y2 = 0; y2 < lineHeight; y2++)
                    {
                        mask[x2, y2] = Math.Max(0, image[target.MinX + x2, y2+lineOffset]);
                    }
                }

                startX = Math.Min(startX, target.MinX);
                endX = Math.Max(endX, target.MaxX + 1);

                results.Add(new TrainingSampleCharacter() { Mask = mask, Width = endX - startX, Character = referenceCharacters[refIndex++] });
                x = endX;
            }
            if (referenceCharacters.Length > refIndex)
            {
                throw new Exception("Length missmatch on training line");
            }

            return results;
        }

        public void TrainOnImages(string trainingDir, string outputDir, int xOffset = 10)
        {
            var trainingImagePaths = Directory.GetFiles(trainingDir).Where(f => f.EndsWith(".png")).ToArray();
            var trainingTextPaths = Directory.GetFiles(trainingDir).Where(f => f.EndsWith(".txt")).ToArray();

            var characters = new List<TrainingSampleCharacter>();

            Console.WriteLine("Looking at training images");
            var lastLineLength = 0;
            for (int i = 0; i < trainingImagePaths.Length; i++)
            {
                Console.Write("\r".PadRight(lastLineLength, ' '));
                Console.Write("\r" + trainingImagePaths[i]);
                lastLineLength = trainingImagePaths[i].Length + 1;
                var correctText = File.ReadAllLines(trainingTextPaths[i]).Select(line => line.Replace(" ", "").ToArray()).ToList();
                var results = TrainOnImage(trainingImagePaths[i], correctText, xOffset);
                results.SelectMany(list => list).ToList().ForEach(t => characters.Add(t));
            }
            var groupedChars = characters.GroupBy(t => t.Character);
            //Get max widths
            Console.WriteLine("\nFinding max widths");
            var maxWidths = new Dictionary<char, int>();
            groupedChars.ToList().ForEach(g => maxWidths[g.Key] = g.Max(t => t.Width));
            //Turn all pixel lists into arrays of max width
            Console.WriteLine("Converting lists to padded arrays");
            var groupedCharArrays = groupedChars.Select(g =>
            {
                Console.Write("\rLooking at: " + g.Key);
                return g.Select(t =>
                {
                    var arr = new float[maxWidths[t.Character], 34];
                    for (int x = 0; x < maxWidths[t.Character] && x < t.Width; x++)
                    {
                        for (int y = 0; y < 34; y++)
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
                var arr = new float[maxWidths[g.Key], 34];
                g.ToList().ForEach(t =>
                {
                    for (int x = 0; x < maxWidths[g.Key]; x++)
                    {
                        for (int y = 0; y < 34; y++)
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
