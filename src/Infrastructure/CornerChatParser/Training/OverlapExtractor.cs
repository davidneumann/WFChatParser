﻿using Application.ChatLineExtractor;
using Application.LineParseResult;
using Application.Utils;
using RelativeChatParser.Extraction;
using RelativeChatParser.Models;
using RelativeChatParser.Recognition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RelativeChatParser.Training
{
    public static class OverlapExtractor
    {
        public static Overlap[] GetOverlapingGlyphs(string textPath, string imagePath)
        {
            var overlaps = new ConcurrentBag<Overlap>();
            using (var b = new Bitmap(imagePath))
            {
                //Boost all the Vs in the chatbox
                for (int x = LineScanner.ChatLeftX; x < LineScanner.ChatWidth + LineScanner.ChatLeftX; x++)
                {
                    for (int y = LineScanner.LineOffsets[0]; y < LineScanner.LineOffsets.Max() + LineScanner.Lineheight; y++)
                    {
                        var hsv = b.GetPixel(x, y).ToHsv();
                        if (hsv.Value > 0.35)
                        {
                            hsv.Value = Math.Min(1f, hsv.Value * 2f);
                            var c = hsv.ToColor();
                            b.SetPixel(x, y, c);
                        }
                    }
                }
                b.Save("debug_boosted.png");

                var ic = new ImageCache(b);
                ic.DebugFilename = imagePath;
                var expectedLines = File.ReadAllLines(textPath).Select(line => line.Replace(" ","").Trim()).ToArray();
                var result = new ChatMessageLineResult[expectedLines.Length];
                //Parallel.For(0, expectedLines.Length, i =>
                for (int i = 0; i < expectedLines.Length; i++)
                {
                    var charI = 0;
                    var glyphs = LineScanner.ExtractGlyphsFromLine(ic, i);
                    //Parallel.ForEach(glyphs, g =>
                    foreach (var g in glyphs)
                    {
                        var e = RelativePixelGlyphIdentifier.IdentifyGlyph(g);
                        if (e.Length != 1 || (e.Length == 1 && e[0].ReferenceMaxWidth + 1 < g.Width) || (e.Length == 1 && e[0].ReferenceMinWidth - 1 > g.Width))
                        {
                            var overlap = new Overlap()
                            {
                                Bitmap = b,
                                Extracted = g,
                                IdentifiedGlyphs = e,
                                ExpectedCharacters = $"{expectedLines[i][charI]}{expectedLines[i][charI + 1]}"
                            };
                            overlaps.Add(overlap);
                            charI += 2;
                        }
                        else
                            charI++;
                        //});
                    }
                    //});
                }
            }
            return overlaps.ToArray();
        }
    }

    public class Overlap
    {
        public Bitmap Bitmap { get; set; }
        public ExtractedGlyph Extracted { get; set; }
        public FuzzyGlyph[] IdentifiedGlyphs { get; set; }
        public string ExpectedCharacters { get; set; }
    }
}
