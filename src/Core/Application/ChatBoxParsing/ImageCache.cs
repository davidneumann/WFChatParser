using Application.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Application.ChatLineExtractor
{
    public class ImageCache
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

        public ImageCache(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                bitmap.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                mem.Seek(0, SeekOrigin.Begin);
                this._image = SixLabors.ImageSharp.Image.Load(mem);
            }

            _valueMap = new float[this._image.Width, this._image.Height];
            _valueMapMask = new bool[this._image.Width, this._image.Height];
        }

        public float this[int x, int y]
        {
            get
            {
                if (!_valueMapMask[x, y])
                {
                    var hsvPixel = _converter.ToHsv(_image[x, y]);
                    var v = hsvPixel.V;
                    //A pure white pixel behind the chat box will have a v of 0.251
                    //We need to remove this from everything
                    const float backgroundNoiseValue = 0.251f;
                    v -= backgroundNoiseValue;

                    var color = GetColor(x, y);
                    if (color == ChatColor.Unknown || color == ChatColor.Redtext)
                        v = 0;
                    else if (color == ChatColor.ChatTimestampName)
                    {
                        //Timestamps and username max out at 0.8 before being drained by background
                        v = Math.Min(1f, (v / (0.8f - backgroundNoiseValue)));
                    }
                    else if (color == ChatColor.Text)
                        v = Math.Min(1f, (v / (0.937f - backgroundNoiseValue)));
                    //else if (color == ChatColor.Redtext)
                    //    v += 0.3f;

                    //Drop all now normalized Vs below our min treshhold.
                    //const float minTreshold = 0.4f;
                    //v = Math.Max(0, (v - minTreshold)) / (1f - minTreshold);
                    _valueMap[x, y] = v;
                    _valueMapMask[x, y] = true;
                }
                return _valueMap[x, y];
            }
        }

        public int Width { get { return _image.Width; } }
        public int Height { get { return _image.Height; } }

        internal SixLabors.ImageSharp.ColorSpaces.Hsv GetHsv(int x, int y)
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
