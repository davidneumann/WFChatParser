using ImageOCR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WFGameCapture;
using WFImageParser;

namespace DebugCLI
{
    class Program
    {        
        static string outputDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 23\Outputs";

        static void Main(string[] args)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            //MonitorChatLive();

            AnalyseImages();
            //ProcessChatLogs();
            //ProcessRivens();
        }

        private static void MonitorChatLive()
        {
            Console.WriteLine("Push enter and then switch to warframe");
            Console.ReadLine();
            for (int i = 0; i < 5; i++)
            {
                Console.Write($"\rStarting in {5 - i} seconds...");
                System.Threading.Thread.Sleep(1000);
            }

            var capture = new GameCapture();
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>("chars.json"));
            var t = new ImageParser();

            using (var fout = new System.IO.StreamWriter("output.txt"))
            {
                string[] messageHistory = new string[100];
                var index = 0;
                while (true)
                {
                    var image = capture.GetTradeChatImage();
                    var processedImagePath = p.ProcessChatImage(image, Environment.CurrentDirectory);
                    var text = t.ParseChatImage(processedImagePath);
                    foreach (var line in text.ChatTextLines)
                    {
                        if (!messageHistory.Contains(line))
                        {
                            Console.WriteLine(line);
                            fout.WriteLine(line);
                            fout.Flush();
                            messageHistory[index++] = line;
                            if (index >= messageHistory.Length)
                                index = 0;
                        }
                    }
                }
            }
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
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>("chars.json"));
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
        
        private static void AnalyseImages()
        {
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>(File.ReadAllText("chars.json")));
            Console.WriteLine("=Processing files=");
            foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Text Inputs"))
            {
                var name = file.Substring(file.LastIndexOf('\\') + 1);
                Console.WriteLine($"=={name}==");

                var outputFile = Path.Combine(outputDir, name);

                var chars = "! # $ % & ' ( ) * + , - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ ` a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ~ – — ’ ‚".Replace(" ", "");
                var rand = new Random();
                for (int i = 0; i < 27; i++)
                {
                    var sb = new StringBuilder();
                    foreach (var character in chars.OrderBy(x => rand.Next()))
                    {
                        sb.Append(character + " ");
                    }
                    Console.WriteLine(sb.ToString().Trim() + " [" + "\n");
                }
                var validVs = new List<float>();
                for (float v = 1.0f; v >= 0.29; v -= 0.01f)
                {
                    p.SaveGreyscaleImage(file, outputFile, v);
                    var charInfoResults = p.AnalyzeInput(file, chars, v);
                    var newP = new ChatImageCleaner(charInfoResults);
                    var charResults = newP.VerifyInput(file, v);
                    if (charResults.SequenceEqual(chars))
                        validVs.Add(v);                        
                }

                //var processedImagePath = p.AnalysisChatMessage(file, outputDir);
                //Console.WriteLine(processedImagePath);
            }
            Console.WriteLine("Jobs done");
        }
    }
}
