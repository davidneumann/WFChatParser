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
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RelativeChatParser.Recognition
{
    public static class FastRelativePixelGlyphIdentifier
    {
        public const int MissedDistancePenalty = 1000;
#if DEBUG
        private const bool _stopOnCords = false;
#endif
        public static string overlapDir = Path.Combine("debug", "overlaps");

        public static event EventHandler SaveRequested;

        static FastRelativePixelGlyphIdentifier()
        {
            if (!Directory.Exists(overlapDir))
                Directory.CreateDirectory(overlapDir);
        }

        public static FastFuzzyGlyph[] IdentifyGlyph(FastExtractedGlyph extracted, bool allowOverlaps = false)
        {
#if DEBUG
            var debugLeft = 134;
            var debugTop = 1413;
            var debugRight = 155;
            var debugBottom = 1448;
            if (ShouldDoDebugGlyphStuff(extracted, debugLeft, debugTop, debugRight, debugBottom))
            {
                System.Diagnostics.Debugger.Break();
            }
#endif

            //var candidates = GlyphDatabase.Instance.AllGlyphs.Where(IsValidCandidate(extracted));
            var candidates = FastGlyphDatabase.Instance.GetGlyphByTargetSize(extracted.Width, extracted.Height).Where(g => g.IsOverlap == allowOverlaps).ToArray();
            //Also remove anything that doesn't look to be aligned correctly
            candidates = candidates.Where(g => extracted.PixelsFromTopOfLine >= g.ReferenceGapFromLineTop - 2
                                            && extracted.PixelsFromTopOfLine <= g.ReferenceGapFromLineTop + 2).ToArray();
            if (!allowOverlaps && candidates.Length == 0)
                return IdentifyGlyph(extracted, true);
            bool useBrights = FilterCandidates(extracted, ref candidates);

#if DEBUG
            if (ShouldDoDebugGlyphStuff(extracted, debugLeft, debugTop, debugRight, debugBottom))
            {
                extracted.Save("bad_glyph.png", useBrights);
            }
#endif

            FastBestMatch current = null;
            foreach (var candidate in candidates)
            {
#if DEBUG
                if (ShouldDoDebugGlyphStuff(extracted, debugLeft, debugTop, debugRight, debugBottom))
                {
                    var name = candidate.Character;
                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        name = Guid.NewGuid().ToString();
                    candidate.SaveVisualization("bad_glyph_candidate_" + name + ".png", useBrights);
                    //System.Diagnostics.Debugger.Break();
                }
#endif
                double distances = ScoreCandidate(extracted, useBrights, candidate);

                if (distances < MissedDistancePenalty && (current == null || current.distanceSum > distances))
                    current = new FastBestMatch(distances, candidate);
            }

            if (current == null && !allowOverlaps)
                return IdentifyGlyph(extracted, !allowOverlaps);

            //Try to break apart an overlap
            if (current == null)
            {
#if DEBUG
                extracted.Save($"overlap_{Guid.NewGuid().ToString()}_{extracted.Left},{extracted.Top}.png");
                if (ShouldDoDebugGlyphStuff(extracted, debugLeft, debugTop, debugRight, debugBottom))
                    System.Diagnostics.Debugger.Break();
#endif
                //RequestToKill?.Invoke(this, EventArgs.Empty);
                var time = DateTime.Now.Ticks;
                extracted.Save(Path.Combine(overlapDir, $"{time}_{Guid.NewGuid()}.png"));
                SaveRequested?.Invoke(null, EventArgs.Empty);

                throw new NotImplementedException();
                //return ExtractOverlap(extracted);
            }

#if DEBUG
            if (ShouldDoDebugGlyphStuff(extracted, debugLeft, debugTop, debugRight, debugBottom))
                System.Diagnostics.Debugger.Break();
#endif

            return current != null ? new[] { current.match } : new FastFuzzyGlyph[0];
        }

#if DEBUG
        private static bool ShouldDoDebugGlyphStuff(FastExtractedGlyph extracted, int debugLeft, int debugTop, int debugRight, int debugBottom)
        {
            return extracted.Left >= debugLeft && extracted.Top >= debugTop
                            && extracted.Left <= debugRight && extracted.Top <= debugBottom
                            && _stopOnCords;
        }
#endif

        private static bool FilterCandidates(FastExtractedGlyph extracted, ref FastFuzzyGlyph[] candidates)
        {
            var useBrights = true;
            //if (allowOverlaps)
            //    useBrights = false;
            //Add a stupid hack for O Q. 
            if (extracted.Height == 25)
                candidates = candidates.Where(g => g.Character != "Q").ToArray();
            if (extracted.Width <= 4 && (candidates.Any(g => g.Character == "I" || g.Character == "|" || g.Character == "l")))
            {
                var maxY = 0;
                var minY = extracted.Height;
                for (int y = 0; y < extracted.Height; y++)
                {
                    for (int x = 0; x < extracted.Width; x++)
                    {
                        if(extracted.RelativeBrights[x, y] >= 0.949f)
                        {
                            maxY = Math.Max(y, maxY);
                            minY = Math.Min(y, minY);
                        }
                    }
                }
                var height = maxY - minY + 1;

                // Try to help with I l.
                // I should be smaller although l and I can both be 24 pixels tall at times
                if (height < 24)
                    candidates = candidates.Where(g => g.Character != "l").ToArray();
                else if (height >= 25)
                    candidates = candidates.Where(g => g.Character != "I").ToArray();
                var likelyMatches = candidates.Where(g =>
                {
                    return extracted.Height >= g.ReferenceMinHeight && extracted.Height <= g.ReferenceMaxHeight
                        && extracted.PixelsFromTopOfLine + 1 >= g.ReferenceGapFromLineTop;
                }).ToArray();
                if (likelyMatches.Length > 0)
                    candidates = likelyMatches;
                useBrights = false;
            }

            if (candidates.Any(c => c.Character == "." || c.Character == "_"))
                useBrights = false;

            // Some characters don't match well with only brights
            if (candidates.Any(c => c.Character == "n" || c.Character == "o" || c.Character == "u" || c.Character == "D" || c.Character == "O" || c.Character == "6" || c.Character.Length > 1))
                useBrights = false;

            return useBrights;
        }

        public static double ScoreCandidate(FastExtractedGlyph extracted, bool useBrights, FastFuzzyGlyph candidate)
        {
            double distances = 0;
            
            int candidatePixelCount;
            float[,] candidatePixels;
            bool[,] candidateEmpties = candidate.RelativeEmpties;
            int extractedPixelCount;
            float[,] extractedPixels;
            bool[,] extractedEmpties = extracted.RelativeEmpties;
            if (useBrights)
            {
                extractedPixels = extracted.RelativeBrights;
                candidatePixels = candidate.RelativeBrights;
                candidatePixelCount = candidate.RelativeBrightsCount;
                extractedPixelCount = extracted.RelativeBrightsCount;
            }
            else
            {
                extractedPixels = extracted.RelativePixels;
                candidatePixels = candidate.RelativePixels;
                candidatePixelCount = candidate.RelativePixelsCount;
                extractedPixelCount = extracted.RelativePixelsCount;
            }
            //Center align both arrays
            if(candidatePixels.GetLength(0) != extractedPixels.GetLength(0) || candidatePixels.GetLength(1) != extractedPixels.GetLength(1))
            {
                int cWidth = candidatePixels.GetLength(0);
                int eWidth = extractedPixels.GetLength(0);
                var maxWidth = Math.Max(cWidth, eWidth);
                int cHeight = candidatePixels.GetLength(1);
                int eHeight = extractedPixels.GetLength(1);
                var maxHeight = Math.Max(cHeight, eHeight);
                float[,] alignedExtractedPixels = extractedPixels;
                float[,] alignedCandidatePixels = candidatePixels;
                bool[,] alignedExtractedEmpties = extractedEmpties;
                bool[,] alignedCandidateEmpties = candidateEmpties;

                if (cWidth != maxWidth || cHeight != maxHeight)
                {
                    alignedCandidatePixels = PadArray(candidatePixels, maxWidth, maxHeight, 0f);
                    alignedCandidateEmpties = PadArray(candidateEmpties, maxWidth, maxHeight, false);
                }

                if (eWidth != maxWidth || eHeight != maxHeight)
                {
                    alignedExtractedPixels = PadArray(extractedPixels, maxWidth, maxHeight, 0f);
                    alignedExtractedEmpties = PadArray(extractedEmpties, maxWidth, maxHeight, false);
                }

                candidatePixels = alignedCandidatePixels;
                candidateEmpties = alignedCandidateEmpties;
                extractedPixels = alignedExtractedPixels;
                extractedEmpties = alignedExtractedEmpties;
            }

            //Match whichever has more pixels agianst the smlaler one
            if (extractedPixelCount > candidatePixelCount)
            {
                distances += GetMinDistanceSum(extractedPixels, candidatePixels);
                var eDistance = GetMinDistanceSum(extractedEmpties, candidateEmpties) * 2f;
                if (eDistance >= MissedDistancePenalty)
                {
                    eDistance = GetMinDistanceSum(extracted.RelativeCombinedMask, candidate.RelativeCombinedMask) * 15f;
                }
                distances += eDistance;
            }
            else
            {
                distances += GetMinDistanceSum(candidatePixels, extractedPixels);
                var eDistance = GetMinDistanceSum(candidateEmpties, extractedEmpties) * 2f;
                if (eDistance >= MissedDistancePenalty)
                {
                    eDistance = GetMinDistanceSum(candidate.RelativeCombinedMask, extracted.RelativeCombinedMask) * 15f;
                }
                distances += eDistance;
            }

            return distances;
        }

        public static T[,] PadArray<T>(T[,] arr, int newWidth, int newHeight, T padValue)
        {
            T[,] newArray = new T[newWidth, newHeight];
            var leftPadding = (int)Math.Floor((newWidth - arr.GetLength(0)) / 2f);
            var topPadding = (int)Math.Floor((newHeight - arr.GetLength(1)) / 2f);

            //Fill in padded areas with 0/false
            for (int x = 0; x < leftPadding; x++)
            {
                for (int y = 0; y < newHeight; y++)
                {
                    newArray[x, y] = padValue;
                }
            }
            for (int x = leftPadding + arr.GetLength(0); x < newWidth; x++)
            {
                for (int y = 0; y < newHeight; y++)
                {
                    newArray[x, y] = padValue;
                }
            }
            for (int y = 0; y < topPadding; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    newArray[x, y] = padValue;
                }
            }
            for (int y = topPadding + arr.GetLength(1); y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    newArray[x, y] = padValue;
                }
            }

            //Fill in the real data
            for (int x = leftPadding; x < arr.GetLength(0) + leftPadding; x++)
            {
                for (int y = topPadding; y < arr.GetLength(1) + topPadding; y++)
                {
                    newArray[x, y] = arr[x - leftPadding, y - topPadding];
                }
            }

            return newArray;
        }

//        private static FastFuzzyGlyph[] ExtractOverlap(FastExtractedGlyph extracted)
//        {
//            var matches = new List<FastBestMatch>();
//            //var origExtractedEmpties = extracted.RelativeEmptyLocations;
//            //var origRelativePixelLocations = extracted.RelativePixelLocations;
//            //var origRelativeBrights = extracted.RelativeBrights;
//            //var origRelativeEmptyLocations = extracted.RelativeEmptyLocations;
//            //var origRelativeCombinedLocations = extracted.CombinedLocations;
//            var origExtracted = extracted.Clone();

//#if DEBUG
//            var guid = Guid.NewGuid().ToString();
//            var eCount = 0;
//#endif

//            while (extracted != null && extracted.RelativeBrights.Length > 0)
//            {
//#if DEBUG
//                extracted.Save($"extracted_source_{eCount++}_{guid}.png");
//#endif
//                var widthAllowance = 1;
//                if (extracted.Height <= 4)
//                    widthAllowance = 2;
//                var candidates = FastGlyphDatabase.Instance.CharsThatCanOverlapByDescSize()
//                    .Where(g => g.ReferenceMaxHeight <= extracted.Height + 1 && g.ReferenceGapFromLineTop >= extracted.PixelsFromTopOfLine - 1 && g.ReferenceMinWidth <= extracted.Width + widthAllowance).ToArray();

//                var useBrights = FilterCandidates(extracted, ref candidates);

//                var maxHeight = 0;
//                var maxWidth = 0;
//                foreach (var c in candidates)
//                {
//                    maxHeight = Math.Max(maxHeight, c.ReferenceMaxHeight);
//                    maxWidth = Math.Max(maxWidth, c.ReferenceMaxWidth);
//                }

//                FastBestMatch best = null;
//                foreach (var candidate in candidates)
//                {
//                    int right = candidate.ReferenceMaxWidth;
//                    if (maxHeight <= 4 || candidate.ReferenceMaxWidth <= 4)
//                        right = maxWidth;

//                    var extractedClone = extracted.Clone();
//                    extractedClone.RelativePixels = extractedClone.RelativePixels.Where(p => p.X < right).ToArray();
//                    extractedClone.RelativeBrights = extractedClone.RelativeBrights.Where(p => p.X < right).ToArray();
//                    extractedClone.RelativeEmpties = extractedClone.RelativeEmpties.Where(p => p.X < right).ToArray();
//                    extractedClone.RelativeCombinedMask = extractedClone.RelativeCombinedMask.Where(p => p.X < right).ToArray();

//                    //The candidate needs to have its details moved down to account for misaligned top things. Think of matching a _ to a _J
//                    var vOffset = (int)(Math.Round(candidate.ReferenceGapFromLineTop)) - extractedClone.PixelsFromTopOfLine;
//                    var adjustedCandidate = candidate;
//                    if (vOffset != 0)
//                    {
//                        adjustedCandidate = candidate.Clone();
//                        adjustedCandidate.RelativePixelLocations = candidate.RelativePixelLocations.Select(p => new Point3(p.X, p.Y + vOffset, p.Z)).ToArray();
//                        adjustedCandidate.RelativeBrights = candidate.RelativeBrights.Select(p => new Point3(p.X, p.Y + vOffset, p.Z)).ToArray();
//                        adjustedCandidate.RelativeEmpties = candidate.RelativeEmpties.Select(p => new Point(p.X, p.Y + vOffset)).ToArray();
//                        adjustedCandidate.RelativeCombinedMask = candidate.RelativeCombinedMask.Select(p => new Point(p.X, p.Y + vOffset)).ToArray();
//                    }
//                    extractedClone.RelativeEmpties = extractedClone.RelativeEmpties.Where(e => e.X < right).ToArray();

//#if DEBUG
//                    try
//                    {
//                        adjustedCandidate.SaveVisualization($"extracted_candidate_{candidate.Character}_{guid}.png", useBrights);
//                    }
//                    catch{ }
//#endif
//                    var distances = ScoreCandidate(extractedClone, useBrights, adjustedCandidate) / right;

//                    if (best == null || best.distanceSum > distances)
//                    {
//                        best = new FastBestMatch(distances, candidate);
//                        best.vOffset = vOffset;
//                    }
//                }

//                //var guid = Guid.NewGuid();
//                //if (origExtracted.Left == 2983 && origExtracted.Top == 820)
//                //    System.Diagnostics.Debugger.Break();

//                //Subract what was seen
//                if (best != null)
//                {
//                    //origExtracted.Save($"debug_remove_{guid}_before.png");
//                    extracted = extracted.Subtract(best.match, best.vOffset);
//                    //if(extracted != null)
//                    //    extracted.Save($"debug_remove_{guid}_after.png");
//                    matches.Add(best);
//                }

//                if (best == null || extracted == null)
//                    break;
//            }

//            return matches.Select(m => m.match).ToArray();
//        }

        private static Func<FastFuzzyGlyph, bool> IsValidCandidate(FastExtractedGlyph extracted)
        {
            return g => extracted.Width >= g.ReferenceMinWidth - 1 &&
                        extracted.Width <= g.ReferenceMaxWidth + 1 &&
                        extracted.Height >= g.ReferenceMinHeight - 1 &&
                        extracted.Height <= g.ReferenceMaxHeight + 1;
        }

        private static double Distance(int x1, int x2, int y1, int y2, float z1, float z2)
        {
            var x = x2 - x1;
            var y = y2 - y1;
            var z = z2 - z1;
            return Math.Sqrt(x * x + y * y + z * z);
        }
        private static double Distance(int x1, int x2, int y1, int y2)
        {
            var x = x2 - x1;
            var y = y2 - y1;
            return Math.Sqrt(x * x + y * y);
        }

        private static double GetMinDistanceSum(float[,] source, float[,] target, int distanceThreshold = 2)
        {
            double result = 0;
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    //If this is a black spot ignore it with no penality
                    if(source[x, y] <= 0f)
                    {
                        continue;
                    }

                    //Search around the distance threshold on the target
                    double minDistance = MissedDistancePenalty;
                    for (int x2 = Math.Max(0, x - distanceThreshold); x2 < Math.Min(width, x + distanceThreshold); x2++)
                    {
                        for (int y2 = Math.Max(0, y - distanceThreshold); y2 < Math.Min(height, y + distanceThreshold); y2++)
                        {
                            minDistance = Math.Min(minDistance, Distance(x, x2, y, y2, source[x, y], target[x2, y2]));
                            if (minDistance == 0)
                                break;
                        }
                        if (minDistance == 0)
                            break;
                    }

                    result += minDistance;
                }
            }
            return result;
        }

        private static double GetMinDistanceSum(bool[,] source, bool[,] target, int distanceThreshold = 7)
        {
            double result = 0;
            int width = source.GetLength(0);
            int height = source.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                double minDistance = MissedDistancePenalty;
                for (int y = 0; y < height; y++)
                {
                    //If this is a black spot ignore it with no penality
                    if (!source[x, y])
                    {
                        minDistance = 0;
                        continue;
                    }

                    //Search around the distance threshold on the target
                    for (int x2 = Math.Max(0, x - distanceThreshold); x2 < Math.Min(width, x + distanceThreshold); x2++)
                    {
                        for (int y2 = Math.Max(0, y - distanceThreshold); y2 < Math.Min(height, y + distanceThreshold); y2++)
                        {
                            if(target[x2, y2])
                                minDistance = Math.Min(minDistance, Distance(x, x2, y, y2));

                            if (minDistance == 0)
                                break;
                        }
                        if (minDistance == 0)
                            break;
                    }
                    if (minDistance == 0)
                        break;
                }

                result += minDistance;
            }
            return result;
        }

        //private static double ScoreGlyph(FastExtractedGlyph extracted, FastFuzzyGlyph candidate, bool strict = false)
        //{
        //    double distances = 0;
        //    //Shift extracted to have bottom match extracted
        //    Point3[] ePixels = extracted.RelativePixels;
        //    Point3[] cPixels = candidate.RelativePixelLocations;
        //    Point[] eEmpties = extracted.RelativeEmpties;

        //    if (strict)
        //    {
        //        ePixels = ePixels.Where(p => p.Z > 0.8f).ToArray();
        //        cPixels = cPixels.Where(p => p.Z > 0.8f).ToArray();
        //    }

        //    ////Align bottoms
        //    //var bottomCandidate = cPixels.Max(p => p.Y);
        //    //var bottomExtracted = ePixels.Max(p => p.Y);
        //    //if (bottomCandidate > bottomExtracted)
        //    //{
        //    //    var diff = bottomCandidate - bottomExtracted;
        //    //    ePixels = ePixels.Select(p => new Point3(p.X, p.Y + diff, p.Z)).ToArray();
        //    //    eEmpties = eEmpties.Select(p => new Point(p.X, p.Y + diff)).ToArray();
        //    //}

        //    //For ever valid pixel find the min distance to a refrence pixel
        //    foreach (var valid in ePixels)
        //    {
        //        double minDistance = MissedDistancePenalty;
        //        foreach (var p in cPixels)
        //        {
        //            var d = p.Distance(valid, 4);
        //            if (d < minDistance)
        //                minDistance = d;
        //            if (d == 0)
        //                break;
        //        }
        //        distances += minDistance;

        //        //distances += candidate.RelativePixelLocations.Min(p => p.Distance(valid));
        //    }
        //    //Do the same but with empties
        //    foreach (var empty in eEmpties)
        //    {
        //        double minDistance = MissedDistancePenalty;
        //        foreach (var p in candidate.RelativeEmpties)
        //        {
        //            var d = p.Distance(empty, 12);
        //            if (extracted.Width <= 4)
        //                d = d * 5f;
        //            if (d < minDistance)
        //                minDistance = d;
        //            if (d == 0)
        //                break;
        //        }
        //        distances += minDistance;

        //        //distances += candidate.RelativeEmptyLocations.Min(p => p.Distance(empty));
        //    }

        //    return distances;
        //}

        //private static (FastFuzzyGlyph[], double, FastBestMatchNode) GetBestBranch(FastBestMatchNode tree, FastFuzzyGlyph[] branch)
        //{
        //    if (tree.Children.Count > 0)
        //    {
        //        var results = new Dictionary<FastBestMatchNode, (FastFuzzyGlyph[], double)>();
        //        foreach (var child in tree.Children)
        //        {
        //            var (g, score, _) = GetBestBranch(child, branch);
        //            results[child] = (g, child.BestMatch.distanceSum + score);
        //        }

        //        var bestChild = results.OrderBy(kvp => kvp.Value.Item2).First();
        //        return (new[] { bestChild.Key.BestMatch.match }.Concat(bestChild.Value.Item1).ToArray(),
        //            bestChild.Value.Item2, bestChild.Key);
        //    }
        //    else
        //        return (new FastFuzzyGlyph[0], 0, tree);
        //}


        //private static FastBestMatchNode TreeMaker(FastExtractedGlyph extracted, FastBestMatchNode node)
        //{
        //    //Console.WriteLine("Tree maker has started");
        //    //In theory the best match is what ever chains the most
        //    FastBestMatch current = null;
        //    var bests = new List<FastBestMatch>();
        //    foreach (var glyph in FastGlyphDatabase.Instance.CharsThatCanOverlapByDescSize().Where(g => g.ReferenceMaxHeight <= extracted.Height + 1 && g.ReferenceGapFromLineTop >= extracted.PixelsFromTopOfLine - 2))
        //    {
        //        // Can't be bigger
        //        if (glyph.ReferenceMinWidth > extracted.Width)
        //            continue;

        //        // Can't be higher up than the extracted glyph
        //        if (glyph.ReferenceGapFromLineTop < extracted.PixelsFromTopOfLine - 2)
        //            continue;


        //        double distances = 0;

        //        var relPixelSubregion = extracted.RelativePixels.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();


        //        var relYMax = 0;
        //        var relYMin = int.MaxValue;
        //        foreach (var p in relPixelSubregion)
        //        {
        //            if (p.Y > relYMax)
        //                relYMax = p.Y;
        //            if (p.Y < relYMin)
        //                relYMin = p.Y;
        //        }
        //        var relTop = extracted.PixelsFromTopOfLine + relYMin;
        //        var relHeight = relYMax - relYMin + 1;

        //        //Skip any glyph that once filtered down is the wrong height or in the wrong Y of the line
        //        if (relHeight < glyph.ReferenceMinHeight - 1 || relHeight > glyph.ReferenceMaxHeight + 1
        //            || relTop < glyph.ReferenceGapFromLineTop - 1 || relTop > glyph.ReferenceGapFromLineTop + 1)
        //            continue;


        //        var relEmptySubregion = extracted.RelativeEmpties.Where(p => p.X < glyph.ReferenceMaxWidth).ToArray();
        //        //distances += GetMinDistanceSum(glyph.RelativePixelLocations, extracted.RelativePixelLocations);
        //        //distances += GetMinDistanceSum(glyph.RelativeEmptyLocations, extracted.RelativeEmptyLocations);
        //        distances += GetMinDistanceSum(relPixelSubregion, glyph.RelativePixelLocations);
        //        distances += GetMinDistanceSum(relEmptySubregion, glyph.RelativeEmpties);

        //        if ((current == null || current.distanceSum > distances) && distances <= 300)
        //        {
        //            current = new FastBestMatch(distances, glyph);
        //            bests.Add(current);
        //        }
        //    }

        //    // Possible exit if bests is 0 items
        //    if (bests.Count == 0)
        //    {
        //        //return node;
        //        if (extracted.Width > 0)
        //            node.BestMatch.distanceSum = 100000;
        //        return node;
        //    }
        //    else
        //    {
        //        var children = bests.Select(b => new FastBestMatchNode(b)).ToList();
        //        node.Children = children;
        //        foreach (var child in children)
        //        {
        //            var remainder = extracted.Subtract(child.BestMatch.match);

        //            if (remainder == null || remainder.Width == 0)
        //                continue;
        //            TreeMaker(remainder, child);
        //        }
        //    }

        //    return node;
        //}

        [System.Diagnostics.DebuggerDisplay("{BestMatch}")]
        private class FastBestMatchNode
        {
            public FastBestMatch FastBestMatch;
            public List<FastBestMatchNode> Children = new List<FastBestMatchNode>();

            public FastBestMatchNode(FastBestMatch match)
            {
                FastBestMatch = match;
            }
        }
        [System.Diagnostics.DebuggerDisplay("{match} {distanceSum}")]
        private class FastBestMatch
        {
            public double distanceSum;
            public FastFuzzyGlyph match;
            public float vOffset = 0f;

            public FastBestMatch(double distance, FastFuzzyGlyph candidate)
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

    //internal static class Extensions
    //{
    //    internal static double Distance(this Point p1, Point p2, int maxAxisDistance = int.MaxValue)
    //    {
    //        var x = p2.X - p1.X;
    //        var y = p2.Y - p1.Y;
    //        if (maxAxisDistance < int.MaxValue && (Math.Abs(x) > maxAxisDistance || Math.Abs(y) > maxAxisDistance))
    //            return RelativePixelGlyphIdentifier.MissedDistancePenalty;
    //        return Math.Sqrt(x * x + y * y);
    //    }

    //    internal static double Distance(this Point3 p1, Point3 p2, int maxAxisDistance)
    //    {
    //        var x = p2.X - p1.X;
    //        var y = p2.Y - p1.Y;
    //        if (maxAxisDistance < int.MaxValue && (Math.Abs(x) > maxAxisDistance || Math.Abs(y) > maxAxisDistance))
    //            return RelativePixelGlyphIdentifier.MissedDistancePenalty;
    //        var c = p2.Z - p1.Z;
    //        return Math.Sqrt(x * x + y * y + c * c);
    //    }
    //}
}
