using ImageOCRBad;
using System;
using System.Diagnostics;
using System.IO;
using WFImageParser;

namespace DebugCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();
            var p = new ChatImageCleaner();
            var t = new ImageParser();
            sw.Stop();
            Console.WriteLine("Initialize finished in: " + sw.Elapsed.TotalSeconds + "s");
            sw.Reset();
            var outputDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 6\Outputs";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            Console.WriteLine("=Processing files=");
            foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs"))
            {
                var name = file.Substring(file.LastIndexOf('\\')+1);
                Console.WriteLine($"=={name}==");
                var totalSeconds = 0.0;
                sw.Reset();
                sw.Start();
                var cleaned = p.CleanImage(file, outputDir);
                sw.Stop();
                Console.WriteLine("Cleaned in: " + sw.Elapsed.TotalSeconds + "s");
                totalSeconds += sw.Elapsed.TotalSeconds;
                sw.Reset();
                sw.Start();
                t.ParseImage(cleaned, outputDir);
                sw.Stop();
                totalSeconds += sw.Elapsed.TotalSeconds;
                Console.WriteLine("Parsed in: " + sw.Elapsed.TotalSeconds + "s");
                sw.Reset();
                Console.WriteLine("File done in: " + totalSeconds + "s");
            }

            Console.WriteLine("Jobs done");
        }
    }
}
