using Application.ChatLineExtractor;
using CornerChatParser.Database;
using CornerChatParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using WebSocketSharp;

namespace CornerChatParser.Extraction
{
    public static class GlyphIdentifier
    {
        //public static Glyph IdentifyGlyph(ExtractedGlyph extracted)
        //{
        //    var lengthRange = extracted.NormalizedCorners.Length <= 12 ? 3 : extracted.NormalizedCorners.Length * 0.5f;
        //    var possibles = GlyphDatabase.AllGLyphs
        //        .Where(g => extracted.NormalizedCorners.Length >= g.Corners.Length - lengthRange &&
        //                    extracted.NormalizedCorners.Length <= g.Corners.Length + lengthRange);

        //    var extractedTotal = extracted.NormalizedCorners.Aggregate((acc, cur) => new Vector2(acc.X + cur.X, acc.Y + cur.Y));
        //    var extractedCenter = new Vector2(extractedTotal.X / extracted.NormalizedCorners.Length, extractedTotal.Y / extracted.NormalizedCorners.Length);

        //    GlyphMatch best = null;
        //    foreach (var possible in possibles)
        //    {
        //        var distance = 0f;
        //        foreach (var corner in extracted.NormalizedCorners)
        //        {
        //            distance += Vector2.Distance(corner, possible.Center);
        //        }
        //        if (best == null || best.MinDistanceSum > distance)
        //        {
        //            best = new GlyphMatch()
        //            {
        //                BestMatch = possible,
        //                MinDistanceSum = distance
        //            };
        //        }
        //    }

        //    return best.BestMatch;
        //}


        //public static Glyph IdentifyGlyph(ExtractedGlyph extracted)
        //{
        //    GlyphMatch currentBest = null;

        //    //if (extracted.Left >= 1049 && extracted.Top >= 806)
        //    //    System.Diagnostics.Debugger.Break();

        //    foreach (var refGlyph in PossibleReferenceGlyphs(extracted))
        //    {
        //        var distanceSum = 0f;
        //        foreach (var refCorner in refGlyph.Corners)
        //        {
        //            var minDistance = float.MaxValue;
        //            var distancePenality = 1f;
        //            if (extracted.Height > 6)
        //            {
        //                if (!IsHighMidEntropy(refGlyph.Corners))
        //                    distancePenality = 1 + Vector2.Distance(new Vector2(refCorner.X, refCorner.Y), new Vector2(refCorner.X, 0.5f)) * 2f;
        //                else
        //                    distancePenality = 1 + DistanceToTopBott(refCorner) * 2f;
        //            }
        //            foreach (var extractedCorner in extracted.NormalizedCorners)
        //            {
        //                minDistance = Math.Min(minDistance, Vector2.Distance(refCorner, extractedCorner) * distancePenality);
        //            }
        //            distanceSum += minDistance;
        //        }
        //        //Do both orders to prevent some jerk with 1 point from crushing the score
        //        foreach (var extractedCorner in extracted.NormalizedCorners)
        //        {
        //            var minDistance = float.MaxValue;
        //            var distancePenality = 1f;
        //            if (extracted.Height > 6)
        //            {
        //                if (!IsHighMidEntropy(refGlyph.Corners))
        //                    distancePenality = 1 + Vector2.Distance(new Vector2(extractedCorner.X, extractedCorner.Y), new Vector2(extractedCorner.X, 0.5f)) * 2f;
        //                else
        //                    distancePenality = 1 + DistanceToTopBott(extractedCorner) * 2f;
        //            }
        //            foreach (var refCorner in refGlyph.Corners)
        //            {
        //                minDistance = Math.Min(minDistance, Vector2.Distance(refCorner, extractedCorner) * distancePenality);
        //            }
        //            distanceSum += minDistance;
        //        }
        //        if (currentBest == null || currentBest.MinDistanceSum > distanceSum)
        //        {
        //            currentBest = new GlyphMatch()
        //            {
        //                MinDistanceSum = distanceSum,
        //                BestMatch = refGlyph
        //            };
        //        }
        //    }

        //    return currentBest.BestMatch;
        //}

        public static Glyph IdentifyGlyph(ImageCache image, ExtractedGlyph extracted)
        {
            GlyphMatch currentBest = null;

            //if (extracted.Left >= 1220 && extracted.Top >= 1008)
            //    System.Diagnostics.Debugger.Break();

            foreach (var refGlyph in PossibleReferenceGlyphs(extracted))
            {
                var distanceSum = ScoreGlyph(extracted, refGlyph, image);
                if (currentBest == null || currentBest.MinDistanceSum > distanceSum)
                {
                    currentBest = new GlyphMatch()
                    {
                        MinDistanceSum = distanceSum,
                        BestMatch = refGlyph
                    };
                }
            }

            if (currentBest != null && (currentBest.BestMatch.Character == '!'
                                     || currentBest.BestMatch.Character == 'l'
                                     || currentBest.BestMatch.Character == 'I'
                                     || currentBest.BestMatch.Character == 'i'))
                return DoubleCheckLIExclaim(image, extracted, currentBest);
            if (currentBest != null && (currentBest.BestMatch.Character == '0'
                                    || currentBest.BestMatch.Character == 'O'
                                    || currentBest.BestMatch.Character == 'Q'
                                    || currentBest.BestMatch.Character == 'C'
                                    || currentBest.BestMatch.Character == '6'
                                    || currentBest.BestMatch.Character == '8'
                                    || currentBest.BestMatch.Character == '3'
                                    || currentBest.BestMatch.Character == 'G'))
                return DoubleCheck0OQC638G(image, extracted, currentBest);
            else if (currentBest != null && (currentBest.BestMatch.Character == '['
                                         || currentBest.BestMatch.Character == ']'))
                return DoubleCheckSquareBrackets(image, extracted, currentBest);
            else if (currentBest != null && (currentBest.BestMatch.Character == '{'
                                         || currentBest.BestMatch.Character == '('))
                return DoubleCheckLeftRoundyBrackets(image, extracted, currentBest);
            else if (currentBest != null && (currentBest.BestMatch.Character == '}'
                                         || currentBest.BestMatch.Character == ')'))
                return DoubleCheckRightRoundyBrackets(image, extracted, currentBest);
            else if (currentBest != null && (currentBest.BestMatch.Character == 'a'
                                             || currentBest.BestMatch.Character == 'u'
                                             || currentBest.BestMatch.Character == 'c'
                                             || currentBest.BestMatch.Character == 'o'
                                             || currentBest.BestMatch.Character == 'e'
                                             || currentBest.BestMatch.Character == 's'))
                return DoubleCheckASUCOE(image, extracted, currentBest);
            else if (currentBest != null && (currentBest.BestMatch.Character == 'E'
                                         || currentBest.BestMatch.Character == 'H'))
                return DoubleCheckEh(image, extracted, currentBest);
            return currentBest.BestMatch;
        }

        private static bool PixelInCenter(ImageCache image, ExtractedGlyph extracted, int radius)
        {
            for (int x = extracted.Left + extracted.Width / 2 - radius;
                     x <= extracted.Left + extracted.Width / 2 + radius; x++)
            {
                for (int y = extracted.Top + extracted.Height / 2 - radius;
                         y <= extracted.Top + extracted.Height / 2 + radius; y++)
                {
                    if (image[x, y] > 0)
                        return true;
                }
            }
            return false;
        }

        private static float ScoreGlyph(ExtractedGlyph extracted, Glyph refGlyph, ImageCache image)
        {
            var distanceSum = 0f;
            foreach (var refCorner in refGlyph.Corners)
            {
                var vD = 1f;
                if (refGlyph.VerticalWeight >= 0.65 || refGlyph.VerticalWeight <= 0.35)
                    vD = (float)Math.Sqrt(Math.Pow(refCorner.Y - refGlyph.VerticalWeight, 2f));
                else
                    vD = (float)Math.Sqrt(Math.Pow(refCorner.Y - refGlyph.HorizontalWeight, 2f));
                var minDistance = float.MaxValue;
                foreach (var extractedCorner in extracted.NormalizedCorners)
                {
                    minDistance = Math.Min(minDistance, Vector2.Distance(refCorner, extractedCorner) * vD);
                }
                distanceSum += minDistance;
            }
            //Do both orders to prevent some jerk with 1 point from crushing the score
            foreach (var extractedCorner in extracted.NormalizedCorners)
            {
                var minDistance = float.MaxValue;
                foreach (var refCorner in refGlyph.Corners)
                {
                    minDistance = Math.Min(minDistance, Vector2.Distance(refCorner, extractedCorner));
                }
                distanceSum += minDistance;
            }
            return distanceSum;
        }

        public static int GetVerticalLines(ImageCache image, ExtractedGlyph extracted, int xOffset)
        {
            var horizLinesHit = 0;
            var onLine = false;
            for (int y = extracted.Top; y < extracted.Bottom; y++)
            {
                var pixelPreset = image[xOffset, y] > 0;
                if (pixelPreset && !onLine)
                {
                    onLine = true;
                    horizLinesHit++;
                }
                else if (!pixelPreset && onLine)
                    onLine = false;
            }

            return horizLinesHit;
        }

        private static Glyph DoubleCheckCO(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            // c has n opening. o does not
            var rightPixelsPresent = false;
            for (int x = extracted.Left + extracted.Width / 2; x < extracted.Right; x++)
            {
                if (image[x, extracted.Top + extracted.Height / 2] > 0)
                {
                    rightPixelsPresent = true;
                    break;
                }
            }

            if (rightPixelsPresent)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 'o');
            else
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 'c');
        }

        private static Glyph DoubleCheckEh(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            var lineCount = 0;
            var currentlyOnLine = false;
            for (int y = extracted.Top; y < extracted.Bottom; y++)
            {
                var pixelPresent = image[extracted.Left + extracted.Width / 2, y] > 0;
                if (pixelPresent && !currentlyOnLine)
                {
                    currentlyOnLine = true;
                    lineCount++;
                }
                else if (!pixelPresent && currentlyOnLine)
                {
                    currentlyOnLine = false;
                }
            }

            // E has 3 lines down the mid
            if (lineCount == 3)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 'E');
            else
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 'H');
        }

        private static Glyph DoubleCheckASUCOE(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            //Check for a solid left line for the a
            var leftRayCastVertHits = 0;
            for (int y = extracted.Bottom - extracted.Height / 3 - 1
                   ; y < extracted.Bottom - extracted.Height / 3 + 1; y++)
            {
                for (int x = extracted.Left; x < extracted.Left + extracted.Width / 2; x++)
                {
                    if (image[x, y] > 0)
                    {
                        leftRayCastVertHits++;
                        break;
                    }
                }
            }

            //Check for a solid left line for the e
            var rightRayCastVertHit = false;
            for (int x = extracted.Left + extracted.Width / 2;
                     x < extracted.Right; x++)
            {
                if (image[x, extracted.Bottom - extracted.Height / 3] > 0)
                {
                    rightRayCastVertHit = true;
                    break;
                }
            }

            // check for a nice opening in the top for a u
            var straightLineDownMidTopBotmHits = 0;
            straightLineDownMidTopBotmHits = GetVertDownMidHits(image, extracted);

            // c has n opening. o does not
            var rightPixelsPresent = false;
            for (int x = extracted.Left + extracted.Width / 2; x < extracted.Right; x++)
            {
                if (image[x, extracted.Top + extracted.Height / 2] > 0)
                {
                    rightPixelsPresent = true;
                    break;
                }
            }

            // ASUCOE
            // asucoe

            //u has a hole in the top
            if (straightLineDownMidTopBotmHits == 1)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 'u');
            else if (straightLineDownMidTopBotmHits == 2)
            {
                //c or o
                if (rightPixelsPresent)
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'o');
                else
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'c');
            }
            else
            {
                //  a  s  or  e
                // 3 top hits with 1 left hit is an a
                if (leftRayCastVertHits >= 2 && rightRayCastVertHit)
                {
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'a');
                }

                // if we did not have a hit in from the bottom right it's an e
                if (leftRayCastVertHits >= 2 && !rightRayCastVertHit)
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'e');

                // probaly an s left
                return GlyphDatabase.AllGLyphs.First(g => g.Character == 's');
            }
        }

        private static int GetVertDownMidHits(ImageCache image, ExtractedGlyph extracted)
        {
            var onLine = false;
            var straightLineDownMidTopBotmHits = 0;
            for (int y = extracted.Top; y < extracted.Bottom; y++)
            {
                var pixelPresnt = image[extracted.Left + extracted.Width / 2 + 1, y] > 0;
                if (pixelPresnt && !onLine)
                {
                    onLine = true;
                    straightLineDownMidTopBotmHits++;
                }
                else if (!pixelPresnt && onLine)
                    onLine = false;
            }

            return straightLineDownMidTopBotmHits;
        }

        private static Glyph DoubleCheck683(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            //A 6 has a break 1/3rd of the way down
            var hitSomething = false;
            for (int x = extracted.Right - 1; x > extracted.Left + extracted.Width / 2; x--)
            {
                if (image[x, extracted.Top + extracted.Height / 3] > 0)
                {
                    hitSomething = true;
                    break;
                }
            }
            if (!hitSomething)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '6');

            // A 3 has a break 1/3rd down and 1/3rd up
            var topHit = false;
            var botHit = false;
            for (int x = extracted.Left; x < extracted.Left + extracted.Width / 2; x++)
            {
                if (image[x, extracted.Top + extracted.Height / 3 + 1] > 0)
                    topHit = true;

                if (image[x, extracted.Bottom - extracted.Height / 3] > 0)
                    botHit = true;
            }
            if (!topHit && !botHit)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '3');

            return GlyphDatabase.AllGLyphs.First(g => g.Character == '8');
        }

        private static Glyph DoubleCheckLeftRoundyBrackets(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            // { is point
            var leftColCount = 0;
            for (int y = extracted.Top; y < extracted.Bottom; y++)
            {
                if (image[extracted.Left + 1, y] > 0)
                    leftColCount++;
            }
            if (leftColCount <= 8)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '{');
            else
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '(');
        }

        private static Glyph DoubleCheckRightRoundyBrackets(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            // } is pointy
            var rightColCount = 0;
            for (int y = extracted.Top; y < extracted.Bottom; y++)
            {
                if (image[extracted.Right - 2, y] > 0)
                    rightColCount++;
            }
            if (rightColCount <= 8)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '}');
            else
                return GlyphDatabase.AllGLyphs.First(g => g.Character == ')');
        }

        private static Glyph DoubleCheckSquareBrackets(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {
            var open = GlyphDatabase.AllGLyphs.First(g => g.Character == '[');
            var close = GlyphDatabase.AllGLyphs.First(g => g.Character == ']');
            // Open has a gap on the right
            for (int y = extracted.Top + 3; y < extracted.Bottom - 3; y++)
            {
                var emptyX = true;
                for (int x = extracted.Right - 2; x < extracted.Right; x++)
                {
                    if (image[x, y] > 0)
                    {
                        emptyX = false;
                        break;
                    }
                }
                if (emptyX)
                    return open;
            }

            // Close has a gap on the left
            for (int y = extracted.Top + 3; y < extracted.Bottom - 3; y++)
            {
                var emptyX = true;
                for (int x = extracted.Left + 1; x < extracted.Left + 2; x++)
                {
                    if (image[x, y] > 0)
                    {
                        emptyX = false;
                        break;
                    }
                }
                if (emptyX)
                    return close;
            }

            return currentBest.BestMatch;
        }

        private static Glyph DoubleCheck0OQC638G(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {

            //0OQC638G

            //A 6 has a break 1/3rd of the way down as does C and G
            var sixHitSomething = false;
            for (int x = extracted.Left + extracted.Width / 2;
                x < extracted.Right; x++)
            {
                if (image[x, extracted.Top + 6] > 0)
                {
                    sixHitSomething = true;
                    break;
                }
            }
            if (!sixHitSomething)
            {
                var six = GlyphDatabase.AllGLyphs.First(g => g.Character == '6');
                // 6 or C or G
                // 6 & G have center thing
                if (PixelInCenter(image, extracted, 2))
                {
                    if (extracted.Width <= six.ReferenceWidth)
                        return six;
                }
            }

            /*
             * 
                else if (extracted.Width >= 17)
                {
                    // C is wide
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'C');
                }
            */
            // C
            var cHitSomething = false;
            for (int x = extracted.Left + extracted.Width / 2;
                x < extracted.Right; x++)
            {
                if (image[x, extracted.Top + 12] > 0)
                {
                    cHitSomething = true;
                    break;
                }
            }
            if (!cHitSomething)
            {
                var cGlyph = GlyphDatabase.AllGLyphs.First(g => g.Character == 'C');
                // 6 or C or G
                // 6 & G have center thing
                if (!PixelInCenter(image, extracted, 2))
                {
                    if (extracted.Width <= cGlyph.ReferenceWidth)
                        return cGlyph;
                }
            }

            var gHitSomething = false;
            for (int x = extracted.Left + extracted.Width / 2;
                x < extracted.Right; x++)
            {
                if (image[x, extracted.Top + 8] > 0)
                {
                    gHitSomething = true;
                    break;
                }
            }

            var sHitSomething = false;
            for (int x = extracted.Left + extracted.Width / 2;
                x < extracted.Right; x++)
            {
                if (image[x, extracted.Bottom - 9] > 0)
                {
                    sHitSomething = true;
                    break;
                }
            }
            if (!gHitSomething && !sHitSomething)
            {
                var sGlyph = GlyphDatabase.AllGLyphs.First(g => g.Character == 'S');
                if (PixelInCenter(image, extracted, 2))
                {
                    if (extracted.Width <= sGlyph.ReferenceWidth)
                        return sGlyph;
                }
            }
            if (!gHitSomething)
            {
                var gGlyph = GlyphDatabase.AllGLyphs.First(g => g.Character == 'G');
                if (PixelInCenter(image, extracted, 2))
                {
                    if (extracted.Width <= gGlyph.ReferenceWidth)
                        return gGlyph;
                }
            }

            // A 3 has a break 1/3rd down and 1/3rd up on left
            var topHit = false;
            var botHit = false;
            for (int x = extracted.Left; x < extracted.Left + extracted.Width / 2; x++)
            {
                if (image[x, extracted.Top + extracted.Height / 3 + 1] > 0)
                    topHit = true;

                if (image[x, extracted.Bottom - extracted.Height / 3] > 0)
                    botHit = true;
            }
            if (!topHit && !botHit)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '3');

            //Only 8 and g has 3 hits down mid
            var midHits = GetVertDownMidHits(image, extracted);
            if (midHits == 3)
            {
                var eight = GlyphDatabase.AllGLyphs.First(g => g.Character == '8');
                if (extracted.Width <= eight.ReferenceWidth)
                    return eight;
                else
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'G');
            }

            var zero = GlyphDatabase.AllGLyphs.First(g => g.Character == '0');
            var oh = GlyphDatabase.AllGLyphs.First(g => g.Character == 'O');
            var que = GlyphDatabase.AllGLyphs.First(g => g.Character == 'Q');
            var cee = GlyphDatabase.AllGLyphs.First(g => g.Character == 'C');
            if (extracted.Width <= zero.ReferenceWidth)
                return zero;
            if (extracted.Width <= cee.ReferenceWidth)
                return cee;
            else if (extracted.Width >= cee.ReferenceWidth && extracted.Height <= oh.ReferenceHeight)
                return oh;
            else if (extracted.Width >= cee.ReferenceWidth && extracted.Height <= que.ReferenceHeight)
                return que;

            return currentBest.BestMatch;
        }

        private static Glyph DoubleCheckLIExclaim(ImageCache image, ExtractedGlyph extracted, GlyphMatch best)
        {
            //It's a ! if it has a gap near the bottom
            for (int y = extracted.Bottom - 1; y > extracted.Top + extracted.Height / 2; y--)
            {
                var fullClearRow = true;
                for (int x = extracted.Left; x < extracted.Right; x++)
                {
                    if (image[x, y] > 0)
                    {
                        fullClearRow = false;
                        break;
                    }
                }

                if (fullClearRow)
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == '!');
            }

            //It's a i if it has a gap near the top
            for (int y = extracted.Top + 1; y < extracted.Bottom - extracted.Height / 2; y++)
            {
                var fullClearRow = true;
                for (int x = extracted.Left; x < extracted.Right; x++)
                {
                    if (image[x, y] > 0)
                    {
                        fullClearRow = false;
                        break;
                    }
                }

                if (fullClearRow)
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'i');
            }

            // It can not be a ! anymore
            var pipe = GlyphDatabase.AllGLyphs.First(g => g.Character == '|');
            var el = GlyphDatabase.AllGLyphs.First(g => g.Character == 'l');
            var EYE = GlyphDatabase.AllGLyphs.First(g => g.Character == 'I');
            if (extracted.Height >= pipe.ReferenceHeight)
                return pipe;
            else if (extracted.Height < pipe.ReferenceHeight
                  && extracted.Height > EYE.ReferenceHeight)
                return el;
            else if (extracted.Height <= EYE.ReferenceHeight)
                return EYE;

            // idk lol
            return best.BestMatch;
        }

        private static float DistanceToTopBott(Vector2 corner)
        {
            var top = new Vector2(0.5f, 0f);
            var bot = new Vector2(0.5f, 1f);
            var dTop = Vector2.Distance(top, corner);
            var dBot = Vector2.Distance(bot, corner);
            return dTop < dBot ? dTop : dBot;
        }

        private static bool IsHighMidEntropy(Vector2[] corners)
        {
            var distanceToMid = 0f;
            var distanceToTopBot = 0f;
            var top = new Vector2(0.5f, 0f);
            var mid = new Vector2(0.5f, 0.5f);
            var bot = new Vector2(0.5f, 1f);
            foreach (var corner in corners)
            {
                var topD = Vector2.Distance(corner, top);
                var midD = Vector2.Distance(corner, mid);
                var botD = Vector2.Distance(corner, bot);
                if (topD < midD || botD < midD)
                    distanceToTopBot += Math.Min(botD, topD);
                else
                    distanceToMid += midD;
            }

            if (distanceToMid > distanceToTopBot)
                return true;
            else
                return false;
        }

        private static IEnumerable<Glyph> PossibleReferenceGlyphs(ExtractedGlyph extracted)
        {
            // with a width of 2 expand aspect ratio by 0.4 and shrink down to 0.2 at 25

            if (extracted.Width > 4 || extracted.Height > 4)
                return GlyphDatabase.AllGLyphs.Where(g => NormalValidityCheck(g, extracted));
            else
                return SmallItemCheck(extracted);
        }

        private static IEnumerable<Glyph> SmallItemCheck(ExtractedGlyph extracted)
        {
            //The top is going to be important
            var smalls = GlyphDatabase.AllGLyphs.Where(g => extracted.Width >= g.ReferenceWidth - 2
                                                         && extracted.Height >= g.ReferenceHeight - 2);
            return smalls.Where(g => g.ReferenceGapFromLineTop - 1 >= extracted.PixelsFromTopOfLine);
        }

        private static bool NormalValidityCheck(Glyph g, ExtractedGlyph extracted)
        {
            var t = Math.Min(1f, (Math.Min(extracted.Width, extracted.Height) - 2f) / 25f);
            var aspectAdjust = Vector2.Lerp(new Vector2(0.4f, 0f), new Vector2(0.15f, 0f), t).X;

            return extracted.AspectRatio >= g.AspectRatio * (1 - aspectAdjust) &&
                   extracted.AspectRatio <= g.AspectRatio * (1 + aspectAdjust) &&
                   extracted.Height >= g.ReferenceHeight - 1 &&
                   extracted.Height <= g.ReferenceHeight + 1 &&
                   extracted.Top - extracted.LineOffset >= g.ReferenceGapFromLineTop - 1 &&
                   extracted.Top - extracted.LineOffset <= g.ReferenceGapFromLineTop + 1;
        }

        private static float Lerp(float min, float max, float t)
        {
            return max - (min * t);
        }

        private class GlyphMatch
        {
            public Glyph BestMatch;
            public float MinDistanceSum;
        }
    }
}
