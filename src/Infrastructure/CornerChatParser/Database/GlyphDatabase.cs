using CornerChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CornerChatParser.Database
{
    public class GlyphDatabase
    {
        private static GlyphDatabase _instance = null;
        public static GlyphDatabase Instance { get
            {
                if (_instance == null)
                {
                    _instance = JsonConvert.DeserializeObject<GlyphDatabase>(File.ReadAllText("CornerDB.json"));
                    //_instance = new GlyphDatabase();
                    _instance.Init();
                }
                return _instance;
            } }

        public List<FuzzyGlyph> AllGlyphs { get; set; } = new List<FuzzyGlyph>();
        public List<GlyphSpaceDefinition> AllSpaces { get; set; } = new List<GlyphSpaceDefinition>();

        private ConcurrentDictionary<(int, int), FuzzyGlyph[]> _targetSizeCache = new ConcurrentDictionary<(int, int), FuzzyGlyph[]>();
        public GlyphDatabase()
        {
            //AllGlyphs = JsonConvert.DeserializeObject<List<FuzzyGlyph>>(File.ReadAllText("CornerDB.json"));
        }

        public void Init()
        {
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

        private int _cachedDescSize = 0;
        private FuzzyGlyph[] _cachedDesdSizeItems;
        public FuzzyGlyph[] GlyphsBySizeDescending()
        {
            return _cachedDesdSizeItems;
        }

        private Dictionary<int, FuzzyGlyph[]> _byWidth = new Dictionary<int, FuzzyGlyph[]>();
        private Dictionary<int, FuzzyGlyph[]> _byHeight = new Dictionary<int, FuzzyGlyph[]>();
        public FuzzyGlyph[] GetGlyphByTargetSize(int width, int height)
        {
            if (_targetSizeCache.ContainsKey((width, height)))
                return _targetSizeCache[(width, height)];

            List<FuzzyGlyph> results = new List<FuzzyGlyph>();
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

        internal void SetSpaces(List<GlyphSpaceDefinition> spaceDefinitions)
        {
            AllSpaces = spaceDefinitions;
        }
    }
}
