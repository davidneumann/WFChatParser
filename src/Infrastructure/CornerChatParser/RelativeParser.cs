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
using System.Threading.Tasks;

namespace RelativeChatParser
{
    public class RelativePixelParser : IChatParser
    {
        private static int _debugCounter = 0;
        private static readonly FuzzyGlyph NullGlyph;

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
            
            Letter[][] allLetters = ExtractLetters(image, lineParseCount, imageCache);

            Word[][] allWords = ConvertToWords(lineParseCount, allLetters);

            EnhancedMessage[] enhancedMessages = GetEnhancedMessages(lineParseCount, allWords);

            var results = new ChatMessageLineResult[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                var firstLetter = allLetters[i].First();
                var lastLetter = allLetters[i].Last();
                var top = allLetters[i].Min(l => l.ExtractedGlyph.Top);
                var height = allLetters[i].Max(l => l.ExtractedGlyph.Bottom) - top;
                var rect = new Rectangle(firstLetter.ExtractedGlyph.Left, top,
                    lastLetter.ExtractedGlyph.Right + 1 - firstLetter.ExtractedGlyph.Left,
                    height);
                var result = new ChatMessageLineResult()
                {
                    RawMessage = allWords[i].Select(word => word.ToString()).Aggregate(new StringBuilder(), (acc, str) => acc.Append(str)).ToString(),
                    EnhancedMessage = enhancedMessages[i].EnhancedString.ToString(),
                    ClickPoints = enhancedMessages[i].ClickPoints,
                    MessageBounds = rect
                };
                lock (results)
                {
                    results[i] = result;
                }
            }
            return results;
        }

        private EnhancedMessage[] GetEnhancedMessages(int lineParseCount, Word[][] allWords)
        {
            var results = new EnhancedMessage[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                var message = new EnhancedMessage();
                var index = 0;
                var sb = new StringBuilder();
                for (int j = 0; j < allWords[i].Length; j++)
                {
                    var currentWord = allWords[i][j];
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
                results[i] = message;
            }
            return results;
        }

        private static Letter[][] ExtractLetters(Bitmap image, int lineParseCount, ImageCache imageCache)
        {
            var allLetters = new Letter[lineParseCount][];
            Parallel.For(0, lineParseCount, i =>
            {
                var letters = LineScanner.ExtractGlyphsFromLine(imageCache, i)
                    .AsParallel().Select(extracted =>
                    {
                        var fuzzies = RelativePixelGlyphIdentifier.IdentifyGlyph(extracted, image);
                        return fuzzies.Select(f => new Letter(f, extracted));
                    }).SelectMany(gs => gs).ToArray();
                lock (allLetters)
                {
                    allLetters[i] = letters;
                }
            });
            return allLetters;
        }

        private static Word[][] ConvertToWords(int lineParseCount, Letter[][] allLetters)
        {
            var allWords = new Word[lineParseCount][];
            for (int i = 0; i < lineParseCount; i++)
            {
                var lineWords = new List<Word>();
                Word currentWord = new Word();
                foreach (var letter in allLetters[i])
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
                allWords[i] = lineWords.ToArray();
            }

            return allWords;
        }

        private class EnhancedMessage
        {
            public StringBuilder EnhancedString = new StringBuilder();
            public List<ClickPoint> ClickPoints = new List<ClickPoint>();
        }
    }
}
