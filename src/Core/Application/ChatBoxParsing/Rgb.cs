using System;
using System.Collections.Generic;
using System.Text;

namespace Application.ChatBoxParsing
{
    public struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte red, byte green, byte blue)
        {
            R = red;
            G = green;
            B = blue;
        }
    }
}
