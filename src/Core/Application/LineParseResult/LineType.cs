using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LineParseResult
{
    public enum LineType
    {
        Unknown,
        NewMessage,
        Continuation,
        RedText
    }
}
