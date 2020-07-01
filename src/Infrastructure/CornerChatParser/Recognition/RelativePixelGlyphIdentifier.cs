using Application.ChatLineExtractor;
using RelativeChatParser.Database;
using RelativeChatParser.Models;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using WebSocketSharp;
using System.Drawing.Imaging;

namespace RelativeChatParser.Recognition
{
    public static class RelativePixelGlyphIdentifier
    {
        private static int _debugOverlapCount;
        public const int MissedDistancePenalty = 10000;

        public static FuzzyGlyph[] IdentifyGlyph(ExtractedGlyph extracted, Bitmap b)
        {
            //if (extracted.Left >= 1382 && extracted.Top >= 1063)
            //{
            //    extracted.Save("bad_glyph.png");
            //    System.Diagnostics.Debugger.Break();
            //}

            //var candidates = GlyphDatabase.Instance.AllGlyphs.Where(IsValidCandidate(extracted));
            var candidates = GlyphDatabase.Instance.GetGlyphByTargetSize(extracted.Width, extracted.Height);
            //Also remove anything that doesn't look to be aligned correctly
            candidates = candidates.Where(g => extracted.PixelsFromTopOfLine >= g.ReferenceGapFromLineTop - 2
                                            && extracted.PixelsFromTopOfLine <= g.ReferenceGapFromLineTop + 2).ToArray();

            var useBrights = true;
            //Add a stupid hack for O Q. 
            if (extracted.Height == 25)
                candidates = candidates.Where(g => g.Character != "Q").ToArray();
            if (extracted.Width <= 4 && (candidates.Any(g => g.Character == "I" || g.Character == "|" || g.Character == "l")))
            {
                var brights = extracted.RelativeBrights.Where(p => p.Z >= 0.949f).ToArray();
                var height = brights.Max(p => p.Y) - brights.Min(p => p.Y) + 1;
                // Try to help with I l.
                // I should be smaller although l and I can both be 24 pixels tall at times
                if (height < 24)
                    candidates = candidates.Where(g => g.Character != "l").ToArray();
                else if(height >= 25)
                    candidates = candidates.Where(g => g.Character != "I").ToArray();
                candidates = candidates.Where(g => {
                    return extracted.Height >= g.ReferenceMinHeight && extracted.Height <= g.ReferenceMaxHeight 
                        && extracted.PixelsFromTopOfLine + 1 >= g.ReferenceGapFromLineTop;
                }).ToArray();
                useBrights = false;
            }
            //if (candidates.Any(g => g.Character == "]" || g.Character == "j"))
            //    candidates = candidates.Where(g => extracted.PixelsFromTopOfLine + 1 >= g.ReferenceGapFromLineTop).ToArray();

            BestMatch current = null;
            foreach (var candidate in candidates)
            {
                var extractedPixels = useBrights ? extracted.RelativeBrights : extracted.RelativePixelLocations;
                var candiatePixels = useBrights ? candidate.RelativeBrights : candidate.RelativePixelLocations;
                //var strict = candidate.Character == "!" || candidate.Character == "i" || candidate.Character == "j" || extracted.Width <= 4 || candidate.Character == "O" || candidate.Character == "Q";
                //double distances = ScoreGlyph(extracted, candidate, strict);
                double distances = 0;
                //Match whichever has more pixels agianst the smlaler one
                //if(extracted.RelativePixelLocations.Where(g => g.Z >= 0.85f).Count() > candidate.RelativePixelLocations.Where(g => g.Z >= 0.85f).Count())
                if(extractedPixels.Length > candiatePixels.Length)
                {
                    distances += GetMinDistanceSum(extractedPixels, candiatePixels);
                    distances += GetMinDistanceSum(extracted.RelativeEmptyLocations, candidate.RelativeEmptyLocations);
                }
                else
                {
                    distances += GetMinDistanceSum(candiatePixels, extractedPixels);
                    distances += GetMinDistanceSum(candidate.RelativeEmptyLocations, extracted.RelativeEmptyLocations);
                }

                if (current == null || current.distanceSum > distances)
                    current = new BestMatch(distances, candidate);
            }

            //if (current.distanceSum > 1000)
            //    Console.WriteLine("Possible error");

            List<FuzzyGlyph> overlaps = new List<FuzzyGlyph>();
            if (current == null)
            {
                //todo: fix tree code not handling way bigger DB
                return overlaps.ToArray();
                //System.Diagnostics.Debugger.Break();
                //Console.WriteLine($"Probably an overlap at {extracted.Left}, {extracted.Top}.");
                var tree = TreeMaker(extracted, new BestMatchNode(new BestMatch(0, null)));
                //Console.WriteLine("Guess\tBranchScore");
                //foreach (var child in tree.Children)
                //{
                //    Console.WriteLine($"{child.BestMatch.match.Character}\t{child.BestMatch.distanceSum}");
                //}
                var (branch, score, bestTree) = GetBestBranch(tree, new FuzzyGlyph[0]);
                var flat = branch.Aggregate("", (acc, glyph) => acc + glyph.Character);
                foreach (var g in branch)
                {
                    overlaps.Add(g);
                }
                //Console.WriteLine(flat);
            }
            //else
            //    Console.Write(current.match.Character);

            return current != null ? new[] { current.match } : overlaps.ToArray();
        }

        private static Func<FuzzyGlyph, bool> IsValidCandidate(ExtractedGlyph extracted)
        {
            return g => extracted.Width >= g.ReferenceMinWidth - 1 &&
                        extracted.Width <= g.ReferenceMaxWidth + 1 &&
                        extracted.Height >= g.ReferenceMinHeight - 1 &&
                        extracted.Height <= g.ReferenceMaxHeight + 1;
        }

        private static double GetMinDistanceSum(Point[] source, Point[] target, int distanceThreshold = 7)
        {
            double result = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in source)
            {
                double minDistance = MissedDistancePenalty;
                foreach (var p in target)
                {
                    var d = p.Distance(valid, distanceThreshold);
                    if (d < minDistance)
                        minDistance = d;
                    if (d == 0)
                        break;
                }
                result += minDistance;

                //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
            }

            return result;
        }

        private static double GetMinDistanceSum(Point3[] source, Point3[] target, int distanceThreshold = 2)
        {
            double result = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in source)
            {
                double minDistance = MissedDistancePenalty;
                foreach (var p in target)
                {
                    var d = p.Distance(valid, distanceThreshold);
                    if (d < minDistance)
                        minDistance = d;
                    if (d == 0)
                        break;
                }
                result += minDistance;
                //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
            }

            return result;
        }

        private static double ScoreGlyph(ExtractedGlyph extracted, FuzzyGlyph candidate, bool strict = false)
        {
            double distances = 0;
            //Shift extracted to have bottom match extracted
            Point3[] ePixels = extracted.RelativePixelLocations;
            Point3[] cPixels = candidate.RelativePixelLocations;
            Point[] eEmpties = extracted.RelativeEmptyLocations;

            if(strict)
            {
                ePixels = ePixels.Where(p => p.Z > 0.8f).ToArray();
                cPixels = cPixels.Where(p => p.Z > 0.8f).ToArray();
            }

            ////Align bottoms
            //var bottomCandidate = cPixels.Max(p => p.Y);
            //var bottomExtracted = ePixels.Max(p => p.Y);
            //if (bottomCandidate > bottomExtracted)
            //{
            //    var diff = bottomCandidate - bottomExtracted;
            //    ePixels = ePixels.Select(p => new Point3(p.X, p.Y + diff, p.Z)).ToArray();
            //    eEmpties = eEmpties.Select(p => new Point(p.X, p.Y + diff)).ToArray();
            //}
            
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in ePixels)
            {
                double minDistance = MissedDistancePenalty;
                foreach (var p in cPixels)
                {
                    var d = p.Distance(valid, 4);
                    if (d < minDistance)
                        minDistance = d;
                    if (d == 0)
                        break;
                }
                distances += minDistance;

                //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
            }
            //Do the same but with empties
            foreach (var empty in eEmpties)
            {
                double minDistance = MissedDistancePenalty;
                foreach (var p in candidate.RelativeEmptyLocations)
                {
                    var d = p.Distance(empty, 12);
                    if (extracted.Width <= 4)
                        d = d * 5f;
                    if (d < minDistance)
                        minDistance = d;
                    if (d == 0)
                        break;
                }
                distances += minDistance;

                //distances += candidate.RelativeEmptyLocations.Min(p => p.Distance(empty));
            }

            return distances;
        }

        private static (FuzzyGlyph[], double, BestMatchNode) GetBestBranch(BestMatchNode tree, FuzzyGlyph[] branch)
        {
            if (tree.Children.Count > 0)
            {
                var results = new Dictionary<BestMatchNode, (FuzzyGlyph[], double)>();
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
                return (new FuzzyGlyph[0], 0, tree);
        }


        private static BestMatchNode TreeMaker(ExtractedGlyph extracted, BestMatchNode node)
        {
            //Console.WriteLine("Tree maker has started");
            //In theory the best match is what ever chains the most
            BestMatch current = null;
            var bests = new List<BestMatch>();
            foreach (var glyph in GlyphDatabase.Instance.GlyphsBySizeDescending())
            {
                // Can't be bigger
                if (glyph.ReferenceMinWidth > extracted.Width)
                    continue;

                // Can't be higher up than the extracted glyph
                if (glyph.ReferenceGapFromLineTop < extracted.PixelsFromTopOfLine - 2)
                    continue;


                double distances = 0;

                var relPixelSubregion = extracted.RelativePixelLocations.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();


                var relYMax = 0;
                var relYMin = int.MaxValue;
                foreach (var p in relPixelSubregion)
                {
                    if (p.Y > relYMax)
                        relYMax = p.Y;
                    if (p.Y < relYMin)
                        relYMin = p.Y;
                }
                var relTop = extracted.PixelsFromTopOfLine + relYMin;
                var relHeight = relYMax - relYMin + 1;

                //Skip any glyph that once filtered down is the wrong height or in the wrong Y of the line
                if (relHeight < glyph.ReferenceMinHeight - 1 || relHeight > glyph.ReferenceMaxHeight + 1
                    || relTop < glyph.ReferenceGapFromLineTop - 1 || relTop > glyph.ReferenceGapFromLineTop + 1)
                    continue;


                var relEmptySubregion = extracted.RelativeEmptyLocations.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();
                //distances += GetMinDistanceSum(glyph.RelativePixelLocations, extracted.RelativePixelLocations);
                //distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, extracted.RelativeEmptyLocations);
                distances += GetMinDistanceSum(relPixelSubregion, glyph.RelativePixelLocations);
                distances += GetMinDistanceSum(relEmptySubregion, glyph.RelativeEmptyLocations);

                if ((current == null || current.distanceSum > distances) && distances <= 300)
                {
                    current = new BestMatch(distances, glyph);
                    bests.Add(current);
                }
            }

            // Possible exit if bests is 0 items
            if (bests.Count == 0)
            {
                //return node;
                if (extracted.Width > 0)
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
            public FuzzyGlyph match;

            public BestMatch(double distance, FuzzyGlyph candidate)
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
        internal static double Distance(this Point p1, Point p2, int maxAxisDistance = int.MaxValue)
        {
            var x = p2.X - p1.X;
            var y = p2.Y - p1.Y;
            if (maxAxisDistance < int.MaxValue && (Math.Abs(x) > maxAxisDistance || Math.Abs(y) > maxAxisDistance))
                return RelativePixelGlyphIdentifier.MissedDistancePenalty;
            return Math.Sqrt(x * x + y * y);
        }

        internal static double Distance(this Point3 p1, Point3 p2, int maxAxisDistance)
        {
            var x = p2.X - p1.X;
            var y = p2.Y - p1.Y;
            if (maxAxisDistance < int.MaxValue && (Math.Abs(x) > maxAxisDistance || Math.Abs(y) > maxAxisDistance))
                return RelativePixelGlyphIdentifier.MissedDistancePenalty;
            var c = p2.Z - p1.Z;
            return Math.Sqrt(x * x + y * y + c * c);
        }
    }
}
