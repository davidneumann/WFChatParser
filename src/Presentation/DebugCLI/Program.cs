using ImageOCR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WFGameCapture;
using WFImageParser;

namespace DebugCLI
{
    class Program
    {        
        static string outputDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 19\Outputs";

        static void Main(string[] args)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            Console.WriteLine("Push enter and then switch to warframe");
            Console.ReadLine();
            for (int i = 0; i < 5; i++)
            {
                Console.Write($"\rStarting in {5 - i} seconds...");
                System.Threading.Thread.Sleep(1000);
            }
            
            var capture = new GameCapture();
            capture.GetTradeChatImage();

            //ProcessChatLogs();
            //ProcessRivens();
        }

        private static void ProcessRivens()
        {
            var sw = new Stopwatch();
            sw.Start();
            var t = new ImageParser();
            sw.Stop();
            Console.WriteLine("Initialize finished in: " + sw.Elapsed.TotalSeconds + "s");
            sw.Reset();

            foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\"))
            {
                sw.Reset();
                sw.Start();
                var text = t.ParseChatImage(file);
                var fileInfo = new FileInfo(file);
                File.WriteAllLines(Path.Combine(outputDir, fileInfo.Name + ".txt"), text.ChatTextLines);
                sw.Stop();
                Console.WriteLine("Parsed riven in: " + sw.Elapsed.TotalSeconds + "s");
                sw.Reset();
            }
        }

        private static void ProcessChatLogs()
        {
            var sw = new Stopwatch();
            sw.Start();
            var p = new ChatImageCleaner();
            var t = new ImageParser();
            sw.Stop();
            Console.WriteLine("Initialize finished in: " + sw.Elapsed.TotalSeconds + "s");
            sw.Reset();
            Console.WriteLine("=Processing files=");
            var fileTimes = new List<double>();
            foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs"))
            {
                var name = file.Substring(file.LastIndexOf('\\') + 1);
                Console.WriteLine($"=={name}==");
                var totalSeconds = 0.0;
                sw.Reset();
                sw.Start();
                var processedImagePath = p.ProcessChatImage(file, outputDir);
                sw.Stop();
                Console.WriteLine("Cleaned in: " + sw.Elapsed.TotalSeconds + "s");
                totalSeconds += sw.Elapsed.TotalSeconds;
                sw.Reset();
                sw.Start();
                var chatContents = t.ParseChatImage(processedImagePath);
                var fileInfo = new FileInfo(file);
                File.WriteAllLines(Path.Combine(outputDir, fileInfo.Name + ".txt"), chatContents.ChatTextLines);
                sw.Stop();
                totalSeconds += sw.Elapsed.TotalSeconds;
                Console.WriteLine("Parsed in: " + sw.Elapsed.TotalSeconds + "s");
                fileTimes.Add(totalSeconds);
                sw.Reset();
                ClickPointVisualizer.DrawClickPointsOnImage(processedImagePath, chatContents.ClickPoints);
                Console.WriteLine("File done in: " + totalSeconds + "s");
            }

            var averageTime = fileTimes.Aggregate(0d, (acc, i) => acc + i) / fileTimes.Count;
            Console.WriteLine($"Average screenshot processing time: {averageTime}s");
            Console.WriteLine("Jobs done");
        }
    }
}
