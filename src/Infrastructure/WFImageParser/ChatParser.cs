using Application.ChatMessages.Model;
using Application.Interfaces;
using Application.LineParseResult;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WFImageParser
{
    public class ChatParser : IImageParser
    {
        private List<CharacterDetails> _scannedCharacters = new List<CharacterDetails>();
        private Dictionary<string, Dictionary<string, int>> _gapPairs = new Dictionary<string, Dictionary<string, int>>();
        private int _maxCharWidth = 0;
        private int _minCharWidth = int.MaxValue;

        private static readonly string GAPSFILE = Path.Combine("ocrdata", "gaps.json");
        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };

        public ChatParser()
        {
            var converter = new ColorSpaceConverter();
            if (Directory.Exists("ocrdata"))
            {
                foreach (var file in Directory.GetFiles("ocrdata").Where(f => f.EndsWith(".png")))
                {
                    var character = new CharacterDetails()
                    {
                        Name = (new FileInfo(file)).Name.Replace(".png", ""),
                        TotalWeights = 0f
                    };
                    using (Image<Rgba32> image = Image.Load(file))
                    {
                        character.VMask = new bool[image.Width, image.Height];
                        character.WeightMappings = new float[image.Width, image.Height];
                        for (int x = 0; x < image.Width; x++)
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                character.WeightMappings[x, y] = (float)image[x, y].R / (float)byte.MaxValue;
                                character.TotalWeights += character.WeightMappings[x, y];
                                if (character.WeightMappings[x, y] > 0)
                                {
                                    character.VMask[x, y] = true;
                                }
                                else
                                    character.VMask[x, y] = false;
                            }
                        }
                        character.Width = image.Width;
                        character.Height = image.Height;
                        _scannedCharacters.Add(character);
                        if (character.Width > _maxCharWidth)
                            _maxCharWidth = character.Width;
                        if (character.Width < _minCharWidth)
                            _minCharWidth = character.Width;
                    }
                }

                //Load up gap pairs
                if (File.Exists(GAPSFILE))
                {
                    var gapPairs = JsonConvert.DeserializeObject<SimpleGapPair[]>(File.ReadAllText(GAPSFILE));
                    foreach (var gapPair in gapPairs)
                    {
                        if (!_gapPairs.ContainsKey(gapPair.Left))
                            _gapPairs.Add(gapPair.Left, new Dictionary<string, int>());
                        if (gapPair.Gap > 0)
                            _gapPairs[gapPair.Left].Add(gapPair.Right, gapPair.Gap - 1); //There is an off by 1 error in the gaps file currently
                        else
                            _gapPairs[gapPair.Left].Add(gapPair.Right, 0);
                    }
                }
            }
        }

        //private int[] _lineOffsets = new int[] { 5, 55, 105, 154, 204, 253, 303, 352, 402, 452, 501, 551, 600, 650, 700, 749, 799, 848, 898, 948, 997, 1047, 1096, 1146, 1195, 1245, 1295 };
        //private int[] _lineOffsetsSmall = new int[] { 737, 776, 815, 853, 892, 931, 970, 1009, 1048, 1087, 1125, 1164, 1203, 1242, 1280, 1320, 1359, 1397, 1436, 1475, 1514, 1553, 1592, 1631, 1669, 1708, 1747, 1786, 1825, 1864, 1903, 1942, 1980, 2019, 2058 };
        private int[] _lineOffsets = new int[] { 768, 818, 868, 917, 967, 1016, 1066, 1115, 1165, 1215, 1264, 1314, 1363, 1413, 1463, 1512, 1562, 1611, 1661, 1711, 1760, 1810, 1859, 1909, 1958, 2008, 2058 };

        public class SimpleGapPair
        {
            public string Left { get; set; }
            public string Right { get; set; }
            public int Gap { get; set; }
        }

        private LineParseResult ParseLineBitmapScan(ImageCache image, float minV, int xOffset, Rectangle chatRect, int lineHeight, int lineOffset, float spaceWidth)
        {
            var rawMessage = new System.Text.StringBuilder();
            var message = new StringBuilder();
            var startX = xOffset;
            var endX = xOffset;
            var lastCharacterEndX = startX;
            var prevMatchedCharacters = new CoordinateList();
            TargetMask prevTargetMask = null;
            CharacterDetails lastCharacterDetails = null;
            var wordStartX = -1;
            var currentWord = new StringBuilder();
            List<ClickPoint> clickPoints = new List<ClickPoint>();
            for (int x = xOffset; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                var firstPixel = Point.Empty;
                for (int i = endX; i < chatRect.Right; i++)
                {
                    var pixelFound = false;
                    for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                    {
                        if (image[i, y] > minV && !prevMatchedCharacters.Any(p => p.X == i && p.Y == y))
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

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, image, prevMatchedCharacters, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                var didRemove = false;
                var newXFocus = targetMask.MinX;
                for (int x2 = 0; x2 < targetMask.Width; x2++)
                {
                    var count = 0;
                    var strength = 0f;
                    for (int y2 = 0; y2 < lineHeight; y2++)
                    {
                        strength += targetMask.SoftMask[x2, y2];
                        if (targetMask.SoftMask[x2, y2] > 0)
                            count++;
                    }
                    if (strength / count < 0.12 && targetMask.Width > 6)
                    {
                        for (int y2 = 0; y2 < lineHeight; y2++)
                        {
                            prevMatchedCharacters.Add(new Point(targetMask.MinX + x2, lineOffset + y2));
                            didRemove = true;
                            newXFocus = x2 + targetMask.MinX;
                        }
                    }
                    else
                        break;
                }
                if (didRemove)
                {
                    for (int i = newXFocus; i < chatRect.Right; i++)
                    {
                        var pixelFound = false;
                        for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                        {
                            if (image[i, y] > minV && !prevMatchedCharacters.Any(p => p.X == i && p.Y == y))
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
                    targetMask = OCRHelpers.FindCharacterMask(firstPixel, image, prevMatchedCharacters, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                }
                if (wordStartX < 0)
                    wordStartX = targetMask.MinX;

                startX = targetMask.MinX;
                endX = targetMask.MaxX + 1;

                if (endX > startX && targetMask.PixelCount > 10)
                {
                    var bestFit = FastGuessPartialCharacter(targetMask, lineOffset);
                    //Try shifting
                    if (bestFit == null)
                    {
                        //using (var debug = new Image<Rgba32>(targetMask.Width, targetMask.SoftMask.GetLength(1)))
                        //{
                        //    for (int x2 = 0; x2 < debug.Width; x2++)
                        //    {
                        //        for (int y2 = 0; y2 < debug.Height; y2++)
                        //        {
                        //            debug[x2, y2] = new Rgba32(targetMask.SoftMask[x2, y2], targetMask.SoftMask[x2, y2], targetMask.SoftMask[x2, y2]);
                        //        }
                        //    }
                        //    debug.Save("debug_target.png");
                        //}

                        var shiftyMask = targetMask;
                        for (int i = 0; i < 2; i++)//Try trimming left
                        {
                            if (shiftyMask.Width - 1 <= 0)
                                break;
                            //PIVOT
                            var boolMask = new bool[shiftyMask.Width - 1, shiftyMask.Mask.GetLength(1)];
                            var hardCount = 0;
                            for (int x2 = 0; x2 < boolMask.GetLength(0); x2++)
                            {
                                for (int y2 = 0; y2 < boolMask.GetLength(1); y2++)
                                {
                                    boolMask[x2, y2] = shiftyMask.Mask[x2 + 1, y2];
                                    if (boolMask[x2, y2])
                                        hardCount++;
                                }
                            }
                            var softCount = 0f;
                            var softMask = new float[boolMask.GetLength(0), boolMask.GetLength(1)];
                            for (int x2 = 0; x2 < boolMask.GetLength(0); x2++)
                            {
                                for (int y2 = 0; y2 < boolMask.GetLength(1); y2++)
                                {
                                    softMask[x2, y2] = shiftyMask.SoftMask[x2 + 1, y2];
                                    softCount += softMask[x2, y2];
                                }
                            }

                            var clippedMask = new TargetMask(boolMask, shiftyMask.MaxX, shiftyMask.MinX + 1, shiftyMask.Width - 1, hardCount, softCount, softMask);
                            shiftyMask = clippedMask;

                            //using (var debug = new Image<Rgba32>(shiftyMask.Width, shiftyMask.SoftMask.GetLength(1)))
                            //{
                            //    for (int x2 = 0; x2 < debug.Width; x2++)
                            //    {
                            //        for (int y2 = 0; y2 < debug.Height; y2++)
                            //        {
                            //            debug[x2, y2] = new Rgba32(shiftyMask.SoftMask[x2, y2], shiftyMask.SoftMask[x2, y2], shiftyMask.SoftMask[x2, y2]);
                            //        }
                            //    }
                            //    debug.Save("debug_shift.png");
                            //}
                            var partialMatch = FastGuessPartialCharacter(clippedMask, lineOffset);
                            //We need to be sure that this new match is solid and not just better than the existing one
                            if (partialMatch != null && partialMatch.Item1 > 0.7)
                            {
                                bestFit = partialMatch;
                                break;
                            }
                        }
                        if (bestFit == null)
                        {
                            shiftyMask = targetMask;
                            for (int i = 1; i <= 2; i++)//Try padding left
                            {
                                //PIVOT
                                var boolMask = new bool[shiftyMask.Width + 1, shiftyMask.Mask.GetLength(1)];
                                var hardCount = 0;
                                for (int x2 = i; x2 < boolMask.GetLength(0); x2++)
                                {
                                    for (int y2 = 0; y2 < boolMask.GetLength(1); y2++)
                                    {
                                        if (shiftyMask.SoftMask[x2 - i, y2] > 0f)
                                        {
                                            boolMask[x2, y2] = shiftyMask.Mask[x2 - i, y2];
                                            if (boolMask[x2, y2])
                                                hardCount++;
                                        }
                                    }
                                }
                                var softCount = 0f;
                                var softMask = new float[boolMask.GetLength(0), boolMask.GetLength(1)];
                                for (int x2 = i; x2 < boolMask.GetLength(0); x2++)
                                {
                                    for (int y2 = 0; y2 < boolMask.GetLength(1); y2++)
                                    {
                                        //if (shiftyMask.SoftMask[x2 - i, y2] > 0.3f)
                                        //{
                                        softMask[x2, y2] = shiftyMask.SoftMask[x2 - i, y2];
                                        softCount += softMask[x2, y2];
                                        //}
                                    }
                                }

                                var clippedMask = new TargetMask(boolMask, shiftyMask.MaxX, shiftyMask.MinX, shiftyMask.Width + 1, hardCount, softCount, softMask);
                                shiftyMask = clippedMask;

                                //using (var debug = new Image<Rgba32>(shiftyMask.Width, shiftyMask.SoftMask.GetLength(1)))
                                //{
                                //    for (int x2 = 0; x2 < debug.Width; x2++)
                                //    {
                                //        for (int y2 = 0; y2 < debug.Height; y2++)
                                //        {
                                //            debug[x2, y2] = new Rgba32(shiftyMask.SoftMask[x2, y2], shiftyMask.SoftMask[x2, y2], shiftyMask.SoftMask[x2, y2]);
                                //        }
                                //    }
                                //    debug.Save("debug_shift.png");
                                //}
                                var partialMatch = FastGuessPartialCharacter(clippedMask, lineOffset);
                                //We need to be sure that this new match is solid and not just better than the existing one
                                if (partialMatch != null && partialMatch.Item1 > 0.7)
                                {
                                    bestFit = partialMatch;
                                    break;
                                }
                            }
                        }
                    }
                    //If all else has failed hope that we are on some sort of horrible overlap and take the best we can find
                    if (bestFit == null)
                    {
                        //var tmpBlacklist = new CoordinateList();
                        //tmpBlacklist.AddRange(prevMatchedCharacters);
                        //Point tmpPixel = Point.Empty;
                        //for (int x2 = 0; x2 < targetMask.Width; x2++)
                        //{
                        //    for (int y2 = 0; y2 < lineHeight; y2++)
                        //    {
                        //        if (targetMask.SoftMask[x2, y2] < 0.3)
                        //            tmpBlacklist.Add(new Point(x2 + targetMask.MinX, y2 + lineOffset));
                        //        else if (tmpPixel == Point.Empty)
                        //            tmpPixel = new Point(x2 + targetMask.MinX, y2 + lineOffset);
                        //    }
                        //}
                        //var frontMask = OCRHelpers.FindCharacterMask(tmpPixel, image, tmpBlacklist, targetMask.MinX, targetMask.MaxX, lineOffset, lineOffset + lineHeight);
                        var boolMask = new bool[targetMask.Mask.GetLength(0), targetMask.Mask.GetLength(1)];
                        var hardCount = 0;
                        for (int x2 = 0; x2 < boolMask.GetLength(0); x2++)
                        {
                            for (int y2 = 0; y2 < boolMask.GetLength(1); y2++)
                            {
                                boolMask[x2, y2] = targetMask.SoftMask[x2, y2] > 0.5f;
                                if (boolMask[x2, y2])
                                    hardCount++;
                            }
                        }
                        var softMask = new float[targetMask.Mask.GetLength(0), targetMask.Mask.GetLength(1)];
                        var softCount = 0f;
                        for (int x2 = 0; x2 < softMask.GetLength(0); x2++)
                        {
                            for (int y2 = 0; y2 < softMask.GetLength(1); y2++)
                            {
                                softMask[x2, y2] = targetMask.SoftMask[x2, y2] > 0.5f ? targetMask.SoftMask[x2, y2] : 0f;
                                softCount += softMask[x2, y2];
                            }
                        }
                        var frontMask = new TargetMask(boolMask, targetMask.MaxX, targetMask.MinX, targetMask.Width, hardCount, softCount, softMask);
                        bestFit = FastGuessPartialCharacter(frontMask, lineOffset, true);
                        //Cleanup any lingering pixels
                        //We are in a bad state so be very aggressive
                        if (bestFit != null)
                        {
                            var columnHunting = true;
                            for (int x2 = targetMask.MinX; x2 <= targetMask.MaxX; x2++)
                            {
                                var columnCount = 0f;
                                for (int y2 = lineOffset; y2 < lineOffset + lineHeight; y2++)
                                {
                                    if (image[x2, y2] < 0.2)
                                        bestFit.Item3.Add(new Point(x2, y2));
                                    else
                                        columnCount += image[x2, y2];
                                }

                                if (columnHunting && columnCount < 1.5)
                                {
                                    for (int y2 = lineOffset; y2 < lineOffset + lineHeight; y2++)
                                    {
                                        bestFit.Item3.Add(new Point(x2, y2));
                                    }
                                }
                                else
                                {
                                    columnHunting = false;
                                }
                            }
                        }
                    }
                    //else if (bestFit.Item1 < 0.7)
                    //{
                    //    var fuzzyMask = OCRHelpers.FindCharacterMask(firstPixel, image, prevMatchedCharacters, Math.Max(0.29f, minV - 0.2f), chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                    //    var fuzzFit = FastGuessCharacter(targetMask, lineOffset);
                    //    if (fuzzFit.Item1 > bestFit.Item1)
                    //        bestFit = fuzzFit;
                    //}

                    //We can allow loose fits on smaller characters
                    if (bestFit != null && bestFit.Item1 < 0.20f && bestFit.Item2 != null && bestFit.Item2.TotalWeights > 40)
                        bestFit = new Tuple<float, CharacterDetails, CoordinateList>(float.MinValue, null, null);

                    if (bestFit != null && bestFit.Item2 != null && endX != lastCharacterEndX)
                    {
                        var name = bestFit.Item2.Name.Replace(".png", "").Replace(".txt", "").Replace("alt_", "");
                        if (name.EndsWith("_upper"))
                            name = name.Substring(0, name.IndexOf('_')).ToUpper();
                        else if (name.EndsWith("_lower"))
                            name = name.Substring(0, name.IndexOf('_')).ToLower();
                        if (name == "colon")
                            name = ":";
                        else if (name == "asterix")
                            name = "*";
                        else if (name == "gt")
                            name = ">";
                        else if (name == "lt")
                            name = "<";
                        else if (name == "backSlash")
                            name = "\\";
                        else if (name == "question")
                            name = "?";
                        else if (name == "forwardSlash")
                            name = "/";
                        else if (name == "pipe")
                            name = "|";
                        else if (name == "comma")
                            name = ",";


                        ////Check if we skipped past a space
                        if (prevTargetMask != null && prevTargetMask.PixelCount > 0)
                        {
                            var safeRightX = -1;
                            var safeLeftX = -1;
                            //We need to account for all the random pixels that are added from noise
                            //Search from right to left on the prev mask for a column that has a pixel with 2 ore more neighbors
                            for (int i = prevTargetMask.Width - 1; i > 0; i--)
                            {
                                for (int y = 0; y < lineHeight; y++)
                                {
                                    if (OCRHelpers.NeighborCount(prevTargetMask, i, y) > 1)
                                    {
                                        safeLeftX = prevTargetMask.MinX + i;
                                        break;
                                    }
                                }
                                if (safeLeftX > -1)
                                    break;
                            }
                            //Search from left to right on current mask for a column that has a pixel with 2 or more neighbors
                            for (int i = 0; i < targetMask.Width; i++)
                            {
                                for (int y = 0; y < lineHeight; y++)
                                {
                                    if (OCRHelpers.NeighborCount(targetMask, i, y) > 1)
                                    {
                                        safeRightX = targetMask.MinX + i + 1;
                                        break;
                                    }
                                }
                                if (safeRightX > -1)
                                    break;
                            }
                            var pixelGap = safeRightX - safeLeftX;
                            if (_gapPairs.ContainsKey(lastCharacterDetails.Name)
                                && _gapPairs[lastCharacterDetails.Name].ContainsKey(bestFit.Item2.Name)
                                && pixelGap > _gapPairs[lastCharacterDetails.Name][bestFit.Item2.Name] + spaceWidth)
                            {
                                AppendSpace(image, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
                                wordStartX = targetMask.MinX;

                            }
                        }

                        //Add character
                        currentWord.Append(name);
                        rawMessage.Append(name);

                        lastCharacterEndX = startX + bestFit.Item2.Width - 2;
                        prevMatchedCharacters.AddRange(bestFit.Item3);
                        prevTargetMask = targetMask;
                        lastCharacterDetails = bestFit.Item2;

                        //Due to new char IDing system we can safely jump a bit ahead to prevent double reading
                        if (endX - startX > _maxCharWidth * 0.6 && targetMask.PixelCount - prevMatchedCharacters.Count > targetMask.PixelCount * 0.3)
                            endX = x = lastCharacterEndX;
                        else
                            endX = x = lastCharacterEndX + 2;
                    }
                    else //failed to ID the character, skip it
                    {
                        //AppendSpace(image, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
                        x = lastCharacterEndX = endX = targetMask.MaxX + 1;
                    }
                }
                else
                {
                    endX = startX = Math.Max(endX + 1, targetMask.MaxX + 1);
                }
            }
            if (rawMessage.Length > 0)
            {
                AppendSpace(image, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
                var result = new LineParseResult()
                {
                    ClickPoints = clickPoints,
                    RawMessage = rawMessage.ToString(),
                    EnhancedMessage = message.ToString()
                };
                return result;
            }
            else return new LineParseResult();
        }

        private static void AppendSpace(ImageCache image, int lineHeight, int lineOffset, StringBuilder rawMessage, StringBuilder message, int wordStartX, StringBuilder currentWord, List<ClickPoint> clickPoints)
        {
            var foundRiven = CheckNewWordForRiven(lineHeight, lineOffset, wordStartX, currentWord.ToString(), clickPoints, image, message.Length);
            var word = currentWord.ToString() + ' ';
            if (foundRiven)
            {
                var index = word.IndexOf("]");
                word = word.Substring(0, index+1) + "(" + (clickPoints.Count - 1) + ")" + word.Substring(index + 1);
            }
            message.Append(word);
            currentWord.Clear();
            rawMessage.Append(' ');
        }

        private static bool CheckNewWordForRiven(int lineHeight, int lineOffset, int wordStartX, string currentWord, List<ClickPoint> clickPoints, ImageCache image, int wordIndex)
        {
            var foundRiven = false;
            var converter = new ColorSpaceConverter();
            if (_suffixes.Any(s => currentWord.Contains(s)))
            {
                Point point = Point.Empty;
                for (int x = Math.Max(0, wordStartX - 5); x < wordStartX + 5 && x < image.Width; x++)
                {
                    for (int y = lineOffset + lineHeight / 2 - 5; y < lineOffset + lineHeight / 2 + 5 && y < image.Height; y++)
                    {
                        var hsvPixel = image.GetHsv(x, y);
                        if (hsvPixel.V > 0.3f && hsvPixel.H >= 176.3 && hsvPixel.H <= 255)
                        {
                            foundRiven = true;
                            point = new Point(x, y);
                            break;
                        }
                    }
                    if (foundRiven)
                        break;
                }
                if (foundRiven)
                    clickPoints.Add(new ClickPoint() { X = point.X, Y = point.Y, Index = wordIndex });
            }
            return foundRiven;
        }

        private Tuple<float, CharacterDetails, CoordinateList> FastGuessCharacter(TargetMask targetMask, int lineOffset)
        {
            var targetWidth = targetMask.Width + 2;

            var cannidates = _scannedCharacters;
            //Try to only look at similiar sized characters when we are looking at a medium width character.
            //if (targetWidth <= _maxCharWidth / 2 && targetWidth > Math.Ceiling(_maxCharWidth * 0.15))
            //    cannidates = _scannedCharacters.Where(c => c.Width >= targetWidth * 0.8f && c.Width <= targetWidth * 1.2f).ToList();
            //else if (targetWidth <= Math.Ceiling(_maxCharWidth * 0.15)) //Only smalls
            //    cannidates = _scannedCharacters.Where(c => c.Width <= Math.Ceiling(_maxCharWidth * 0.15)).ToList();
            //Else it will be anything as we may be dealing with a partial match

            var bestMatchConf = 0f;
            CharacterDetails bestMatchCharacter = null;
            CoordinateList bestMatchingPixels = null;

            foreach (var character in cannidates)
            {
                var characterPixelsMatched = 0f;
                var dVMask = character.VMask;
                var charWidth = character.Width;

                var matchingPixels = new CoordinateList();
                for (int x = 0; x < character.Width && x < targetMask.Width; x++)
                {
                    for (int y = 0; y < character.Height; y++)
                    {
                        if (character.VMask[x, y] && targetMask.Mask[x, y])
                        {
                            characterPixelsMatched += character.WeightMappings[x, y];
                            matchingPixels.Add(new Point(x + targetMask.MinX, y + lineOffset));
                        }
                    }
                }

                //In English an empty horizontal line conveys a huge amount of meaning, i ! j, so penalize heavily for not getting that right
                if (character.Width <= 6 || targetMask.Width <= 6)
                {
                    for (int y = 0; y < character.Height; y++)
                    {
                        var isTargetEmpty = true;
                        for (int x = 0; x < targetMask.Width; x++)
                        {
                            if (targetMask.Mask[x, y])
                            {
                                isTargetEmpty = false;
                                break;
                            }
                        }

                        if (isTargetEmpty)
                        {
                            for (int x = 0; x < character.Width; x++)
                            {
                                if (character.VMask[x, y])
                                    characterPixelsMatched--;
                            }
                        }

                        var isCharacterEmpty = true;
                        for (int x = 0; x < character.Width; x++)
                        {
                            if (character.VMask[x, y])
                            {
                                isCharacterEmpty = false;
                                break;
                            }
                        }

                        if (isCharacterEmpty)
                        {
                            for (int x = 0; x < targetMask.Width; x++)
                            {
                                if (targetMask.Mask[x, y])
                                    characterPixelsMatched--;
                            }
                        }
                    }
                }

                var conf = (float)characterPixelsMatched / (float)(Math.Max(targetMask.SoftPixelCount, character.TotalWeights));
                if (conf > bestMatchConf)
                {
                    bestMatchConf = conf;
                    bestMatchCharacter = character;
                    bestMatchingPixels = matchingPixels;
                }
            }

            return new Tuple<float, CharacterDetails, CoordinateList>(bestMatchConf, bestMatchCharacter, bestMatchingPixels);
        }

        private Tuple<float, CharacterDetails, CoordinateList> FastGuessPartialCharacter(TargetMask targetMask, int lineOffset, bool takeLeastBad = false)
        {
            var targetWidth = targetMask.Width;
            var minWidth = takeLeastBad ? 0 : targetWidth * 0.5f;
            var leastBadMatch = float.MinValue;
            CharacterDetails leastBadCharacter = null;
            CoordinateList leastBadMatchingPixels = null;
            foreach (var group in _scannedCharacters.Where(c => c.Width >= minWidth && c.Width <= targetWidth + 2).OrderByDescending(c => c.Width).GroupBy(c => c.Width))
            {
                var bestMatch = float.MinValue;
                CharacterDetails bestCharacter = null;
                CoordinateList bestMatchingPixels = null;
                foreach (var character in group)
                {
                    //Determine if 80%+ of pixels are covered in target
                    //If so return a new best fit with a conf of pixel coverage
                    var characterPixelsMatched = 0f;
                    var matchingPixels = new CoordinateList();
                    for (int y = 0; y < character.Height; y++)
                    {
                        var charCount = 0;
                        for (int x = 0; x < character.Width && x < targetMask.Width; x++)
                        {
                            if (character.VMask[x, y])
                                charCount++;
                            if (character.VMask[x, y] && targetMask.Mask[x, y])
                            {
                                characterPixelsMatched += character.WeightMappings[x, y];// + targetMask.SoftMask[x,y];
                                matchingPixels.Add(new Point(x + targetMask.MinX, y + lineOffset));
                            }
                            else if (character.TotalWeights > 55 && targetMask.Mask[x, y] && !character.VMask[x, y])
                                characterPixelsMatched -= targetMask.SoftMask[x, y] / 2;
                            else if (character.TotalWeights > 55 && character.VMask[x, y] && !targetMask.Mask[x, y])
                                characterPixelsMatched -= character.WeightMappings[x, y] / 4;
                            //else if (targetWidth > _maxCharWidth * 0.75 && x <= targetWidth / 3 && !character.VMask[x, y] && targetMask.Mask[x, y]) //The first few pixels are most important. Punish missing them
                            //{
                            //    characterPixelsMatched--;
                            //}
                        }

                        //Make sure empty lines are empty
                        if (charCount == 0)
                        {
                            for (int x = 0; x < character.Width && x < targetMask.Width; x++)
                            {
                                characterPixelsMatched -= targetMask.SoftMask[x, y];
                                if (character.TotalWeights < 51 && targetMask.Width <= character.Width) //Tiny characters really need to make sure their empty lines are empty
                                    characterPixelsMatched -= targetMask.SoftMask[x, y];
                            }
                        }
                    }
                    var coverage = characterPixelsMatched / character.TotalWeights;
                    //Tiny elements have huge variance from random noise. Give them a boost.
                    //if (character.TotalWeights < 40)
                    //    coverage *= 1.2f;
                    if (coverage > bestMatch)
                    {
                        bestMatch = coverage;
                        bestCharacter = character;
                        bestMatchingPixels = matchingPixels;
                    }
                    if (takeLeastBad && coverage > leastBadMatch && targetMask.Width > 4)
                    {
                        leastBadMatch = coverage;
                        leastBadCharacter = character;
                        leastBadMatchingPixels = matchingPixels;
                    }
                }
                //var coverage = cleanTargetPixels.Where(p => p.X - startX > 0 && p.X - startX < character.Width && character.VMask[p.X - startX, p.Y - lineOffset] > minV).Count() / (float)character.PixelCount;
                if (bestMatch > 0.85 || (bestMatch > 0.7 && takeLeastBad))
                {
                    return new Tuple<float, CharacterDetails, CoordinateList>(bestMatch, bestCharacter, bestMatchingPixels);
                }
            }
            if (takeLeastBad && leastBadCharacter != null)
                return new Tuple<float, CharacterDetails, CoordinateList>(leastBadMatch, leastBadCharacter, leastBadMatchingPixels);
            return null;
        }

        public LineParseResult[] ParseChatImage(string imagePath, int xOffset)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var results = new List<LineParseResult>();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var cache = new ImageCache(rgbImage);
                var offsets = _lineOffsets;
                var lineHeight = 36;
                var endLine = offsets.Length;
                var regex = new Regex(@"^\[\d\d:\d\d\]", RegexOptions.Compiled);
                var kickRegex = new Regex(@"\w was kicked.", RegexOptions.Compiled);
                //var results = new string[endLine - startLine];
                for (int i = 0; i < endLine && i < offsets.Length; i++)
                {
                    var line = ParseLineBitmapScan(cache, 0.3f, xOffset, chatRect, lineHeight, offsets[i], 6);
                    if (line != null && line.RawMessage != null && line.RawMessage.Length > 0 && kickRegex.Match(line.RawMessage).Success)
                        continue;
                    if (line.RawMessage != null && regex.Match(line.RawMessage).Success)
                        results.Add(line);
                    else if (results.Count > 0 && line.RawMessage != null)
                    {
                        var last = results.Last();
                        //results.Remove(last);
                        //results.Add(last + " " + line);
                        last.Append(line);
                    }
                }
            }

            return results.ToArray();
        }

        public string[] ParseRivenImage(string imagePath)
        {
            throw new NotImplementedException();
        }

        public LineParseResult[] ParseChatImage(string imagePath)
        {
            return ParseChatImage(imagePath, 4);
        }

        private class CharacterDetails
        {
            public bool[,] VMask { get; set; }
            public float[,] WeightMappings { get; set; }
            //public int PixelCount { get; set; }
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public float TotalWeights { get; internal set; }
        }
    }
}
