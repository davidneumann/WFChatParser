using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser
{
    internal class ImageCache
    {
        private Image<Rgba32> _image;
        private float[,] _valueMap;
        private bool[,] _valueMapMask;
        private ColorSpaceConverter _converter = new ColorSpaceConverter();
        //private Hsv[,] _valueMap;

        public ImageCache(Image<Rgba32> image)
        {
            this._image = image;
            _valueMap = new float[image.Width, image.Height];
            _valueMapMask = new bool[image.Width, image.Height];
        }

        public float this[int x, int y]
        {
            get
            {
                if (!_valueMapMask[x, y])
                {
                    var hsvPixel = _converter.ToHsv(_image[x, y]);
                    var v = Math.Max(0,(hsvPixel.V - 0.21f)) / (1f - 0.21f);
                    var color = GetColor(x, y);
                    if (color == ChatColor.Unknown || color == ChatColor.Redtext)
                        v = 0;
                    else if (color == ChatColor.ChatTimestampName)
                        v = Math.Min(1f, (v / 0.8f)); //Timestamps and username max out at 0.8
                    //else if (color == ChatColor.Redtext)
                    //    v += 0.3f;
                    _valueMap[x, y] = v;
                    _valueMapMask[x, y] = true;
                }
                return _valueMap[x, y];
            }
        }

        public int Width { get { return _image.Width; } }
        public int Height { get { return _image.Height; } }

        internal Hsv GetHsv(int x, int y)
        {
            return _converter.ToHsv(_image[x, y]);
        }

        internal ChatColor GetColor(int x, int y)
        {
            var hsvPixel = GetHsv(x, y);


            if ((hsvPixel.H >= 175 && hsvPixel.H <= 190)
                && hsvPixel.S > 0.1) //green
                return ChatColor.ChatTimestampName;
            if (hsvPixel.S < 0.3) //white
                return ChatColor.Text;
            if (hsvPixel.H >= 190 && hsvPixel.H <= 210 && hsvPixel.V >= 0.25) // blue
                return ChatColor.ItemLink;
            if ((hsvPixel.H <= 1 || hsvPixel.H >= 359) && hsvPixel.S >= 0.7f && hsvPixel.S <= 0.8f) //redtext
                return ChatColor.Ignored;

            return ChatColor.Unknown;
        }

        internal enum ChatColor
        {
            Unknown,
            ChatTimestampName,
            Redtext,
            Text,
            ItemLink,
            Ignored
        }

        //internal Hsv GetHsv(int x, int y)
        //{
        //    return _converter.ToHsv(_image[x, y]);
        //}
    }
}
