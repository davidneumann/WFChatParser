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

        private LineParseResult ParseLineBitmapScan(float minV, int xOffset, ColorSpaceConverter converter, Rectangle chatRect, Image<Rgba32> rgbImage, int lineHeight, int lineOffset, float spaceWidth)
        {
            var rawMessage = new System.Text.StringBuilder();
            var message = new StringBuilder();
            var startX = xOffset;
            var endX = xOffset;
            var lastCharacterEndX = startX;
            List<Point> prevMatchedCharacters = new List<Point>();
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
                        if (converter.ToHsv(rgbImage[i, y]).V > minV && !prevMatchedCharacters.Any(p => p.X == i && p.Y == y))
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

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, rgbImage, prevMatchedCharacters, minV, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                if (wordStartX < 0)
                    wordStartX = targetMask.MinX;

                startX = targetMask.MinX;
                endX = targetMask.MaxX + 1;

                if (endX > startX && targetMask.PixelCount > 10)
                {
                    var bestFit = FastGuessCharacter(targetMask, lineOffset);
                    if (bestFit.Item1 < 0.7 && targetMask.Width > 4)
                    {
                        var partialMatch = FastGuessPartialCharacter(targetMask, lineOffset);
                        //We need to be sure that this new match is solid and not just better than the existing one
                        if (partialMatch != null && partialMatch.Item1 > 0.7 && partialMatch.Item1 > bestFit.Item1)
                            bestFit = partialMatch;
                    }
                    else if (bestFit.Item1 < 0.7)
                    {
                        var fuzzyMask = OCRHelpers.FindCharacterMask(firstPixel, rgbImage, prevMatchedCharacters, minV - 0.2f, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);
                        var fuzzFit = FastGuessCharacter(targetMask, lineOffset);
                        if (fuzzFit.Item1 > bestFit.Item1)
                            bestFit = fuzzFit;
                    }

                    //We can allow loose fits on smaller characters
                    if (bestFit.Item1 < 0.20f && bestFit.Item2 != null && bestFit.Item2.TotalWeights > 40)
                        bestFit = bestFit = new Tuple<float, CharacterDetails, List<Point>>(float.MinValue, null, null);

                    if (bestFit.Item2 != null && endX != lastCharacterEndX)
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
                                AppendSpace(rgbImage, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
                                wordStartX = targetMask.MinX;

                            }
                        }

                        //Add character
                        currentWord.Append(name);
                        rawMessage.Append(name);

                        lastCharacterEndX = startX + bestFit.Item2.Width;
                        prevMatchedCharacters = bestFit.Item3;
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
                        AppendSpace(rgbImage, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
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
                AppendSpace(rgbImage, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
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

        private static void AppendSpace(Image<Rgba32> rgbImage, int lineHeight, int lineOffset, StringBuilder rawMessage, StringBuilder message, int wordStartX, StringBuilder currentWord, List<ClickPoint> clickPoints)
        {
            var foundRiven = CheckNewWordForRiven(lineHeight, lineOffset, wordStartX, currentWord.ToString(), clickPoints, rgbImage, message.Length);
            if (foundRiven)
                message.Append("[" + (clickPoints.Count - 1) + "]");
            message.Append(currentWord.ToString() + ' ');
            currentWord.Clear();
            rawMessage.Append(' ');
        }

        private static bool CheckNewWordForRiven(int lineHeight, int lineOffset, int wordStartX, string currentWord, List<ClickPoint> clickPoints, Image<Rgba32> image, int wordIndex)
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
                        var hsvPixel = converter.ToHsv(image[x, y]);
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

        private Tuple<float, CharacterDetails, List<Point>> FastGuessCharacter(TargetMask targetMask, int lineOffset)
        {
            var targetWidth = targetMask.Width;

            var cannidates = _scannedCharacters;
            //Try to only look at similiar sized characters when we are looking at a medium width character.
            if (targetWidth <= _maxCharWidth / 2 && targetWidth > Math.Ceiling(_maxCharWidth * 0.15))
                cannidates = _scannedCharacters.Where(c => c.Width >= targetWidth * 0.8f && c.Width <= targetWidth * 1.2f).ToList();
            else if (targetWidth <= Math.Ceiling(_maxCharWidth * 0.15)) //Only smalls
                cannidates = _scannedCharacters.Where(c => c.Width <= Math.Ceiling(_maxCharWidth * 0.15)).ToList();
            //Else it will be anything as we may be dealing with a partial match

            var bestMatchConf = 0f;
            CharacterDetails bestMatchCharacter = null;
            List<Point> bestMatchingPixels = null;
            foreach (var character in cannidates)
            {
                var characterPixelsMatched = 0f;
                var dVMask = character.VMask;
                var charWidth = character.Width;

                var matchingPixels = new List<Point>();
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

                var conf = (float)characterPixelsMatched / (float)(Math.Max(targetMask.PixelCount, character.TotalWeights));
                if (conf > bestMatchConf)
                {
                    bestMatchConf = conf;
                    bestMatchCharacter = character;
                    bestMatchingPixels = matchingPixels;
                }
            }

            return new Tuple<float, CharacterDetails, List<Point>>(bestMatchConf, bestMatchCharacter, bestMatchingPixels);
        }

        private Tuple<float, CharacterDetails, List<Point>> FastGuessPartialCharacter(TargetMask targetMask, int lineOffset)
        {
            var targetWidth = targetMask.Width;
            foreach (var group in _scannedCharacters.Where(c => c.Width <= targetWidth + 2).OrderByDescending(c => c.Width).GroupBy(c => c.Width))
            {
                var bestMatch = float.MinValue;
                CharacterDetails bestCharacter = null;
                List<Point> bestMatchingPixels = null;
                foreach (var character in group)
                {
                    //Determine if 80%+ of pixels are covered in target
                    //If so return a new best fit with a conf of pixel coverage
                    var characterPixelsMatched = 0f;
                    var matchingPixels = new List<Point>();
                    for (int x = 0; x < character.Width && x < targetMask.Width; x++)
                    {
                        for (int y = 0; y < character.Height; y++)
                        {
                            if (character.VMask[x, y] && targetMask.Mask[x, y])
                            {
                                characterPixelsMatched += character.WeightMappings[x, y];
                                matchingPixels.Add(new Point(x + targetMask.MinX, y + lineOffset));
                            }
                            else if (x <= targetWidth / 3 && !character.VMask[x, y] && targetMask.Mask[x, y]) //The first few pixels are most important. Punish missing them
                            {
                                characterPixelsMatched--;
                            }
                        }
                    }
                    var coverage = characterPixelsMatched / character.TotalWeights;
                    //Tiny elements have huge variance from random noise. Give them a boost.
                    if (character.TotalWeights < 40)
                        coverage *= 1.2f;
                    if (coverage > bestMatch)
                    {
                        bestMatch = coverage;
                        bestCharacter = character;
                        bestMatchingPixels = matchingPixels;
                    }
                }
                //var coverage = cleanTargetPixels.Where(p => p.X - startX > 0 && p.X - startX < character.Width && character.VMask[p.X - startX, p.Y - lineOffset] > minV).Count() / (float)character.PixelCount;
                if (bestMatch > 0.7)
                {
                    return new Tuple<float, CharacterDetails, List<Point>>(bestMatch, bestCharacter, bestMatchingPixels);
                }
            }
            return null;
        }

        public LineParseResult[] ParseChatImage(string imagePath, int xOffset)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var results = new List<LineParseResult>();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var offsets = _lineOffsets;
                var lineHeight = 36;
                var endLine = offsets.Length;
                var regex = new Regex(@"^\[\d\d:\d\d\]", RegexOptions.Compiled);
                //var results = new string[endLine - startLine];
                for (int i = 0; i < endLine && i < offsets.Length; i++)
                {
                    var line = ParseLineBitmapScan(0.44f, xOffset, converter, chatRect, rgbImage, lineHeight, offsets[i], 6);
                    if (regex.Match(line.RawMessage).Success)
                        results.Add(line);
                    else if (results.Count > 0)
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
