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
    public class GlyphDatabase
    {
        public GlyphDetails[] KnownGlyphs { get; private set; }
        public Dictionary<string, Dictionary<string, int>> GapPairs { get; private set; } = new Dictionary<string, Dictionary<string, int>>();
        public int MaxCharWidth { get; private set; } = 0;
        public int MinCharWidth { get; private set; } = 0;

        public GlyphDatabase(string dataDirectory)
        {
            var knownGlyphs = new List<GlyphDetails>();
            if (Directory.Exists(dataDirectory))
            {
                foreach (var file in Directory.GetFiles(dataDirectory).Where(f => f.EndsWith(".png")))
                {
                    using (Image<Rgba32> image = Image.Load(file))
                    {
                        var vMask = new bool[image.Width, image.Height];
                        var weightMappings = new float[image.Width, image.Height];
                        var totalWeight = 0f;
                        for (int x = 0; x < image.Width; x++)
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                weightMappings[x, y] = (float)image[x, y].R / (float)byte.MaxValue;
                                totalWeight += weightMappings[x, y];
                                if (weightMappings[x, y] > 0)
                                {
                                    vMask[x, y] = true;
                                }
                                else
                                    vMask[x, y] = false;
                            }
                        }
                        var character = new GlyphDetails(vMask, weightMappings, (new FileInfo(file)).Name.Replace(".png", ""),
                            image.Width, image.Height, totalWeight);
                        knownGlyphs.Add(character);
                        if (character.Width > MaxCharWidth && !character.Name.Contains(","))
                            MaxCharWidth = character.Width;
                        if (!character.Name.Contains(",") && (MinCharWidth == 0 || character.Width < MinCharWidth))
                            MinCharWidth = character.Width;
                    }
                }

                //Load up gap pairs
                var gapsFile = Path.Combine(dataDirectory, "gaps.json");
                if (File.Exists(gapsFile))
                {
                    var gapPairs = JsonConvert.DeserializeObject<SimpleGapPair[]>(File.ReadAllText(gapsFile));
                    foreach (var gapPair in gapPairs)
                    {
                        if (!GapPairs.ContainsKey(gapPair.Left))
                            GapPairs.Add(gapPair.Left, new Dictionary<string, int>());
                        if (gapPair.Gap > 0)
                            GapPairs[gapPair.Left].Add(gapPair.Right, gapPair.Gap); //- 1); //There is an off by 1 error in the gaps file currently
                        else
                            GapPairs[gapPair.Left].Add(gapPair.Right, 0);
                    }
                }
            }
            KnownGlyphs = knownGlyphs.ToArray();
        }
    }
}
