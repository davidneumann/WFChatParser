using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    internal class VCache
    {
        private Image<Rgba32> _image;
        private float[,] _hsvMap;
        private bool[,] _hsvCachedMap;
        private ColorSpaceConverter _converter = new ColorSpaceConverter();

        public VCache(Image<Rgba32> image)
        {
            this._image = image;
            _hsvMap = new float[image.Width, image.Height];
            _hsvCachedMap = new bool[image.Width, image.Height];
        }

        public float this[int x, int y]
        {
            get
            {
                if (!_hsvCachedMap[x, y])
                {
                    _hsvMap[x, y] = _converter.ToHsv(_image[x, y]).V;
                    _hsvCachedMap[x, y] = true;
                }
                return _hsvMap[x, y];
            }
        }

        public int Width { get { return _image.Width; } }
        public int Height { get { return _image.Height; } }

        internal Hsv GetHsv(int x, int y)
        {
            return _converter.ToHsv(_image[x, y]);
        }
    }
}
