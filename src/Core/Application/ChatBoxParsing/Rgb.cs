using System;
using System.Collections.Generic;
using System.Text;

namespace Application.ChatBoxParsing
{
    public struct Rgb
    {
        byte R;
        byte G;
        byte B;

        public Rgb(byte red, byte green, byte blue)
        {
            R = red;
            G = green;
            B = blue;
        }
    }
}
