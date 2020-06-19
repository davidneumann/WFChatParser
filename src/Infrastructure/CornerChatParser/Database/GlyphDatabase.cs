using CornerChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CornerChatParser.Database
{
    public static class GlyphDatabase
    {
        public static List<Glyph> AllGlyphs = new List<Glyph>();

        static GlyphDatabase()
        {
            AllGlyphs = JsonConvert.DeserializeObject<List<Glyph>>(File.ReadAllText("CornerDB.json"));
        }

        private static int _cachedDescSize;
        private static Glyph[] _cachedDesdSizeItems;
        public static Glyph[] GlyphsBySizeDescending()
        {
            if (AllGlyphs.Count != _cachedDescSize)
            {
                _cachedDescSize = AllGlyphs.Count;
                _cachedDesdSizeItems = AllGlyphs.OrderByDescending(g => g.ReferenceMaxWidth).ToArray();
            }
            return _cachedDesdSizeItems;
        }
    }
}
