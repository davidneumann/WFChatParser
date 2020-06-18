using CornerChatParser.Database;
using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CornerChatParser.Recognition
{
    public static class RelativePIxelIdentifier
    {
        public static Glyph IdentifyGlyph(ExtractedGlyph extracted)
        {
            var candidates = GlyphDatabase.AllGLyphs.Where(g => extracted.Width >= g.ReferenceMinWidth &&
                                                                extracted.Width <= g.ReferenceMaxWidth &&
                                                                extracted.Height >= g.ReferenceMinHeight &&
                                                                extracted.Height <= g.ReferenceMaxHeight);
            //Also remove anything that doesn't look to be aligned correctly
            candidates = candidates.Where(g => extracted.PixelsFromTopOfLine >= g.ReferenceGapFromLineTop - 1
                                            && extracted.PixelsFromTopOfLine <= g.ReferenceGapFromLineTop + 1);
            
            BestMatch current = null;
            foreach (var candidate in candidates)
            {
                double distances = 0;
                //For ever valid pixel find the min distance to a refrence pixel
                foreach (var valid in extracted.RelativePixelLocations)
                {
                    double minDistance = double.MaxValue;
                    foreach (var p in candidate.RelativePixelLocations)
                    {
                        var d = p.Distance(valid);
                        if (d < minDistance)
                            minDistance = d;
                        if (d == 0)
                            break;
                    }
                    if (minDistance < double.MaxValue)
                        distances += minDistance;

                    //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
                }
                //Do the same but with empties
                foreach (var empty in extracted.RelativeEmptyLocations)
                {
                    double minDistance = double.MaxValue;
                    foreach (var p in candidate.RelativeEmptyLocations)
                    {
                        var d = p.Distance(empty);
                        if (d < minDistance)
                            minDistance = d;
                        if (d == 0)
                            break;
                    }
                    if (minDistance < double.MaxValue)
                        distances += minDistance;

                    //distances += candidate.RelativeEmptyLocations.Min(p => p.Distance(empty));
                }

                if (current == null || current.distanceSum > distances)
                    current = new BestMatch(distances, candidate);
            }

            if (current == null)
                System.Diagnostics.Debugger.Break();
            else
                Console.Write(current.match.Character);

            return current.match;
        }

        private class BestMatch
        {
            public double distanceSum;
            public Glyph match;

            public BestMatch(double distance, Glyph candidate)
            {
                distanceSum = distance;
                match = candidate;
            }
        }
    }

    internal static class Extensions
    {
        internal static double Distance(this Point p1, Point p2)
        {
            var a = p2.X - p1.X;
            var b = p2.Y - p1.Y;
            return Math.Sqrt(a*a + b*b);
        }
    }
}
