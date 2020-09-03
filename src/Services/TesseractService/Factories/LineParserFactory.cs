using Application.Enums;
using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using TesseractService.Parsers;

namespace TesseractService.Factories
{
    public class LineParserFactory : ILineParserFactory
    {
        public ILineParser CreateParser(ClientLanguage language)
        {
            return new LineParser(language);
        }
    }
}
