using RelativeChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RelativeChatParser.Recognition;
using System.Drawing;
using WebSocketSharp;

namespace RelativeChatParser.Database
{
    public class GlyphDatabase
    {
        private int _cachedDescSize = 0;
        private FuzzyGlyph[] _cachedDesdSizeItems;
        private Dictionary<string, Dictionary<string, int>> _spaceCache = new Dictionary<string, Dictionary<string, int>>();
        private ConcurrentDictionary<(int, int), FuzzyGlyph[]> _targetSizeCache = new ConcurrentDictionary<(int, int), FuzzyGlyph[]>();

        public const float BrightMinV = 0.85f;

        private static GlyphDatabase _instance = null;
        private static object _threadLock = new object();
        public static GlyphDatabase Instance
        {
            get
            {
                lock (_threadLock)
                {
                    if (_instance == null)
                    {
                        try
                        {
                            _instance = JsonConvert.DeserializeObject<GlyphDatabase>(File.ReadAllText("RelativeDB.json"));
                        }
                        catch (Exception e)
                        {
                            _instance = new GlyphDatabase();
                            Console.WriteLine("DATABASE FAILED TO INITIALIZE\n" + e.ToString());
                        }
                        //_instance = new GlyphDatabase();
                        _instance.Init();
                    }
                    return _instance;
                }
            }
        }

        public List<FuzzyGlyph> AllGlyphs { get; set; } = new List<FuzzyGlyph>();
        public List<GlyphSpaceDefinition> AllSpaces { get; set; } = new List<GlyphSpaceDefinition>();

        public GlyphDatabase()
        {
            //AllGlyphs = JsonConvert.DeserializeObject<List<FuzzyGlyph>>(File.ReadAllText("RelativeDB.json"));
        }

        public void Init()
        {
            foreach (var glyph in AllGlyphs)
            {
                glyph.RelativeBrights = glyph.RelativePixelLocations.Where(p =>
                {
                    return p.Z >= BrightMinV;
                }).ToArray();

                var validEmpties = new List<Point>();
                foreach (var empty in glyph.RelativeEmptyLocations.ToList())
                {
                    var neighborCount = glyph.RelativeEmptyLocations.Where(p => p != empty ? p.Distance(empty, 2) <= 1 : false).Count();
                    if (neighborCount != 0)
                        validEmpties.Add(empty);
                }
                glyph.RelativeEmptyLocations = validEmpties.ToArray();

                if (glyph.Character == "l" || glyph.Character == "I")
                {
                    glyph.RelativeEmptyLocations = new System.Drawing.Point[0];
                }

                glyph.RelativeCombinedLocations = glyph.RelativePixelLocations.Select(p => new Point(p.X, p.Y)).Union(glyph.RelativeEmptyLocations).ToArray();
            }

            _cachedDescSize = AllGlyphs.Count;
            _cachedDesdSizeItems = AllGlyphs.OrderByDescending(g => g.ReferenceMaxWidth).ToArray();

            _targetSizeCache.Clear();
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

            _spaceCache.Clear();
            foreach (var space in AllSpaces)
            {
                if (!_spaceCache.ContainsKey(space.LeftCharacter))
                    _spaceCache[space.LeftCharacter] = new Dictionary<string, int>();

                _spaceCache[space.LeftCharacter][space.RightCharacter] = space.SpaceSize;
            }
        }

        public FuzzyGlyph[] GlyphsBySizeDescending()
        {
            return _cachedDesdSizeItems;
        }

        internal int GetDefaultSpace()
        {
            return 7;
        }

        public FuzzyGlyph[] GetGlyphByTargetSize(int width, int height)
        {
            if (_targetSizeCache.ContainsKey((width, height)))
                return _targetSizeCache[(width, height)];

            var results = AllGlyphs
                .Where(g => width >= g.ReferenceMinWidth - 1 && width <= g.ReferenceMaxWidth + 1
                         && height >= g.ReferenceMinHeight - 1 && height <= g.ReferenceMaxHeight + 1).ToArray();

            _targetSizeCache[(width, height)] = results;

            //_targetSizeCache[(width, height)] = AllGlyphs.Where(g => g.ReferenceMinWidth == width && g.ReferenceMinHeight == height).ToArray();
            return _targetSizeCache[(width, height)];
        }

        internal int GetSpace(string character1, string character2)
        {
            if (character1.Length > 1)
                character1 = character1.Last().ToString();
            if (character2.Length > 1)
                character2 = character2[0].ToString();
            if (_spaceCache.ContainsKey(character1) && _spaceCache[character1].ContainsKey(character2))
                return _spaceCache[character1][character2];
            return GetDefaultSpace();
        }

        internal void SetSpaces(List<GlyphSpaceDefinition> spaceDefinitions)
        {
            AllSpaces = spaceDefinitions;
        }
    }
}
