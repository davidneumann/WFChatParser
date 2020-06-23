using CornerChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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

        private static ConcurrentDictionary<(int, int), Glyph[]> _targetSizeCache = new ConcurrentDictionary<(int, int), Glyph[]>();
        static GlyphDatabase()
        {
            AllGlyphs = JsonConvert.DeserializeObject<List<Glyph>>(File.ReadAllText("CornerDB.json"));
            _cachedDescSize = AllGlyphs.Count;
            _cachedDesdSizeItems = AllGlyphs.OrderByDescending(g => g.ReferenceMaxWidth).ToArray();

            foreach (var group in AllGlyphs.GroupBy(g => g.ReferenceMinWidth))
            {
                _byWidth[group.Key] = group.ToArray();
            }
            foreach (var group in AllGlyphs.GroupBy(g => g.ReferenceMinHeight))
            {
                _byHeight[group.Key] = group.ToArray();
            }

            foreach (var glyph in AllGlyphs)
            {
                for (int width = Math.Max(1, glyph.ReferenceMinWidth - 1); width <= glyph.ReferenceMinWidth + 1; width++)
                {
                    for (int height = Math.Max(1, glyph.ReferenceMinHeight); height <= glyph.ReferenceMinHeight + 1; height++)
                    {
                        GetGlyphByTargetSize(width, height);
                    }
                }
            }
        }

        public static void Init()
        {

        }

        private static int _cachedDescSize = 0;
        private static Glyph[] _cachedDesdSizeItems;
        public static Glyph[] GlyphsBySizeDescending()
        {
            return _cachedDesdSizeItems;
        }

        private static Dictionary<int, Glyph[]> _byWidth = new Dictionary<int, Glyph[]>();
        private static Dictionary<int, Glyph[]> _byHeight = new Dictionary<int, Glyph[]>();
        public static Glyph[] GetGlyphByTargetSize(int width, int height)
        {
            if (_targetSizeCache.ContainsKey((width, height)))
                return _targetSizeCache[(width, height)];

            List<Glyph> results = new List<Glyph>();
            if (_byWidth.ContainsKey(width - 1))
                results.AddRange(_byWidth[width - 1]);
            if (_byWidth.ContainsKey(width))
                results.AddRange(_byWidth[width]);
            if (_byWidth.ContainsKey(width + 1))
                results.AddRange(_byWidth[width + 1]);

            _targetSizeCache[(width, height)] = results.Where(g => height >= g.ReferenceMinHeight - 1 && height <= g.ReferenceMinHeight + 1).ToArray();

            //_targetSizeCache[(width, height)] = AllGlyphs.Where(g => g.ReferenceMinWidth == width && g.ReferenceMinHeight == height).ToArray();
            return _targetSizeCache[(width, height)];
        }
    }
}
