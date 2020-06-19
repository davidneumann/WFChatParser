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
            var candidates = GlyphDatabase.AllGlyphs.Where(IsValidCandidate(extracted));
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
                var tree = TreeMaker(extracted, new BestMatchNode(new BestMatch(0, null)));
                Console.WriteLine("Guess\tBranchScore");
                foreach (var child in tree.Children)
                {
                    Console.WriteLine($"{child.BestMatch.match.Character}\t{child.BestMatch.distanceSum}");
                }
                var (branch, score, bestTree) = GetBestBranch(tree, new Glyph[0]);
                var flat = new string(branch.Select(n => n.Character).ToArray());
                Console.WriteLine(flat);
                //ParseOverlappingGlyph(extracted);
            }
            //else
            //    Console.Write(current.match.Character);

            return current != null ? current.match : null;
        }

        private static Func<Glyph, bool> IsValidCandidate(ExtractedGlyph extracted)
        {
                return g => extracted.Width >= g.ReferenceMinWidth &&
                            extracted.Width <= g.ReferenceMaxWidth &&
                            extracted.Height >= g.ReferenceMinHeight &&
                            extracted.Height <= g.ReferenceMaxHeight;
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
                // Can't be bigger
                if (glyph.ReferenceMinWidth > extracted.Width)
                    continue;

                // Can't be higher up than the extracted glyph
                if (glyph.ReferenceGapFromLineTop < extracted.PixelsFromTopOfLine - 1)
                    continue;

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
                var remainder = extracted.Subtract(guess.match);
                var nextBest = ParseOverlappingGlyphStep2(remainder);
                Console.WriteLine(guess.match.Character + "\t" + (Math.Round(guess.distanceSum, 2)) + "\t" + guess.match.ReferenceMaxWidth);
                if (guess.match.Character == '&')
                {
                    Console.WriteLine($"Remainder {remainder.Left},{remainder.Top}. {remainder.Width}x{remainder.Height}.");
                }
            }

            // & _
            // [& _]
            // 

            return current != null ? current.match : null;
        }

        private static (Glyph[], double, BestMatchNode) GetBestBranch(BestMatchNode tree, Glyph[] branch)
        {
            if (tree.Children.Count > 0)
            {
                var results = new Dictionary<BestMatchNode, (Glyph[], double)>();
                foreach (var child in tree.Children)
                {
                    var (g, score, _) = GetBestBranch(child, branch);
                    results[child] = (g, child.BestMatch.distanceSum + score);
                }

                var bestChild = results.OrderBy(kvp => kvp.Value.Item2).First();
                return (new[] { bestChild.Key.BestMatch.match }.Concat(bestChild.Value.Item1).ToArray(),
                    bestChild.Value.Item2, bestChild.Key);
            }
            else
                return (new Glyph[0], 0, tree);
        }


        private static BestMatchNode TreeMaker(ExtractedGlyph extracted, BestMatchNode node)
        {
            //In theory the best match is what ever chains the most
            BestMatch current = null;
            var bests = new List<BestMatch>();
            foreach (var glyph in GlyphDatabase.GlyphsBySizeDescending())
            {
                // Can't be bigger
                if (glyph.ReferenceMinWidth > extracted.Width)
                    continue;

                // Can't be higher up than the extracted glyph
                if (glyph.ReferenceGapFromLineTop < extracted.PixelsFromTopOfLine - 1)
                    continue;

                double distances = 0;
                var relPixelSubregion = extracted.RelativePixelLocations.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();
                var relEmptySubregion = extracted.RelativeEmptyLocations.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();
                //distances += GetMinDistanceSum(glyph.RelativePixelLocations, extracted.RelativePixelLocations);
                //distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, extracted.RelativeEmptyLocations);
                distances += GetMinDistanceSum(relPixelSubregion, glyph.RelativePixelLocations);
                distances += GetMinDistanceSum(relEmptySubregion, glyph.RelativeEmptyLocations);

                if (current == null || current.distanceSum > distances)
                {
                    current = new BestMatch(distances, glyph);
                    bests.Add(current);
                }
            }

            // Possible exit if bests is 0 items
            if(bests.Count == 0)
            {
                //return node;
                if(extracted.Width > 0)
                    node.BestMatch.distanceSum = 100000;
                return node;
            }
            else
            {
                var children = bests.Select(b => new BestMatchNode(b)).ToList();
                node.Children = children;
                foreach (var child in children)
                {
                    var remainder = extracted.Subtract(child.BestMatch.match);

                    if (remainder == null || remainder.Width == 0)
                        continue;
                    TreeMaker(remainder, child);
                }
            }

            return node;
        }






        private static BestMatch ParseOverlappingGlyphStep2(ExtractedGlyph remainder)
        {
            BestMatch current = null;
            var bests = new List<BestMatch>();
            foreach (var glyph in GlyphDatabase.GlyphsBySizeDescending())
            {
                // Can't be bigger
                if (glyph.ReferenceMinWidth > remainder.Width)
                    continue;

                // Can't be higher up than the extracted glyph
                if (glyph.ReferenceGapFromLineTop < remainder.PixelsFromTopOfLine - 1)
                    continue;

                double distances = 0;
                distances += GetMinDistanceSum(glyph.RelativePixelLocations, remainder.RelativePixelLocations);
                distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, remainder.RelativeEmptyLocations);

                if (current == null || current.distanceSum > distances)
                {
                    current = new BestMatch(distances, glyph);
                    bests.Add(current);
                }
            }

            return bests.DefaultIfEmpty()?.Last();
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

        [System.Diagnostics.DebuggerDisplay("{BestMatch}")]
        private class BestMatchNode
        {
            public BestMatch BestMatch;
            public List<BestMatchNode> Children = new List<BestMatchNode>();

            public BestMatchNode(BestMatch match)
            {
                BestMatch = match;
            }
        }
        [System.Diagnostics.DebuggerDisplay("{match} {distanceSum}")]
        private class BestMatch
        {
            public double distanceSum;
            public Glyph match;

            public BestMatch(double distance, Glyph candidate)
            {
                distanceSum = distance;
                match = candidate;
            }

            public override string ToString()
            {
                return match.Character + " " + distanceSum;
            }
        }
    }

    internal static class Extensions
    {
        internal static double Distance(this Point p1, Point p2)
        {
            var a = p2.X - p1.X;
            var b = p2.Y - p1.Y;
            return Math.Sqrt(a * a + b * b);
        }
    }
}
