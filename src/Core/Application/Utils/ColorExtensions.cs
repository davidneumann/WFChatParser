using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Utils
{
    public struct Hsv
    {
        public float Hue { get; set; }
        public float Saturation { get; set; }
        public float Value { get; set; }

        public static Hsv Black { get; } = new Hsv() { Hue = 0f, Saturation = 0f, Value = 0f };

        public static Hsv White { get; } = new Hsv() { Hue = 0f, Saturation = 0f, Value = 1f };

        public static Hsv FromHsv(float h, float s, float v)
        {
            var result = new Hsv();
            result.Hue = h;
            result.Saturation = s;
            result.Value = v;
            return result;
        }
    }

    public static class ColorExtensions
    {
        private static readonly Hsv[,,] LookupTable = new Hsv[256, 256, 256];

        static ColorExtensions()
        {
            for (var r = 0; r <= 255; r++)
            {
                for (var g = 0; g <= 255; g++)
                {
                    for (var b = 0; b <= 255; b++)
                    {
                        LookupTable[r, g, b] = Color.FromArgb(255, r, g, b).ToHsvReal();
                    }
                }
            }
        }

        // Credit sam - (http://lolengine.net/blog/2013/01/13/fast-rgb-to-hsv)
        public static Hsv ToHsvReal(this Color color)
        {
            var hsv = new Hsv();

            var r = color.R / 255f;
            var g = color.G / 255f;
            var b = color.B / 255f;

            var k = 0f;

            if (g < b)
            {
                (g, b) = (b, g);
                k = -1f;
            }

            if (r < g)
            {
                (g, r) = (r, g);
                k = -2 / 6f - k;
            }

            var chroma = r - Math.Min(g, b);
            hsv.Hue = Math.Abs(k + (g - b) / (6 * chroma + 1e-20f)) * 360;
            hsv.Saturation = chroma / (r + 1e-20f);
            hsv.Value = r;

            return hsv;
        }

        public static Hsv ToHsv(this Color color)
        {
            return LookupTable[color.R, color.G, color.B];
        }
    }
}