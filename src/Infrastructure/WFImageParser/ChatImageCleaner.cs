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
using System.Threading.Tasks;

namespace WFImageParser
{
    public class ChatImageCleaner : IChatImageProcessor
    {
        public ChatImageCleaner(CharInfo[] knownCharacters)
        {
            _knownCharacters = knownCharacters;
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

        private int[] _lineOffsets = new int[] { 5, 55, 105, 154, 204, 253, 303, 352, 402, 452, 501, 551, 600, 650, 700, 749, 799, 848, 898, 948, 997, 1047, 1096, 1146, 1195, 1245, 1295 };
        private readonly CharInfo[] _knownCharacters;

        public string[] ConvertScreenshotToChatText(string imagePath, float minV)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                //var maxHsv = 0.29;
                var allText = new List<string>();
                foreach (var lineOffset in _lineOffsets)
                {
                    var hitPoints = new int[36];
                    var hitPointCount = 0;
                    var chars = new System.Text.StringBuilder();
                    IEnumerable<CharInfo> possibleChars = null;
                    var sliceIndex = 0;
                    for (int x = chatRect.Left; x < chatRect.Right; x++)
                    {
                        hitPointCount = 0;
                        for (int y = chatRect.Top + lineOffset; y < chatRect.Top + lineOffset + 36; y++)
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
                    allText.Add(chars.ToString());
                }
                
                return allText.ToArray();
            }
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
        public CharInfo[] AnalyzeInput(string imagePath, string referenceChars, float minV)
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
                for (int x = 0; x < rgbImage.Width; x++)
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

        public string VerifyInput(string imagePath, float minV)
        {
            var converter = new ColorSpaceConverter();
            using (Image<Rgba32> rgbImage = Image.Load(imagePath))
            {
                var hitPoints = new int[36];
                var hitPointCount = 0;
                var chars = new System.Text.StringBuilder();
                IEnumerable<CharInfo> possibleChars = null;
                var sliceIndex = 0;
                for (int x = 0; x < rgbImage.Width; x++)
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

        public string AnalysisChatMessage(string imagePath, string outputDirectory)
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
    }
}
