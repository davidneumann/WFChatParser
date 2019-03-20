using ImageOCR;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WFGameCapture;
using WFImageParser;
using DataStream;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Application.ChatMessages.Model;
using WarframeDriver;

namespace DebugCLI
{
    class Program
    {
        static string outputDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 23\Outputs";

        private static DShowCapture _gameCapture;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            //MouseTests();

            //JsonMessagerHelper();
            //TrainOnImages();
            var c = new ChatParser();
            var cleaner = new ImageCleaner();
            cleaner.SaveGreyscaleImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png", 0.44f);
            var res = c.ParseChatImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png");
            //foreach (var line in res)
            //{
            //    if (line.Contains(":]"))
            //        Debugger.Break();
            //}
            //VerifyNoErrors(2);
            //var v = 0.5f;

            //TestDataSender();

            //MonitorChatLive();

            //GenerateCharStrings(27);
            //SpaceTest(27);

            //var minErrors = int.MaxValue;
            //var minErrorsV = float.MaxValue;
            //for (float v = 0.6f; v > 0.2f; v -= 0.1f)
            //{
            //MakeBitmapsSmall(v, false);
            //AverageBitmapsSmall(v, false, 0.5f);//0.415f);
            //    var sw = new Stopwatch();
            //    sw.Start();
            //    var errors = ParseWithBitmap(v, 11, verboseLevel:2);
            //    sw.Stop();
            //    Console.WriteLine("Ran in: " + sw.Elapsed.TotalSeconds);
            //    Console.WriteLine("Found " + errors + " errors");
            //    if (errors < minErrors)
            //    {
            //        minErrors = errors;
            //        minErrorsV = v;
            //        Console.WriteLine("New min errors: " + errors + " v: " + minErrorsV);
            //    }
            //}
            //MakeBitmapsSmall();

            //TrainOCR();
            //TrainOCRSmall();
            //VerifyOCR();
            //AnalyseImages();
            //ProcessChatLogs();
            //ProcessRivens();^

            //var minErrors = int.MaxValue;
            //var goodSpaceWidth = int.MaxValue;
            //for (int spaceWidth = 11; spaceWidth > 5; spaceWidth--)
            //{
            //var c = new ChatImageCleaner();
            //c.SaveGreyscaleImage(@"C:\Users\david\Downloads\Untitled.png", @"C:\Users\david\Downloads\Untitled_grey.png", v);
            //var spaceWidth = 6;
            //Console.WriteLine("space width: " + spaceWidth);
            //var sw = new Stopwatch();
            //sw.Start();
            //var errors = ParseWithBitmap(verboseLevel: 2, fastFail: false, xOffset: 0);
            //sw.Stop();
            //Console.WriteLine("Ran in: " + sw.Elapsed.TotalSeconds);
            //Console.WriteLine("Found " + errors + " errors");
            //if (errors < minErrors)
            //{
            //    minErrors = errorsz;
            //    goodSpaceWidth = spaceWidth;
            //    Console.WriteLine("New min errors: " + errors + " spaceWidth: " + goodSpaceWidth);

            //    if (minErrors == 0)
            //        break;
            //}
            //}

            //DoFullParse(0.49999999f);
        }

        private static void MouseTests()
        {
            System.Threading.Thread.Sleep(5000);
            var clicker = new Clicker();
            //clicker.MoveCursorTo(0, 0);
            System.Threading.Thread.Sleep(66);
            clicker.MoveCursorTo(1920 / 2, 1080 / 2);
        }

        private static void JsonMessagerHelper()
        {
            var r1 = new Riven()
            {
                Drain = 18,
                MasteryRank = 69,
                Rolls = 7,
                MessagePlacementId = 0,
                Modifiers = new string[] { "+50% to skill", "17% fire rate" },
                Polarity = Polarity.Madurai,
                Rank = 8,
                Name = "[Tonkor cri-shaboo]"
            };
            var r2 = new Riven()
            {
                Drain = 7,
                MessagePlacementId = 1,
                Modifiers = new string[] { "-100% damage", "+69% lens flare", "+12% particles" },
                Polarity = Polarity.Naramon,
                Rank = 0,
                MasteryRank = 7,
                Name = "[Lenz parti-maker]",
                Rolls = 100
            };
            var r3 = new Riven()
            {
                Drain = 10,
                MasteryRank = 5,
                Rolls = 20,
                MessagePlacementId = 2,
                Modifiers = new string[] { "+50% to skill", "17% fire rate", "-25% likeability" },
                Polarity = Polarity.VaZarin,
                Rank = 2,
                Name = "[Tonkor cri-shaboo]"
            };
            var m = new ChatMessageModel()
            {
                Timestamp = "[00:12]",
                Author = "joeRivenMan",
                Raw = "WTB ||| [Opticor Vandal] ||| WTS [Tonkor cri-shaboo] [[Lenz parti-maker] [Tonkor cri-shaboo] PMO",
                Rivens = new Riven[] { r1, r2, r3 },
                SpecialMessage = "WTB ||| [Opticor Vandal] ||| WTS [0][Tonkor cri-shaboo] [1][Lenz parti-maker] [2][Tonkor cri-shaboo] PMO"
            };
            var json = JsonConvert.SerializeObject(m);
            Console.WriteLine(json);
        }

        private static void TrainOnImages()
        {
            var trainer = new OCRTrainer();
            trainer.TrainOnImages(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Training Inputs", "newdata");
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            CleanUp();
        }

        private static void CleanUp()
        {
            if (_gameCapture != null)
                _gameCapture.Dispose();
        }

        //private static int VerifyNoErrors(int verboseLevel = 0, bool fastFail = false, int xOffset = 4)
        //{
        //    var trainingImages = new List<string>();
        //    Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
        //    //Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
        //    var trainingText = new List<string>();
        //    Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs").Where(f => f.EndsWith(".txt")).ToList().ForEach(f => trainingText.Add(f));
        //    //Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith(".txt")).ToList().ForEach(f => trainingText.Add(f));
        //    //var trainingImages = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".png")).ToArray();
        //    //var trainingText = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".txt")).ToArray();
        //    //var trainingImages = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith("e1.png")).ToArray();
        //    //var trainingText = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith("e1.txt")).ToArray();

        //    var errorCount = 0;
        //    for (int k = 0; k < trainingImages.Count; k++)
        //    {
        //        var fileInfo = new FileInfo(trainingText[k]);
        //        Console.WriteLine($"=={fileInfo.Name}==");
        //        var masterKeyFile = trainingImages[k];
        //        var correctResults = File.ReadAllLines(trainingText[k]).Select(line => line.Trim()).ToArray();
        //        var c = new ChatParser();
        //        var cleaner = new ImageCleaner();
        //        cleaner.SaveGreyscaleImage(masterKeyFile, Path.Combine(outputDir, (new FileInfo(masterKeyFile)).Name), minV:0.44f);
        //        var result = c.ParseChatImage(masterKeyFile, xOffset: xOffset).ToArray();

        //        Console.WriteLine("Expected");
        //        Console.WriteLine("Recieved");
        //        Console.WriteLine();

        //        if (correctResults.Length != result.Length)
        //        {
        //            errorCount += correctResults.Length;
        //            return errorCount;
        //        }
        //        for (int i = 0; i < result.Length; i++)
        //        {
        //            if (verboseLevel >= 1)
        //            {
        //                Console.WriteLine(correctResults[i]);
        //                Console.WriteLine(result[i]);
        //            }
        //            if (verboseLevel >= 2)
        //            {
        //                if (Enumerable.SequenceEqual(correctResults[i], result[i].Raw))
        //                {
        //                    Console.WriteLine("They match!");
        //                }
        //            }
        //            if (!String.Equals(correctResults[i].Trim(), result[i].Raw.Trim()))
        //            {
        //                if (verboseLevel >= 2)
        //                {
        //                    if (correctResults[i].Length == result[i].Raw.Length)
        //                    {
        //                        for (int j = 0; j < correctResults[i].Length; j++)
        //                        {
        //                            if (result[i].Raw[j] != correctResults[i][j])
        //                            {
        //                                Console.WriteLine("^");
        //                                break;
        //                            }
        //                            else
        //                            {
        //                                Console.Write(" ");
        //                            }
        //                        }
        //                    }
        //                    Console.WriteLine("They don't match");
        //                }
        //                errorCount++;
        //            }

        //            if (verboseLevel >= 2)
        //            {
        //                Console.WriteLine();
        //            }
        //        }

        //        if (errorCount > 0 && fastFail)
        //        {
        //            return errorCount;
        //        }
        //    }

        //    if (verboseLevel >= 2)
        //    {
        //        Console.WriteLine("Errors: " + errorCount);
        //    }
        //    return errorCount;
        //}

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

        private static void GenerateCharStrings(int count = 35)
        {
            var chars = "! # $ % & ' ( ) * + - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ,".Replace(" ", "");
            var rand = new Random();
            using (var fout = new StreamWriter("charstrings.txt"))
            {
                var sb = new StringBuilder();
                foreach (var character in chars)
                {
                    //sb.Clear();
                    //foreach (var otherCharacter in chars)
                    //{
                    //    sb.Append(character);
                    //    sb.Append(otherCharacter);
                    //    sb.Append(' ');
                    //}
                    //fout.WriteLine(sb.ToString().Trim());
                    //sb.Append('.');
                    //sb.Append('[');
                    //sb.Append(character);
                    //sb.AppendLine();
                }
                //fout.WriteLine(sb.ToString() + " [");
                for (int i = 0; i < count; i++)
                {
                    sb.Clear();
                    foreach (var character in chars.OrderBy(x => rand.Next()))
                    {
                        sb.Append(character + " ");
                    }
                    Console.WriteLine(sb.ToString().Trim() + "[" + "\n");
                    fout.WriteLine(sb.ToString() + "[");
                }
            }
        }

        private static void SpaceTest(int count = 35)
        {
            var chars = "! # $ % & ' ( ) * + - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ,".Replace(" ", "");
            var rand = new Random();
            using (var fout = new StreamWriter("charstrings.txt"))
            {
                for (int i = 0; i < count; i++)
                {
                    var sb = new StringBuilder();
                    foreach (var character in chars.OrderBy(x => rand.Next()))
                    {
                        sb.Append("A" + character + "a" + character + " ");
                    }
                    Console.WriteLine(sb.ToString().Trim() + "[" + "\n");
                    fout.WriteLine(sb.ToString() + "[");
                }
            }
        }
    }
}
