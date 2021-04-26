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
    public partial class ImageCache
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

        public static float MinV = 0f;

        public float this[int x, int y]
        {
            get
            {
                if (!_valueMapMask[x, y])
                {
                    var hsvPixel = _converter.ToHsv(_image[x, y]);
                    var v = hsvPixel.V;

                    var color = GetColor(x, y);
                    if (color == ChatColor.ChatTimestampName)
                    {
                        var minV = 0.231f;
                        //Timestamps and username max out at 0.8
                        v = Math.Max(0f, v - minV);
                        if (v > 0)
                            v += minV;
                        v = Math.Min(1f, v / 0.808f); //0.808
                        //if (v < 0.5528f)
                        //v = 0;
                    }
                    else if (color == ChatColor.Text)
                    {
                        var minV = 0.35f;
                        v = Math.Max(0f, v - minV);
                        if (v > 0)
                            v += minV;
                        //v = Math.Min(1f, ((v - minV) / (0.937f - minV)));
                        v = Math.Min(1f, v / 0.937f);
                        //if (v < 0.4327f)
                        //    v = 0;
                    }
                    else if (color == ChatColor.ClanTimeStampName)
                    {
                        var minV = 0.231f;
                        v = Math.Max(0f, v - minV);
                        if(v > 0)
                            v += minV;
                        v = Math.Min(1f, v / 0.667f);
                        //v = Math.Min(1f, (v - minV) / (0.7f - minV));
                        //if (v < 0.358)
                        //    v = 0;
                    }
                    else if (color == ChatColor.ItemLink)
                    {
                        var minV = 0.35f;
                        v = Math.Max(0f, v - minV);
                        if (v > 0)
                            v += minV;
                        //v = Math.Max(0f, v - minV); // Drop any background interference
                        //if(v > 0)
                        //    v += 0.35f; // item links max at 1 so just add that back if it survived
                        //if (v < 0.251)
                        //    v = 0;
                    }
                    else
                        v = 0;

                    if (v < MinV)
                        v = 0;

                    _valueMap[x, y] = Math.Max(0f, Math.Min(v, 1f));
                    _valueMapMask[x, y] = true;
                }
                return _valueMap[x, y];
            }
        }

        public int Width { get { return _image.Width; } }
        public int Height { get { return _image.Height; } }

        public string DebugFilename { get; set; } = "";

        public SixLabors.ImageSharp.ColorSpaces.Hsv GetHsv(int x, int y)
        {
            return _converter.ToHsv(_image[x, y]);
        }

        public ChatColor GetColor(int x, int y)
        {
            var hsvPixel = GetHsv(x, y);


            if ((hsvPixel.H >= 177 && hsvPixel.H <= 187)
                && hsvPixel.S >= 0.31 && hsvPixel.S <= 0.40) //green
                return ChatColor.ChatTimestampName;
            if (hsvPixel.S < 0.1 && hsvPixel.V >= 0.22) //white
                return ChatColor.Text;
            if (hsvPixel.H >= 190 && hsvPixel.H <= 200 && hsvPixel.S >= 0.74) // blue
                return ChatColor.ItemLink;
            if ((hsvPixel.H <= 1 || hsvPixel.H >= 359) && hsvPixel.S >= 0.7f && hsvPixel.S <= 0.8f) //redtext
                return ChatColor.Ignored;
            if (hsvPixel.H >= 160 && hsvPixel.H <= 170 && hsvPixel.S >= 0.7 && hsvPixel.S <= 0.80)
                return ChatColor.ClanTimeStampName;

            return ChatColor.Unknown;
        }

        //internal Hsv GetHsv(int x, int y)
        //{
        //    return _converter.ToHsv(_image[x, y]);
        //}
    }
}
