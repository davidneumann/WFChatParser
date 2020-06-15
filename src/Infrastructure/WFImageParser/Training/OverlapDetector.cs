using Application.ChatLineExtractor;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using WFImageParser.GlyphRecognition;

namespace WFImageParser.Training
{
    public static class OverlapDetector
    {
        public static void ExtractPixelGroupsOnImages(string badLinesCSV, string inputDir, string outputDir)
        {
            //Headers: filename, line index, y
            var inputs = File.ReadAllLines(badLinesCSV);
            var files = inputs.Select(f => f.Split(',').First()).Distinct();
            var count = 0;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            foreach (var file in files)
            {
                var path = Path.Combine(inputDir, file + ".png");
                var bitmap = new System.Drawing.Bitmap(path);
                var cache = new ImageCache(bitmap);
                var rects = new List<Rectangle>();

                var ys = inputs.Where(line => line.StartsWith(file + ",")).Select(line => line.Split(',').Last()).Distinct();
                foreach (var y in ys)
                {
                    var realY = int.Parse(y);
                    rects.AddRange(GetRects(cache, realY));
                }

                foreach (var rect in rects)
                {
                    Console.WriteLine("Saving rect " + rect.ToString());
                    SaveBlockOfCache(Path.Combine(outputDir, count++ + ".png"), rect, cache);
                }
            }
        }

        private static void SaveBlockOfCache(string outputPath, Rectangle rect, ImageCache cache)
        {
            var output = new Bitmap(rect.Width, rect.Height);
            for (int x = rect.Left; x < rect.Right; x++)
            {
                for (int y = rect.Top; y < rect.Bottom; y++)
                {
                    var debug = cache[x, y];
                    var v = (int)(cache[x, y] * 255);
                    var c = Color.FromArgb(v, v, v);
                    output.SetPixel(x - rect.Left, y - rect.Top, c);
                }
            }
            output.Save(outputPath);
        }

        private static List<Rectangle> GetRects(ImageCache cache, int y)
        {
            var rects = new List<Rectangle>();

            var chatRect = new Rectangle(4, 763, 3236, 1350);

            var prevMatchedCharacters = new CoordinateList();
            for (int x = chatRect.Left; x < chatRect.Right; x++)
            {            
                //Advance until next pixel
                Point firstPixel = ChatParser.GetFirstPixel(cache, chatRect.Right, OCRHelpers.LINEHEIGHT, y, x, prevMatchedCharacters);

                //Make sure we didn't escape
                if (firstPixel == System.Drawing.Point.Empty)
                    return rects;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, prevMatchedCharacters, chatRect.Left, chatRect.Right,
                    y, y + OCRHelpers.LINEHEIGHT);

                rects.Add(new Rectangle(targetMask.MinX, y, targetMask.Width, 34));
                x = targetMask.MaxX + 1;
            }

            return rects;

        }

        public static void DetectOverlaps(string sourceDir, string dataDirectory)
        {
            if (Directory.Exists(Path.Combine("overlaps", "hits")))
            {
                Directory.Delete(Path.Combine("overlaps", "hits"), true);
                System.Threading.Thread.Sleep(1000);
            }
            Directory.CreateDirectory(Path.Combine("overlaps", "hits"));

            var inputs = GetValidInputs(sourceDir);
            foreach (var input in inputs)
            {
                ImageCleaner.SaveSoftMask(input + ".png", "overlap.png");
                using (var b = new Bitmap(input + ".png"))
                {
                    var expectedLines = File.ReadAllLines(input + ".txt");
                    var cache = new ImageCache(b);

                        Console.WriteLine($"Looking at {input}");
                    for (int i = 0; i < expectedLines.Length; i++)
                    {
                        //var charCount = GetCharacterCount(i, cache);
                        //int expectedCharCount = expectedLines[i].Replace(" ", "").Length;
                        //if (expectedCharCount != charCount)
                        //{
                        //    Console.WriteLine($"Overlap detected in {input}, line index {i} line y {OCRHelpers.LineOffsets[i]}. Expected {expectedCharCount} but found {charCount}");
                        //    //throw new Exception("Overlap detected on " + input + " chat line " + (i + 1) + " y of " + OCRHelpers.LineOffsets[i]);
                        //}

                        SaveTouchingHits(cache, i, expectedLines[i].Replace(" ", "").ToCharArray(), dataDirectory);
                    }
                }
            }
        }

        private static List<(char left, char right)> SaveTouchingHits(ImageCache cache, int offsetIndex, char[] expectedCharacters, string dataDirectory)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var prevMatchedCharacters = new CoordinateList();
            var characterIndex = 0;
            var touchingChars = new List<(char left, char right)>();
            var glyphDatabase = new GlyphDatabase(dataDirectory);
            TargetMask prevTargetMask = null;
            var prevPoints = new List<Vector2>();
            for (int x = chatRect.Left; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                Point firstPixel = ChatParser.GetFirstPixel(cache, chatRect.Right, OCRHelpers.LINEHEIGHT, OCRHelpers.LineOffsets[offsetIndex], x, prevMatchedCharacters);

                //Make sure we didn't escape
                if (firstPixel == System.Drawing.Point.Empty)
                    return touchingChars;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, prevMatchedCharacters, chatRect.Left, chatRect.Right,
                    OCRHelpers.LineOffsets[offsetIndex], OCRHelpers.LineOffsets[offsetIndex] + OCRHelpers.LINEHEIGHT);
                var targetMaskPoints = new List<Vector2>();
                for (int maskX = targetMask.MinX; maskX <= targetMask.MaxX; maskX++)
                {
                    for (int maskY = 0; maskY < OCRHelpers.LINEHEIGHT; maskY++)
                    {
                        if (targetMask.Mask[maskX - targetMask.MinX, maskY])
                        {
                            targetMaskPoints.Add(new Vector2(maskX, OCRHelpers.LineOffsets[offsetIndex] + maskY));
                        }
                    }
                }

                if (targetMask.Width > 0)
                {
                    var possibleMatches = glyphDatabase.KnownGlyphs.Where(g => g.Width >= targetMask.Width - 2 && g.Width <= targetMask.Width + 2);
                    float bestDiff = float.NaN;
                    foreach (var match in possibleMatches)
                    {
                        var maxWidth = Math.Max(targetMask.Width, match.Width);
                        float diff = 0f;
                        for (int scanX = 0; scanX < maxWidth; scanX++)
                        {
                            for (int scanY = 0; scanY < OCRHelpers.LINEHEIGHT; scanY++)
                            {
                                if (scanX >= targetMask.Width)
                                    diff -= match.WeightMappings[scanX, scanY];
                                else if (scanX >= match.Width)
                                    diff -= targetMask.SoftMask[scanX, scanY];
                                else
                                    diff -= Math.Abs(match.WeightMappings[scanX, scanY] - targetMask.SoftMask[scanX, scanY]);
                            }
                        }
                        if (float.IsNaN(bestDiff) || diff > bestDiff)
                            bestDiff = diff;
                    }

                    if (float.IsNaN(bestDiff) || (bestDiff < - 10 && (-bestDiff / targetMask.SoftPixelCount) > 0.55f) )
                    {
                        touchingChars.Add((expectedCharacters[characterIndex], expectedCharacters[characterIndex + 1]));
                        using (var maskBitmap = new Bitmap(targetMask.Width, OCRHelpers.LINEHEIGHT))
                        {
                            for (int bitmapX = 0; bitmapX < maskBitmap.Width; bitmapX++)
                            {
                                for (int bitmapY = 0; bitmapY < maskBitmap.Height; bitmapY++)
                                {
                                    var value = (int)(targetMask.SoftMask[bitmapX, bitmapY] * 255);
                                    var c = Color.FromArgb(value, value, value);
                                    maskBitmap.SetPixel(bitmapX, bitmapY, c);
                                }
                            }
                            Console.WriteLine($"Characters {expectedCharacters[characterIndex]} and {expectedCharacters[characterIndex + 1]} touch.");
                            maskBitmap.Save(Path.Combine("overlaps", "hits",
                                ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex])
                                + ","
                                + ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex + 1])
                                + ".png"));

                            characterIndex += 2;
                        }
                    }
                    else
                    {
                        var toClose = false;
                        foreach (var prevPoint in prevPoints)
                        {
                            if (toClose)
                                break;

                            foreach (var curPoint in targetMaskPoints)
                            {
                                if(Vector2.DistanceSquared(prevPoint, curPoint) <= 4f)
                                {
                                    toClose = true;
                                    break;
                                }
                            }
                        }

                        if(toClose)
                        {
                            //Handle something being to close
                            touchingChars.Add((expectedCharacters[characterIndex - 1], expectedCharacters[characterIndex]));
                            using (var maskBitmap = new Bitmap(targetMask.MaxX - prevTargetMask.MinX, OCRHelpers.LINEHEIGHT))
                            {
                                for (int bitmapX = 0; bitmapX < maskBitmap.Width; bitmapX++)
                                {
                                    for (int bitmapY = 0; bitmapY < maskBitmap.Height; bitmapY++)
                                    {
                                        var value = (int)(cache[bitmapX + prevTargetMask.MinX, OCRHelpers.LineOffsets[offsetIndex] + bitmapY] * 255);
                                        var c = Color.FromArgb(value, value, value);
                                        maskBitmap.SetPixel(bitmapX, bitmapY, c);
                                    }
                                }
                                Console.WriteLine($"Characters {expectedCharacters[characterIndex - 1]} and {expectedCharacters[characterIndex]} might touch in the future.");
                                maskBitmap.Save(Path.Combine("overlaps", "hits",
                                    ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex - 1])
                                    + ","
                                    + ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex])
                                    + ".png"));
                            }
                        }
                        characterIndex++;
                    }

                    x = targetMask.MaxX;
                    prevTargetMask = targetMask;
                    prevPoints = targetMaskPoints;
                    for (int maskX = 0; maskX < targetMask.Mask.GetLength(0); maskX++)
                    {
                        for (int maskY = 0; maskY < targetMask.Mask.GetLength(1); maskY++)
                        {
                            if (targetMask.Mask[maskX, maskY])
                            {
                                prevMatchedCharacters.Add(targetMask.MinX + maskX, OCRHelpers.LineOffsets[offsetIndex] + maskY);
                            }
                        }
                    }
                    continue;

                }
                else
                    throw new Exception("Target mask of no width found!?");
            }

            return touchingChars;
        }

        private static int GetCharacterCount(int offsetIndex, ImageCache cache)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var prevMatchedCharacters = new CoordinateList();
            var charactersFound = 0;
            for (int x = chatRect.Left; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                Point firstPixel = ChatParser.GetFirstPixel(cache, chatRect.Right, OCRHelpers.LINEHEIGHT, OCRHelpers.LineOffsets[offsetIndex], x, prevMatchedCharacters);

                //Make sure we didn't escape
                if (firstPixel == System.Drawing.Point.Empty)
                    return charactersFound;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, prevMatchedCharacters, chatRect.Left, chatRect.Right,
                    OCRHelpers.LineOffsets[offsetIndex], OCRHelpers.LineOffsets[offsetIndex] + OCRHelpers.LINEHEIGHT);
                if (targetMask.Width > 0)
                {
                    charactersFound++;
                    x = targetMask.MaxX;
                    for (int maskX = 0; maskX < targetMask.Mask.GetLength(0); maskX++)
                    {
                        for (int maskY = 0; maskY < targetMask.Mask.GetLength(1); maskY++)
                        {
                            if (targetMask.Mask[maskX, maskY])
                                prevMatchedCharacters.Add(targetMask.MinX + maskX, OCRHelpers.LineOffsets[offsetIndex] + maskY);
                        }
                    }
                    continue;
                }
                else
                    throw new Exception("Target mask of no width found!?");
            }

            return charactersFound;
        }

        private static IEnumerable<string> GetValidInputs(string sourceDir)
        {
            var allFiles = Directory.GetFiles(sourceDir);
            var possibleInputs = allFiles.Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct();
            return possibleInputs.Where(f => allFiles.Contains(f + ".png") && allFiles.Contains(f + ".txt"));
        }
    }
}
