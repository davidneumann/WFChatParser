using Application.ChatLineExtractor;
using RelativeChatParser.Extraction;
using RelativeChatParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace RelativeChatParser.Training
{
    public static class TrainingDataExtractor
    {
        public static Dictionary<char, List<FastExtractedGlyph>> ExtractGlyphs(IEnumerable<TrainingInput> inputs)
        {
            var glyphDict = new Dictionary<char, List<FastExtractedGlyph>>();
            foreach (var input in inputs)
            {
                var inputShort = input.ImageFilePath;
                if (inputShort.Contains("\\"))
                    inputShort = input.ImageFilePath.Substring(input.ImageFilePath.LastIndexOf("\\") + 1);

                Console.WriteLine($"Extracing glyphs from {inputShort}.");

                var bitmap = new Bitmap(input.ImageFilePath);
                var ic = new ImageCache(bitmap);
                ic.DebugFilename = input.ImageFilePath;
                var textLines = File.ReadAllLines(input.CorrectTextPath).Where(l => l.ToLower() != "clear").ToArray();
                var glyphLines = textLines.Select((u, i) => FastLineScanner.ExtractGlyphsFromLineShim(ic, i)).ToArray();
                for (int i = 0; i < textLines.Length; i++)
                {
                    var cleanText = textLines[i].Replace(" ", "").Trim();
                    if (cleanText.Length != glyphLines[i].Length)
                    {
                        Console.WriteLine($"Fatal error in {inputShort}! Glyph text count mistmatch on line index {i}\n{textLines[i]}");
                        Console.WriteLine("Dumping glyphs to training_errors\\");
                        FastLineScanner.SaveExtractedGlyphs(ic, "training_errors", glyphLines[i]);
                        throw new Exception("Input mismatch");
                    }

                    for (int j = 0; j < cleanText.Length; j++)
                    {
                        char c = cleanText[j];
                        if (!glyphDict.ContainsKey(c))
                        {
                            glyphDict[c] = new List<FastExtractedGlyph>();
                        }
                        glyphDict[c].Add(glyphLines[i][j]);
                    }
                }
                bitmap.Dispose();
            }

            return glyphDict;
        }
    }

    public class TrainingInput
    {
        public string ImageFilePath { get; set; }
        public string CorrectTextPath { get; set; }

        public TrainingInput(string imagePath, string textPath)
        {
            ImageFilePath = imagePath;
            CorrectTextPath = textPath;
        }
    }
}
