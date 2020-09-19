using Application.ChatLineExtractor;
using RelativeChatParser.Database;
using RelativeChatParser.Extraction;
using RelativeChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace RelativeChatParser.Training
{
    public static class SpaceTrainer
    {
        public static void TrainOnSpace(string inputDir, string outputFilepath)
        {
            var allFiles = Directory.GetFiles(inputDir);

            var spaceDefinitions = new List<GlyphSpaceDefinition>();
            foreach (var file in allFiles.Select(f => f.Substring(0, f.LastIndexOf('.'))).Distinct())
            {
                var textFile = file + ".txt";
                var imageFile = file + ".png";
                if (!allFiles.Contains(textFile) || !allFiles.Contains(imageFile))
                {
                    Console.WriteLine("Missing either image or text file for " + file);
                    throw new Exception("File missing");
                }

                Console.WriteLine($"Looking at file {file}");
                using (var b = new Bitmap(imageFile))
                {
                    var ic = new ImageCache(b);

                    var expectedLines = File.ReadAllLines(textFile).Select(f => f.Replace(" ", "")).ToArray();
                    for (int i = 0; i < expectedLines.Length; i++)
                    {
                        var glyphs = FastLineScanner.ExtractGlyphsFromLine(ic, i);
                        if(expectedLines[i].Length != glyphs.Length)
                        {
                            Console.WriteLine($"Glyph count unextected on line {i}");
                            throw new Exception("Glyph count incorrect");
                        }
                        for (int j = 0; j < expectedLines[i].Length; j += 2)
                        {
                            var def = new GlyphSpaceDefinition();
                            def.LeftCharacter = expectedLines[i][j].ToString();
                            def.RightCharacter = expectedLines[i][j+1].ToString();
                            def.SpaceSize = glyphs[j + 1].Left - glyphs[j].Right;
                            spaceDefinitions.Add(def);
                        }
                    }
                }
            }

            Console.WriteLine($"Saving resulting DB with {spaceDefinitions.Count} space definitions to {outputFilepath}");
            GlyphDatabase.Instance.AllSpaces = spaceDefinitions;
            GlyphDatabase.Instance.Init();
            var json = JsonConvert.SerializeObject(GlyphDatabase.Instance);
            var fileInfo = new FileInfo(outputFilepath);
            if (!fileInfo.Directory.Exists)
                Directory.CreateDirectory(fileInfo.Directory.FullName);
            File.WriteAllText(fileInfo.FullName, json);
        }
    }
}
