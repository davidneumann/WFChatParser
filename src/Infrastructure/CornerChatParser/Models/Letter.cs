using System;
using System.Collections.Generic;
using System.Text;

namespace RelativeChatParser.Models
{
    public class Letter
    {
        public FuzzyGlyph FuzzyGlyph;
        public ExtractedGlyph ExtractedGlyph;
        public Letter(FuzzyGlyph fuzzyGlyph, ExtractedGlyph extractedGlyph)
        {
            FuzzyGlyph = fuzzyGlyph;
            ExtractedGlyph = extractedGlyph;
        }

        public override string ToString()
        {
            if (FuzzyGlyph != null)
                return FuzzyGlyph.Character;
            else
                return "";
        }
    }
}
