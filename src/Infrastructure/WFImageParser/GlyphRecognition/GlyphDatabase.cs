using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static WFImageParser.ChatParser;

namespace WFImageParser.GlyphRecognition
{
    internal static class GlyphDatabase
    {
        public static GlyphDetails[] KnownGlyphs { get;  private set; }
        private static Dictionary<string, Dictionary<string, int>> _gapPairs = new Dictionary<string, Dictionary<string, int>>();
        public static int MaxCharWidth { get; private set; } = 0;
        public static int MinCharWidth { get; private set; } = 0;

        private static readonly string GAPSFILE = Path.Combine("ocrdata", "gaps.json");

        static GlyphDatabase()
        {
            var knownGlyphs = new List<GlyphDetails>();
            if (Directory.Exists("ocrdata"))
            {
                foreach (var file in Directory.GetFiles("ocrdata").Where(f => f.EndsWith(".png")))
                {
                    var character = new GlyphDetails()
                    {
                        Name = (new FileInfo(file)).Name.Replace(".png", ""),
                        TotalWeights = 0f
                    };
                    using (Image<Rgba32> image = Image.Load(file))
                    {
                        character.VMask = new bool[image.Width, image.Height];
                        character.WeightMappings = new float[image.Width, image.Height];
                        for (int x = 0; x < image.Width; x++)
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                character.WeightMappings[x, y] = (float)image[x, y].R / (float)byte.MaxValue;
                                character.TotalWeights += character.WeightMappings[x, y];
                                if (character.WeightMappings[x, y] > 0)
                                {
                                    character.VMask[x, y] = true;
                                }
                                else
                                    character.VMask[x, y] = false;
                            }
                        }
                        character.Width = image.Width;
                        character.Height = image.Height;
                        knownGlyphs.Add(character);
                        if (character.Width > MaxCharWidth)
                            MaxCharWidth = character.Width;
                        if (MinCharWidth == 0 || character.Width < MinCharWidth)
                            MinCharWidth = character.Width;
                    }
                }

                //Load up gap pairs
                if (File.Exists(GAPSFILE))
                {
                    var gapPairs = JsonConvert.DeserializeObject<SimpleGapPair[]>(File.ReadAllText(GAPSFILE));
                    foreach (var gapPair in gapPairs)
                    {
                        if (!_gapPairs.ContainsKey(gapPair.Left))
                            _gapPairs.Add(gapPair.Left, new Dictionary<string, int>());
                        if (gapPair.Gap > 0)
                            _gapPairs[gapPair.Left].Add(gapPair.Right, gapPair.Gap); //- 1); //There is an off by 1 error in the gaps file currently
                        else
                            _gapPairs[gapPair.Left].Add(gapPair.Right, 0);
                    }
                }
            }
            KnownGlyphs = knownGlyphs.ToArray();
        }
    }
}
