using Application.Enums;
using Application.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageOCR
{
    public class ComplexRivenParser
    {
        private ClientLanguage _clientLanguage;
        const int _bodyBottomY = 548;

        public ComplexRivenParser(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
        }

        public void DebugGetLineDetails(Bitmap b)
        {
            var lines = GetLineDetails(new RivenImage(b));
            foreach (var rect in lines.OrderByDescending(r => r.LineRect.Height).Select(ld => ld.LineRect))
            {
                Console.WriteLine($"Line {rect.X},{rect.Y} {rect.Width}x{rect.Height}");
            }
        }

        private List<LineDetails> GetLineDetails(RivenImage croppedRiven)
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
            var results = new List<LineDetails>();
            results.Add(new LineDetails(drainRect, croppedRiven));
            results.Add(new LineDetails(MRRollRect, croppedRiven));

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
                    else if(!purpleFound && startY > 0) //Bottom of line
                    {
                        var height = y - startY;
                        if(nameHeight == 0)
                            nameHeight = height; //First line will always be a name line

                        //Two modifier lines can overlap given tall enough characters
                        //Two+ modifier lines will always be taller than the name
                        if (height > nameHeight * 1.1f)
                        {
                            //We expect a name to be about 1.35x as tall
                            height = height / 2;
                            results.Add(new LineDetails(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), croppedRiven));
                            results.Add(new LineDetails(new Rectangle(bodyRect.Left, startY + height, bodyRect.Width, height), croppedRiven));
                        }
                        else
                        {
                            results.Add(new LineDetails(new Rectangle(bodyRect.Left, startY, bodyRect.Width, height), croppedRiven));
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

        private class RivenImage
        {
            private Hsv[,] _hsvs;
            private bool[,] _purples;
            private bool[,] _purplesCache;
            private bool[,] _hsvCache;
            private Bitmap _image;
            public RivenImage(Bitmap image)
            {
                _hsvs = new Hsv[image.Width, image.Height];
                _hsvCache = new bool[image.Width, image.Height];
                _purples = new bool[image.Width, image.Height];
                _purplesCache = new bool[image.Width, image.Height];
                _image = image;
            }
            private static Hsv _minPurple = Hsv.FromHsv(270f * 0.99f, 0.385f * 0.99f, 0.835f * 0.99f);
            private static Hsv _maxPurple = Hsv.FromHsv(270f * 1.01f, 0.385f * 1.01f, 0.835f * 1.01f);

            public static bool IsPurple(Hsv hsv)
            {
                return hsv.Hue >= _minPurple.Hue && hsv.Hue <= _maxPurple.Hue
                    && hsv.Saturation >= _minPurple.Saturation && hsv.Saturation <= _maxPurple.Saturation
                    && hsv.Value >= _minPurple.Value && hsv.Saturation <= _maxPurple.Value;
                //return hsv.Hue >= _minPurple.Hue && hsv.Hue <= _maxPurple.Hue;
            }
            public bool IsPurple(int x, int y)
            {
                if (!_purplesCache[x, y])
                    _purples[x, y] = IsPurple(this[x, y]);
                return _purples[x, y];
            }

            public Hsv this[int x, int y]
            {
                get
                {
                    if (!_hsvCache[x, y])
                    {
                        _hsvs[x, y] = _image.GetPixel(x, y).ToHsv();
                        _hsvCache[x, y] = true;
                    }
                    return _hsvs[x, y];
                }
                set
                {
                    _hsvCache[x, y] = true;
                    _hsvs[x, y] = value;
                }
            }

            public void CacheRect(Rectangle rect)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    for (int y = rect.Top; y < rect.Height; y++)
                    {
                        if (_hsvCache[x, y] && _purplesCache[x,y])
                            continue;
                        else
                        {
                            _hsvCache[x, y] = true;
                            _hsvs[x, y] = _image.GetPixel(x, y).ToHsv();
                            _purples[x, y] = IsPurple(_hsvs[x, y]);
                            _purplesCache[x, y] = true;
                        }
                    }
                }
            }

            public bool HasNeighbor(int x, int y, int distance = 3)
            {
                for (int x2 = x - distance; x2 < x + distance; x2++)
                {
                    if (x2 < 0 || x2 >= Width)
                        continue;
                    for (int y2 = y - distance; y2 < y + distance; y2++)
                    {
                        if (y2 < 0 || y2 >= Height)
                            continue;
                        if (IsPurple(x2, y2))
                            return true;
                    }
                }
                return false;
                //return highlights[x - 1, y - 1].Value < 0.5f //Top
                //    || highlights[x + 1, y].Value < 0.5f //Right
                //    || highlights[x, y + 1].Value < 0.5f //Bottom
                //    || highlights[x - 1, y].Value < 0.5f; //Left
            }

            public int Width
            {
                get { return _image.Width; }
            }
            public int Height
            {
                get { return _image.Height; }
            }
        }
        private class LineDetails
        {
            public Rectangle LineRect { get; set; }

            private RivenImage _rivenImage;

            private List<Rectangle> _charRects = new List<Rectangle>();
            public List<Rectangle> CharacterRects
            {
                get
                {
                    if (_charRects.Count == 0)
                        _charRects = UpdateCharacterRects();
                    return _charRects;
                }
            }

            public LineDetails(Rectangle lineRect, RivenImage rivenImage)
            {
                LineRect = lineRect;
                _rivenImage = rivenImage;
            }

            public List<Rectangle> UpdateCharacterRects()
            {
                var results = new List<Rectangle>();
                var onChar = false;
                var startX = 0;
                var startY = -1;
                var endY = 0;
                for (int x = LineRect.Left; x < LineRect.Right; x++)
                {
                    if (x < 0 || x >= _rivenImage.Width)
                        continue;
                    var purpleFound = false;
                    for (int y = LineRect.Top; y < LineRect.Bottom; y++)
                    {
                        if (y < 0 || y >= _rivenImage.Height)
                            continue;
                        if(_rivenImage.IsPurple(x,y) || _rivenImage.HasNeighbor(x,y, 1))
                        {
                            purpleFound = true;
                            if (startY == -1 || y < startY)
                                startY = y;
                            if (y > endY)
                                endY = y + 1;
                        }
                    }
                    if(!onChar && purpleFound) //Start of character
                    {
                        startX = x;
                        onChar = true;
                    }
                    else if(onChar && !purpleFound) //Character ended
                    {
                        //Add 1 pixel of spacing around characters
                        var safeXStart = Math.Max(0, startX - 1);
                        var safeYStart = Math.Max(0, startY - 1);
                        var safeWidth = Math.Min(_rivenImage.Width, x + 1 - safeXStart);
                        var safeHeight = Math.Min(_rivenImage.Height, endY + 1 - safeYStart);
                        results.Add(new Rectangle(safeXStart, safeYStart, safeWidth, safeHeight));
                        startX = 0;
                        startY = -1;
                        endY = 0;
                        onChar = false;
                    }
                }
                return results;
            }
        }
    }
}
