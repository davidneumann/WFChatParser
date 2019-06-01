using Application.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.ChatLineExtractor
{
    public class ImageCache
    {
        private Bitmap _image;
        private float[,] _valueMap;
        private bool[,] _valueMapMask;

        public ImageCache(Bitmap image)
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
                    var hsvPixel = _image.GetPixel(x, y).ToHsv();
                    var v = Math.Max(0, (hsvPixel.Value - 0.6f)) / (1f - 0.6f);
                    var color = GetColor(x, y);
                    if (color == ChatColor.Unknown || color == ChatColor.Redtext)
                        v = 0;
                    else if (color == ChatColor.ChatTimestampName)
                        v = Math.Max(0, (Math.Min(1f, (hsvPixel.Value / 0.8f)) - 0.6f)) / (1f - 0.6f); //Timestamps and username max out at 0.8
                    //else if (color == ChatColor.Redtext)
                    //    v += 0.3f;
                    if (v > 1f || v < 0)
                        System.Diagnostics.Debugger.Break();
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
            return _image.GetPixel(x, y).ToHsv();
        }

        internal ChatColor GetColor(int x, int y)
        {
            var hsvPixel = GetHsv(x, y);

            if ((hsvPixel.Hue >= 175 && hsvPixel.Hue <= 190)
                && hsvPixel.Saturation > 0.1) //green
                return ChatColor.ChatTimestampName;
            if (hsvPixel.Saturation < 0.3) //white
                return ChatColor.Text;
            if (hsvPixel.Hue >= 190 && hsvPixel.Hue <= 210 && hsvPixel.Value >= 0.25) // blue
                return ChatColor.ItemLink;
            if ((hsvPixel.Hue <= 1 || hsvPixel.Hue >= 359) && hsvPixel.Saturation >= 0.7f && hsvPixel.Saturation <= 0.8f) //redtext
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
    }
}
