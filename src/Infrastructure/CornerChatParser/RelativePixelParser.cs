using Application.ChatLineExtractor;
using Application.ChatMessages.Model;
using Application.Interfaces;
using Application.LineParseResult;
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
        private static int _debugCounter = 0;
        private static readonly FuzzyGlyph NullGlyph;

        private Queue<string> _timeUserCache = new Queue<string>();

        static RelativePixelParser()
        {
            NullGlyph = new FuzzyGlyph()
            {
                Character = "",
            };
        }

        public void InvalidateCache(string key)
        {
            throw new NotImplementedException();
        }

        public bool IsChatFocused(Bitmap chatIconBitmap)
        {
            throw new NotImplementedException();
        }

        public bool IsScrollbarPresent(Bitmap fullScreenBitmap)
        {
            throw new NotImplementedException();
        }

        public ChatMessageLineResult[] ParseChatImage(Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            var imageCache = new ImageCache(image);

            var results = new List<ChatMessageLineResult>();
            ChatMessageLineResult last = null;
            for (int i = 0; i < lineParseCount; i++)
            {
                //Get only the start to see if we have already parsed this line in a previous image
                var headLetters = ExtractLettersSingleLine(i, imageCache, true, 0);
                var headWords = ConvertToWordsSingleLine(headLetters);

                var line = GetUsernameAndTimestampSingleLine(headWords);
                bool inCache = IsLineInCache(line);

                //If this line is in the cache then skip
                if (inCache && useCache)
                {
                    last = null;
                    continue;
                }
                else
                    _timeUserCache.Enqueue(GetLineKey(line));

                //Skip any line that is a continuation when the last message is unkown
                if(last == null && 
                    (headLetters.Length == 0 || !headLetters[0].ExtractedGlyph.FirstPixelColor.IsTimestamp()))
                {
                    continue;
                }

                var right = 0;
                if (headWords.Length >= 1)
                    right = headWords.Last().Letters.Last().ExtractedGlyph.Right + 1;
                var remainingLetters = ExtractLettersSingleLine(i, imageCache, false, right);
                var remainingWords = ConvertToWordsSingleLine(remainingLetters);
                var fullWords = headWords.Concat(remainingWords).ToArray();

                var enhancedMessage = GetEnhancedMessageSingleLine(fullWords);
                line.RawMessage = fullWords.Select(word => word.ToString()).Aggregate(new StringBuilder(), (acc, str) => acc.Append(str)).ToString();
                line.EnhancedMessage = enhancedMessage.EnhancedString.ToString();
                line.ClickPoints = enhancedMessage.ClickPoints;
                line.MessageBounds = GetLineRect(headLetters.Concat(remainingLetters).ToArray());

                //Append to last message if wrapped line
                if (!fullWords.First().WordColor.IsTimestamp() && last != null)
                    last.Append(line, LineScanner.Lineheight, LineScanner.LineOffsets[i]);
                else
                {
                    last = line;
                    results.Add(line);
                }
            }

            return results.ToArray();
        }

        private bool IsLineInCache(ChatMessageLineResult line)
        {
            var inCache = false;
            if (!string.IsNullOrEmpty(line.Timestamp) && !string.IsNullOrEmpty(line.Username))
            {
                string key = GetLineKey(line);
                if (_timeUserCache.Contains(key))
                    inCache = true;
            }

            return inCache;
        }

        private static string GetLineKey(ChatMessageLineResult line)
        {
            return line.Timestamp.Trim() + line.Username.Trim();
        }

        private static Letter[] ExtractLettersSingleLine(int i, ImageCache imageCache, bool abortAfterUsername, int startX = 0)
        {
            return LineScanner.ExtractGlyphsFromLine(imageCache, i, abortAfterUsername, startX)
                /*.AsParallel()*/.Select(extracted =>
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
                var top = lineLetters.Min(l => l.ExtractedGlyph.Top);
                var height = lineLetters.Max(l => l.ExtractedGlyph.Bottom) - top;
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
            if (lineWords.Length >= 3)
            {
                if (!lineWords[0].WordColor.IsTimestamp())
                    return null;

                result.Timestamp = lineWords[0].ToString();
                if (lineWords[1].ToString().Trim().Length == 0)
                    result.Username = lineWords[2].ToString();
                else
                    result.Username = lineWords[1].ToString();
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
