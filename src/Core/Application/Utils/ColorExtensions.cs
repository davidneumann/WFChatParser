using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Utils
{
    public class Hsv
    {
        public float Hue { get; set; }
        public float Saturation { get; set; }
        public float Value { get; set; }
    }
    public static class ColorExtensions
    {
        //Credit Greg - (https://stackoverflow.com/a/1626175)
        public static Hsv ToHsv(this Color color)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            return new Hsv()
            {
                Hue = color.GetHue(),
                Saturation = (max == 0) ? 0 : 1f - (1f * min / max),
                Value = max / 255f
            };
        }
    }
}
