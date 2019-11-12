using Application.Enums;
using Application.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageOCRBad
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
            GetLineDetails(new RivenImage(b));
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
            croppedRiven.CacheRect(new Rectangle(376, 0, 87, 42)); //Drain/polairty
            croppedRiven.CacheRect(new Rectangle(51, 565, 362, 34)); //MR/rerolls
            var leftBackgroundRect = new Rectangle(croppedRiven.Width / 3 - 7, 46, 15, _bodyBottomY - 46); //Left scan line for background
            var rightBackgroundRect = new Rectangle((croppedRiven.Width / 3) * 2 - 7, 46, 15, _bodyBottomY - 46); //Right scan line for background
            croppedRiven.CacheRect(leftBackgroundRect);
            croppedRiven.CacheRect(rightBackgroundRect);

            var results = new List<LineDetails>();
            var pastBackground = false;
            var startY = 0;
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
                        croppedRiven.CacheRect(new Rectangle(8, y, 450, _bodyBottomY - y));//Rest of the text
                    }
                }
                if (pastBackground)//Not else as we need the first occurance
                {
                    if (startY == 0)
                        startY = y;
                    else
                    {

                    }
                }
            }

            //DEBUG
            using (var debugBitmap = new Bitmap(croppedRiven.Width, croppedRiven.Height))
            {
                for (int x = 0; x < croppedRiven.Width; x++)
                {
                    for (int y = 0; y < croppedRiven.Height; y++)
                    {
                        if (croppedRiven.IsPurple(x, y) || croppedRiven.HasNeighbor(x, y))
                        {
                            //croppedRiven.Restore(x, y);
                            var p = croppedRiven[x, y];
                            var v = byte.MaxValue - (byte)(byte.MaxValue * Math.Min(1f, Math.Max(0f, p.Value - 0.153f) / (0.835f - 0.153f)));
                            debugBitmap.SetPixel(x, y, Color.FromArgb(v, v, v));
                        }
                        else
                            debugBitmap.SetPixel(x, y, Color.White);
                    }
                }
                debugBitmap.Save("debug_complex_riven.png");
            }

            //Add drain
            results.Add(new LineDetails(new Rectangle(375, 2, 47, 39)));

            //Add MR and rolls
            results.Add(new LineDetails(new Rectangle(53, 565, 358, 31)));

            return results;
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

            public bool HasNeighbor(int x, int y)
            {
                const int distance = 3;
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
            public List<Rectangle> CharacterRects { get; set; } = new List<Rectangle>();

            public LineDetails(Rectangle lineRect)
            {
                LineRect = lineRect;
            }
        }
    }
}
