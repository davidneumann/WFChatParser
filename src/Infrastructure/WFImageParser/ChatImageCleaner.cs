using Application.Interfaces;
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
using System.Threading.Tasks;

namespace WFImageParser
{
    public class ChatImageCleaner : IChatImageProcessor
    {
        public ChatImageCleaner(CharInfo[] knownCharacters)
        {
            _knownCharacters = knownCharacters;
        }

        private List<CharacterDetails> _scannedCharacters = new List<CharacterDetails>();

        public ChatImageCleaner()
        {
            var converter = new ColorSpaceConverter();
            if (Directory.Exists("final"))
            {
                foreach (var file in Directory.GetFiles("final").Where(f => f.EndsWith(".png")))
                {
                    if (File.Exists(file + ".txt"))
                    {
                        var character = new CharacterDetails()
                        {
                            Name = (new FileInfo(file)).Name.Replace(".png", ""),
                            PixelCount = Int32.Parse(File.ReadAllText(file + ".txt"))
                        };
                        var image = Image.Load(file);
                        character.VMask = new float[image.Width, image.Height];
                        for (int x = 0; x < image.Width; x++)
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                character.VMask[x, y] = converter.ToHsv(image[x, y]).V;
                            }
                        }
                        character.Width = image.Width;
                        character.Height = image.Height;
                        _scannedCharacters.Add(character);
                    }
                }
            }
        }
        /// <summary>
        /// Converts the full color game window into a image of the chat window in grayscale.
        /// </summary>
        /// <param name="imagePath">The path to the game screenshot</param>
        /// <param name="outputDirectory">The directory to save the processed image</param>
        /// <returns>The full path to the processed image</returns>
        public string ProcessChatImage(string imagePath, string outputDirectory)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 893, 3236, 1350);
            //Image.Load(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screenshot (7).png")
            //@"C:\Users\david\OneDrive\Documents\WFChatParser\friday_last_moved.png"
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                if (rgbImage.Height == 1440)
                    chatRect = new Rectangle(4, 530, 2022, 850);
                else if (rgbImage.Height == 1080)
                    chatRect = new Rectangle(4, 370, 1507, 650);

                rgbImage.Mutate(x => x.Crop(chatRect));

                var minV = 0.29;

                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                var file = new FileInfo(imagePath);
                var outputPath = Path.Combine(outputDirectory, file.Name);
                rgbImage.Save(outputPath);
                return outputPath;
            }
        }

        public void AverageBitmaps(Dictionary<string, List<string>> files, float minV, bool smallText = true)
        {
            var converter = new ColorSpaceConverter();
            if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "final")))
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "final"));

            foreach (var character in files.Keys)
            {
                var size = 26;
                if (!smallText)
                    size = 36;
                using (Image<Rgba32> averageImage = new Image<Rgba32>(new Configuration(), size, size, Rgba32.Black))
                {
                    foreach (var file in files[character])
                    {
                        using (Image<Rgba32> image = Image.Load(file))
                        {
                            if (image.Width != averageImage.Width)
                            {
                                averageImage.Mutate(i => i.Resize(image.Width, image.Height));
                            }

                            for (int x = 0; x < image.Width; x++)
                            {
                                for (int y = 0; y < image.Height; y++)
                                {
                                    byte newR = (byte)(averageImage[x, y].R + 1);
                                    if (converter.ToHsv(image[x, y]).V > minV)
                                        averageImage[x, y] = new Rgba32(newR, newR, newR);
                                }
                            }
                        }
                    }

                    using (Image<Rgba32> finalImage = new Image<Rgba32>(averageImage.Width, averageImage.Height))
                    {
                        var validCount = 0;
                        for (int x = 0; x < finalImage.Width; x++)
                        {
                            for (int y = 0; y < finalImage.Height; y++)
                            {
                                if (averageImage[x, y].R >= (float)files[character].Count * 0.5)
                                {
                                    finalImage[x, y] = Rgba32.White;
                                    validCount++;
                                }
                                else
                                    finalImage[x, y] = Rgba32.Black;
                            }
                        }
                        finalImage.Save(Path.Combine(Environment.CurrentDirectory, "final", character));
                        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "final", character + ".txt"), validCount.ToString());
                    }
                }
            }
        }

        //private int[] _lineOffsets = new int[] { 5, 55, 105, 154, 204, 253, 303, 352, 402, 452, 501, 551, 600, 650, 700, 749, 799, 848, 898, 948, 997, 1047, 1096, 1146, 1195, 1245, 1295 };
        private int[] _lineOffsetsSmall = new int[] { 737, 776, 815, 853, 892, 931, 970, 1009, 1048, 1087, 1125, 1164, 1203, 1242, 1280, 1320, 1359, 1397, 1436, 1475, 1514, 1553, 1592, 1631, 1669, 1708, 1747, 1786, 1825, 1864, 1903, 1942, 1980, 2019, 2058 };
        private int[] _lineOffsets = new int[] { 768, 818, 868, 917, 967, 1016, 1066, 1115, 1165, 1215, 1264, 1314, 1363, 1413, 1463, 1512, 1562, 1611, 1661, 1711, 1760, 1810, 1859, 1909, 1958, 2008, 2058 };

        private readonly CharInfo[] _knownCharacters;

        public string[] ConvertScreenshotToChatText(string imagePath, float minV, int xOffset = 0, bool useSmallLocations = true, int startLine = 0, int endLine = int.MaxValue)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                //var maxHsv = 0.29;
                var allText = new List<string>();
                var offsets = useSmallLocations ? _lineOffsetsSmall : _lineOffsets;
                var lineHeight = useSmallLocations ? 26 : 36;
                for (int i = startLine; i < endLine && i < offsets.Length; i++)
                {
                    ParseLine(minV, xOffset, converter, chatRect, rgbImage, allText, lineHeight, offsets[i]);
                }

                return allText.ToArray();
            }
        }

        public string[] ConvertScreenshotToChatTextWithBitmap(string imagePath, float minV, int xOffset = 0, int startLine = 0, int endLine = int.MaxValue, bool smallText = true)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            if (!smallText)
                chatRect = chatRect;
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                //var maxHsv = 0.29;
                var allText = new List<string>();
                var offsets = smallText ? _lineOffsetsSmall : _lineOffsets;
                var lineHeight = 26;
                if (!smallText)
                    lineHeight = 36;
                for (int i = startLine; i < endLine && i < offsets.Length; i++)
                {
                    ParseLineBitmapScan(minV, xOffset, converter, chatRect, rgbImage, allText, lineHeight, offsets[i]);
                }

                return allText.ToArray();
            }
        }

        private void ParseLineBitmapScan(float minV, int xOffset, ColorSpaceConverter converter, Rectangle chatRect, Image<Rgba32> rgbImage, List<string> allText, int lineHeight, int lineOffset)
        {
            var sb = new System.Text.StringBuilder();
            var emptySlices = 0;
            var onSpaceChar = false;
            for (int x = xOffset; x < chatRect.Right; x++)
            {
                var pixelFound = false;
                for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                {
                    if (converter.ToHsv(rgbImage[x, y]).V > minV)
                    {
                        pixelFound = true;
                        break;
                    }
                }

                if (!pixelFound)
                {
                    if (++emptySlices >= 4 && !onSpaceChar)
                    {
                        sb.Append(" ");
                        onSpaceChar = true;
                    }
                    continue;
                }
                else
                {
                    emptySlices = 0;
                    onSpaceChar = false;
                }


                var startX = x;
                var endX = startX + 1;
                while (true)
                {
                    var foundNeighbor = false;
                    for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                    {
                        if (converter.ToHsv(rgbImage[endX, y]).V > minV)
                        {
                            //look around to see if any valid pixels are here

                            if (foundNeighbor)
                                break;

                            for (int y2 = y - 1; y2 <= y + 1; y2++)
                            {
                                if (converter.ToHsv(rgbImage[endX - 1, y2]).V > minV)
                                {
                                    endX++;
                                    foundNeighbor = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!foundNeighbor)
                    {
                        break;
                    }
                }

                if (endX > startX)
                {
                    //using (Image<Rgba32> debug1 = rgbImage.Clone())
                    //{
                    //    debug1.Mutate(i => i.Crop(new Rectangle(startX, lineOffset, endX - startX, lineHeight)));
                    //    debug1.Save("test_target.png");
                    //}

                    var targetPixels = 0;
                    for (int x2 = startX; x2 < endX; x2++)
                    {
                        for (int y = lineOffset; y < lineHeight + lineOffset; y++)
                        {
                            if (converter.ToHsv(rgbImage[x2, y]).V > minV)
                            {
                                targetPixels++;
                            }
                        }
                    }


                    //Console.WriteLine("Target: " + targetPixels);
                    //Console.WriteLine("{0,12} {1,12} {2,12} {3,12}", "Name", "PixelCont", "Match", "Confi");

                    var bestFit = new Tuple<float, CharacterDetails>(float.MinValue, null);
                    var targetWidth = endX - startX;
                    foreach (var scannedCharacter in _scannedCharacters.Where(c => c.Width >= targetWidth -1 && c.Width <= targetWidth + 1).ToArray())
                    {
                        CharacterDetails details = null;
                        if (scannedCharacter.Width == targetWidth)
                            details = scannedCharacter;
                        else
                        {
                            using (Image<Rgba32> resizedScan = new Image<Rgba32>(scannedCharacter.Width, scannedCharacter.Height))
                            {
                                for (int x2 = 0; x2 < scannedCharacter.Width; x2++)
                                {
                                    for (int y = 0; y < scannedCharacter.Height; y++)
                                    {
                                        resizedScan[x2, y] = scannedCharacter.VMask[x2, y] > minV ? Rgba32.White : Rgba32.Black;
                                    }
                                }
                                resizedScan.Mutate(i => i.Resize(targetWidth, lineHeight));
                                details = new CharacterDetails()
                                {
                                    Height = lineHeight,
                                    Name = scannedCharacter.Name,
                                    Width = targetWidth
                                };
                                details.PixelCount = 0;
                                details.VMask = new float[targetWidth, lineHeight];
                                for (int x2 = 0; x2 < targetWidth; x2++)
                                {
                                    for (int y = 0; y < lineHeight; y++)
                                    {
                                        if (resizedScan[x2, y].R > 128)
                                        {
                                            details.PixelCount++;
                                            details.VMask[x2, y] = 1f;
                                        }
                                        else
                                            details.VMask[x2, y] = 0f;                                        
                                    }
                                }
                            }
                        }
                        //using (Image<Rgba32> debug2 = new Image<Rgba32>(details.Width, details.Height))
                        //{
                        //    for (int x2 = 0; x2 < details.Width; x2++)
                        //    {
                        //        for (int y = 0; y < details.Height; y++)
                        //        {
                        //            if (details.VMask[x2, y] > minV)
                        //                debug2[x2, y] = Rgba32.White;
                        //            else
                        //                debug2[x2, y] = Rgba32.Black;
                        //        }
                        //    }
                        //    debug2.Save("test_reference.png");
                        //}

                        var matchingPixels = 0;
                        var matchingPixelsMask = new int[details.Width, details.Height];
                        for (int x2 = startX; x2 < endX; x2++)
                        {
                            for (int y2 = 0; y2 < details.Height; y2++)
                            {
                                var pV = converter.ToHsv(rgbImage[x2, y2 + lineOffset]).V;
                                var dV = details.VMask[x2 - startX, y2];
                                if ((pV > minV && dV > minV))
                                {
                                    matchingPixels++;
                                    matchingPixelsMask[x2 - startX, y2] = 1;
                                }
                                else
                                {
                                    matchingPixelsMask[x2 - startX, y2] = 0;
                                }
                            }
                        }

                        //using (Image<Rgba32> debug2 = new Image<Rgba32>(details.Width, details.Height))
                        //{
                        //    for (int x2 = 0; x2 < details.Width; x2++)
                        //    {
                        //        for (int y = 0; y < details.Height; y++)
                        //        {
                        //            if (matchingPixelsMask[x2, y] > 0)
                        //                debug2[x2, y] = Rgba32.White;
                        //            else
                        //                debug2[x2, y] = Rgba32.Black;
                        //        }
                        //    }
                        //    debug2.Save("test_matching.png");
                        //}

                        
                        var conf = (float)matchingPixels / (float)(Math.Max(targetPixels, details.PixelCount));
                        //Console.WriteLine("{0,12} {1,12} {2,12} {3,12}", details.Name, details.PixelCount, matchingPixels, conf);
                        if (conf > 0.66f && conf > bestFit.Item1)
                        {
                            bestFit = new Tuple<float, CharacterDetails>(conf, details);
                        }
                    }

                    if (bestFit.Item2 != null)
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

                        sb.Append(name);
                        x = endX;
                    }
                }
            }
            allText.Add(sb.ToString().Trim());
        }
        private string ParseLine(float minV, int xOffset, ColorSpaceConverter converter, Rectangle chatRect, Image<Rgba32> rgbImage, List<string> allText, int lineHeight, int lineOffset)
        {
            var hitPoints = new int[36];
            var hitPointCount = 0;
            var chars = new System.Text.StringBuilder();
            IEnumerable<CharInfo> possibleChars = null;
            var sliceIndex = 0;
            for (int x = chatRect.Left + xOffset; x < chatRect.Right; x++)
            {
                hitPointCount = 0;
                for (int y = lineOffset; y < lineOffset + lineHeight; y++)
                {
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        hitPoints[hitPointCount++] = y - lineOffset;
                    }
                }

                if (hitPointCount > 0)
                {
                    IEnumerable<CharInfo> newPossibleChars = null;
                    if (possibleChars == null)
                        newPossibleChars = _knownCharacters.Where(c => DoesCharactersMatch(sliceIndex, hitPoints, hitPointCount, c)).ToList();
                    else
                        newPossibleChars = possibleChars.Where(c => DoesCharactersMatch(sliceIndex, hitPoints, hitPointCount, c)).ToList();

                    if (newPossibleChars.Count() > 1)
                    {
                        sliceIndex++;
                        possibleChars = newPossibleChars;
                    }
                    else if (newPossibleChars.Count() == 1)
                    {
                        sliceIndex = 0;
                        var character = newPossibleChars.Single();
                        chars.Append(character.Character);
                        x += character.Width - 1;
                    }
                }
                else
                {
                    possibleChars = null;
                    sliceIndex = 0;
                }
            }
            allText.Add(chars.ToString());
            return chars.ToString();
        }

        public void SaveGreyscaleImage(string imagePath, string outputPath, float minV = 0.29f)
        {
            var converter = new ColorSpaceConverter();

            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                {
                    var x = i % rgbImage.Width;
                    var y = i / rgbImage.Width;
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        rgbImage[x, y] = Rgba32.Black;
                    }
                    else
                        rgbImage[x, y] = Rgba32.White;
                }

                rgbImage.Save(outputPath);
            }
        }

        public CharInfo[] AnalyzeInput(string imagePath, string referenceChars, float minV, int xOffset = 0)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                //var maxHsv = 0.29;
                var hitPoints = new int[36];
                var hitPointCount = 0;
                var onChar = false;
                var charInfos = new List<CharInfo>();
                CharInfo currentChar = null;
                for (int x = xOffset; x < rgbImage.Width; x++)
                {
                    hitPointCount = 0;
                    for (int y = 0; y < rgbImage.Height; y++)
                    {
                        var pixel = rgbImage[x, y];
                        var hsvPixel = converter.ToHsv(pixel);
                        if (hsvPixel.V > minV)
                        {
                            hitPoints[hitPointCount++] = y;
                        }
                    }

                    if (hitPointCount > 0)
                    {
                        if (!onChar)
                        {
                            onChar = true;
                            currentChar = new CharInfo();
                            if (charInfos.Count == referenceChars.Length)
                                return new CharInfo[0];
                            currentChar.Character = referenceChars[charInfos.Count];
                        }
                        var cleanPoints = new int[hitPointCount];
                        for (int i = 0; i < hitPointCount; i++)
                        {
                            cleanPoints[i] = hitPoints[i];
                        }
                        currentChar.Slices.Add(new List<int>(cleanPoints));
                        currentChar.Width++;
                    }
                    if (onChar && hitPointCount == 0)
                    {
                        charInfos.Add(currentChar);
                        currentChar = null;
                        onChar = false;
                    }
                }

                return charInfos.ToArray();
            }
        }

        private CharInfo[] AnalyzeInputSmall(int xOffset, Image<Rgba32> rgbImage, float minV, string referenceChars)
        {
            var converter = new ColorSpaceConverter();
            var charInfos = new List<CharInfo>();
            //var maxHsv = 0.29;
            var hitPoints = new int[26];
            var hitPointCount = 0;
            var onChar = false;
            CharInfo currentChar = null;
            for (int x = xOffset; x < rgbImage.Width; x++)
            {
                hitPointCount = 0;
                for (int y = 0; y < rgbImage.Height; y++)
                {
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        hitPoints[hitPointCount++] = y;
                    }
                }

                if (hitPointCount > 0)
                {
                    if (!onChar)
                    {
                        onChar = true;
                        currentChar = new CharInfo();
                        if (charInfos.Count == referenceChars.Length)
                            return new CharInfo[0];
                        currentChar.Character = referenceChars[charInfos.Count];
                    }
                    var cleanPoints = new int[hitPointCount];
                    for (int i = 0; i < hitPointCount; i++)
                    {
                        cleanPoints[i] = hitPoints[i];
                    }
                    currentChar.Slices.Add(new List<int>(cleanPoints));
                    currentChar.Width++;
                }
                if (onChar && hitPointCount == 0)
                {
                    charInfos.Add(currentChar);
                    currentChar = null;
                    onChar = false;
                }
            }

            return charInfos.ToArray();
        }
        public CharInfo[] AnalyzeInputSmall(string imagePath, string referenceChars, float minV, int xOffset = 0)
        {
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                return AnalyzeInputSmall(xOffset, rgbImage, minV, referenceChars);
            }
        }

        public CharInfo[] AnalyzeInputSmallFromScreenshot(string imagePath, string referenceChars, float minV, int xOffset, int lineNumber)
        {
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var newImage = rgbImage.Clone();
                newImage.Mutate(i => i.Crop(new Rectangle(4, _lineOffsetsSmall[lineNumber], 3244, 26)));
                newImage.Save("test.png");
                SaveGreyscaleImage("test.png", "test2.png", minV);
                return AnalyzeInputSmall(xOffset, newImage, minV, referenceChars);
            }
        }

        public void MakeBitmapDictionary(string imagePath, string referenceChars, float minV, int xOffset, int lineNumber, int directoryOffset, bool smallText = true)
        {
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var newImage = rgbImage.Clone();
                if (smallText)
                {
                    newImage.Mutate(i => i.Crop(new Rectangle(4, _lineOffsetsSmall[lineNumber], 3244, 26)));
                }
                else
                    newImage.Mutate(i => i.Crop(new Rectangle(4, _lineOffsets[lineNumber], 3244, 36)));
                newImage.Save("test.png");
                SaveGreyscaleImage("test.png", "test2.png", minV);
                MakeBitmapsForLine(xOffset, newImage, minV, referenceChars, lineNumber, directoryOffset, smallText);
            }
        }

        private void MakeBitmapsForLine(int xOffset, Image<Rgba32> rgbImage, float minV, string referenceChars, int lineNumber, int directoryOffset, bool smallText = true)
        {
            if (!Directory.Exists("line_" + (lineNumber + directoryOffset)))
                Directory.CreateDirectory("line_" + (lineNumber + directoryOffset));

            var converter = new ColorSpaceConverter();
            //var maxHsv = 0.29;
            var hitPointCount = 0;
            var onChar = false;
            var startX = xOffset;
            var refIndex = 0;
            var usedNames = new List<string>();
            for (int x = xOffset; x < rgbImage.Width; x++)
            {
                hitPointCount = 0;
                for (int y = 0; y < rgbImage.Height; y++)
                {
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > minV)
                    {
                        hitPointCount++;
                        rgbImage[x, y] = Rgba32.White;
                    }
                    else
                        rgbImage[x, y] = Rgba32.Black;
                }

                if (hitPointCount > 0)
                {
                    if (!onChar)
                    {
                        onChar = true;
                        startX = x;
                    }
                }
                if (onChar && hitPointCount == 0)
                {
                    using (var clone = rgbImage.Clone())
                    {
                        if (smallText)
                            clone.Mutate(i => i.Crop(new Rectangle(startX, 0, x - startX, 26)));
                        else
                            clone.Mutate(i => i.Crop(new Rectangle(startX, 0, x - startX, 36)));
                        var name = (referenceChars[refIndex++] + "");
                        if (name.ToUpper() != name.ToLower() && name.ToUpper() == name)
                            name = name + "_upper";
                        else if (name.ToUpper() != name.ToLower() && name.ToLower() == name)
                            name = name + "_lower";
                        if (name == ":")
                            name = "colon";
                        else if (name == "*")
                            name = "asterix";
                        else if (name == ">")
                            name = "gt";
                        else if (name == "<")
                            name = "lt";
                        else if (name == "\\")
                            name = "backSlash";
                        else if (name == "?")
                            name = "question";
                        else if (name == "/")
                            name = "forwardSlash";
                        else if (name == "|")
                            name = "pipe";
                        else if (name == "," || name[0] == ',')
                            name = "comma";
                        if (usedNames.Contains(name))
                            name = "alt_" + name;
                        usedNames.Add(name);
                        clone.Save(Path.Combine("line_" + (lineNumber + directoryOffset), name + ".png"));
                    }
                    onChar = false;
                }
            }
        }

        public string VerifyInput(string imagePath, float minV, int xOffset = 0)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var hitPoints = new int[36];
                var hitPointCount = 0;
                var chars = new System.Text.StringBuilder();
                IEnumerable<CharInfo> possibleChars = null;
                var sliceIndex = 0;
                for (int x = xOffset; x < rgbImage.Width; x++)
                {
                    hitPointCount = 0;
                    for (int y = 0; y < rgbImage.Height; y++)
                    {
                        var pixel = rgbImage[x, y];
                        var hsvPixel = converter.ToHsv(pixel);
                        if (hsvPixel.V > minV)
                        {
                            hitPoints[hitPointCount++] = y;
                        }
                    }

                    if (hitPointCount > 0)
                    {
                        IEnumerable<CharInfo> newPossibleChars = null;
                        if (possibleChars == null)
                            newPossibleChars = _knownCharacters.Where(c => DoesCharactersMatch(0, hitPoints, hitPointCount, c)).ToList();
                        else
                            newPossibleChars = possibleChars.Where(c => DoesCharactersMatch(sliceIndex, hitPoints, hitPointCount, c)).ToList();

                        if (newPossibleChars.Count() > 1)
                        {
                            possibleChars = newPossibleChars;
                            sliceIndex++;
                        }
                        else if (newPossibleChars.Count() == 1)
                        {
                            sliceIndex = 0;
                            var character = newPossibleChars.Single();
                            chars.Append(character.Character);
                            x += character.Width - 1;
                        }
                    }
                    else
                        possibleChars = null;
                }
                return chars.ToString();
            }
        }
        public void FindLineCoords(string imagePath, float v = 0.35f, bool smallText = true)
        {
            var converter = new ColorSpaceConverter();
            var locations = new List<int>();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                rgbImage.Save(imagePath + "_extra.png");
                var chatRect = new Rectangle(6, 737, 3249, 1362);
                if (!smallText)
                    chatRect = new Rectangle(3, 768, 3250, 1332);

                var onChar = false;
                var x = smallText ? 8 : 7;
                for (int y = chatRect.Top; y < chatRect.Bottom; y++)
                {
                    var pixel = rgbImage[x, y];
                    var hsvPixel = converter.ToHsv(pixel);
                    if (hsvPixel.V > v && !onChar)
                    {
                        onChar = true;
                        var realY = smallText ? y - 2 : y - 3;
                        Console.WriteLine("[ found at y: " + (realY));
                        locations.Add(realY);
                    }
                    else if (hsvPixel.V <= v)
                        onChar = false;
                }
            }

            Console.Write("private int[] _lineOffsetsSmall = new int[] { ");
            foreach (var location in locations)
            {
                Console.Write(location + ",");
            }
        }

        public string VerifyInputSmall(string imagePath, float minV, int xOffset = 0)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var hitPoints = new int[26];
                var hitPointCount = 0;
                var chars = new System.Text.StringBuilder();
                IEnumerable<CharInfo> possibleChars = null;
                var sliceIndex = 0;
                for (int x = xOffset; x < rgbImage.Width; x++)
                {
                    hitPointCount = 0;
                    for (int y = 0; y < rgbImage.Height; y++)
                    {
                        var pixel = rgbImage[x, y];
                        var hsvPixel = converter.ToHsv(pixel);
                        if (hsvPixel.V > minV)
                        {
                            hitPoints[hitPointCount++] = y;
                        }
                    }

                    if (hitPointCount > 0)
                    {
                        IEnumerable<CharInfo> newPossibleChars = null;
                        if (possibleChars == null)
                            newPossibleChars = _knownCharacters.Where(c => DoesCharactersMatch(0, hitPoints, hitPointCount, c)).ToList();
                        else
                            newPossibleChars = possibleChars.Where(c => DoesCharactersMatch(sliceIndex, hitPoints, hitPointCount, c)).ToList();

                        if (newPossibleChars.Count() > 1)
                        {
                            possibleChars = newPossibleChars;
                            sliceIndex++;
                        }
                        else if (newPossibleChars.Count() == 1)
                        {
                            sliceIndex = 0;
                            var character = newPossibleChars.Single();
                            chars.Append(character.Character);
                            x += character.Width - 1;
                        }
                    }
                    else
                        possibleChars = null;
                }
                return chars.ToString();
            }
        }

        private static bool DoesCharactersMatch(int sliceIndex, int[] hitPoints, int hitPointCount, CharInfo character)
        {
            if (character.Slices.Count <= sliceIndex || character.Slices[sliceIndex].Count != hitPointCount)
                return false;

            var valid = true;
            for (int i = 0; i < hitPointCount; i++)
            {
                if (character.Slices[sliceIndex][i] != hitPoints[i])
                {
                    valid = false;
                    break;
                }
            }

            return valid;
        }

        public string AnalyzeChatMessages(string imagePath, string outputDirectory)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3249, 1337);
            //Image.Load(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screenshot (7).png")
            //@"C:\Users\david\OneDrive\Documents\WFChatParser\friday_last_moved.png"
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                if (rgbImage.Height == 1440)
                    chatRect = new Rectangle(4, 530, 2022, 850);
                else if (rgbImage.Height == 1080)
                    chatRect = new Rectangle(4, 370, 1507, 650);

                rgbImage.Mutate(x => x.Crop(chatRect));

                var maxHsv = 0.29;
                var hitPoints = new int[36];
                var hitPointCount = 0;
                for (int line = 0; line < _lineOffsets.Length; line++)
                {
                    for (int x = chatRect.Left; x < chatRect.Right; x++)
                    {
                        hitPointCount = 0;
                        for (int y = _lineOffsets[line] + chatRect.Top; y < _lineOffsets[line] + chatRect.Top; y++)
                        {
                            var pixel = rgbImage[x, y];
                            var hsvPixel = converter.ToHsv(pixel);
                            if (hsvPixel.V > maxHsv)
                            {
                                hitPoints[hitPointCount++] = y;
                            }
                        }
                        if (hitPointCount > 0)
                        {
                            //Check database of known characters
                        }
                    }
                }

                //for (int i = 0; i < rgbImage.Width * rgbImage.Height; i++)
                //{
                //    var x = i % rgbImage.Width;
                //    var y = i / rgbImage.Width;
                //    var pixel = rgbImage[x, y];
                //    var hsvPixel = converter.ToHsv(pixel);
                //    if (hsvPixel.V > maxHsv)
                //    {
                //        rgbImage[x, y] = Rgba32.Black;
                //    }
                //    else
                //        rgbImage[x, y] = Rgba32.White;
                //}
                //var sb = new System.Text.StringBuilder();
                //var fileInfo = new FileInfo(imagePath);
                //sb.AppendLine(fileInfo.Name);
                //var line = 0;
                //var prevLineBlack = false;
                //var lineOffsets = new int[27];
                //for (int y = 0; y < rgbImage.Height; y++)
                //{
                //    var x = 2;
                //    var pixel = rgbImage[x, y];
                //    var hsvPixel = converter.ToHsv(pixel);
                //    if (hsvPixel.V > maxHsv && !prevLineBlack)
                //    {
                //        rgbImage[x, y] = Rgba32.Black;
                //        lineOffsets[line] = y - 3;
                //        sb.AppendLine("Line " + ++line + " [ at y: " + y);
                //        prevLineBlack = true;
                //    }
                //    else if(hsvPixel.V <= maxHsv)
                //        prevLineBlack = false;
                //}

                //Console.Write("private int[] _lineOffsets = new int[]{");
                //foreach (var offset in lineOffsets)
                //{
                //    Console.Write(offset + ",");
                //}

                //return sb.ToString();
                throw new NotImplementedException();
            }
        }

        private class CharacterDetails
        {
            public float[,] VMask { get; set; }
            public int PixelCount { get; set; }
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}
