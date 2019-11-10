using Application.Enums;
using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageOCR
{
    public class RivenParserFactory : IRivenParserFactory
    {
        private ClientLanguage _clientLanguage;

        public RivenParserFactory(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
        }
        public IRivenParser CreateRivenParser()
        {
            return new RivenParser(_clientLanguage);
        }
    }
}
