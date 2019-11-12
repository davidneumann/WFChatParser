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
        private Graphics _tessGrpahics;
        private Brush _whiteBrush = new SolidBrush(Color.White);

        public ComplexRivenParser(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
            _characterParser = new CharacterParser(clientLanguage);
        }

        public void DebugGetLineDetails(Bitmap b)
        {
            RivenImage rivenImage = new RivenImage(b);
            var lines = GetLineDetails(rivenImage);
            foreach (var rect in lines.OrderByDescending(r => r.LineRect.Height).Select(ld => ld.LineRect))
            {
                Console.WriteLine($"Line {rect.X},{rect.Y} {rect.Width}x{rect.Height}");
            }
            if (!System.IO.Directory.Exists("debug_tess"))
                System.IO.Directory.CreateDirectory("debug_tess");
            else
                System.IO.Directory.GetFiles("debug_tess").ToList().ForEach(f => System.IO.File.Delete(f));
            var debugI = 0;
            foreach (var line in lines)
            {
                using (var debug = rivenImage.BackingImage.Clone(line.LineRect, rivenImage.BackingImage.PixelFormat))
                {
                    debug.Save(System.IO.Path.Combine("debug_tess", "line_" + debugI++ + ".png"));
                }
                foreach (var character in line.Characters)
                {
                    ParseCharacter(rivenImage, character, line.LineRect.Height);
                    Console.Write(character.ParsedValue);
                }
                Console.WriteLine();
            }
        }

        private static int _DEBUG = 0;
        private void ParseCharacter(RivenImage rivenImage, CharacterDetail characterDetail, int lineHeight)
        {
            //if (_DEBUG == 21)
            //    System.Diagnostics.Debugger.Break();
            var scale = 48f / (float)lineHeight;
            var height = 48;
            var width = (int)Math.Round(characterDetail.CharacterRect.Width * scale);
            //Note: 20 pixels of padding
            const int sidePadding = 0;
            const int fullPadding = sidePadding * 2;
            if (_tessBitmap != null && _tessBitmap.Width < width + fullPadding)
            {
                _tessBitmap.Dispose();
                _tessBitmap = null;
                _tessGrpahics.Dispose();
            }
            if (_tessBitmap == null)
            {
                _tessBitmap = new Bitmap(width + fullPadding, height + fullPadding);
                _tessGrpahics = Graphics.FromImage(_tessBitmap);
                _tessGrpahics.FillRectangle(_whiteBrush, 0, 0, _tessBitmap.Width, _tessBitmap.Height);
            }

            using (var charBitmap = rivenImage.BackingImage.Clone(characterDetail.CharacterRect, rivenImage.BackingImage.PixelFormat))
            {
                var sizedBitmap = charBitmap;
                for (int x = 0; x < charBitmap.Width; x++)
                {
                    for (int y = 0; y < charBitmap.Height; y++)
                    {
                        var p = rivenImage[characterDetail.CharacterRect.Left + x, characterDetail.CharacterRect.Top + y];
                        var v = byte.MaxValue - (byte)(byte.MaxValue * Math.Min(1f, Math.Max(0f, p.Value - 0.153f) / (0.835f - 0.153f)));
                        charBitmap.SetPixel(x, y, Color.FromArgb(v, v, v));
                    }
                }
                if (width != characterDetail.CharacterRect.Width)
                {
                    sizedBitmap = new Bitmap(width, height);
                    var resizeG = Graphics.FromImage(sizedBitmap);
                    resizeG.FillRectangle(_whiteBrush, 0, 0, sizedBitmap.Width, sizedBitmap.Height);
                    int scaledHeight = (int)Math.Round((charBitmap.Height * scale));
                    resizeG.DrawImage(charBitmap, new Rectangle(0, sizedBitmap.Height - scaledHeight, width, scaledHeight));
                    resizeG.Dispose();
                    sizedBitmap.Save(System.IO.Path.Combine("debug_tess", "sizedBitmap.png"));
                }

                var g = Graphics.FromImage(_tessBitmap);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.DrawImage(sizedBitmap, new Point(sidePadding, sidePadding));
                g.FillRectangle(_whiteBrush, sizedBitmap.Width + sidePadding, sidePadding, _tessBitmap.Width - sizedBitmap.Width - sidePadding, _tessBitmap.Height - fullPadding);
            }

            _tessBitmap.Save(System.IO.Path.Combine("debug_tess", "debug_tess_" + _DEBUG++ + ".png"));
            characterDetail.ParsedValue = _characterParser.ParseCharacter(_tessBitmap);
        }

        private List<LineDetail> GetLineDetails(RivenImage croppedRiven)
        {
            if (croppedRiven.Width != 466)
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
                    croppedRiven[x, y] = Hsv.Black;
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
                for (int x = croppedRiven.Width - 1 - offset; x >= croppedRiven.Width - 24; x--)
                {
                    croppedRiven[x, y] = Hsv.Black;
                }
            }

            //Warm up the cache
            Rectangle drainRect = new Rectangle(376, 0, 87, 42);
            croppedRiven.CacheRect(drainRect); //Drain/polairty
            Rectangle MRRollRect = new Rectangle(51, 565, 362, 34);
            croppedRiven.CacheRect(MRRollRect); //MR/rerolls
            var leftBackgroundRect = new Rectangle(croppedRiven.Width / 3 - 7, 46, 15, _bodyBottomY - 46); //Left scan line for background
            var rightBackgroundRect = new Rectangle((croppedRiven.Width / 3) * 2 - 7, 46, 15, _bodyBottomY - 46); //Right scan line for background
            croppedRiven.CacheRect(leftBackgroundRect);
            croppedRiven.CacheRect(rightBackgroundRect);

            //Add the two we know about
            var results = new List<LineDetail>();
            results.Add(new LineDetail(drainRect, croppedRiven));
            results.Add(new LineDetail(MRRollRect, croppedRiven));

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
                        Vs[x] = croppedRiven[x + leftBackgroundRect.Left, y].Value;
                        Vs[x + 15] = croppedRiven[x + rightBackgroundRect.Left, y].Value;
                    }
                    if (Vs.Average() >= 0.165)
                        continue;
                    else
                    {
                        pastBackground = true;
                        bodyRect = new Rectangle(8, y, 450, _bodyBottomY - y);
                        croppedRiven.CacheRect(bodyRect);//Rest of the text
                    }
                }
                if (pastBackground)//Not else as we need the first occurance
                {
                    var purpleFound = false;
                    for (int x = bodyRect.Left; x < bodyRect.Right; x++)
                    {
                        if (croppedRiven.IsPurple(x, y) || croppedRiven.HasNeighbor(x, y))
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
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), croppedRiven));
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY + height, bodyRect.Width, height), croppedRiven));
                        }
                        else
                        {
                            results.Add(new LineDetail(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), croppedRiven));
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
            //        foreach (var charRect in line.CharacterRects)
            //        {
            //            DrawRectBorders(debugBitmap, charRect, 1, Color.PaleVioletRed);
            //        }
            //    }
            //    debugBitmap.Save("debug_complex_riven.png");
            //}

            return results;
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
