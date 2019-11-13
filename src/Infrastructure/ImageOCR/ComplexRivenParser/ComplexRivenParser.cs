using Application.Enums;
using Application.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageOCR.ComplexRivenParser
{
    public partial class ComplexRivenParser
    {
        private ClientLanguage _clientLanguage;
        private CharacterParser _characterParser;
        const int _bodyBottomY = 548;
        Bitmap _tessBitmap;
        private Graphics _tessGraphics;
        private Brush _whiteBrush = new SolidBrush(Color.White);

        public ComplexRivenParser(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
            _characterParser = new CharacterParser(clientLanguage);
        }

        public void DebugIdentifyNumbers(Bitmap b, string debugName = "")
        {
#if DEBUG
            _DEBUGName = debugName;
#endif

            RivenImage rivenImage = new RivenImage(b);
            var lines = GetLineDetails(rivenImage);

            var debugI = 0;
            int nameHeight = lines.Max(line => line.LineRect.Height);
            var safeBodyHeight = nameHeight * 0.935f;
            foreach (var line in lines)
            {
                var lineHeight = line.LineRect.Height;
                IEnumerable<CharacterDetail> numbers = null;
                if (line.LineRect.Top < 5) //Drain Polarity
                    numbers = line.Characters.Take(line.Characters.Count - 1); //Skip polarity
                else if (line.LineRect.Top > 548) //MR (lock) x    (rolls) x
                {
                    //If we have any thing with an x beyond 321 then it's MR (lock) x     (reroll) x
                    var rerolls = line.Characters.Where(r => r.CharacterRect.Left > 321).OrderBy(r => r.CharacterRect.Left).Skip(1);
                    //MR will always be the 3rd+ character on the left side
                    var mr = line.Characters.Where(r => r.CharacterRect.Left < 321).OrderBy(r => r.CharacterRect.Left).Skip(3);
                    numbers = rerolls.Concat(mr);
                }
                else if (line.LineRect.Height <= safeBodyHeight) //Modifier
                {
                    lineHeight = (int)(nameHeight * 0.7105263157894737f);
                    var gap = 0;
                    for (int i = 1; i < line.Characters.Count; i++)
                    {
                        gap = line.Characters[i].CharacterRect.Left - line.Characters[i - 1].CharacterRect.Right;
                        if (gap >= 8) //space detected
                        {
                            //Take all non-square rects
                            numbers = line.Characters.Take(i).Where(c => (float)c.CharacterRect.Width / (float)c.CharacterRect.Height < 0.86f);
                            break;
                        }
                    }
                }

                //using (var debug = rivenImage.BackingImage.Clone(line.LineRect, rivenImage.BackingImage.PixelFormat))
                //{
                //    debug.Save(System.IO.Path.Combine("debug_tess", "line_" + debugI++ + ".png"));
                //}
                if (numbers != null)
                {
                    foreach (var character in numbers)
                    {
                        ParseDigit(rivenImage, character, lineHeight);
                        //Console.Write(character.ParsedValue);
                    }
                    //Console.Write(" ");
                }
            }
        }

        private static int _DEBUG = 0;
        private string _DEBUGName = string.Empty;

        private void ParseDigit(RivenImage rivenImage, CharacterDetail characterDetail, int lineHeight)
        {
            //if (_DEBUG == 21)
            //    System.Diagnostics.Debugger.Break();
            var height = 48; //Internet says 48 pixels is right
            var scale = 38f / (float)characterDetail.CharacterRect.Height;
            var width = (int)Math.Round(characterDetail.CharacterRect.Width * scale);
            //Note: 20 pixels of padding
            const int sidePadding = 10;
            const int fullPadding = sidePadding * 2;
            if (_tessBitmap != null && _tessBitmap.Width < width + fullPadding)
            {
                _tessBitmap.Dispose();
                _tessBitmap = null;
                _tessGraphics.Dispose();
            }
            if (_tessBitmap == null)
            {
                _tessBitmap = new Bitmap(width + fullPadding, height + fullPadding);
                _tessGraphics = Graphics.FromImage(_tessBitmap);
                _tessGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                _tessGraphics.FillRectangle(_whiteBrush, 0, 0, _tessBitmap.Width, _tessBitmap.Height);
            }

            using (var charBitmap = rivenImage.BackingImage.Clone(characterDetail.CharacterRect, rivenImage.BackingImage.PixelFormat))
            {
                var sizedBitmap = charBitmap;
                for (int x = 0; x < charBitmap.Width; x++)
                {
                    for (int y = 0; y < charBitmap.Height; y++)
                    {
                        var p = rivenImage[characterDetail.CharacterRect.Left + x, characterDetail.CharacterRect.Top + y];
                        const float minV = 0.3f;
                        var v = (byte)(byte.MaxValue * Math.Min(1f, Math.Max(0f, p.Value - minV) / (0.835f - minV)));
                        var invertedV = byte.MaxValue - v;
                        charBitmap.SetPixel(x, y, Color.FromArgb(invertedV, invertedV, invertedV));
                    }
                }

                if (width != characterDetail.CharacterRect.Width)
                {
                    int scaledHeight = (int)Math.Round((charBitmap.Height * scale));
                    sizedBitmap = new Bitmap(width, scaledHeight);
                    var resizeG = Graphics.FromImage(sizedBitmap);
                    resizeG.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    resizeG.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    resizeG.FillRectangle(_whiteBrush, 0, 0, sizedBitmap.Width, sizedBitmap.Height);
                    resizeG.DrawImage(charBitmap, new Rectangle(0, 0, width, scaledHeight));
                    resizeG.Dispose();

                    //for (int x = 0; x < sizedBitmap.Width; x++)
                    //{
                    //    for (int y = 0; y < sizedBitmap.Height; y++)
                    //    {
                    //        Color p = sizedBitmap.GetPixel(x, y);
                    //        var halfP = Math.Max(0, p.R - 128);
                    //        if (p.R < 255)
                    //            sizedBitmap.SetPixel(x, y, Color.FromArgb(halfP, halfP, halfP));
                    //    }
                    //}
                    //sizedBitmap.Save(System.IO.Path.Combine("debug_tess", "sizedBitmap.png"));
                }

                //using (var preSobel = new Bitmap(sizedBitmap.Width + 6, sizedBitmap.Height + 6))
                //{
                //    var g = Graphics.FromImage(preSobel);
                //    g.FillRectangle(_whiteBrush, 0, 0, preSobel.Width, preSobel.Height);
                //    g.DrawImage(sizedBitmap, 3, 3);

                //    //Sobel
                //    var sobel = Sobel.GetSobelRestult(preSobel);
                //    preSobel.Save("debug_presobel.png");
                //    sobel.Save("debug_sobel.png");
                //}


                //_tessGraphics.FillRectangle(_whiteBrush, sizedBitmap.Width + sidePadding, sidePadding, _tessBitmap.Width - sizedBitmap.Width - sidePadding, _tessBitmap.Height - fullPadding);
                _tessGraphics.FillRectangle(_whiteBrush, 0, 0, _tessBitmap.Width, _tessBitmap.Height);
                //DrawRectBorders(_tessBitmap, new Rectangle(sidePadding / 2, sidePadding / 2, _tessBitmap.Width - sidePadding, _tessBitmap.Height - sidePadding), 1, Color.Black);
                _tessGraphics.DrawImage(sizedBitmap, _tessBitmap.Width / 2f - sizedBitmap.Width / 2f, _tessBitmap.Height / 2f - sizedBitmap.Height / 2f, sizedBitmap.Width, sizedBitmap.Height);
                //_tessGraphics.DrawImage(sizedBitmap, new Point(sidePadding, sidePadding));

                //sizedBitmap.Save(System.IO.Path.Combine("debug_tess", "debug_tess_" + _DEBUG++ + ".png"));
                var value = _characterParser.ParseCharacter(_tessBitmap);
                var canParse = int.TryParse(value, out _);
                if (value != null && value.Length > 0)
                    if (value == null || value.Length <= 0 || !canParse)
                    {
                        //Console.Write($"{_DEBUGName} failure: {characterDetail.CharacterRect.X},{characterDetail.CharacterRect.Y} {characterDetail.CharacterRect.Width}x{characterDetail.CharacterRect.Height}");
                        if (value != null && value.Length > 0)
                        {
                            //Console.Write($" {value[0]}");
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_tess", "error"));
                            sizedBitmap.Save(System.IO.Path.Combine("debug_tess", "error", $"{Guid.NewGuid()}.png"));
                        }
                        //Console.WriteLine();
                    }
                if (value != null && value.Length > 0 && canParse)
                {
                    characterDetail.ParsedValue = value[0].ToString();
                    var output = new System.IO.FileInfo(System.IO.Path.Combine("debug_tess", characterDetail.ParsedValue, $"{Guid.NewGuid()}.png"));
                    System.IO.Directory.CreateDirectory(output.DirectoryName);
                    sizedBitmap.Save(System.IO.Path.Combine("debug_tess", characterDetail.ParsedValue, $"{Guid.NewGuid()}.png"));
                }
                else
                    characterDetail.ParsedValue = "";

                if (sizedBitmap != charBitmap)
                    sizedBitmap.Dispose();
                charBitmap.Dispose();
            }

            //_tessBitmap.Save(System.IO.Path.Combine("debug_tess", "debug_tess_" + _DEBUG++ + ".png"));
            //characterDetail.ParsedValue = _characterParser.ParseCharacter(_tessBitmap);
        }

        private List<LineDetail> GetLineDetails(RivenImage rivenImage)
        {
            if (rivenImage.Width != 466)
                throw new Exception("Riven not cropped");

            //Wipe out the little winglets above the MR line
            //Left winglet
            for (int y = 520; y < 520 + 30; y++)
            {
                //Handle their slope
                var offset = 0;
                if (y <= 524)
                    offset = 14;
                else if (y <= 529)
                    offset = 24;
                for (int x = offset; x < 32; x++)
                {
                    rivenImage[x, y] = Hsv.Black;
                }
            }
            //Right winglet
            for (int y = 514; y < 514 + 34; y++)
            {
                var offset = 0;
                if (y >= 518)
                    offset = 12;
                else if (y >= 523)
                    offset = 17;
                for (int x = rivenImage.Width - 1 - offset; x >= rivenImage.Width - 24; x--)
                {
                    rivenImage[x, y] = Hsv.Black;
                }
            }

            //Warm up the cache
            Rectangle drainRect = new Rectangle(376, 5, 87, 32);
            rivenImage.CacheRect(drainRect); //Drain/polairty
            Rectangle MRRollRect = new Rectangle(51, 566, 362, 32);
            rivenImage.CacheRect(MRRollRect); //MR/rerolls
            var leftBackgroundRect = new Rectangle(rivenImage.Width / 3 - 7, 46, 15, _bodyBottomY - 46); //Left scan line for background
            var rightBackgroundRect = new Rectangle((rivenImage.Width / 3) * 2 - 7, 46, 15, _bodyBottomY - 46); //Right scan line for background
            rivenImage.CacheRect(leftBackgroundRect);
            rivenImage.CacheRect(rightBackgroundRect);

            //Add the two we know about
            var results = new List<LineDetail>();
            results.Add(new LineDetail(drainRect, rivenImage));
            results.Add(new LineDetail(MRRollRect, rivenImage));

            var pastBackground = false;
            var startY = 0;
            var onLine = false;
            Rectangle bodyRect = Rectangle.Empty;
            var nameHeight = 0;
            for (int y = 46; y < _bodyBottomY; y++)
            {
                if (!pastBackground)
                {
                    var Vs = new float[30];
                    for (int x = 0; x < 15; x++)
                    {
                        Vs[x] = rivenImage[x + leftBackgroundRect.Left, y].Value;
                        Vs[x + 15] = rivenImage[x + rightBackgroundRect.Left, y].Value;
                    }
                    if (Vs.Average() >= 0.165)
                        continue;
                    else
                    {
                        pastBackground = true;
                        bodyRect = new Rectangle(8, y, 450, _bodyBottomY - y);
                        rivenImage.CacheRect(bodyRect);//Rest of the text
                    }
                }
                if (pastBackground)//Not else as we need the first occurance
                {
                    var purpleFound = false;
                    for (int x = bodyRect.Left; x < bodyRect.Right; x++)
                    {
                        if (rivenImage.IsPurple(x, y)) //|| croppedRiven.HasNeighbor(x, y))
                        {
                            purpleFound = true;
                            break;
                        }
                    }
                    if (purpleFound && startY == 0) // Top of line
                        startY = y;
                    else if (!purpleFound && startY > 0) //Bottom of line
                    {
                        var height = y - startY;
                        if (nameHeight == 0)
                            nameHeight = height; //First line will always be a name line

                        //Two modifier lines can overlap given tall enough characters
                        //Two+ modifier lines will always be taller than the name
                        if (height > nameHeight * 1.1f)
                        {
                            //We expect a name to be about 1.35x as tall
                            height = height / 2;
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), rivenImage));
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY + height, bodyRect.Width, height), rivenImage));
                        }
                        else
                        {
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), rivenImage));
                        }

                        startY = 0;
                    }
                }
            }

            ////DEBUG
            //using (var debugBitmap = new Bitmap(croppedRiven.Width, croppedRiven.Height))
            //{
            //    for (int x = 0; x < croppedRiven.Width; x++)
            //    {
            //        for (int y = 0; y < croppedRiven.Height; y++)
            //        {
            //            if (croppedRiven.IsPurple(x, y) || croppedRiven.HasNeighbor(x, y))
            //            {
            //                //croppedRiven.Restore(x, y);
            //                var p = croppedRiven[x, y];
            //                var v = byte.MaxValue - (byte)(byte.MaxValue * Math.Min(1f, Math.Max(0f, p.Value - 0.153f) / (0.835f - 0.153f)));
            //                debugBitmap.SetPixel(x, y, Color.FromArgb(v, v, v));
            //            }
            //            else
            //                debugBitmap.SetPixel(x, y, Color.White);
            //        }
            //    }

            //    //Draw a box around each line
            //    foreach (var line in results)
            //    {
            //        var lineRect = line.LineRect;
            //        DrawRectBorders(debugBitmap, lineRect, 2, Color.Red);

            //        //Draw boxes around characters
            //        foreach (var charRect in line.Characters.Select(c => c.CharacterRect))
            //        {
            //            DrawRectBorders(debugBitmap, charRect, 1, Color.PaleVioletRed);
            //        }
            //    }
            //    debugBitmap.Save("debug_complex_riven.png");
            //}

            return results;
        }

        public void DebugGetFirstCharacterRemove(Bitmap b, string debugName = "")
        {
            RivenImage rivenImage = new RivenImage(b);
            var lines = GetLineDetails(rivenImage);
            int nameHeight = lines.Max(line => line.LineRect.Height);
            var safeBodyHeight = nameHeight * 0.935f;
            foreach (var line in lines.Where(l => l.LineRect.Height <= safeBodyHeight && l.LineRect.Height > 32))
            {
                var first = line.Characters[0];
                //-
                Rectangle rect = first.CharacterRect;
                //if (rivenImage.IsPurple(rect.Left + 3, rect.Top + rect.Height /2)
                //    && rivenImage.IsPurple(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
                //    && rivenImage.IsPurple(rect.Right - 3, rect.Top + rect.Height / 2)
                //    && !rivenImage.IsPurple(rect.Left + rect.Width / 2, rect.Top + 3)
                //    && !rivenImage.IsPurple(rect.Left + rect.Width / 2, rect.Bottom - 3))
                if ((float)rect.Width / (float)rect.Height > 2f)
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_first", "dash"));
                    using (var firstBitmap = b.Clone(rect, b.PixelFormat))
                    {
                        firstBitmap.Save(System.IO.Path.Combine("debug_first", "dash", debugName + "_" + (Guid.NewGuid()).ToString() + ".png"));
                    }
                }
                //+
                else if (!rivenImage.IsPurple(rect.X + 1, rect.Y + 1)
                    && !rivenImage.IsPurple(rect.X + 2, rect.Y + 2)
                    && !rivenImage.IsPurple(rect.X + 1, rect.Bottom - 2)
                    && !rivenImage.IsPurple(rect.X + 2, rect.Bottom - 3)
                    && !rivenImage.IsPurple(rect.Right - 2, rect.Top + 1)
                    && !rivenImage.IsPurple(rect.Right - 3, rect.Top + 2)
                    && !rivenImage.IsPurple(rect.Right - 2, rect.Bottom - 2)
                    && !rivenImage.IsPurple(rect.Right - 3, rect.Bottom - 3)
                    && rivenImage.IsPurple(rect.X + rect.Width / 2, rect.Y + rect.Height / 2))
                {
                    var valid = true;
                    for (int y = rect.Top; y < rect.Bottom; y++)
                    {
                        var onPurple = false;
                        var columns = 0;
                        for (int x = rect.Left; x < rect.Right; x++)
                        {
                            if (!onPurple && rivenImage.IsPurple(x, y))
                                onPurple = true;
                            else if (onPurple && !rivenImage.IsPurple(x, y))
                            {
                                onPurple = false;
                                columns++;
                                if (columns > 1)
                                {
                                    valid = false;
                                    break;
                                }
                            }
                        }
                        if (columns > 1)
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid)
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_first", "plus"));
                        using (var firstBitmap = b.Clone(rect, b.PixelFormat))
                        {
                            firstBitmap.Save(System.IO.Path.Combine("debug_first", "plus", debugName + "_" + (Guid.NewGuid()).ToString() + ".png"));
                        }
                    }
                }
                else
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_first", "unknown"));
                    using (var firstBitmap = b.Clone(rect, b.PixelFormat))
                    {
                        firstBitmap.Save(System.IO.Path.Combine("debug_first", "unknown", debugName + "_" + (Guid.NewGuid()).ToString() + ".png"));
                    }
                }

            }
        }

        public void DebugGetRightSideSize(Bitmap b, string debugName = "")
        {
            var lines = GetLineDetails(new RivenImage(b));

            int nameHeight = lines.Max(line => line.LineRect.Height);
            var safeBodyHeight = nameHeight * 0.935f;
            foreach (var line in lines.Where(l => l.LineRect.Height <= safeBodyHeight && l.LineRect.Height > 32))
            {
                var gap = 0;
                var splitFound = false;
                for (int i = 1; i < line.Characters.Count; i++)
                {
                    gap = line.Characters[i].CharacterRect.Left - line.Characters[i - 1].CharacterRect.Right;
                    if (gap >= 12) //space detected
                    {
                        //Get everything to the right
                        var rect = new Rectangle(line.Characters[i].CharacterRect.Left, line.LineRect.Top,
                            line.Characters[line.Characters.Count - 1].CharacterRect.Right - line.Characters[i].CharacterRect.Left,
                            line.LineRect.Height);

                        using (var descriptionBitmap = new Bitmap(rect.Width, rect.Height))
                        {
                            using (var g = Graphics.FromImage(descriptionBitmap))
                            {
                                g.DrawImage(b, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
                                System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_width", rect.Width.ToString()));
                                descriptionBitmap.Save(System.IO.Path.Combine("debug_width", rect.Width.ToString(), debugName + "_" + (Guid.NewGuid()) + ".png"));
                                splitFound = true;
                                break;
                            }
                        }
                    }
                }
                if (!splitFound)
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine("debug_width", "error"));
                    b.Save(System.IO.Path.Combine("debug_width", "error", debugName));
                }
            }
        }

        private static void DrawRectBorders(Bitmap debugBitmap, Rectangle lineRect, int thickness, Color color)
        {
            for (int x = lineRect.Left - (thickness - 1); x < lineRect.Right + 1; x++)
            {
                if (x < 0 || x >= debugBitmap.Width)
                    continue;
                //Top line
                for (int y = lineRect.Top - (thickness - 1); y < lineRect.Top + 1; y++)
                {
                    if (y < 0 || y >= debugBitmap.Height)
                        continue;
                    debugBitmap.SetPixel(x, y, color);
                }
                //Bottom line
                for (int y = lineRect.Bottom - (thickness - 1); y < lineRect.Bottom + 1; y++)
                {
                    if (y < 0 || y >= debugBitmap.Height)
                        continue;
                    debugBitmap.SetPixel(x, y, color);
                }
            }
            for (int y = lineRect.Top - (thickness - 1); y < lineRect.Bottom + 1; y++)
            {
                if (y < 0 || y >= debugBitmap.Height)
                    continue;
                //Left line
                for (int x = lineRect.Left - (thickness - 1); x < lineRect.Left + 1; x++)
                {
                    if (x < 0 || x >= debugBitmap.Width)
                        continue;
                    debugBitmap.SetPixel(x, y, color);
                }
                for (int x = lineRect.Right - (thickness - 1); x < lineRect.Right + 1; x++)
                {
                    if (x < 0 || x >= debugBitmap.Width)
                        continue;
                    debugBitmap.SetPixel(x, y, color);
                }
            }
        }
    }
}
