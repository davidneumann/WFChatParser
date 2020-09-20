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

        private FuzzyGlyph[] _cachedSingleCharOverlaps;
        
        public const float BrightMinV = 0.95f;

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
                            _instance = JsonConvert.DeserializeObject<GlyphDatabase>(File.ReadAllText("FastRelativeDB.json"));
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
                //Remove any empty that does not have a neighbor
                //Also update brights to be only where current threshold is
                //Also setup combined mask
                int width = glyph.RelativeEmpties.GetLength(0);
                int height = glyph.RelativeEmpties.GetLength(1);
                var brightCount = 0;
                glyph.RelativeBrights = new float[width, height];
                glyph.RelativeCombinedMask = new bool[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        //Only do empty stuff if on an actual empty
                        if (glyph.RelativeEmpties[x, y])
                        {
                            var valid = false;
                            //Left
                            if (x > 0 && glyph.RelativeEmpties[x - 1, y])
                                valid = true;
                            //Right
                            if (x + 1 < width && glyph.RelativeEmpties[x + 1, y])
                                valid = true;

                            //Top
                            if (y > 0 && glyph.RelativeEmpties[x, y - 1])
                                valid = true;
                            //Bottom
                            if (y + 1 < height && glyph.RelativeEmpties[x, y + 1])
                                valid = true;

                            if (!valid)
                                glyph.RelativeEmpties[x, y] = false;
                        }

                        //Setup brights
                        if (glyph.RelativePixels[x, y] >= BrightMinV)
                        {
                            glyph.RelativeBrights[x, y] = glyph.RelativePixels[x, y];
                            brightCount++;
                        }
                        else
                            glyph.RelativeBrights[x, y] = 0f;

                        //Add to combined mask
                        glyph.RelativeCombinedMask[x, y] = glyph.RelativeEmpties[x, y] || glyph.RelativePixels[x, y] > 0;
                    }
                }
                glyph.RelativeBrightsCount = brightCount;

                if (glyph.Character == "l" || glyph.Character == "I")
                {
                    glyph.RelativeEmpties = new bool[width, height];
                }
            }

            _cachedDescSize = AllGlyphs.Count;
            _cachedDesdSizeItems = AllGlyphs.OrderByDescending(g => g.ReferenceMaxWidth).ToArray();

            var pureGlyphs = AllGlyphs.Where(g => !g.IsOverlap).ToArray();
            var charsThatCanOverlap = AllGlyphs.Where(g => g.IsOverlap).SelectMany(g => g.Character.ToCharArray()).Distinct().ToArray();
            if(charsThatCanOverlap.Length > 0)
                _cachedSingleCharOverlaps = pureGlyphs.Where(g => charsThatCanOverlap.Contains(g.Character[0])).ToArray();

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
        public FuzzyGlyph[] CharsThatCanOverlapByDescSize()
        {
            return _cachedSingleCharOverlaps;
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
