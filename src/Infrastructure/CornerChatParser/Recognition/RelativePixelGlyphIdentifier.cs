using Application.ChatLineExtractor;
using CornerChatParser.Database;
using CornerChatParser.Models;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CornerChatParser.Recognition
{
    public static class RelativePixelGlyphIdentifier
    {
        public static Glyph IdentifyGlyph(ExtractedGlyph extracted)
        {
            var candidates = GlyphDatabase.AllGlyphs.Where(g => extracted.Width >= g.ReferenceMinWidth &&
                                                                extracted.Width <= g.ReferenceMaxWidth &&
                                                                extracted.Height >= g.ReferenceMinHeight &&
                                                                extracted.Height <= g.ReferenceMaxHeight);
            //Also remove anything that doesn't look to be aligned correctly
            candidates = candidates.Where(g => extracted.PixelsFromTopOfLine >= g.ReferenceGapFromLineTop - 1
                                            && extracted.PixelsFromTopOfLine <= g.ReferenceGapFromLineTop + 1);
            
            BestMatch current = null;
            foreach (var candidate in candidates)
            {
                double distances = ScoreGlyph(extracted, candidate);

                if (current == null || current.distanceSum > distances)
                    current = new BestMatch(distances, candidate);
            }

            if (current == null)
            {
                //System.Diagnostics.Debugger.Break();
                Console.WriteLine($"Probably an overlap at {extracted.Left}, {extracted.Top}.");
                ParseOverlappingGlyph(extracted);
            }
            //else
            //    Console.Write(current.match.Character);

            return current != null ? current.match : null;
        }

        private static double GetMinDistanceSum(Point[] source, Point[] target)
        {
            double result = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in source)
            {
                double minDistance = double.MaxValue;
                foreach (var p in target)
                {
                    var d = p.Distance(valid);
                    if (d < minDistance)
                        minDistance = d;
                    if (d == 0)
                        break;
                }
                if (minDistance < double.MaxValue)
                    result += minDistance;

                //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
            }

            return result;
        }

        private static double ScoreGlyph(ExtractedGlyph extracted, Glyph candidate)
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

            return distances;
        }

        private static Glyph ParseOverlappingGlyph(ExtractedGlyph extracted)
        {
            ImageCache ic = null;
            //In theory the best match is what ever chains the most
            BestMatch current = null;
            var bests = new List<BestMatch>();
            foreach (var glyph in GlyphDatabase.GlyphsBySizeDescending())
            {
                double distances = 0;
                distances += GetMinDistanceSum(glyph.RelativePixelLocations, extracted.RelativePixelLocations);
                distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, extracted.RelativeEmptyLocations);

                if (current == null || current.distanceSum > distances)
                {
                    current = new BestMatch(distances, glyph);
                    bests.Add(current);
                }
            }

            Console.WriteLine("Guess\tScore\tWidth");
            foreach (var guess in bests)
            {
                //var removed = extracted.Remove(guess);
                Console.WriteLine(guess.match.Character + "\t" + (Math.Round(guess.distanceSum, 2)) + "\t" + guess.match.ReferenceMaxWidth);
            }

            return current != null ? current.match : null;
        }

        //private static BestMatch[] GetBestMatchChain(ExtractedGlyph extracted)
        //{
        //    var bests = new List<BestMatch>();
        //    foreach (var glyph in GlyphDatabase.GlyphsBySizeDescending())
        //    {
        //        double distances = 0;
        //        distances += GetMinDistanceSum(glyph.RelativePixelLocations, extracted.RelativePixelLocations);
        //        distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, extracted.RelativeEmptyLocations);

        //        if (current == null || current.distanceSum > distances)
        //        {
        //            current = new BestMatch(distances, glyph);
        //            bests.Add(current);
        //        }
        //    }

        //    var temp = bests.First();
        //    var next = GetBestMatchChain(extracted.Remove(temp));
        //}

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
