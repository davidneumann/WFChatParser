﻿using Application.ChatLineExtractor;
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

namespace RelativeChatParser.Recognition
{
    public static class RelativePixelGlyphIdentifier
    {
        private static int _debugOverlapCount;

        public static FuzzyGlyph[] IdentifyGlyph(ExtractedGlyph extracted, Bitmap b)
        {
            //var candidates = GlyphDatabase.Instance.AllGlyphs.Where(IsValidCandidate(extracted));
            var candidates = GlyphDatabase.Instance.GetGlyphByTargetSize(extracted.Width, extracted.Height);
            //Also remove anything that doesn't look to be aligned correctly
            candidates = candidates.Where(g => extracted.PixelsFromTopOfLine >= g.ReferenceGapFromLineTop - 2
                                            && extracted.PixelsFromTopOfLine <= g.ReferenceGapFromLineTop + 2).ToArray();

            BestMatch current = null;
            foreach (var candidate in candidates)
            {
                double distances = ScoreGlyph(extracted, candidate);

                if (current == null || current.distanceSum > distances)
                    current = new BestMatch(distances, candidate);
            }

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

        private static double GetMinDistanceSum(Point[] source, Point[] target)
        {
            double result = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in source)
            {
                double minDistance = double.MaxValue;
                foreach (var p in target)
                {
                    var d = p.Distance(valid, 4);
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

        private static double GetMinDistanceSum(Point3[] source, Point3[] target)
        {
            double result = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in source)
            {
                double minDistance = double.MaxValue;
                foreach (var p in target)
                {
                    var d = p.Distance(valid, 4);
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

        private static double ScoreGlyph(ExtractedGlyph extracted, FuzzyGlyph candidate)
        {
            double distances = 0;
            //For ever valid pixel find the min distance to a refrence pixel
            foreach (var valid in extracted.RelativePixelLocations)
            {
                double minDistance = double.MaxValue;
                foreach (var p in candidate.RelativePixelLocations)
                {
                    var d = p.Distance(valid, 4);
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
                    var d = p.Distance(empty, 4);
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
                return 10000;
            return Math.Sqrt(x * x + y * y);
        }

        internal static double Distance(this Point3 p1, Point3 p2, int maxAxisDistance)
        {
            var x = p2.X - p1.X;
            var y = p2.Y - p1.Y;
            if (maxAxisDistance < int.MaxValue && (Math.Abs(x) > maxAxisDistance || Math.Abs(y) > maxAxisDistance))
                return 10000;
            var c = p2.Z - p1.Z;
            return Math.Sqrt(x * x + y * y + c * c);
        }
    }
}
