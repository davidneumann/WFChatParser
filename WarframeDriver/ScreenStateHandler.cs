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
            if (isChat(bitmap))
                return ScreenState.ChatWindow;
            if (IsLoadingScreen(bitmap))
                return ScreenState.LoadingScreen;
            if (IsLoginScreen(bitmap))
                return ScreenState.LoginScreen;

            return ScreenState.Unknown;
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
            if(lightPixles.Any(p =>
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
            var warframeLogoPoints = new Point[] { new Point(1885,1964), new Point(1956, 1973), new Point(2003, 2000), new Point(2022, 1985), new Point(2080, 1970), new Point(2116, 2003), new Point(2122, 1977), new Point(2209, 2003) };
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

        private bool IsRiven(Bitmap bitmap)
        {
            var p = bitmap.GetPixel(2009, 284);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(2015, 285);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(2022, 286);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not Dark
                return false;
            p = bitmap.GetPixel(2028, 283);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(2033, 282);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(2005, 300);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(2012, 298);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(2021, 300);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;

            return true;
        }
    }
}
