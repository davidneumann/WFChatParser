using Application.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface ILineParserFactory
    {
        ILineParser CreateParser(ClientLanguage language);
    }
}
