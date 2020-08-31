using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Interfaces
{
    public interface ILineParser
    {
        string ParseLine(Bitmap bitmap);
    }
}
