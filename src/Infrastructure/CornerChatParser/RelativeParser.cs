﻿using Application.ChatLineExtractor;
using Application.Interfaces;
using Application.LineParseResult;
using CornerChatParser.Database;
using CornerChatParser.Extraction;
using CornerChatParser.Models;
using CornerChatParser.Recognition;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CornerChatParser
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

            //Break up words on spaces
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
                        if(last != null)
                        {
                            var space = GlyphDatabase.Instance.GetDefaultSpace();
                            var gap = letter.ExtractedGlyph.Left - last.ExtractedGlyph.Right;
                            if (gap >= space)
                            {
                                lineWords.Add(currentWord);
                                lineWords.Add(Word.MakeSpaceWord((int)(Math.Round((float)gap / space))));
                                currentWord = new Word();
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
                            }
                            else
                                currentWord.Letters.Add(letter);
                        }
                        else
                            currentWord.Letters.Add(letter);
                    }
                }
                if (currentWord.Letters.Count > 0)
                    lineWords.Add(currentWord);
                allWords[i] = lineWords.ToArray();
            }

            var results = new ChatMessageLineResult[lineParseCount];
            for (int i = 0; i < lineParseCount; i++)
            {
                var result = new ChatMessageLineResult();
                result.RawMessage = allWords[i].Select(word => word.ToString()).Aggregate(new StringBuilder(), (acc, str) => acc.Append(str)).ToString();
                lock (results)
                {
                    results[i] = result;
                }
            }
            return results;
        }
    }
}
