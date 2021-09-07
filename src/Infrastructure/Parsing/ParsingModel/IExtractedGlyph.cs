using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParsingModel
{
    public interface IExtractedGlyph
    {
        int Height { get; }
        int Width { get; }
        int PixelsFromTopOfLine { get; }
        Point3[] RelativeBrights { get; }
    }
}
