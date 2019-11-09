using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace WFImageParser.Training
{
    public static class OverlapDetector
    {
        public static void DetectOverlaps(string sourceDir)
        {
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
                            //List<(char left, char right)> touchingChars = FindTouchingChars(cache, i, expectedLines[i].Replace(" ", ""));
                        }
                    }
                }
            }
        }

        private static List<(char left, char right)> FindTouchingChars(ImageCache cache, int i, string v)
        {
            throw new NotImplementedException();
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
