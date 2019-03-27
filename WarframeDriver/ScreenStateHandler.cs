using Application.Enums;
using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
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

            return ScreenState.Unknown;
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
