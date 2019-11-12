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
        private static Hsv _minPurple = Hsv.FromHsv(270f * 0.99f, 0.385f * 0.99f, 0.835f * 0.99f);
        private static Hsv _maxPurple = Hsv.FromHsv(270f * 1.01f, 0.385f * 1.01f, 0.835f * 1.01f);

        public ComplexRivenParser(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
        }

        public void DebugGetLineDetails(Bitmap b)
        {
            GetLineDetails(new HsvImage(b));
        }
        private List<LineDetails> GetLineDetails(HsvImage croppedRiven)
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
            var results = new List<LineDetails>();
            var pastBackground = false;
            var startY = 0;
            var leftStartOffset = (croppedRiven.Width / 2) - (croppedRiven.Width / 3);
            var rightStartOffset = (croppedRiven.Width / 2) + (croppedRiven.Width / 3);
            for (int y = 46; y < 548; y++)
            {
                if (!pastBackground)
                {
                    var Vs = new float[30];
                    for (int x = 0; x < 15; x++)
                    {
                        Vs[x] = croppedRiven.GetFastVertical(leftStartOffset + x, y).Value;
                        Vs[x + 15] = croppedRiven.GetFastVertical(rightStartOffset + x, y).Value;
                    }
                    if (Vs.Average() >= 0.165)
                        continue;
                    else
                    {
                        pastBackground = true;
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
                var highlights = new bool[croppedRiven.Width, croppedRiven.Height];
                for (int x = 0; x < croppedRiven.Width; x++)
                {
                    for (int y = 0; y < croppedRiven.Height; y++)
                    {
                        if (IsPurple(croppedRiven[x, y]))
                            highlights[x, y] = true;
                        //debugBitmap.SetPixel(x, y, Color.Black);
                        else
                            highlights[x, y] = false;
                        //debugBitmap.SetPixel(x, y, Color.White);
                    }
                }
                for (int x = 1; x < croppedRiven.Width - 1; x++)
                {
                    for (int y = 1; y < croppedRiven.Height - 1; y++)
                    {
                        if (!highlights[x, y] && HasNeighbor(highlights, x, y))
                        {
                            //croppedRiven.Restore(x, y);
                            var p = croppedRiven[x, y];
                            var v = byte.MaxValue - (byte)(byte.MaxValue * Math.Min(1f, Math.Max(0f, p.Value - 0.153f) / (0.835f - 0.153f)));
                            debugBitmap.SetPixel(x, y, Color.FromArgb(v, v, v));
                        }
                        else if (highlights[x, y])
                            debugBitmap.SetPixel(x, y, Color.Black);
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

        private bool HasNeighbor(bool[,] highlights, int x, int y)
        {
            var width = highlights.GetLength(0);
            var height = highlights.GetLength(1);
            const int distance = 3;
            for (int x2 = x - distance; x2 < x + distance; x2++)
            {
                if (x2 < 0 || x2 >= width)
                    continue;
                for (int y2 = y - distance; y2 < y + distance; y2++)
                {
                    if (y2 < 0 || y2 >= height)
                        continue;
                    if (highlights[x2, y2])
                        return true;
                }
            }
            return false;
            //return highlights[x - 1, y - 1].Value < 0.5f //Top
            //    || highlights[x + 1, y].Value < 0.5f //Right
            //    || highlights[x, y + 1].Value < 0.5f //Bottom
            //    || highlights[x - 1, y].Value < 0.5f; //Left
        }

        private bool IsPurple(Hsv hsv)
        {
            return hsv.Hue >= _minPurple.Hue && hsv.Hue <= _maxPurple.Hue
                && hsv.Saturation >= _minPurple.Saturation && hsv.Saturation <= _maxPurple.Saturation
                && hsv.Value >= _minPurple.Value && hsv.Saturation <= _maxPurple.Value;
            //return hsv.Hue >= _minPurple.Hue && hsv.Hue <= _maxPurple.Hue;
        }

        private class HsvImage
        {
            private Hsv[,] _hsvs;
            private bool[,] _cache;
            private Bitmap _image;
            public HsvImage(Bitmap image)
            {
                _hsvs = new Hsv[image.Width, image.Height];
                _cache = new bool[image.Width, image.Height];
                _image = image;
            }

            public Hsv this[int x, int y]
            {
                get
                {
                    if (!_cache[x, y])
                    {
                        _hsvs[x, y] = _image.GetPixel(x, y).ToHsv();
                        _cache[x, y] = true;
                    }
                    return _hsvs[x, y];
                }
                set
                {
                    _cache[x, y] = true;
                    _hsvs[x, y] = value;
                }
            }

            public int Width
            {
                get { return _image.Width; }
            }
            public int Height
            {
                get { return _image.Height; }
            }

            public void Restore(int x, int y)
            {
                _cache[x, y] = true;
                _hsvs[x, y] = _image.GetPixel(x, y).ToHsv();
            }

            public Hsv GetFastHorizontal(int x, int y)
            {
                if (!_cache[x, y])
                {
                    //var data = _image.LockBits(new Rectangle(x, 0, 1, Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, _image.PixelFormat);
                    //TODO: somehow store these pixels into _hsv
                    for (int y2 = 0; y2 < _image.Height; y2++)
                    {
                        _hsvs[x, y2] = _image.GetPixel(x, y2).ToHsv();
                        _cache[x, y2] = true;
                    }
                }

                return _hsvs[x, y];
            }

            public Hsv GetFastVertical(int x, int y)
            {
                if (!_cache[x, y])
                {
                    //var data = _image.LockBits(new Rectangle(0, y, Width, 1), System.Drawing.Imaging.ImageLockMode.ReadOnly, _image.PixelFormat);
                    //TODO: somehow store these pixels into _hsv
                    for (int y2 = 0; y2 < _image.Height; y2++)
                    {
                        _hsvs[x, y2] = _image.GetPixel(x, y2).ToHsv();
                        _cache[x, y2] = true;
                    }
                }
                return _hsvs[x, y];
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
