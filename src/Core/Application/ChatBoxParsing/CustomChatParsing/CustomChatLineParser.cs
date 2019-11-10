using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Application.Data;
using Application.LineParseResult;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Application.ChatBoxParsing.CustomChatParsing
{
    public class CustomChatLineParser : BaseChatLineParser, IChatLineParser
    {
        private List<CharacterDetails> _scannedCharacters = new List<CharacterDetails>();
        private Dictionary<string, Dictionary<string, int>> _gapPairs = new Dictionary<string, Dictionary<string, int>>();
        private int _maxCharWidth = 0;
        private int _minCharWidth = int.MaxValue;
        private int _referenceHeight = 0;
        private static readonly List<Regex> _blacklistedRegex = new List<Regex>();

        public CustomChatLineParser() : base()
        {
            //Load blacklists
            var blacklistFile = Path.Combine(DataHelper.OcrDataPathEnglish, "MessageBlacklists.txt");
            if (Directory.Exists(DataHelper.OcrDataPathEnglish) && File.Exists(blacklistFile))
            {
                foreach (var line in File.ReadAllLines(blacklistFile))
                {
                    _blacklistedRegex.Add(new Regex(line, RegexOptions.Compiled));
                }
            }

            if (Directory.Exists(DataHelper.OcrDataPathEnglish))
            {
                foreach (var file in Directory.GetFiles(DataHelper.OcrDataPathEnglish).Where(f => f.EndsWith(".png")))
                {
                    var character = new CharacterDetails()
                    {
                        Name = (new FileInfo(file)).Name.Replace(".png", ""),
                        TotalWeights = 0f
                    };
                    using (var image = new Bitmap(file))
                    {
                        if (image.Height > _referenceHeight)
                            _referenceHeight = image.Height;
                        character.VMask = new bool[image.Width, image.Height];
                        character.WeightMappings = new float[image.Width, image.Height];
                        for (int x = 0; x < image.Width; x++)
                        {
                            for (int y = 0; y < image.Height; y++)
                            {
                                character.WeightMappings[x, y] = (float)image.GetPixel(x, y).R / (float)byte.MaxValue;
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
                        _scannedCharacters.Add(character);
                        if (character.Width > _maxCharWidth)
                            _maxCharWidth = character.Width;
                        if (character.Width < _minCharWidth)
                            _minCharWidth = character.Width;
                    }
                }

                //Load up gap pairs
                var gapsFile = Path.Combine(DataHelper.OcrDataPathEnglish, "gaps.json");
                if (File.Exists(gapsFile))
                {
                    var gapPairs = JsonConvert.DeserializeObject<SimpleGapPair[]>(File.ReadAllText(gapsFile));
                    foreach (var gapPair in gapPairs)
                    {
                        if (!_gapPairs.ContainsKey(gapPair.Left))
                            _gapPairs.Add(gapPair.Left, new Dictionary<string, int>());
                        if (gapPair.Gap > 0)
                            _gapPairs[gapPair.Left].Add(gapPair.Right, gapPair.Gap - 1); //There is an off by 1 error in the gaps file currently
                        else
                            _gapPairs[gapPair.Left].Add(gapPair.Right, 0);
                    }
                }
            }
        }

        public BaseLineParseResult ParseLine(Bitmap lineImage)
        {
            Bitmap line = lineImage;
            if (lineImage.Height != _referenceHeight)
            {
                //Adjust to reference height
                using (var mem = new MemoryStream())
                {
                    lineImage.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    mem.Seek(0, SeekOrigin.Begin);
                    using (Image<Rgba32> rgbImage = SixLabors.ImageSharp.Image.Load(mem))
                    {
                        rgbImage.Mutate(m => m.EntropyCrop());
                        var scale = (float)_referenceHeight / rgbImage.Height;
                        rgbImage.Mutate(m => m.Resize((int)Math.Round(rgbImage.Width * scale), (int)Math.Round(rgbImage.Height * scale)));
                        mem.Seek(0, SeekOrigin.Begin);
                        mem.SetLength(0);
                        rgbImage.Save(mem, new PngEncoder());
                    }

                    mem.Seek(0, SeekOrigin.Begin);
                    line = new Bitmap(mem);
                    line.Save("debug.png");
                }
            }

            if (line != lineImage)
                line.Dispose();

            return new ChatMessageLineResult();
        }
    }
}
