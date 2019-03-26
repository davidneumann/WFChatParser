using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LineParseResult
{
    public class RedtextLineResult : BaseLineParseResult
    {
        public RedtextLineResult()
        {
            this.RawMessage = string.Empty;
        }

        public override string GetKey()
        {
            return this.RawMessage;
        }
    }
}
