using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LineParseResult
{
    public abstract class BaseLineParseResult
    {
        public string RawMessage { get; set; }
        public LineType LineType { get; set; } = LineType.Unknown;

        public abstract string GetKey();
    }
}
