using Application.ChatLineExtractor;
using Application.LineParseResult;
using CornerChatParser.Extraction;
using CornerChatParser.Models;
using CornerChatParser.Recognition;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CornerChatParser.Training
{
    public static class OverlapExtractor
    {
        public static Overlap[] GetOverlapingGlyphs(string textPath, string imagePath)
        {
            var overlaps = new ConcurrentBag<Overlap>();
            using (var b = new Bitmap(imagePath))
            {
                var ic = new ImageCache(b);
                ic.DebugFilename = imagePath;
                var expectedLines = File.ReadAllLines(textPath);
                var result = new ChatMessageLineResult[expectedLines.Length];
                Parallel.For(0, expectedLines.Length, i =>
                //for (int i = 0; i < lineParseCount; i++)
                {
                    var glyphs = LineScanner.ExtractGlyphsFromLine(ic, i);
                    Parallel.ForEach(glyphs, g =>
                    {
                        var e = RelativePixelGlyphIdentifier.IdentifyGlyph(g, b);
                        if (e.Length > 1)
                        {
                            var overlap = new Overlap()
                            {
                                Bitmap = b,
                                Extracted = g,
                                IdentifiedGlyphs = e
                            };
                            overlaps.Add(overlap);
                        }
                    });
                });
                //}
            }
            return overlaps.ToArray();
        }
    }

    public class Overlap
    {
        public Bitmap Bitmap { get; set; }
        public ExtractedGlyph Extracted { get; set; }
        public Glyph[] IdentifiedGlyphs { get; set; }
    }
}
