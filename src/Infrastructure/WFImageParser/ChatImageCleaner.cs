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
        private int _maxCharWidth = 0;

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
                            PixelCount = 0
                        };
                        using (Image<Rgba32> image = Image.Load(file))
                        {
                            character.VMask = new float[image.Width, image.Height];
                            for (int x = 0; x < image.Width; x++)
                            {
                                for (int y = 0; y < image.Height; y++)
                                {
                                    character.VMask[x, y] = converter.ToHsv(image[x, y]).V;
                                    if (character.VMask[x, y] >= 0.5)
                                        character.PixelCount++;
                                }
                            }
                            character.Width = image.Width;
                            character.Height = image.Height;
                            _scannedCharacters.Add(character);
                            if (character.Width > _maxCharWidth)
                                _maxCharWidth = character.Width;
                        }
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

        public void AverageBitmaps(Dictionary<string, List<string>> files, float minV, float threashold, bool smallText = true)
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
                                if (averageImage[x, y].R >= (float)files[character].Count * threashold)
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

        public List<List<CharacterPair>> ConvertScreenshotToChatTextWithBitmap(string imagePath, float minV, int spaceOffset, int xOffset = 0, int startLine = 0, int endLine = int.MaxValue, bool smallText = true)
        {
            var converter = new ColorSpaceConverter();
            var chatRect = new Rectangle(4, 763, 3236, 1350);
            if (!smallText)
                chatRect = chatRect;
            List<List<CharacterPair>> characterPairs = new List<List<CharacterPair>>();
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
                    var results = ParseLineBitmapScan(minV, xOffset, converter, chatRect, rgbImage, allText, lineHeight, offsets[i], spaceOffset);
                    if(results.Count > 0)
                        characterPairs.Add(results);
                }

                //return allText.ToArray();
                return characterPairs;
            }
        }

        public class ParseResult
        {

        }
        public class CharacterPair
        {
            public Character LeftCharacter { get; set; }
            public Character RightCharacter { get; set; }
            public int SpaceBetween { get; set; }
        }
        public class Character
        {
            public BoundingBox Location { get; set; }
            public List<Coordinates> Pixels { get; set; }
            public char Value { get; set; }
            public string Name { get; set; }
            public Character(BoundingBox location, List<Coordinates> pixels, char value, string name)
            {
                Location = location;
                Pixels = pixels;
                Value = value;
                Name = name;
            }
        }
        public struct BoundingBox
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        public struct Coordinates
        {
            public int X;
            public int Y;
            public Coordinates(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
        private List<CharacterPair> ParseLineBitmapScan(float minV, int xOffset, ColorSpaceConverter converter, Rectangle chatRect, Image<Rgba32> rgbImage, List<string> allText, int lineHeight, int lineOffset, float spaceWidth)
        {
            var sb = new System.Text.StringBuilder();
            var emptySlices = 0;
            //var onSpaceChar = false;
            var startX = xOffset;
            var endX = xOffset;
            //var spaceWidth = 0.40 * lineHeight;
            var lastCharacterEndX = startX;
            List<Point> prevMatchedCharacters = new List<Point>();
            List<Point> prevTargetCharacters = new List<Point>();
            List<Point> targetCharacterPixels = null;
            CharacterDetails lastCharacterDetails = null;
            List<CharacterPair> characterPairs = new List<CharacterPair>();
            Character lastCharacter = null;
            var dotSkipped = false;
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

                var cleanTargetPixels = new List<Point>();
                startX = chatRect.Right;
                FindCharacterPoints(minV, converter, ref chatRect, rgbImage, lineHeight, lineOffset, prevMatchedCharacters, firstPixel, cleanTargetPixels);
                targetCharacterPixels = new List<Point>(cleanTargetPixels);

                cleanTargetPixels.ForEach(p => { if (p.X < startX) startX = p.X; if (p.X > endX) endX = p.X; });
                endX++;

                //Certain charcters can touch, such as AZ, make sure we didn't jump back to the start of the touching letters
                if (lastCharacterEndX > startX)// && lastCharacterEndX - startX > lineHeight * 0.3)
                {
                    cleanTargetPixels.RemoveAll(p => prevMatchedCharacters.Any(p2 => p2.X == p.X && p2.Y == p.Y));
                    //Clean up any scraps

                    cleanTargetPixels.GroupBy(p => p.X).Where(g => g.Count() == 1).SelectMany(g => g).ToList().ForEach(p => cleanTargetPixels.Remove(p));
                    if (cleanTargetPixels.Count > 0 && false)
                        startX = Math.Max(cleanTargetPixels.Min(p => p.X), lastCharacterEndX);
                    else
                        startX = cleanTargetPixels.Min(p => p.X);//startX = lastCharacterEndX;
                }

                //All 3/4 width chars have been turned into 2 width to get rid of aliasing problems
                if (endX - startX > 2 && endX - startX <= 4 && cleanTargetPixels.Count > 34)
                {
                    var pixelsByX = cleanTargetPixels.GroupBy(p => p.X).Select(g => new KeyValuePair<int, int>(g.First().X, g.Count())).OrderByDescending(g => g.Value).Skip(2);
                    pixelsByX.ToList().ForEach(kvp => cleanTargetPixels.RemoveAll(p => p.X == kvp.Key));
                    startX = cleanTargetPixels.Min(p => p.X);
                    endX = cleanTargetPixels.Max(p => p.X) + 1;
                    //var leftMostchars = charPixels.Where(p => p.X == startX).Count();
                    //var rightMostChars = charPixels.Where(p => p.X == endX - 1).Count();
                    //if (leftMostchars >= rightMostChars)
                    //{
                    //    charPixels.RemoveAll(p => p.X == endX - 1);
                    //    endX--;
                    //}
                    //else
                    //{
                    //    startX++;
                    //    charPixels.RemoveAll(p => p.X == startX);
                    //}
                }

                if (endX - startX > lineHeight * 0.8333333333333333f) //We have a ton of characters combined, limit it
                {
                    endX = startX + (int)(lineHeight * 0.8333333333333333f);
                    cleanTargetPixels.RemoveAll(p => p.X >= endX);
                }

                if (endX > startX)
                {
                    //using (Image<Rgba32> debug1 = rgbImage.Clone())
                    //{
                    //    debug1.Mutate(i => i.Crop(new Rectangle(startX, lineOffset, endX - startX, lineHeight)));
                    //    debug1.Save("test_target.png");
                    //}

                    //Console.WriteLine("Target: " + targetPixels);
                    //Console.WriteLine("{0,12} {1,12} {2,12} {3,12}", "Name", "PixelCont", "Match", "Confi");

                    var bestFit = new Tuple<float, CharacterDetails, List<Point>>(float.MinValue, null, null);
                    bestFit = GuessCharacter(minV, converter, rgbImage, lineOffset, startX, endX, cleanTargetPixels, bestFit);
                    var origStartX = startX;
                    var origEndX = endX;
                    var didRemoveJaggies = false;
                    //try removing some low entropy columns
                    for (int i = 0; i < 2; i++)
                    {
                        var removedPixels = new List<Point>(prevMatchedCharacters);
                        if (bestFit.Item1 <= 0.25f && endX - startX < _maxCharWidth && cleanTargetPixels.Count > 0 && bestFit.Item2.Name != "f_lower")
                        {
                            if (!didRemoveJaggies)
                            {
                                didRemoveJaggies = true;
                                var fuzzyCharacter = cleanTargetPixels.Where(p => cleanTargetPixels.Count(p2 => p2.X == p.X) > 1 && cleanTargetPixels.Count(p2 => p2.Y == p.Y) > 1).ToList();
                                if (fuzzyCharacter.Count > 0)
                                {
                                    var newMinX = fuzzyCharacter.Min(p => p.X);
                                    var newMaxX = fuzzyCharacter.Max(p => p.X) + 1;
                                    bestFit = GuessCharacter(minV, converter, rgbImage, lineOffset, newMinX, newMaxX, fuzzyCharacter, bestFit);
                                }
                            }
                            else if (endX - startX >= _maxCharWidth * 0.6)
                            {
                                var lowEntropyGroups = cleanTargetPixels.GroupBy(p => p.X).OrderBy(g => g.Count());
                                var minColumnCount = lowEntropyGroups.First().Count();
                                var lastMinColumn = lowEntropyGroups.Where(g => g.Count() == minColumnCount).Last().ToList();//.Last().ToList();
                                lastMinColumn.ForEach(p => { cleanTargetPixels.Remove(p); removedPixels.Add(p); });
                                var fuzzyCharacter = new List<Point>();
                                FindCharacterPoints(minV, converter, ref chatRect, rgbImage, lineHeight, lineOffset, removedPixels,
                                    cleanTargetPixels.First(p => p.X == cleanTargetPixels.Min(p2 => p2.X)), fuzzyCharacter);
                                var newMinX = fuzzyCharacter.Min(p => p.X);
                                var newMaxX = fuzzyCharacter.Max(p => p.X) + 1;
                                bestFit = GuessCharacter(minV, converter, rgbImage, lineOffset, newMinX, newMaxX, fuzzyCharacter, bestFit);
                            }
                            else
                            {
                                var leftPixels = cleanTargetPixels.Count(p => p.X == startX);
                                var rightPixels = cleanTargetPixels.Count(p => p.X == endX - 1);
                                var minY = cleanTargetPixels.Min(p => p.Y);
                                var maxY = cleanTargetPixels.Max(p => p.Y);
                                var topPixels = cleanTargetPixels.Count(p => p.Y == minY);
                                var bottomPixels = cleanTargetPixels.Count(p => p.Y == maxY);
                                var lowestCount = Math.Min(leftPixels, Math.Min(rightPixels, Math.Min(topPixels, bottomPixels)));
                                if (leftPixels == lowestCount)
                                {
                                    cleanTargetPixels.RemoveAll(p => p.X == startX);
                                    startX++;
                                }
                                else if (rightPixels == lowestCount)
                                {
                                    cleanTargetPixels.RemoveAll(p => p.X == endX - 1);
                                    endX--;
                                }
                                else if (topPixels == lowestCount)
                                    cleanTargetPixels.RemoveAll(p => p.Y == minY);
                                else if (bottomPixels == lowestCount)
                                    cleanTargetPixels.RemoveAll(p => p.Y == maxY);
                                var newMinX = cleanTargetPixels.Min(p => p.X);
                                var newMaxX = cleanTargetPixels.Max(p => p.X) + 1;
                                bestFit = GuessCharacter(minV, converter, rgbImage, lineOffset, newMinX, newMaxX, cleanTargetPixels, bestFit);
                            }
                        }
                        else
                            break;
                    }
                    startX = origStartX;
                    endX = origEndX;

                    //We can allow loose fits on smaller characters
                    if (bestFit.Item1 < 0.25f && bestFit.Item2 != null && bestFit.Item2.PixelCount > 40)
                        bestFit = bestFit = new Tuple<float, CharacterDetails, List<Point>>(float.MinValue, null, null);

                    //if (bestFit.Item2 == null)
                    //    Console.WriteLine("failed to identify at: " + startX + " " + lineOffset);
                    //else
                    //    Console.WriteLine($"Identified {bestFit.Item2.Name} with conf {bestFit.Item1}");

                    if (bestFit.Item2 != null && endX != lastCharacterEndX && cleanTargetPixels.Count >= _scannedCharacters.Min(p => p.PixelCount) * 0.8)
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


                        //Check if we skipped past a space
                        var jaggyFreePrev = prevTargetCharacters.Where(p => prevTargetCharacters.Count(p2 => p2.X == p.X) > 1 && prevTargetCharacters.Count(p2 => p2.Y == p.Y) > 1).ToArray();
                        var jaggyFreeChar = targetCharacterPixels.Where(p => targetCharacterPixels.Count(p2 => p2.X == p.X) > 1 && targetCharacterPixels.Count(p2 => p2.Y == p.Y) > 1).ToArray();

                        ////Find closest pixel above baseline in new found character
                        ////or if there is none the closest one below baseline
                        ////We limit to first 10 columns as certain touching combos, such as _}, will break things
                        //var aboveBaselinePixels = jaggyFreeChar.Where(p => p.X <= startX + 10 && p.Y < lineOffset + lineHeight * 0.75f).ToArray();
                        //var closestPixelX = startX;
                        //if (aboveBaselinePixels.Length > 0)
                        //    closestPixelX = aboveBaselinePixels.Min(p => p.X);
                        //else
                        //    closestPixelX = jaggyFreeChar.Min(p => p.X);
                        var closestPixelX = jaggyFreeChar.Min(p => p.X);

                        var lastPixelX = jaggyFreePrev.Length > 0 ? jaggyFreePrev.Max(p => p.X) + 1 : lastCharacterEndX;
                        var adjustedSpaceWidth = spaceWidth;
                        //1 has a lot of space build into after it contents and | has a bunch of space on both sides
                        if (lastCharacterDetails != null &&
                            (lastCharacterDetails.Name == "pipe" || lastCharacterDetails.Name == "1" || bestFit.Item2.Name == "pipe"))
                            adjustedSpaceWidth += (int)(lineHeight * 0.175);
                        if (closestPixelX - lastPixelX >= adjustedSpaceWidth
                            && endX - startX < lineHeight * 1.5) //Make sure we aren't getting tricked by 2 characters touching
                            sb.Append(' ');

                        //Add character
                        sb.Append(name);

                        //Add parsed pair for space testing stuff
                        if (dotSkipped)
                        {
                            var newCharacter = new Character(new BoundingBox()
                            {
                                Left = targetCharacterPixels.Min(p => p.X),
                                Bottom = targetCharacterPixels.Max(p => p.Y),
                                Right = targetCharacterPixels.Max(p => p.X),
                                Top = targetCharacterPixels.Min(p => p.Y)
                            },
                                targetCharacterPixels.Select(p => new Coordinates(p.X, p.Y)).ToList(),
                                name[0],
                                bestFit.Item2.Name
                            );
                            if (lastCharacter != null)
                            {
                                var newPair = new CharacterPair()
                                {
                                    LeftCharacter = lastCharacter,
                                    RightCharacter = newCharacter,
                                    SpaceBetween = closestPixelX - lastPixelX
                                };
                                characterPairs.Add(newPair);
                                lastCharacter = null;
                            }
                            else
                                lastCharacter = newCharacter;
                        }
                        else if (!dotSkipped && name == ".")
                            dotSkipped = true;

                        lastCharacterEndX = startX + bestFit.Item2.Width;
                        prevMatchedCharacters = bestFit.Item3;
                        prevTargetCharacters = targetCharacterPixels;
                        lastCharacterDetails = bestFit.Item2;
                        //if (endX - startX > _maxCharWidth * 0.6 && bestFit.Item2.Width < _maxCharWidth * 0.6)
                        //    lastCharacterEndX++; // overcome the antia aliasing that brought us here

                        //Due to new char IDing system we can safely jump a bit ahead to prevent double reading
                        if (endX - startX > _maxCharWidth * 0.6 && cleanTargetPixels.Count - prevMatchedCharacters.Count > cleanTargetPixels.Count * 0.3)
                            endX = x = lastCharacterEndX;
                        else
                            endX = x = lastCharacterEndX + 2;
                    }
                    else //failed to ID the character, skip it
                    {
                        sb.Append(' ');
                        x = lastCharacterEndX = endX = cleanTargetPixels.Max(p => p.X) + 1;
                    }
                }
            }
            allText.Add(sb.ToString().Trim());
            return characterPairs;
        }

        private static void FindCharacterPoints(float minV, ColorSpaceConverter converter, ref Rectangle chatRect, Image<Rgba32> rgbImage, int lineHeight, int lineOffset, List<Point> prevMatchedCharacters, Point firstPixel, List<Point> cleanTargetPixels)
        {
            int startX = int.MaxValue;
            int endX = int.MinValue;
            //Add all pixels of this character to a collection
            //Account for gaps such as in i or j
            for (int y = firstPixel.Y; y < lineOffset + lineHeight; y++)
            {
                FindCharPixels(minV, converter, rgbImage, cleanTargetPixels, new Point(firstPixel.X, y), prevMatchedCharacters);
            }
            //Account for gaps like in ;
            {
                var midX = (int)cleanTargetPixels.Average(p => p.X);
                var minY = cleanTargetPixels.Min(p => p.Y);
                for (int i = lineOffset; i < minY; i++)
                {
                    FindCharPixels(minV, converter, rgbImage, cleanTargetPixels, new Point(midX, i), prevMatchedCharacters);
                }
            }
            //Account for crazy gaps such as in %
            var foundNewPixels = false;
            do
            {
                foundNewPixels = false;
                startX = chatRect.Right;
                cleanTargetPixels.ForEach(p => { if (p.X < startX) startX = p.X; if (p.X > endX) endX = p.X; });
                for (int y = cleanTargetPixels.Where(p => p.X == cleanTargetPixels.Max(p2 => p2.X)).Min(p => p.Y); y < lineOffset + lineHeight * 0.75f; y++)
                {
                    var newFoundPixel = FindCharPixels(minV, converter, rgbImage, cleanTargetPixels, new Point(endX, y), prevMatchedCharacters);
                    if (newFoundPixel)
                        foundNewPixels = true;
                }
            } while (foundNewPixels);
        }

        private Tuple<float, CharacterDetails, List<Point>> GuessCharacter(float minV, ColorSpaceConverter converter, Image<Rgba32> rgbImage, int lineOffset, int startX, int endX, List<Point> charPixels, Tuple<float, CharacterDetails, List<Point>> bestFit)
        {
            var targetWidth = endX - startX;

            //using (Image<Rgba32> debug1 = new Image<Rgba32>(null, charPixels.Max(p => p.X) - charPixels.Min(p => p.X) + 1, _scannedCharacters.First().Height, Rgba32.Black))
            //{
            //    var leftmost = charPixels.Min(p => p.X);
            //    charPixels.ForEach(p => debug1[p.X - leftmost, p.Y - lineOffset] = Rgba32.White);
            //    debug1.Save("test_target.png");
            //}

            var cannidates = _scannedCharacters;
            if (targetWidth <= _maxCharWidth / 2 && targetWidth > Math.Round(_maxCharWidth * 0.15))
                cannidates = _scannedCharacters.Where(c => c.Width >= targetWidth * 0.8f && c.Width <= targetWidth * 1.2f).ToList();
            else if (targetWidth <= Math.Round(_maxCharWidth * 0.15))
                cannidates = _scannedCharacters.Where(c => c.Width <= _maxCharWidth * 0.15).ToList();
            foreach (var details in cannidates)
            {
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

                var matchingPixelsCount = 0;
                var dVMask = details.VMask;
                var pixelCount = details.PixelCount;
                var charWidth = details.Width;
                //Just like above we need to slam down some skinny characters
                //All 3/4 width chars have been turned into 2 width to get rid of aliasing problems
                if (targetWidth == 2 && details.PixelCount > 34 && (details.Width >= 3 || details.Width <= 4))
                {
                    pixelCount = 0;
                    var top2Xs = new KeyValuePair<int, int>[2];
                    for (int x = 0; x < details.Width; x++)
                    {
                        var count = 0;
                        for (int y = 0; y < details.Height; y++)
                        {
                            if (dVMask[x, y] > minV)
                                count++;
                        }
                        if (count > top2Xs[0].Value)
                            top2Xs[0] = new KeyValuePair<int, int>(x, count);
                        else if (count > top2Xs[1].Value)
                            top2Xs[1] = new KeyValuePair<int, int>(x, count);
                    }
                    var newMask = new float[2, details.Height];
                    for (int x = 0; x < 2; x++)
                    {
                        for (int y = 0; y < details.Height; y++)
                        {
                            newMask[x, y] = details.VMask[top2Xs[x].Key, y];
                            if (newMask[x, y] > minV)
                                pixelCount++;
                        }
                    }
                    dVMask = newMask;
                    charWidth = 2;
                }
                //if (targetWidth != details.Width && targetWidth <= 4 && targetWidth >= 3 && details.Width == 2)
                //{
                //    var newMask = new float[targetWidth, dVMask.GetLength(1)];
                //    for (int x = 0; x < details.Width; x++)
                //    {
                //        for (int y = 0; y < dVMask.GetLength(1); y++)
                //        {
                //            newMask[x, y] = dVMask[x, y];
                //        }
                //    }
                //    for (int x = details.Width; x < targetWidth; x++)
                //    {
                //        for (int y = 0; y < dVMask.GetLength(1); y++)
                //        {
                //            newMask[x, y] = dVMask[details.Width - 1, y];
                //            if (newMask[x, y] > minV)
                //                pixelCount++;
                //        }
                //    }
                //    dVMask = newMask;
                //    charWidth = targetWidth;
                //}
                var width = Math.Min(endX - startX, charWidth);
                var maxX = startX + width;
                var matchingPixels = charPixels.Where(p => p.X >= startX && p.X < maxX && dVMask[p.X - startX, p.Y - lineOffset] > minV).ToList();
                matchingPixelsCount = charPixels.Count(p => p.X >= startX && p.X < maxX && dVMask[p.X - startX, p.Y - lineOffset] > minV);
                //var leftMatchingPixels = charPixels.Where(p => p.X <= startX + (endX - startX) / 2).Count(p => p.X >= startX && p.X < maxX && dVMask[p.X - startX, p.Y - lineOffset] > minV);
                //var rightMatchingPixels = charPixels.Where(p => p.X > startX + (endX - startX) / 2).Count(p => p.X >= startX && p.X < maxX && dVMask[p.X - startX, p.Y - lineOffset] > minV);
                //for (int x2 = startX; x2 < endX && x2 < startX + details.Width; x2++)
                //{
                //    for (int y2 = 0; y2 < details.Height; y2++)
                //    {
                //        var pV = converter.ToHsv(rgbImage[x2, y2 + lineOffset]).V;
                //        var dV = details.VMask[x2 - startX, y2];
                //        //if ((pV > minV && dV > minV) || (pV < minV && dV < minV))
                //        //{
                //        //    matchingPixels++;
                //        //}
                //        //else
                //        //    matchingPixels--;
                //        if (pV > minV && dV > minV)
                //            matchingPixels++;
                //    }
                //}

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

                //Filter out mangled matches from 2 characters touching where left most pixels matter more
                if (targetWidth > _maxCharWidth / 2)
                {
                    //Punish this character for pixels that are in the target but not in the character
                    charPixels.Where(p => p.X - startX > 0 && p.X < maxX).ToList()
                        .ForEach(p =>
                        {
                            if (dVMask[p.X - startX, p.Y - lineOffset] < minV)
                                matchingPixelsCount--;
                        });
                }

                var conf = (float)matchingPixelsCount / (float)(Math.Max(charPixels.Count, pixelCount));
                //var conf = matchingPixels;
                //Console.WriteLine("{0,12} {1,12} {2,12} {3,12}", details.Name, details.PixelCount, matchingPixels, conf);
                if (conf > bestFit.Item1)
                {
                    bestFit = new Tuple<float, CharacterDetails, List<Point>>(conf, details, matchingPixels);
                }
            }

            return bestFit;
        }

        private static bool FindCharPixels(float minV, ColorSpaceConverter converter, Image<Rgba32> rgbImage, List<Point> charPixels, Point checkPoint, List<Point> blacklistedPoints)
        {
            if (!charPixels.Any(p => p.X == checkPoint.X && p.Y == checkPoint.Y)
                && !blacklistedPoints.Any(p => p.X == checkPoint.X && p.Y == checkPoint.Y)
                && converter.ToHsv(rgbImage[checkPoint.X, checkPoint.Y]).V > minV)
            {
                var didFindNew = false;
                var neighborCount = 0;
                if (converter.ToHsv(rgbImage[checkPoint.X - 1, checkPoint.Y]).V > minV)
                    neighborCount++;
                if (converter.ToHsv(rgbImage[checkPoint.X + 1, checkPoint.Y]).V > minV)
                    neighborCount++;
                if (converter.ToHsv(rgbImage[checkPoint.X, checkPoint.Y - 1]).V > minV)
                    neighborCount++;
                if (converter.ToHsv(rgbImage[checkPoint.X, checkPoint.Y + 1]).V > minV)
                    neighborCount++;

                if (neighborCount >= 1)
                {
                    charPixels.Add(checkPoint);
                    didFindNew = true;
                }
                var neighborAdded = FindCharPixels(minV, converter, rgbImage, charPixels, new Point(checkPoint.X - 1, checkPoint.Y), blacklistedPoints);
                if (neighborAdded) didFindNew = true;
                neighborAdded = FindCharPixels(minV, converter, rgbImage, charPixels, new Point(checkPoint.X + 1, checkPoint.Y), blacklistedPoints);
                if (neighborAdded) didFindNew = true;
                neighborAdded = FindCharPixels(minV, converter, rgbImage, charPixels, new Point(checkPoint.X, checkPoint.Y - 1), blacklistedPoints);
                if (neighborAdded) didFindNew = true;
                neighborAdded = FindCharPixels(minV, converter, rgbImage, charPixels, new Point(checkPoint.X, checkPoint.Y + 1), blacklistedPoints);
                if (neighborAdded) didFindNew = true;
                return didFindNew;
            }
            return false;
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
