using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageOCR.ComplexRivenParser
{
    internal class CharacterDetail
    {
        public Rectangle CharacterRect { get; private set; }
        public string ParsedValue { get; set; } = null;

        public CharacterDetail(Rectangle charRect)
        {
            CharacterRect = charRect;
        }
    }
}
