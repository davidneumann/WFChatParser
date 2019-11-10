using Application.ChatMessages.Model;
using Application.Data;
using Application.Interfaces;
using Application.LineParseResult;
using Application.Logger;
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
using WFImageParser.GlyphRecognition;

namespace WFImageParser
{
    public partial class ChatParser : IChatParser
    {
        private GlyphDatabase _glyphDatabase;

        //private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };
        private static readonly List<string> _suffixes = new List<string>();
        private static readonly List<Regex> _blacklistedRegex = new List<Regex>();
        public ChatParser(ILogger logger, string dataDirectory)
        {
            _logger = logger;

            //Load suffixes
            string affixFile = Path.Combine(Application.Data.DataHelper.RivenDataPath, "affixcombos.txt");
            if (Directory.Exists(Application.Data.DataHelper.RivenDataPath) &&
                File.Exists(affixFile))
            {
                foreach (var line in File.ReadAllLines(affixFile))
                {
                    _suffixes.Add(line.Trim() + ']');
                }
            }

            //Load blacklists
            if (File.Exists(Path.Combine(DataHelper.OcrDataPathEnglish, @"MessageBlacklists.txt")))
            {
                foreach (var line in File.ReadAllLines(Path.Combine(DataHelper.OcrDataPathEnglish, @"MessageBlacklists.txt")))
                {
                    _blacklistedRegex.Add(new Regex(line, RegexOptions.Compiled));
                }
            }

            if (!Directory.Exists(dataDirectory))
                throw new FileNotFoundException("Missing data directory", dataDirectory);
            _glyphDatabase = new GlyphDatabase(dataDirectory);
        }

        //private int[] _lineOffsets = new int[] { 5, 55, 105, 154, 204, 253, 303, 352, 402, 452, 501, 551, 600, 650, 700, 749, 799, 848, 898, 948, 997, 1047, 1096, 1146, 1195, 1245, 1295 };
        //private int[] _lineOffsetsSmall = new int[] { 737, 776, 815, 853, 892, 931, 970, 1009, 1048, 1087, 1125, 1164, 1203, 1242, 1280, 1320, 1359, 1397, 1436, 1475, 1514, 1553, 1592, 1631, 1669, 1708, 1747, 1786, 1825, 1864, 1903, 1942, 1980, 2019, 2058 };

        ////                                                 NO                                     No                            NO                           
        //private int[] _lineOffsets = new int[] { 768, 818, 867, 917, 967, 1016, 1066, 1115, 1165, 1214, 1264, 1314, 1363, 1413, 1462, 1512, 1562, 1611,
        //    //    NO          NO                            NO
        //    1661, 1711, 1760, 1809, 1859, 1909, 1958, 2008, 2057 };
        private int[] _lineOffsets = new int[] { 768, 818, 868, 917, 967, 1016, 1066, 1115, 1165, 1215, 1264, 1314, 1363, 1413, 1463, 1512, 1562, 1611, 1661, 1711, 1760, 1810, 1859, 1909, 1958, 2008, 2058 };


        private Queue<string> _sentItems = new Queue<string>();
        private ILogger _logger;

        private BaseLineParseResult ParseLineBitmapScan(ImageCache image, float minV, int xOffset, Rectangle chatRect, int lineHeight, int lineOffset, float spaceWidth, LineType prevLineType)
        {
            BaseLineParseResult result = null;
            var rawMessage = new System.Text.StringBuilder();
            var message = new StringBuilder();
            var startX = xOffset;
            var endX = xOffset;
            var lastCharacterEndX = startX;
            var prevMatchedCharacters = new CoordinateList();
            TargetMask prevTargetMask = null;
            GlyphDetails lastCharacterDetails = null;
            var wordStartX = -1;
            var currentWord = new StringBuilder();
            List<ClickPoint> clickPoints = new List<ClickPoint>();
            var currentLineType = LineType.Unknown;
            var checkedKey = false;
            for (int x = xOffset; x < chatRect.Right; x++)
            {
                //Advance until next pixel
                System.Drawing.Point firstPixel = GetFirstPixel(image, chatRect.Right, lineHeight, lineOffset, endX, prevMatchedCharacters);
                x = firstPixel.X;

                //Make sure we didn't escape
                if (x >= chatRect.Right || firstPixel == System.Drawing.Point.Empty)
                    break;


                //if (startX >= 98 && lineOffset >= 1779)
                //    System.Diagnostics.Debugger.Break();

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, image, prevMatchedCharacters, chatRect.Left, chatRect.Right, lineOffset, lineOffset + lineHeight);

                if (currentLineType == LineType.Unknown)
                {
                    Point maxPoint = new Point(-1);
                    var maxPointV = float.MinValue;
                    for (int x2 = 0; x2 < targetMask.Width; x2++)
                    {
                        for (int y2 = 0; y2 < lineHeight; y2++)
                        {
                            if (targetMask.SoftMask[x2, y2] > maxPointV)
                            {
                                maxPoint = new Point(x2 + targetMask.MinX, y2 + lineOffset);
                                maxPointV = targetMask.SoftMask[x2, y2];
                            }
                        }
                    }

                    var color = image.GetColor(maxPoint.X, maxPoint.Y);
                    if (color == ImageCache.ChatColor.Ignored)
                    {
                        _logger.Log("Ignored color detected while parsing chat line");
                        return null;
                    }
                    //if (color == ImageCache.ChatColor.Redtext)
                    //{
                    //    result = new RedtextLineResult();
                    //    currentLineType = LineType.RedText;
                    //}
                    if (color == ImageCache.ChatColor.Text || color == ImageCache.ChatColor.ItemLink)
                    {
                        result = new ChatMessageLineResult();
                        currentLineType = LineType.Continuation;
                    }
                    else if (color == ImageCache.ChatColor.ChatTimestampName)
                    {
                        result = new ChatMessageLineResult();
                        currentLineType = LineType.NewMessage;
                    }

                    if (currentLineType == LineType.Continuation && !(prevLineType == LineType.Continuation || prevLineType == LineType.NewMessage))
                    {
                        _logger.Log("Chat box parser aborted contiuation parsing early due to invalid prev type");
                        return null;
                    }
                }

                RemoveSeenPixels(image, minV, ref chatRect, lineHeight, lineOffset, prevMatchedCharacters, ref wordStartX, ref x, ref firstPixel, ref targetMask);

                startX = targetMask.MinX;
                endX = targetMask.MaxX + 1;

                if (endX > startX && targetMask.PixelCount > 7)
                {
                    Tuple<float, GlyphDetails, CoordinateList> bestFit = GetBestMatchingCharacter(image, lineHeight, lineOffset, targetMask);

                    if (bestFit != null && bestFit.Item2 != null && endX != lastCharacterEndX)
                    {
                        string name = GetCharacterName(bestFit.Item2.Name);

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
                            var safeName = bestFit.Item2.Name;
                            if (safeName.Contains(","))
                                safeName = safeName.Split(',').First();
                            if (_glyphDatabase.GapPairs.ContainsKey(lastCharacterDetails.Name)
                                && _glyphDatabase.GapPairs[lastCharacterDetails.Name].ContainsKey(safeName)
                                && pixelGap >= _glyphDatabase.GapPairs[lastCharacterDetails.Name][safeName] + spaceWidth)
                            {
                                if (currentLineType == LineType.NewMessage && !checkedKey)
                                {
                                    var clr = (ChatMessageLineResult)result;
                                    if (clr.Timestamp == string.Empty)
                                        ((ChatMessageLineResult)result).Timestamp = currentWord.ToString().Trim();
                                    else if (clr.Username == string.Empty)
                                        ((ChatMessageLineResult)result).Username = currentWord.ToString().Trim().TrimEnd(':');
                                    if (clr.KeyReady())
                                    {
                                        checkedKey = true;
                                        if (_sentItems.Any(i => i == clr.GetKey()))
                                        {
                                            _logger.Log("Chat box parser aborting line parse due to cache hit for timestamp + username");
                                            return null;
                                        }
                                    }
                                }

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
                        if (lastCharacterDetails.Name.Contains(","))
                            lastCharacterDetails.Name = lastCharacterDetails.Name.Split(',').Last();

                        //Due to new char IDing system we can safely jump a bit ahead to prevent double reading
                        if (endX - startX > _glyphDatabase.MaxCharWidth * 0.6 && targetMask.PixelCount - prevMatchedCharacters.Count > targetMask.PixelCount * 0.3)
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

            //Handle messages with no content due to a line wrap
            if (rawMessage.Length <= 0 && result is ChatMessageLineResult)
            {
                var clr = result as ChatMessageLineResult;
                if (clr.Username == null || clr.Username.Length <= 0)
                {
                    ((ChatMessageLineResult)result).Username = currentWord.ToString().Trim().TrimEnd(':');
                }
                rawMessage.Append(" ");
            }

            if (rawMessage.Length > 0)
            {
                AppendSpace(image, lineHeight, lineOffset, rawMessage, message, wordStartX, currentWord, clickPoints);
                result.LineType = currentLineType;
                if (currentLineType == LineType.NewMessage || currentLineType == LineType.Continuation)
                {
                    var clr = result as ChatMessageLineResult;
                    clr.ClickPoints = clickPoints;
                    clr.RawMessage = rawMessage.ToString().Trim();
                    if (currentLineType == LineType.NewMessage)
                    {
                        var enhancedRegex = new Regex(Regex.Escape(clr.Timestamp) + @"\s*" + Regex.Escape(clr.Username) + @"\s*:?\s+(.+)");
                        var matches = enhancedRegex.Match(message.ToString().Trim());
                        clr.EnhancedMessage = matches.Groups[1].Value;
                    }
                    else if (currentLineType == LineType.Continuation)
                    {
                        clr.EnhancedMessage = message.ToString();
                    }
                    //    message.ToString().Trim();
                    //if (clr.EnhancedMessage.StartsWith(clr.Timestamp))
                    //    clr.EnhancedMessage = clr.EnhancedMessage.Substring(clr.Timestamp.Length).Trim(); //Remove timestamp
                    //if (clr.EnhancedMessage.StartsWith(clr.Username) && clr.EnhancedMessage.Length > clr.Username.Length + 1)
                    //    clr.EnhancedMessage = clr.EnhancedMessage.Substring(clr.Username.Length + 1).Trim();//Remove username and :
                    return result;
                }
                else
                {
                    result.RawMessage = rawMessage.ToString().Trim();
                    return result;
                }
            }
            else return null;
        }

        private static string GetCharacterName(string encodedName)//Tuple<float, GlyphDetails, CoordinateList> bestFit)
        {
            encodedName = encodedName.Replace(".png", "").Replace(".txt", "").Replace("alt_", "");
            if (encodedName.Contains(","))
            {
                return encodedName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Aggregate("", (acc, iter) => acc + GetCharacterName(iter));
            }
            if (encodedName.EndsWith("_upper"))
                encodedName = encodedName.Substring(0, encodedName.IndexOf('_')).ToUpper();
            else if (encodedName.EndsWith("_lower"))
                encodedName = encodedName.Substring(0, encodedName.IndexOf('_')).ToLower();
            if (encodedName == "colon")
                encodedName = ":";
            else if (encodedName == "asterix")
                encodedName = "*";
            else if (encodedName == "gt")
                encodedName = ">";
            else if (encodedName == "lt")
                encodedName = "<";
            else if (encodedName == "backSlash")
                encodedName = "\\";
            else if (encodedName == "question")
                encodedName = "?";
            else if (encodedName == "forwardSlash")
                encodedName = "/";
            else if (encodedName == "pipe")
                encodedName = "|";
            else if (encodedName == "comma")
                encodedName = ",";
            return encodedName;
        }

        private Tuple<float, GlyphDetails, CoordinateList> GetBestMatchingCharacter(ImageCache image, int lineHeight, int lineOffset, TargetMask targetMask)
        {
            var bestFit = FastGuessPartialCharacter(targetMask, lineOffset);
            //Try trimming any, and then to the left of, column with less than 15% of the height worth of pixels 
            if (bestFit == null)
            {
                bestFit = GuessQuickSplitResult(targetMask, lineOffset);
            }
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
                    //Blacklist any column that we matched to except last column
                    var minX = image.Width;
                    var maxX = 0;
                    foreach (var p in bestFit.Item3)
                    {
                        if (p.X > maxX)
                            maxX = p.X;
                        if (p.X < minX)
                            minX = p.X;
                    }
                    for (int x = minX; x < maxX - 1; x++)
                    {
                        for (int y = lineOffset; y < lineOffset + 34; y++)
                        {
                            if (!bestFit.Item3.Any(p => p.X == x && p.Y == y))
                                bestFit.Item3.Add(x, y);
                        }
                    }
                }
            }

            //We can allow loose fits on smaller characters
            if (bestFit != null && bestFit.Item1 < 0.20f && bestFit.Item2 != null)
                bestFit = new Tuple<float, GlyphDetails, CoordinateList>(float.MinValue, null, null);

            return bestFit;
        }

        private Tuple<float, GlyphDetails, CoordinateList> GuessQuickSplitResult(TargetMask targetMask, int lineOffset)
        {
            var lowUseColumn = 0;
            for (int x = (int)(targetMask.Width * 0.66f); x > 0; x--)
            {
                var colCount = 0f;
                for (int y = 0; y < OCRHelpers.LINEHEIGHT; y++)
                {
                    colCount += targetMask.SoftMask[x, y];
                }
                if (colCount < 3f)
                {
                    lowUseColumn = x;
                    break;
                }
            }

            //If we didn't find one then abort
            if (lowUseColumn == 0)
                return null;

            //Take everything up to this column with almost nothing in it
            var width = lowUseColumn;
            var pixelCount = 0;
            var softPixelCount = 0f;
            var boolMask = new bool[width, OCRHelpers.LINEHEIGHT];
            var softMask = new float[width, OCRHelpers.LINEHEIGHT];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < OCRHelpers.LINEHEIGHT; y++)
                {
                    boolMask[x, y] = targetMask.Mask[x, y];
                    softMask[x, y] = targetMask.SoftMask[x, y];
                    if (boolMask[x, y])
                        pixelCount++;
                    softPixelCount += softMask[x, y];
                }
            }

            var quickMask = new TargetMask(boolMask, lowUseColumn, targetMask.MinX, width, pixelCount, softPixelCount, softMask);
            return FastGuessPartialCharacter(quickMask, lineOffset);
        }

        private static void RemoveSeenPixels(ImageCache image, float minV, ref Rectangle chatRect, int lineHeight, int lineOffset, CoordinateList prevMatchedCharacters, ref int wordStartX, ref int x, ref System.Drawing.Point firstPixel, ref TargetMask targetMask)
        {
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
                    break;
                    //This is handled in the ImageCache floor [x,y] getter
                    //for (int y2 = 0; y2 < lineHeight; y2++)
                    //{
                    //    prevMatchedCharacters.Add(new Point(targetMask.MinX + x2, lineOffset + y2));
                    //    didRemove = true;
                    //    newXFocus = x2 + targetMask.MinX;
                    //}
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
                            firstPixel = new System.Drawing.Point(i, y);
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
        }

        internal static System.Drawing.Point GetFirstPixel(ImageCache image, int chatRectRight, int lineHeight, int lineOffset, int startX, CoordinateList prevMatchedCharacters)
        {
            var firstPixel = System.Drawing.Point.Empty;
            for (int i = startX; i < chatRectRight; i++)
            {
                var pixelFound = false;
                for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                {
                    if (image[i, y] > 0f && !prevMatchedCharacters.Any(p => p.X == i && p.Y == y))
                    {
                        pixelFound = true;
                        firstPixel = new System.Drawing.Point(i, y);
                        break;
                    }
                }

                if (pixelFound)
                {
                    break;
                }
            }

            return firstPixel;
        }

        private void AppendSpace(ImageCache image, int lineHeight, int lineOffset, StringBuilder rawMessage, StringBuilder message, int wordStartX, StringBuilder currentWord, List<ClickPoint> clickPoints)
        {
            var foundRiven = CheckNewWordForRiven(lineHeight, lineOffset, wordStartX, currentWord.ToString(), clickPoints, image, message);
            var word = currentWord.ToString() + ' ';
            if (foundRiven)
            {
                var index = word.IndexOf("]");
                word = word.Substring(0, index + 1) + "(" + (clickPoints.Count - 1) + ")" + word.Substring(index + 1);
            }
            message.Append(word);
            currentWord.Clear();
            rawMessage.Append(' ');
        }

        private bool CheckNewWordForRiven(int lineHeight, int lineOffset, int wordStartX, string currentWord, List<ClickPoint> clickPoints, ImageCache image, StringBuilder rawMessage)
        {
            var foundRiven = false;
            var converter = new ColorSpaceConverter();
            if (currentWord.Length > 0 && currentWord[0] != '[' && currentWord.IndexOf(']') > 0)
            {
                var rivenBit = currentWord.Substring(0, currentWord.IndexOf(']') + 1).ToLower();
                if (rivenBit.IndexOf('-') > 0)
                    rivenBit = rivenBit.Substring(rivenBit.IndexOf('-') + 1);
                if (_suffixes.BinarySearch(rivenBit) > 0)
                {
                    Point point = Point.Empty;
                    for (int y = lineOffset + (int)(lineHeight * 0.75f); y > lineOffset && y > 0; y--)
                    {
                        for (int x = wordStartX + _glyphDatabase.MinCharWidth; x < wordStartX + _glyphDatabase.MaxCharWidth && x < image.Width; x++)
                        {
                            var hsvPixel = image.GetHsv(x, y);
                            if (image.GetColor(x, y) == ImageCache.ChatColor.ItemLink)
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
                    {
                        var str = rawMessage.ToString();
                        str = str.Substring(str.LastIndexOf('[') + 1) + currentWord.Substring(0, currentWord.IndexOf(']'));
                        clickPoints.Add(new ClickPoint() { X = point.X, Y = point.Y, Index = clickPoints.Count, RivenName = str });
                    }
                }
            }
            return foundRiven;
        }

        private Tuple<float, GlyphDetails, CoordinateList> FastGuessCharacter(TargetMask targetMask, int lineOffset)
        {
            var targetWidth = targetMask.Width + 2;

            var cannidates = _glyphDatabase.KnownGlyphs;
            //Try to only look at similiar sized characters when we are looking at a medium width character.
            //if (targetWidth <= _glyphDatabase.MaxCharWidth / 2 && targetWidth > Math.Ceiling(_glyphDatabase.MaxCharWidth * 0.15))
            //    cannidates = _glyphDatabase.KnownGlyphs.Where(c => c.Width >= targetWidth * 0.8f && c.Width <= targetWidth * 1.2f).ToList();
            //else if (targetWidth <= Math.Ceiling(_glyphDatabase.MaxCharWidth * 0.15)) //Only smalls
            //    cannidates = _glyphDatabase.KnownGlyphs.Where(c => c.Width <= Math.Ceiling(_glyphDatabase.MaxCharWidth * 0.15)).ToList();
            //Else it will be anything as we may be dealing with a partial match

            var bestMatchConf = 0f;
            GlyphDetails bestMatchCharacter = null;
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

            return new Tuple<float, GlyphDetails, CoordinateList>(bestMatchConf, bestMatchCharacter, bestMatchingPixels);
        }

        private Tuple<float, GlyphDetails, CoordinateList> FastGuessPartialCharacter(TargetMask targetMask, int lineOffset, bool takeLeastBad = false)
        {
            var targetWidth = targetMask.Width;
            var minWidth = takeLeastBad ? 0 : targetWidth * 0.5f;
            var leastBadMatch = float.MinValue;
            GlyphDetails leastBadCharacter = null;
            CoordinateList leastBadMatchingPixels = null;
            IEnumerable<IGrouping<int, GlyphDetails>> possibleGroups =
                _glyphDatabase.KnownGlyphs
                    .Where((c) =>
                    {
                        if (c.Name.Contains(",") && !takeLeastBad)
                            return c.Width >= targetMask.Width - 1 && c.Width <= targetMask.Width + 1;
                        else
                            return c.Width >= minWidth && c.Width <= targetWidth + 2;
                    })
                    .OrderByDescending(c => c.Width)
                    .GroupBy(c => c.Width);
            foreach (var group in possibleGroups)
            {
                var bestMatch = float.MinValue;
                GlyphDetails bestCharacter = null;
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
                            else if (character.Name == "l_lower" && y <= 2 && !targetMask.Mask[x, y])
                                characterPixelsMatched -= 0.5f;
                            else if (character.TotalWeights > 55 && targetMask.Mask[x, y] && !character.VMask[x, y])
                                characterPixelsMatched -= targetMask.SoftMask[x, y] / 2;
                            else if (character.TotalWeights > 55 && character.VMask[x, y] && !targetMask.Mask[x, y])
                                characterPixelsMatched -= character.WeightMappings[x, y] / 4;
                            //else if (character.Width <= 4 && character.VMask[x, y] && !targetMask.Mask[x, y])
                            //    characterPixelsMatched -= Math.Min(1f, character.WeightMappings[x,y] * 1.5f) * (character.Height - y / character.Height);
                            //else if (targetWidth > _glyphDatabase.MaxCharWidth * 0.75 && x <= targetWidth / 3 && !character.VMask[x, y] && targetMask.Mask[x, y]) //The first few pixels are most important. Punish missing them
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
                if (bestMatch > 0.92 || (bestMatch > 0.7 && takeLeastBad))
                {
                    return new Tuple<float, GlyphDetails, CoordinateList>(bestMatch, bestCharacter, bestMatchingPixels);
                }
            }
            if (takeLeastBad && leastBadCharacter != null)
                return new Tuple<float, GlyphDetails, CoordinateList>(leastBadMatch, leastBadCharacter, leastBadMatchingPixels);
            return null;
        }

        public System.Drawing.Bitmap[] ExtractChatLines(System.Drawing.Bitmap fullScreenImage)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var lines = new System.Drawing.Bitmap[_lineOffsets.Length];
            for (int i = 0; i < _lineOffsets.Length; i++)
            {
                lines[i] = new System.Drawing.Bitmap(chatRect.Width, 34);
                for (int x = 0; x < lines[i].Width; x++)
                {
                    for (int y = 0; y < lines[i].Height; y++)
                    {
                        lines[i].SetPixel(x, y, fullScreenImage.GetPixel(chatRect.X + x, _lineOffsets[i] + y));
                    }
                }
            }

            return lines;
        }

        public string GetUsernameFromChatLine(System.Drawing.Bitmap chatLine)
        {
            var chatRect = new Rectangle(0, 0, chatLine.Width, chatLine.Height);
            var startX = 0;
            var endX = 0;
            var lastCharacterEndX = startX;
            var prevMatchedCharacters = new CoordinateList();
            TargetMask prevTargetMask = null;
            GlyphDetails lastCharacterDetails = null;
            var wordStartX = -1;
            var currentWord = new StringBuilder();
            var currentLineType = LineType.Unknown;
            var checkedKey = false;
            var image = new ImageCache(chatLine);
            var lineOffset = 0;
            var lineHeight = chatLine.Height;
            var minV = 0f;
            string timestamp = string.Empty;
            for (int x = startX; x < chatLine.Width; x++)
            {
                //Advance until next pixel
                System.Drawing.Point firstPixel = GetFirstPixel(image, chatRect.Right, 34, 0, endX, prevMatchedCharacters);
                x = firstPixel.X;

                //Make sure we didn't escape
                if (x >= chatRect.Right || firstPixel == System.Drawing.Point.Empty)
                    break;

                var targetMask = OCRHelpers.FindCharacterMask(firstPixel, image, prevMatchedCharacters, chatRect.Left, chatRect.Right, 0, 34);

                if (currentLineType == LineType.Unknown)
                {
                    Point maxPoint = new Point(-1);
                    var maxPointV = float.MinValue;
                    for (int x2 = 0; x2 < targetMask.Width; x2++)
                    {
                        for (int y2 = 0; y2 < lineHeight; y2++)
                        {
                            if (targetMask.SoftMask[x2, y2] > maxPointV)
                            {
                                maxPoint = new Point(x2 + targetMask.MinX, y2 + lineOffset);
                                maxPointV = targetMask.SoftMask[x2, y2];
                            }
                        }
                    }

                    var color = image.GetColor(maxPoint.X, maxPoint.Y);
                    if (color == ImageCache.ChatColor.Ignored)
                    {
                        _logger.Log("Ignored color detected while parsing chat line");
                        return null;
                    }
                    //if (color == ImageCache.ChatColor.Redtext)
                    //{
                    //    result = new RedtextLineResult();
                    //    currentLineType = LineType.RedText;
                    //}
                    if (color == ImageCache.ChatColor.Text || color == ImageCache.ChatColor.ItemLink)
                    {
                        return null;
                    }
                    else if (color == ImageCache.ChatColor.ChatTimestampName)
                    {
                        currentLineType = LineType.NewMessage;
                    }
                }

                RemoveSeenPixels(image, minV, ref chatRect, lineHeight, lineOffset, prevMatchedCharacters, ref wordStartX, ref x, ref firstPixel, ref targetMask);

                startX = targetMask.MinX;
                endX = targetMask.MaxX + 1;

                if (endX > startX && targetMask.PixelCount > 10)
                {
                    Tuple<float, GlyphDetails, CoordinateList> bestFit = GetBestMatchingCharacter(image, lineHeight, lineOffset, targetMask);

                    if (bestFit != null && bestFit.Item2 != null && endX != lastCharacterEndX)
                    {
                        string name = GetCharacterName(bestFit.Item2.Name);

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
                            int spaceWidth = 6;
                            if (_glyphDatabase.GapPairs.ContainsKey(lastCharacterDetails.Name)
                                && _glyphDatabase.GapPairs[lastCharacterDetails.Name].ContainsKey(bestFit.Item2.Name)
                                && pixelGap > _glyphDatabase.GapPairs[lastCharacterDetails.Name][bestFit.Item2.Name] + spaceWidth)
                            {
                                if (currentLineType == LineType.NewMessage)
                                {
                                    if (timestamp == string.Empty)
                                        timestamp = currentWord.ToString().Trim();
                                    else
                                        return currentWord.ToString().Trim().TrimEnd(':');
                                }

                                currentWord.Clear();
                                wordStartX = targetMask.MinX;

                            }
                        }

                        //Add character
                        currentWord.Append(name);

                        lastCharacterEndX = startX + bestFit.Item2.Width - 2;
                        prevMatchedCharacters.AddRange(bestFit.Item3);
                        prevTargetMask = targetMask;
                        lastCharacterDetails = bestFit.Item2;

                        //Due to new char IDing system we can safely jump a bit ahead to prevent double reading
                        if (endX - startX > _glyphDatabase.MaxCharWidth * 0.6 && targetMask.PixelCount - prevMatchedCharacters.Count > targetMask.PixelCount * 0.3)
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

            return null;
        }

        public class UsernameParseResult
        {
            public System.Drawing.Rectangle LineRect;
            public string Timestamp;
            public string Username;

            public void Append(int lineOffset)
            {
                if(LineRect.Bottom + OCRHelpers.LINEHEIGHT > lineOffset)
                {
                    LineRect = new System.Drawing.Rectangle(LineRect.Left, LineRect.Y, LineRect.Width, (lineOffset + OCRHelpers.LINEHEIGHT) - LineRect.Top);
                }
            }

            internal string GetKey()
            {
                return Timestamp + Username;
            }
        }
        public UsernameParseResult[] ParseUsernamesFromChatImage(System.Drawing.Bitmap bitmapImage, bool isScrolledUp)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var results = new List<UsernameParseResult>();
            using (var mem = new MemoryStream())
            {
                bitmapImage.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                mem.Seek(0, SeekOrigin.Begin);
                using (Image<Rgba32> rgbImage = Image.Load(mem))
                {
                    var cache = new ImageCache(rgbImage);
                    var offsets = _lineOffsets;
                    var lineHeight = OCRHelpers.LINEHEIGHT;
                    var endLine = offsets.Length;
                    var newMessageRegex = new Regex(@"^\[\d\d:\d\d\]", RegexOptions.Compiled);
                    var prevLineType = LineType.Unknown;
                    //var results = new string[endLine - startLine];
                    for (int i = 0; i < offsets.Length; i++)
                    {
                        //var line = ParseLineBitmapScan(cache, 0.3f, xOffset, chatRect, lineHeight, offsets[i], 0, prevType);
                        var line = ParseLineBitmapScan(cache, 0f, chatRect.Left, chatRect, lineHeight, offsets[i], 0, prevLineType);

                        if (line == null)
                        {
                            prevLineType = LineType.Unknown;
                            continue;
                        }
                        else
                            prevLineType = line.LineType;

                        // There may be more to this chat message below the current scrolled amount when looking at the final line
                        if (isScrolledUp)
                        {
                            if (i == offsets.Length - 1 && line != null && line.LineType == LineType.Continuation && results.Count > 0)
                            {
                                _logger.Log("Last line in chat box is contiuation. Removing last real message to prevent partial cut off.");
                                var last = results.Last();
                                results.Remove(last);
                            }
                            else if (i == offsets.Length - 1 && line != null && line.LineType == LineType.NewMessage)
                            {
                                _logger.Log("Last line in chat box is a new message. Possible contiuation off screen, not adding.");
                                continue;
                            }
                        }

                        if (i >= endLine && line.LineType != LineType.Continuation)
                            break;

                        //Add new messages
                        if (line.RawMessage != null && line.LineType == LineType.NewMessage)
                        {
                            var clr = line as ChatMessageLineResult;
                            if (clr.Timestamp != string.Empty && clr.Username != string.Empty)
                            {
                                var result = new UsernameParseResult()
                                {
                                    LineRect = new System.Drawing.Rectangle(chatRect.Left, _lineOffsets[i], chatRect.Width, OCRHelpers.LINEHEIGHT),
                                    Timestamp = clr.Timestamp,
                                    Username = clr.Username
                                };
                                results.Add(result);
                            }
                        }
                        //Append continuation of messages onto last message
                        else if (results.Count > 0 && line.RawMessage != null && line.LineType == LineType.Continuation)
                        {
                            var last = results.Last() as UsernameParseResult;
                            _logger.Log("Appending continuation to last message.");
                            last.Append(_lineOffsets[i]);
                        }
                    }
                }
            }

            //Add results to cache
            foreach (var result in results)
            {
                _sentItems.Enqueue(result.GetKey());
            }
            while (_sentItems.Count > 100)
            {
                _sentItems.Dequeue();
            }

            return results.ToArray();
        }
        public BaseLineParseResult[] ParseChatImage(System.Drawing.Bitmap bitmapImage, int xOffset, bool useCache, bool isScrolledUp, int lineParseCount = 27)
        {
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            var results = new List<BaseLineParseResult>();
            using (var mem = new MemoryStream())
            {
                bitmapImage.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                mem.Seek(0, SeekOrigin.Begin);
                using (Image<Rgba32> rgbImage = Image.Load(mem))
                {
                    var cache = new ImageCache(rgbImage);
                    var offsets = _lineOffsets;
                    var lineHeight = 34;
                    var endLine = Math.Min(offsets.Length, lineParseCount);
                    var newMessageRegex = new Regex(@"^\[\d\d:\d\d\]", RegexOptions.Compiled);
                    var kickRegex = new Regex(@"\w was kicked.", RegexOptions.Compiled);
                    var prevType = LineType.Unknown;
                    //var results = new string[endLine - startLine];
                    for (int i = 0; i < offsets.Length; i++)
                    {
                        //var line = ParseLineBitmapScan(cache, 0.3f, xOffset, chatRect, lineHeight, offsets[i], 0, prevType);
                        var line = ParseLineBitmapScan(cache, 0f, xOffset, chatRect, lineHeight, offsets[i], 0, prevType);

                        //Handle null results
                        if (line != null)
                            prevType = line.LineType;
                        else
                        {
                            _logger.Log("Invalid line found in chat box parsing. Skipping");
                            prevType = LineType.Unknown;
                            continue;
                        }

                        // There may be more to this chat message below the current scrolled amount when looking at the final line
                        if (isScrolledUp)
                        {
                            if (i == offsets.Length - 1 && line != null && line.LineType == LineType.Continuation && results.Count > 0)
                            {
                                _logger.Log("Last line in chat box is contiuation. Removing last real message to prevent partial cut off.");
                                var last = results.Last();
                                results.Remove(last);
                            }
                            else if (i == offsets.Length - 1 && line != null && line.LineType == LineType.NewMessage)
                            {
                                _logger.Log("Last line in chat box is a new message. Possible contiuation off screen, not adding.");
                                continue;
                            }
                        }

                        if (i >= endLine && line.LineType != LineType.Continuation)
                            break;

                        //Reset the known type when we hit a kick message to prevent accidently combining messages
                        if (line != null && line.RawMessage != null && line.RawMessage.Length > 0 && kickRegex.Match(line.RawMessage).Success)
                        {
                            prevType = LineType.Unknown;
                            continue;
                        }

                        //Add new messages
                        if (line.RawMessage != null && line.LineType == LineType.NewMessage)
                        {
                            var clr = line as ChatMessageLineResult;
                            if (clr.Timestamp != string.Empty && clr.Username != string.Empty)
                            {
                                results.Add(line);
                            }
                        }
                        //Append continuation of messages onto last message
                        else if (results.Count > 0 && line.RawMessage != null && line.LineType == LineType.Continuation
                            && !_blacklistedRegex.Any(regex => regex.Match(line.RawMessage).Success))
                        {
                            var last = results.Last() as ChatMessageLineResult;
                            _logger.Log("Appending continuation to last message. Last: " + last.RawMessage + " cont: " + line.RawMessage);
                            last.Append(line as ChatMessageLineResult);
                        }
                    }
                }
            }

            //Add results to cache
            foreach (var result in results)
            {
                _sentItems.Enqueue(result.GetKey());
            }
            while (_sentItems.Count > 100)
            {
                _sentItems.Dequeue();
            }

            return results.ToArray();
        }

        public string[] ParseRivenImage(string imagePath)
        {
            throw new NotImplementedException();
        }

        public BaseLineParseResult[] ParseChatImage(System.Drawing.Bitmap image)
        {
            return ParseChatImage(image, 3, true, false);
        }

        public BaseLineParseResult[] ParseChatImage(System.Drawing.Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            return ParseChatImage(image, 3, useCache, isScrolledUp, lineParseCount);
        }

        public bool IsScrollbarPresent(System.Drawing.Bitmap screenImage)
        {
            if (screenImage.Width != 4096 || screenImage.Height != 2160)
                return false;

            var threshold = (byte)252;
            for (int y = 2097; y > 655; y--)
            {
                var pixel = screenImage.GetPixel(3256, y);
                if (pixel.R > threshold && pixel.G > threshold && pixel.B > threshold)
                    return true;
            }

            return false;
        }

        public bool IsChatFocused(System.Drawing.Bitmap chatIconBitmap)
        {
            var darkPixels = new Point[] { new Point(23, 15), new Point(30, 35), new Point(37, 15), new Point(43, 35) };
            var lightPixles = new Point[] { new Point(17, 25), new Point(24, 12), new Point(26, 19), new Point(32, 24), new Point(40, 32), new Point(30, 43) };
            if (darkPixels.Any(p =>
            {
                var pixel = chatIconBitmap.GetPixel(p.X, p.Y);
                if (pixel.R > 100 || pixel.G > 100 || pixel.G > 100)
                    return true;
                return false;
            }))
                return false;
            if (lightPixles.Any(p =>
            {
                var pixel = chatIconBitmap.GetPixel(p.X, p.Y);
                if (pixel.R < 180 || pixel.G < 180 || pixel.G < 180)
                    return true;
                return false;
            }))
                return false;
            return true;
        }

        public void InvalidCache(string key)
        {
            var duplicateCache = new Queue<string>(_sentItems.Where(i => i != key));
            _sentItems = duplicateCache;
        }
    }
}
