using Application.Enums;
using Application.Interfaces;
using Application.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace WarframeDriver
{
    public class ScreenStateHandler : IScreenStateHandler
    {
        public ScreenState GetScreenState(Bitmap bitmap)
        {
            if (IsRiven(bitmap))
                return ScreenState.RivenWindow;
            if (IsLoadingScreen(bitmap))
                return ScreenState.LoadingScreen;
            if (IsLoginScreen(bitmap))
                return ScreenState.LoginScreen;
            if (IsDailyRewardScreenItem(bitmap))
                return ScreenState.DailyRewardScreenItem;
            if (IsDailyRewardScreenPlat(bitmap))
                return ScreenState.DailyRewardScreenPlat;
            if (IsWarframeControl(bitmap))
                return ScreenState.ControllingWarframe;
            if (IsMainMenu(bitmap))
                return ScreenState.MainMenu;
            if (IsProfileMenu(bitmap))
                return ScreenState.ProfileMenu;
            if (IsGlyphScreen(bitmap))
                return ScreenState.GlyphWindow;

            return ScreenState.Unknown;
        }

        private bool IsGlyphScreen(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(1953, 118), new Point(1983, 175), new Point(2045, 147), new Point(2118, 135), new Point(2185, 170) };
            var darkPixels = new Point[] { new Point(1928, 142), new Point(1989, 163), new Point(2045, 119), new Point(2099, 133), new Point(2162, 159) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        private bool IsProfileMenu(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(569, 929), new Point(585, 956), new Point(659, 980), new Point(673, 926), new Point(760, 951) };
            var darkPixels = new Point[] { new Point(571, 957), new Point(647, 953), new Point(694, 932), new Point(749, 940), new Point(810, 932) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        private bool IsMainMenu(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(554, 959), new Point(650, 975), new Point(680, 933), new Point(778, 950), new Point(810, 977) };
            var darkPixels = new Point[] { new Point(568, 942), new Point(626, 972), new Point(700, 948), new Point(771, 939), new Point(902, 956) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        private bool IsWarframeControl(Bitmap bitmap)
        {
            var miniProfileIconPoints = new Point[] { new Point(198, 172), new Point(208, 171), new Point(218, 170), new Point(228, 173), new Point(238, 174), new Point(248, 170) };
            var largeProfileIconPoints = new Point[] { new Point(196, 194), new Point(206, 199), new Point(216, 195), new Point(226, 196), new Point(236, 197), new Point(246, 198) };
            return !miniProfileIconPoints.Any(p => { var pixel = bitmap.GetPixel(p.X, p.Y); return pixel.R >= 178 && pixel.R <= 198 && pixel.G >= 155 && pixel.G <= 175 && pixel.B >= 91 && pixel.B <= 111; })
                && !largeProfileIconPoints.Any(p => { var pixel = bitmap.GetPixel(p.X, p.Y); return pixel.R >= 178 && pixel.R <= 198 && pixel.G >= 155 && pixel.G <= 175 && pixel.B >= 91 && pixel.B <= 111; });
        }

        private bool IsDailyRewardScreenPlat(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(3265, 1939), new Point(3291, 1964), new Point(3325, 1958), new Point(3347, 1963), new Point(3383, 1942) };
            var darkPixels = new Point[] { new Point(3271, 1951), new Point(3300, 1950), new Point(3325, 1964), new Point(3373, 1941), new Point(3381, 1964) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        private bool IsDailyRewardScreenItem(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(2706, 1875), new Point(2743, 1897), new Point(2776, 1897), new Point(2830, 1873), new Point(2956, 1885) };
            var darkPixels = new Point[] { new Point(2766, 1894), new Point(2868, 1875), new Point(2963, 1879), new Point(3038, 1889), new Point(3162, 1885) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        private bool IsLoginScreen(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(2885, 1324), new Point(2913, 1347), new Point(2928, 1325), new Point(2960, 1339), new Point(3005, 1346) };
            var darkPixels = new Point[] { new Point(2878, 1336), new Point(2894, 1335), new Point(2921, 1335), new Point(2951, 1328), new Point(2994, 1346) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value < 0.65f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value > 0.4f);
        }

        public bool IsExitable(Bitmap b)
        {
            var darkPixels = new Point[] { new Point(3788, 1998), new Point(3789, 2008), new Point(3776, 2013), new Point(3790, 2023), new Point(3787, 2034), new Point(3857, 1996), new Point(3850, 2013) };
            var lightPixles = new Point[] { new Point(3790, 2002), new Point(3781, 2013), new Point(3789, 2015), new Point(3782, 2029), new Point(3815, 2016), new Point(3857, 2003) };
            if (darkPixels.Any(p =>
            {
                var pixel = b.GetPixel(p.X, p.Y);
                if (pixel.R > 100 || pixel.G > 100 || pixel.G > 100)
                    return true;
                return false;
            }))
                return false;
            if (lightPixles.Any(p =>
             {
                 var pixel = b.GetPixel(p.X, p.Y);
                 if (pixel.R < 180 || pixel.G < 180 || pixel.G < 180)
                     return true;
                 return false;
             }))
                return false;
            return true;
        }

        private bool IsLoadingScreen(Bitmap bitmap)
        {
            var warframeLogoPoints = new Point[] { new Point(1885, 1964), new Point(1956, 1973), new Point(2003, 2000), new Point(2022, 1985), new Point(2080, 1970), new Point(2116, 2003), new Point(2122, 1977), new Point(2209, 2003) };
            var notWarframeLogoPoints = new Point[] { new Point(1900, 1969), new Point(1927, 1968), new Point(1956, 1999), new Point(1994, 1977), new Point(2037, 1996), new Point(2069, 1977), new Point(2100, 1996) };
            var warframePointsPresent = !warframeLogoPoints.Any(p => bitmap.GetPixel(p.X, p.Y).GetBrightness() < 0.95);
            var notWarframePointsDark = notWarframeLogoPoints.Select(p => bitmap.GetPixel(p.X, p.Y).GetBrightness()).Average() < 0.9;
            return warframePointsPresent && notWarframePointsDark;
        }

        private bool isChat(Bitmap bitmap)
        {
            var p = bitmap.GetPixel(146, 618);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(150, 618);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(160, 618);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(172, 617);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(179, 607);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(81, 2120);
            if (p.R > 40 || p.G > 40 || p.B > 40) //Not super dark
                return false;
            p = bitmap.GetPixel(76, 2122);
            if (p.R < 65 || p.G < 65 || p.B < 65 ||
                p.R > 95 || p.G > 95 || p.B > 95) //Not kinda darkish
                return false;

            return true;
        }

        private bool IsPurple(Color color)
        {
            var hsv = color.ToHsv();
            var h = hsv.Hue;
            var v = hsv.Value;
            if (h > 240 && h < 280
                && v > 0.45)
                return true;
            return false;
        }
        private bool IsRiven(Bitmap bitmap)
        {
            var purplePixelAnchors = new Point[] { new Point(1831, 1160), new Point(2262, 1160), new Point(2262, 459), new Point(2250, 517), new Point(1815, 432), new Point(2338, 896) };
            return !purplePixelAnchors.Any(p => !IsPurple(bitmap.GetPixel(p.X, p.Y)));
        }

        public bool IsChatCollapsed(Bitmap screen)
        {
            //Already moved down pixels
            var lightPixelsLower = new Point[] { new Point(151, 2115), new Point(158, 2136), new Point(166, 2115), new Point(173, 2124), new Point(171, 2136) };
            var darkPixelsLower = new Point[] { new Point(156, 2120), new Point(168, 2131), new Point(172, 2110), new Point(146, 2118), new Point(176, 2133) };
            var isLowerAndCollapsed = !lightPixelsLower.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value <= 0.3f)
                && !darkPixelsLower.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value >= 0.15f);
            if (isLowerAndCollapsed)
                return true;

            //Not yet moved down pixels
            var lightPixelsHigher = lightPixelsLower.Select(p => new Point(p.X, p.Y - 27));
            var darkPixelsHigher = darkPixelsLower.Select(p => new Point(p.X, p.Y - 27));
            var isHigherAndCollapsed = !lightPixelsHigher.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value <= 0.3f)
                && !darkPixelsHigher.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value >= 0.15f);

            return isLowerAndCollapsed || isHigherAndCollapsed;
        }

        public bool GlyphFiltersPresent(Bitmap bitmap)
        {
            var lightPixels = new Point[] { new Point(367, 256), new Point(401, 275), new Point(427, 257), new Point(440, 272), new Point(456, 255) };
            var darkPixels = new Point[] { new Point(369, 261), new Point(395, 271), new Point(419, 264), new Point(447, 251), new Point(460, 265) };
            return !lightPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value <= 0.5f)
                && !darkPixels.Any(p => bitmap.GetPixel(p.X, p.Y).ToHsv().Value >= 0.5f);
        }

        public bool IsChatOpen(Bitmap screen)
        {
            var lightPixels = new Point[] { new Point(147, 617), new Point(154, 617), new Point(170, 612), new Point(175, 629), new Point(153, 634) };
            var darkPixels = new Point[] { new Point(151, 608), new Point(154, 630), new Point(170, 630), new Point(172, 608), new Point(162, 609) };
            return !lightPixels.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value <= 0.5f)
                && !darkPixels.Any(p => screen.GetPixel(p.X, p.Y).ToHsv().Value >= 0.5f);
        }
    }
}
