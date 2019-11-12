using Application.Utils;
using System.Drawing;

namespace ImageOCR.ComplexRivenParser
{
    public partial class ComplexRivenParser
    {
        private class RivenImage
        {
            private Hsv[,] _hsvs;
            private bool[,] _purples;
            private bool[,] _purplesCache;
            private bool[,] _hsvCache;
            public Bitmap BackingImage { get; private set; }
            public RivenImage(Bitmap image)
            {
                _hsvs = new Hsv[image.Width, image.Height];
                _hsvCache = new bool[image.Width, image.Height];
                _purples = new bool[image.Width, image.Height];
                _purplesCache = new bool[image.Width, image.Height];
                BackingImage = image;
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
                        _hsvs[x, y] = BackingImage.GetPixel(x, y).ToHsv();
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
                            _hsvs[x, y] = BackingImage.GetPixel(x, y).ToHsv();
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
                get { return BackingImage.Width; }
            }
            public int Height
            {
                get { return BackingImage.Height; }
            }
        }
    }
}
