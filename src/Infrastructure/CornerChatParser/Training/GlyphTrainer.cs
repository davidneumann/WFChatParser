using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CornerChatParser.Training
{
    public static class GlyphTrainer
    {
        public static Glyph CombineExtractedGlyphs(char character, IEnumerable<ExtractedGlyph> glyphs)
        {
            var glyphRect = new Rectangle(0, 0, glyphs.Select(g => g.Width).Max(),
                                                      glyphs.Select(g => g.Height).Max());
            var masterEGlyph =
                new ExtractedGlyph()
                {
                    AspectRatio = glyphs.Average(glyph => glyph.AspectRatio),
                    NormalizedCorners = glyphs.SelectMany(glyph => glyph.NormalizedCorners).ToArray(),
                    Left = glyphRect.Left,
                    Bottom = glyphRect.Bottom,
                    Height = glyphRect.Height,
                    Right = glyphRect.Right,
                    Top = glyphRect.Top,
                    Width = glyphRect.Width,
                    LineOffset = glyphs.Select(g => g.Top - g.LineOffset).Min(),
                    PixelsFromTopOfLine = (int)Math.Round(glyphs.Select(g => g.Top - g.LineOffset).Average())
                };

            var total = glyphs.Count();
            var mask = new bool[masterEGlyph.Width, masterEGlyph.Height];
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                for (int y = 0; y < mask.GetLength(1); y++)
                {
                    var count = glyphs.Where(g =>
                    {
                        if (x < g.LocalDetectedCorners.GetLength(0) && y < g.LocalDetectedCorners.GetLength(1))
                            return g.LocalDetectedCorners[x, y];
                        else return false;
                    }).Count();
                    if (count > total / 2)
                        mask[x, y] = true;
                }
            }

            var cornerCount = glyphs.Max(g => g.NormalizedCorners.Length);
            int[,] frequency = null;
            if (masterEGlyph.AspectRatio <= 1f)
                frequency = new int[cornerCount, (int)Math.Round(cornerCount / masterEGlyph.AspectRatio)];
            else
                frequency = new int[(int)Math.Round(cornerCount * masterEGlyph.AspectRatio), cornerCount];
            var flatFreq = new Dictionary<Point, int>();
            foreach (var vec in masterEGlyph.NormalizedCorners)
            {
                var x = (int)(vec.X * (frequency.GetLength(0) - 1));
                var y = (int)(vec.Y * (frequency.GetLength(1) - 1));
                frequency[x, y]++;
                var p = new Point(x, y);
                if (flatFreq.ContainsKey(p))
                    flatFreq[p]++;
                else
                    flatFreq[p] = 1;
            }
            var mid = new Vector2(0.5f, 0.5f);
            var closetToMid = flatFreq.Where(pair => pair.Value > 0).OrderBy(pair => Vector2.Distance(mid, PointToV2(pair.Key, frequency.GetLength(0), frequency.GetLength(1)))).First();
            var topCorners = flatFreq.OrderByDescending(pair => pair.Value).Take(cornerCount).Select(pair => pair.Key).ToArray();

            return new Glyph()
            {
                AspectRatio = masterEGlyph.AspectRatio,
                ReferenceWidth = masterEGlyph.Width,
                ReferenceHeight = masterEGlyph.Height,
                ReferenceGapFromLineTop = masterEGlyph.PixelsFromTopOfLine,
                Character = character,
                Corners = topCorners.Select(p => new Vector2((float)p.X / (frequency.GetLength(0) - 1),
                                                             (float)p.Y / (frequency.GetLength(1) - 1)))
                    .Append(PointToV2(closetToMid.Key, frequency.GetLength(0), frequency.GetLength(1))).ToArray()
            };
        }

        private static Vector2 PointToV2(Point p, int width, int height)
        {
            return new Vector2((float)p.X / (width - 1), (float)p.Y / (height - 1));
        }
    }
}
