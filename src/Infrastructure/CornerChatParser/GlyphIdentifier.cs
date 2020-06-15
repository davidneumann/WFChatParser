using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using WebSocketSharp;

namespace CornerChatParser
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

        //    //if (extracted.GlobalGlpyhRect.Left >= 1049 && extracted.GlobalGlpyhRect.Top >= 806)
        //    //    System.Diagnostics.Debugger.Break();

        //    foreach (var refGlyph in PossibleReferenceGlyphs(extracted))
        //    {
        //        var distanceSum = 0f;
        //        foreach (var refCorner in refGlyph.Corners)
        //        {
        //            var minDistance = float.MaxValue;
        //            var distancePenality = 1f;
        //            if (extracted.GlobalGlpyhRect.Height > 6)
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
        //            if (extracted.GlobalGlpyhRect.Height > 6)
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

            //if (extracted.GlobalGlpyhRect.Left >= 343 && extracted.GlobalGlpyhRect.Top >= 863)
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
                                    || currentBest.BestMatch.Character == '3'))
                return DoubleCheck0OQC638(image, extracted, currentBest);
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

        private static float ScoreGlyph(ExtractedGlyph extracted, Glyph refGlyph, ImageCache image)
        {
            if (refGlyph.Character == 'G')
            {
                if (CheckG(extracted, image))
                    return float.MinValue;
            }
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

        private static bool CheckG(ExtractedGlyph extracted, ImageCache image)
        {
            var g = GlyphDatabase.AllGLyphs.First(glyph => glyph.Character == 'G');
            if ((extracted.GlobalGlpyhRect.Width >= g.ReferenceWidth - 1
                && extracted.GlobalGlpyhRect.Width <= g.ReferenceWidth))
                return false;

            // Line down slightly off center should hit 3 and rightmost side shoudl have a gap
            var rightSideLinesCount = GetVerticalLines(image, extracted, extracted.GlobalGlpyhRect.Right - 3);

            var bottomRightPresent = false;
            for (int x = extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x < extracted.GlobalGlpyhRect.Right; x++)
            {
                if (bottomRightPresent)
                    break;
                for (int y = extracted.GlobalGlpyhRect.Top - 1 + extracted.GlobalGlpyhRect.Height / 2
                       ; y <= extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 2 + 1; y++)
                {
                    if (image[x, y] > 0)
                    {
                        bottomRightPresent = true;
                        break;
                    }
                }
            }

            // Line down the left should have no gaps
            var leftHasGap = false;
            for (int y = extracted.GlobalGlpyhRect.Top + 7; y < extracted.GlobalGlpyhRect.Top + 17; y++)
            {
                if(image[extracted.GlobalGlpyhRect.Left, y] <= 0)
                {
                    leftHasGap = true;
                    break;
                }
            }

            var horizLinesHit = 0;
            var onLine = false;
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
            {
                var pixelPreset = image[extracted.GlobalGlpyhRect.Left + 2 + extracted.GlobalGlpyhRect.Width / 2, y] > 0;
                if (pixelPreset && !onLine)
                {
                    onLine = true;
                    horizLinesHit++;
                }
                else if (!pixelPreset && onLine)
                    onLine = false;
            }

            if (rightSideLinesCount == 2 && horizLinesHit == 3 && !leftHasGap && bottomRightPresent)
                return true;

            return false;
        }

        public static int GetVerticalLines(ImageCache image, ExtractedGlyph extracted, int xOffset)
        {
            var horizLinesHit = 0;
            var onLine = false;
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
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
            for (int x = extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x < extracted.GlobalGlpyhRect.Right; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 2] > 0)
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
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
            {
                var pixelPresent = image[extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2, y] > 0;
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
            for (int y = extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 3 - 1
                   ; y < extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 3 + 1; y++)
            {
                for (int x = extracted.GlobalGlpyhRect.Left; x < extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x++)
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
            for (int x = extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2;
                     x < extracted.GlobalGlpyhRect.Right; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 3] > 0)
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
            for (int x = extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x < extracted.GlobalGlpyhRect.Right; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 2] > 0)
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
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
            {
                var pixelPresnt = image[extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2, y] > 0;
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
            for (int x = extracted.GlobalGlpyhRect.Right - 1; x > extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x--)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 3] > 0)
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
            for (int x = extracted.GlobalGlpyhRect.Left; x < extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 3 + 1] > 0)
                    topHit = true;

                if (image[x, extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 3] > 0)
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
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
            {
                if (image[extracted.GlobalGlpyhRect.Left + 1, y] > 0)
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
            for (int y = extracted.GlobalGlpyhRect.Top; y < extracted.GlobalGlpyhRect.Bottom; y++)
            {
                if (image[extracted.GlobalGlpyhRect.Right - 2, y] > 0)
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
            for (int y = extracted.GlobalGlpyhRect.Top + 3; y < extracted.GlobalGlpyhRect.Bottom - 3; y++)
            {
                var emptyX = true;
                for (int x = extracted.GlobalGlpyhRect.Right - 2; x < extracted.GlobalGlpyhRect.Right; x++)
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
            for (int y = extracted.GlobalGlpyhRect.Top + 3; y < extracted.GlobalGlpyhRect.Bottom - 3; y++)
            {
                var emptyX = true;
                for (int x = extracted.GlobalGlpyhRect.Left + 1; x < extracted.GlobalGlpyhRect.Left + 2; x++)
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

        private static Glyph DoubleCheck0OQC638(ImageCache image, ExtractedGlyph extracted, GlyphMatch currentBest)
        {

            //0OQC638

            //A 6 has a break 1/3rd of the way down as does C
            var hitSomething = false;
            for (int x = extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2;
                x < extracted.GlobalGlpyhRect.Right; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 3] > 0)
                {
                    hitSomething = true;
                    break;
                }
            }
            if (!hitSomething)
            {
                // 6 or C
                // C is wide
                if (extracted.GlobalGlpyhRect.Width >= 17)
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == 'C');
                else
                    return GlyphDatabase.AllGLyphs.First(g => g.Character == '6');
            }

            // A 3 has a break 1/3rd down and 1/3rd up on left
            var topHit = false;
            var botHit = false;
            for (int x = extracted.GlobalGlpyhRect.Left; x < extracted.GlobalGlpyhRect.Left + extracted.GlobalGlpyhRect.Width / 2; x++)
            {
                if (image[x, extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 3 + 1] > 0)
                    topHit = true;

                if (image[x, extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 3] > 0)
                    botHit = true;
            }
            if (!topHit && !botHit)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '3');

            //Only 8 has 3 hits down mid
            var midHits = GetVertDownMidHits(image, extracted);
            if (midHits == 3)
                return GlyphDatabase.AllGLyphs.First(g => g.Character == '8');

            var zero = GlyphDatabase.AllGLyphs.First(g => g.Character == '0');
            var oh = GlyphDatabase.AllGLyphs.First(g => g.Character == 'O');
            var que = GlyphDatabase.AllGLyphs.First(g => g.Character == 'Q');
            var cee = GlyphDatabase.AllGLyphs.First(g => g.Character == 'C');
            if (extracted.GlobalGlpyhRect.Width <= zero.ReferenceWidth)
                return zero;
            if (extracted.GlobalGlpyhRect.Width <= cee.ReferenceWidth)
                return cee;
            else if (extracted.GlobalGlpyhRect.Width >= cee.ReferenceWidth && extracted.GlobalGlpyhRect.Height <= oh.ReferenceHeight)
                return oh;
            else if (extracted.GlobalGlpyhRect.Width >= cee.ReferenceWidth && extracted.GlobalGlpyhRect.Height == que.ReferenceHeight)
                return que;

            return currentBest.BestMatch;
        }

        private static Glyph DoubleCheckLIExclaim(ImageCache image, ExtractedGlyph extracted, GlyphMatch best)
        {
            //It's a ! if it has a gap near the bottom
            for (int y = extracted.GlobalGlpyhRect.Bottom - 1; y > extracted.GlobalGlpyhRect.Top + extracted.GlobalGlpyhRect.Height / 2; y--)
            {
                var fullClearRow = true;
                for (int x = extracted.GlobalGlpyhRect.Left; x < extracted.GlobalGlpyhRect.Right; x++)
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
            for (int y = extracted.GlobalGlpyhRect.Top + 1; y < extracted.GlobalGlpyhRect.Bottom - extracted.GlobalGlpyhRect.Height / 2; y++)
            {
                var fullClearRow = true;
                for (int x = extracted.GlobalGlpyhRect.Left; x < extracted.GlobalGlpyhRect.Right; x++)
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
            if (extracted.GlobalGlpyhRect.Height >= pipe.ReferenceHeight)
                return pipe;
            else if (extracted.GlobalGlpyhRect.Height < pipe.ReferenceHeight
                  && extracted.GlobalGlpyhRect.Height > EYE.ReferenceHeight)
                return el;
            else if (extracted.GlobalGlpyhRect.Height <= EYE.ReferenceHeight)
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

            if (extracted.GlobalGlpyhRect.Width > 4 || extracted.GlobalGlpyhRect.Height > 4)
                return GlyphDatabase.AllGLyphs.Where(g => NormalValidityCheck(g, extracted));
            else
                return SmallItemCheck(extracted);
        }

        private static IEnumerable<Glyph> SmallItemCheck(ExtractedGlyph extracted)
        {
            //The top is going to be important
            var smalls = GlyphDatabase.AllGLyphs.Where(g => extracted.GlobalGlpyhRect.Width >= g.ReferenceWidth - 2
                                                         && extracted.GlobalGlpyhRect.Height >= g.ReferenceHeight - 2);
            return smalls.Where(g => g.ReferenceGapFromLineTop - 1 >= extracted.PixelsFromTopOfLine);
        }

        private static bool NormalValidityCheck(Glyph g, ExtractedGlyph extracted, ImageCache image)
        {
            var t = Math.Min(1f, (Math.Min(extracted.GlobalGlpyhRect.Width, extracted.GlobalGlpyhRect.Height) - 2f) / 25f);
            var aspectAdjust = Vector2.Lerp(new Vector2(0.4f, 0f), new Vector2(0.15f, 0f), t).X;

            return extracted.AspectRatio >= g.AspectRatio * (1 - aspectAdjust) &&
                   extracted.AspectRatio <= g.AspectRatio * (1 + aspectAdjust) &&
                   extracted.GlobalGlpyhRect.Height >= g.ReferenceHeight - 1 &&
                   extracted.GlobalGlpyhRect.Height <= g.ReferenceHeight + 1 &&
                   extracted.GlobalGlpyhRect.Top - extracted.LineOffset >= g.ReferenceGapFromLineTop - 1 &&
                   extracted.GlobalGlpyhRect.Top - extracted.LineOffset <= g.ReferenceGapFromLineTop + 1 &&
                   GetVerticalLines(image, extracted, extracted.GlobalGlpyhRect.Width / 2 + 1 + extracted.GlobalGlpyhRect.Left) == g.CenterLines;
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
