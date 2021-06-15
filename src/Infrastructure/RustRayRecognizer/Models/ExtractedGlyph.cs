using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RustRayRecognizer.Models
{
    class ExtractedGlyph
    {
        public ushort Width { get; set; }
        public byte Height { get; set; }
        public byte PixelsFromTop { get; set; }
        public bool[][] BrightMask { get; set; }
    }
}
