using Application.ChatLineExtractor;
using Application.ChatMessages.Model;
using Application.Data;
using Application.Interfaces;
using Application.LineParseResult;
using Application.Logger;
using ImageMagick;
using RelativeChatParser.Database;
using RelativeChatParser.Extraction;
using RelativeChatParser.Models;
using RelativeChatParser.Recognition;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RelativeChatParser
{
    public class RelativePixelParser : IChatParser
    {
        private readonly ILogger _logger;
        private static readonly FuzzyGlyph NullGlyph;
        private Queue<string> _timeUserCache = new Queue<string>();
        private static List<Regex> _blacklistedRegex = new List<Regex>();
        private static readonly Regex _kickRegex =  new Regex(@"\w was kicked.", RegexOptions.Compiled);

        static RelativePixelParser()
        {
            NullGlyph = new FuzzyGlyph()
            {
                Character = "",
            };

            //Load blacklists
            if (File.Exists(Path.Combine(DataHelper.OcrDataPathEnglish, @"MessageBlacklists.txt")))
            {
                foreach (var line in File.ReadAllLines(Path.Combine(DataHelper.OcrDataPathEnglish, @"MessageBlacklists.txt")))
                {
                    _blacklistedRegex.Add(new Regex(line, RegexOptions.Compiled));
                }
            }
        }

        public RelativePixelParser(ILogger logger)
        {
            _logger = logger;
        }

        public void InvalidateCache(string key)
        {
            var duplicateCache = new Queue<string>(_timeUserCache.Where(i => i != key));
            _timeUserCache = duplicateCache;
        }

        public bool IsChatFocused(Bitmap chatIconBitmap)
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

        public bool IsScrollbarPresent(Bitmap screenImage)
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

        public ChatMessageLineResult[] ParseChatImage(Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            lineParseCount = Math.Min(lineParseCount, LineScanner.LineOffsets.Length);
            var imageCache = new ImageCache(image);
            while (_timeUserCache.Count > 75)
            {
                var removed = _timeUserCache.Dequeue();
                _logger.Log($"Removed {removed} from parser cache");
            }

            //Phase 1: Get all usernames and timestamps in parallel
            var headWords = ConvertToWords(lineParseCount, ExtractLetters(image, lineParseCount, imageCache, true));
            var headLines = GetUsernameAndTimestamp(lineParseCount, headWords);


            //Phase 2: Figure out which messages should even be parsed
            var headLinesValid = new bool[lineParseCount];
            ChatMessageLineResult lastValidHeadLine = null;
            for (int i = 0; i < lineParseCount; i++)
            {
                if (useCache && IsLineInCache(headLines[i]))
                {
                    headLinesValid[i] = false;
                    lastValidHeadLine = null;
                }
                else if (useCache && headLines[i].LineType == LineType.Continuation && lastValidHeadLine != null)
                {
                    headLinesValid[i] = true;
                }
                else if(headLines[i].LineType == LineType.NewMessage)
                {
                    headLinesValid[i] = true;
                    lastValidHeadLine = headLines[i];
                }
            }

            //Phase 3: Parse all message bodies
            var bodyWords = new Word[lineParseCount][];
            Parallel.For(0, lineParseCount, i =>
            {
                if (headLinesValid[i])
                    bodyWords[i] = ConvertToWordsSingleLine(ExtractLettersSingleLine(i, imageCache, false, headLines[i].MessageBounds.Right + 1));
            });

            var fullWords = new Word[lineParseCount][];
            for (int i = 0; i < lineParseCount; i++)
            {
                if (!headLinesValid[i])
                    continue;

                fullWords[i] = headWords[i].Concat(bodyWords[i]).ToArray();

                var enhancedMessage = GetEnhancedMessageSingleLine(fullWords[i]);
                headLines[i].RawMessage = fullWords[i].Select(word => word.ToString()).Aggregate(new StringBuilder(), (acc, str) => acc.Append(str)).ToString();
                headLines[i].EnhancedMessage = enhancedMessage.EnhancedString.ToString().Trim();
                headLines[i].ClickPoints = enhancedMessage.ClickPoints;
                var letters = headWords[i].Select(w => w.Letters).SelectMany(l => l).Concat(bodyWords[i].Select(w => w.Letters).SelectMany(l => l)).ToArray();
                headLines[i].MessageBounds = GetLineRect(letters);
            }

            //Phase 4: Wrangle all the wraps
            var results = new List<ChatMessageLineResult>();
            ChatMessageLineResult last = null;
            for (int i = 0; i < lineParseCount; i++)
            {
                if(!headLinesValid[i])
                {
                    last = null;
                    continue;
                }

                //Append to last message if wrapped line
                if (headLines[i] != null && headLines[i].RawMessage != null && headLines[i].RawMessage.Length > 0 && _kickRegex.Match(headLines[i].RawMessage).Success)
                {
                    continue;
                }

                if (!fullWords[i].First().WordColor.IsTimestamp() && last != null)
                {
                    if (isScrolledUp && i == LineScanner.LineOffsets.Length - 1 && headLines[i] != null && headLines[i].LineType == LineType.Continuation && results.Count > 0)
                    {
                        //_logger.Log("Last line in chat box is contiuation. Removing last real message to prevent partial cut off.");
                        var tempLast = results.Last();
                        results.Remove(tempLast);
                    }
                    else if(!_blacklistedRegex.Any(r => r.Match(headLines[i].RawMessage).Success))
                        last.Append(headLines[i], LineScanner.Lineheight, LineScanner.LineOffsets[i]);
                }
                else
                {
                    if (isScrolledUp && i == LineScanner.LineOffsets.Length - 1 && headLines[i] != null && headLines[i].LineType == LineType.NewMessage)
                    {
                        //_logger.Log("Last line in chat box is a new message. Possible contiuation off screen, not adding.");
                        continue;
                    }
                    last = headLines[i];
                    results.Add(headLines[i]);
                }
            }

            foreach (var result in results)
            {
                if(result != null && !string.IsNullOrEmpty(result.Username) && !string.IsNullOrEmpty(result.Timestamp))
                {
                    _timeUserCache.Enqueue(result.GetKey());
                    _logger.Log($"Adding {result.GetKey()} to parser cache");
                }
            }
            return results.ToArray();
        }

        private bool IsLineInCache(ChatMessageLineResult line)
        {
            var inCache = false;
            if (!string.IsNullOrEmpty(line.Timestamp) && !string.IsNullOrEmpty(line.Username))
            {
                string key = line.GetKey();
                if (_timeUserCache.Contains(key))
                    inCache = true;
            }

            return inCache;
        }

        private static Letter[] ExtractLettersSingleLine(int i, ImageCache imageCache, bool abortAfterUsername, int startX = 0)
        {
            return LineScanner.ExtractGlyphsFromLine(imageCache, i, abortAfterUsername, startX)
                .AsParallel().Select(extracted =>
                {
                    var fuzzies = RelativePixelGlyphIdentifier.IdentifyGlyph(extracted);
                    return fuzzies.Select(f => new Letter(f, extracted));
                }).SelectMany(gs => gs).ToArray();
        }

        private static Rectangle GetLineRect(Letter[] lineLetters)
        {
            var rect = Rectangle.Empty;
            if (lineLetters.Length != 0)
            {
                var firstLetter = lineLetters.First();
                var lastLetter = lineLetters.Last();
                var top = lineLetters.Min(l => l.ExtractedGlyph != null ? l.ExtractedGlyph.Top : int.MaxValue);
                var height = lineLetters.Max(l => l.ExtractedGlyph != null ? l.ExtractedGlyph.Bottom : int.MinValue) - top;
                rect = new Rectangle(firstLetter.ExtractedGlyph.Left, top,
                    lastLetter.ExtractedGlyph.Right + 1 - firstLetter.ExtractedGlyph.Left,
                    height);
            }

            return rect;
        }

        private ChatMessageLineResult[] GetUsernameAndTimestamp(int lineParseCount, Word[][] allWords)
        {
            var results = new ChatMessageLineResult[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                Word[] lineWords = allWords[i];
                ChatMessageLineResult result = GetUsernameAndTimestampSingleLine(lineWords);
                results[i] = result;
            }
            return results;
        }

        private static ChatMessageLineResult GetUsernameAndTimestampSingleLine(Word[] lineWords)
        {
            var result = new ChatMessageLineResult();
            if (lineWords.Length > 0 && lineWords[0].ToString().Length > 0)
            {
                var firstGlyph = lineWords[0].Letters[0].ExtractedGlyph;
                var lastGlyph = lineWords.Last().Letters.Last().ExtractedGlyph;
                var width = lastGlyph.Right - firstGlyph.Left;
                var height = Math.Max(lastGlyph.Height, firstGlyph.Height);
                var top = Math.Min(lastGlyph.Top, firstGlyph.Top);
                result.MessageBounds = new Rectangle(firstGlyph.Left, top, width, height);
            }
            else
                result.MessageBounds = Rectangle.Empty;

            if (lineWords.Length >= 3)
            {
                if (!lineWords[0].WordColor.IsTimestamp())
                {
                    result.LineType = LineType.Continuation;
                }
                else
                {
                    result.LineType = LineType.NewMessage;
                    result.Timestamp = lineWords[0].ToString();
                    if (lineWords[1].ToString().Trim().Length == 0)
                        result.Username = lineWords[2].ToString();
                    else
                        result.Username = lineWords[1].ToString();
                }
            }
            else
            {
                result.LineType = LineType.Continuation;
            }

            return result;
        }

        private EnhancedMessage[] GetEnhancedMessages(int lineParseCount, Word[][] allWords)
        {
            var results = new EnhancedMessage[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                Word[] lineWords = allWords[i];
                EnhancedMessage message = GetEnhancedMessageSingleLine(lineWords);
                results[i] = message;
            }
            return results;
        }

        private static EnhancedMessage GetEnhancedMessageSingleLine(Word[] lineWords)
        {
            var message = new EnhancedMessage();
            var index = 0;
            var sb = new StringBuilder();
            for (int j = 0; j < lineWords.Length; j++)
            {
                var currentWord = lineWords[j];
                //Skip everything in the timestamp, username, first : 
                if (currentWord.WordColor.IsTimestamp())
                    continue;
                if (j >= 2 && j <= 7 && currentWord.ToString() == ":" && (lineWords[j - 1].WordColor.IsTimestamp() || lineWords[j - 2].WordColor.IsTimestamp()))
                    continue;
                var str = currentWord.ToString().Trim();
                //Skip any easily incorrect word
                if (currentWord.WordColor != ChatColor.ItemLink || !str.Contains("]")
                    || str.Length <= 0 || str[0] == '[')
                {
                    sb.Append(currentWord);
                    message.EnhancedString.Append(currentWord);
                    continue;
                }

                if (RivenRecognizer.StringContainsRiven(str))
                {
                    var line = sb.ToString().Trim();
                    var rivenName = line.Substring(line.LastIndexOf('[') + 1) + " " + str.Substring(0, str.IndexOf(']'));
                    //rivenName = rivenName.Substring(0, rivenName.IndexOf(']'));
                    message.EnhancedString.Append(str.Substring(0, str.IndexOf(']') + 1));
                    message.EnhancedString.Append("(");
                    message.EnhancedString.Append(index);
                    message.EnhancedString.Append(")");
                    message.EnhancedString.Append(str.Substring(str.IndexOf(']') + 1));
                    ExtractedGlyph extractedGlyph = currentWord.Letters[0].ExtractedGlyph;
                    message.ClickPoints.Add(new ClickPoint()
                    {
                        Index = index++,
                        X = extractedGlyph.Left,
                        Y = extractedGlyph.Top + extractedGlyph.Height / 2,
                        RivenName = rivenName
                    });
                }
                else
                    message.EnhancedString.Append(currentWord);

                sb.Append(currentWord);
            }

            return message;
        }

        private static Letter[][] ExtractLetters(Bitmap image, int lineParseCount, ImageCache imageCache, bool abortAfterUsername = false)
        {
            var allLetters = new Letter[lineParseCount][];
            Parallel.For(0, lineParseCount, i =>
            //for (int i = 0; i < lineParseCount; i++)
            {
                var letters = ExtractLettersSingleLine(i, imageCache, abortAfterUsername);
                lock (allLetters)
                {
                    allLetters[i] = letters;
                }
            });
            //}
            return allLetters;
        }

        private static Word[][] ConvertToWords(int lineParseCount, Letter[][] allLetters)
        {
            var allWords = new Word[lineParseCount][];
            for (int i = 0; i < lineParseCount; i++)
            {
                var lineLetters = allLetters[i];
                var lineWords = ConvertToWordsSingleLine(lineLetters);
                allWords[i] = lineWords.ToArray();
            }

            return allWords;
        }

        private static Word[] ConvertToWordsSingleLine(Letter[] lineLetters)
        {
            var lineWords = new List<Word>();
            Word currentWord = new Word();
            foreach (var letter in lineLetters)
            {
                 if (letter.FuzzyGlyph == null)
                {
                    var last = currentWord.Letters.LastOrDefault();
                    if (last != null)
                    {
                        var space = GlyphDatabase.Instance.GetDefaultSpace();
                        var gap = letter.ExtractedGlyph.Left - last.ExtractedGlyph.Right;
                        if (gap >= space)
                        {
                            lineWords.Add(currentWord);
                            lineWords.Add(Word.MakeSpaceWord((int)(Math.Round((float)gap / space))));
                            currentWord = new Word();
                            currentWord.WordColor = letter.ExtractedGlyph.FirstPixelColor;
                        }
                    }
                    currentWord.Letters.Add(new Letter(NullGlyph, letter.ExtractedGlyph));
                }
                else
                {
                    if (currentWord.Letters.Count > 0)
                    {
                        Letter last = currentWord.Letters.Last();
                        var space = GlyphDatabase.Instance.GetSpace(last.FuzzyGlyph.Character,
                            letter.FuzzyGlyph.Character);
                        var gap = letter.ExtractedGlyph.Left - last.ExtractedGlyph.Right;
                        if (gap >= (space - 2))
                        {
                            lineWords.Add(currentWord);
                            lineWords.Add(Word.MakeSpaceWord(gap / space));
                            currentWord = new Word();
                            currentWord.Letters.Add(letter);
                            currentWord.WordColor = letter.ExtractedGlyph.FirstPixelColor;
                        }
                        else
                            currentWord.Letters.Add(letter);
                    }
                    else
                    {
                        currentWord.Letters.Add(letter);
                        currentWord.WordColor = letter.ExtractedGlyph.FirstPixelColor;
                    }
                }
            }
            if (currentWord.Letters.Count > 0)
                lineWords.Add(currentWord);
            return lineWords.ToArray();
        }

        private class EnhancedMessage
        {
            public StringBuilder EnhancedString = new StringBuilder();
            public List<ClickPoint> ClickPoints = new List<ClickPoint>();
        }
    }
}
