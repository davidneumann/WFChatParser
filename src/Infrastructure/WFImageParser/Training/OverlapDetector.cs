using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using WFImageParser.GlyphRecognition;

namespace WFImageParser.Training
{
    public static class OverlapDetector
    {
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
                    for (int i = 0; i < expectedLines.Length; i++)
                    {
                        var charCount = GetCharacterCount(i, cache);
                        int expectedCharCount = expectedLines[i].Replace(" ", "").Length;
                        if (expectedCharCount != charCount)
                        {
                            Console.WriteLine($"Overlap detected in {input}, line index {i} line y {OCRHelpers.LineOffsets[i]}. Expected {expectedCharCount} but found {charCount}");
                            //throw new Exception("Overlap detected on " + input + " chat line " + (i + 1) + " y of " + OCRHelpers.LineOffsets[i]);

                            //TODO: implement this
                            List<(char left, char right)> touchingChars = FindTouchingChars(cache, i, expectedLines[i].Replace(" ", "").ToCharArray(), dataDirectory);
                        }
                    }
                }
            }
        }

        private static List<(char left, char right)> FindTouchingChars(ImageCache cache, int offsetIndex, char[] expectedCharacters, string dataDirectory)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var prevMatchedCharacters = new CoordinateList();
            var characterIndex = 0;
            var touchingChars = new List<(char left, char right)>();
            var glyphDatabase = new GlyphDatabase(dataDirectory);
            for (int x = chatRect.Left; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                Point firstPixel = ChatParser.GetFirstPixel(cache, chatRect.Right, OCRHelpers.LINEHEIGHT, OCRHelpers.LineOffsets[offsetIndex], x, prevMatchedCharacters);

                //Make sure we didn't escape
                if (firstPixel == System.Drawing.Point.Empty)
                    return touchingChars;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, cache, prevMatchedCharacters, chatRect.Left, chatRect.Right,
                    OCRHelpers.LineOffsets[offsetIndex], OCRHelpers.LineOffsets[offsetIndex] + OCRHelpers.LINEHEIGHT);
                if (targetMask.Width > 0)
                {
                    var possibleMatches = glyphDatabase.KnownGlyphs.Where(g => g.Width >= targetMask.Width - 1 && g.Width <= targetMask.Width + 1);
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

                    if (float.IsNaN(bestDiff) || (-bestDiff / targetMask.SoftPixelCount) > 0.55f)
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
                            maskBitmap.Save(Path.Combine("overlaps", "hits",
                                ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex])
                                + ","
                                + ImageCleaner.ConvertCharacterToName(expectedCharacters[characterIndex + 1])
                                + ".png"));

                            characterIndex += 2;
                        }
                    }
                    else
                        characterIndex++;

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
