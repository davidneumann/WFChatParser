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
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using Application.LineParseResult;
using Application.Enums;

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
            //var t = Task.Run(() => MouseTests());
            //while(!t.IsCompleted)
            //{
            //    System.Threading.Thread.Sleep(1000);
            //}
            //if (t.IsFaulted)
            //    Console.WriteLine(t.Exception);

            //FixImages();
            //PrepareRivens();
            //VisualizeClickpoints();
            //TestScreenHandler();
            //TestRivenStuff();
            //SimulateParseRiven();
            VerifyNoErrors(2);
            //JsonMessagerHelper();
            //TrainOnImages();
            //var c = new ChatParser();
            //var cleaner = new ImageCleaner();
            //cleaner.SaveGreyscaleImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png", 0.44f);
            //var res = c.ParseChatImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png");
            //foreach (var line in res)
            //{
            //    if (line.Contains(":]"))
            //        Debugger.Break();
            //}
            //var v = 0.5f;
            }

        private static void VisualizeClickpoints()
        {
            var cp = new ChatParser();
            var r = cp.ParseChatImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs\error_1.png");
            var list = new CoordinateList();
            r.Where(r1 => r1 is ChatMessageLineResult).Cast<ChatMessageLineResult>().SelectMany(r1 => r1.ClickPoints).ToList().ForEach(p => list.Add(p.X, p.Y));
            var ic = new ImageCleaner();
            ic.SaveClickMarkers(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs\error_1.png",
                Path.Combine(outputDir, "error_1_clicks.png"),
                list);                
        }

        private static void TestScreenHandler()
        {
            var c = new GameCapture();
            var ss = new ScreenStateHandler();

            Bitmap b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chat.png");
            var isChat = ss.GetScreenState(b) == ScreenState.ChatWindow;
            Console.WriteLine("Is chat: " + isChat + " should be true");

            b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\riven.png");
            var isRiven = ss.GetScreenState(b) == ScreenState.RivenWindow;
            Console.WriteLine("Is riven: " + isRiven + " should be true");
            b.Dispose();
        }

        private static void TestRivenStuff()
        {
            var c = new GameCapture();
            var rp = new RivenParser();
            var ss = new ScreenStateHandler();

            var image = "test.png";
            var b = c.GetFullImage();
            b.Save("test.png");
            b.Dispose();

            var p = new ChatParser();
            var results = p.ParseChatImage(image, true, true).Where(r => r is ChatMessageLineResult).Cast<ChatMessageLineResult>();

            var clean = new ImageCleaner();
            var coords = new CoordinateList();
            results.SelectMany(r => r.ClickPoints).ToList().ForEach(i => coords.Add(i.X, i.Y));
            clean.SaveClickMarkers("test.png", "test_marked.png", coords);

            var mouse = new MouseHelper();

            var index = 0;
            var sw = new Stopwatch();
            foreach (var clr in results.Where(r => r is ChatMessageLineResult).Cast<ChatMessageLineResult>())
            {
                foreach (var click in clr.ClickPoints)
                {
                    b = c.GetFullImage();
                    if (ss.GetScreenState(b) == ScreenState.ChatWindow)
                    {
                        //Hover over riven
                        System.Threading.Thread.Sleep(17);
                        mouse.MoveTo(click.X, click.Y);

                        //Click riven
                        System.Threading.Thread.Sleep(17);
                        mouse.Click(click.X, click.Y);
                        System.Threading.Thread.Sleep(17);
                    }

                    //Move mouse out of the way
                    mouse.MoveTo(0, 0);
                    sw.Restart();
                    var tries = 0;
                    while (true)
                    {
                        try
                        {
                            var bitmap2 = c.GetFullImage();
                            if(ss.GetScreenState(bitmap2) == ScreenState.RivenWindow)
                            {
                                var crop = rp.CropToRiven(bitmap2);
                                crop.Save(index.ToString() + ".png");
                                crop.Dispose();
                                bitmap2.Dispose();
                                break;
                            }
                            bitmap2.Dispose();
                        }
                        catch { }
                        tries++;
                        if (tries > 15)
                        {
                            Console.WriteLine("Riven not detected! Abort!");
                            break;
                        }
                    }
                    Console.WriteLine("Got \"riven\" in " + sw.Elapsed.TotalSeconds + " seconds");

                    //Hover over exit
                    System.Threading.Thread.Sleep(33);
                    mouse.MoveTo(3816, 2013);

                    //Click exit
                    var bitmap = c.GetFullImage();
                    if (ss.GetScreenState(bitmap) == ScreenState.RivenWindow)
                    {
                        System.Threading.Thread.Sleep(17);
                        mouse.Click(3816, 2013);
                        System.Threading.Thread.Sleep(17);
                    }
                    bitmap.Dispose();

                    //Move mouse out of the way
                    System.Threading.Thread.Sleep(17);
                    mouse.MoveTo(0, 0);

                    System.Threading.Thread.Sleep(17);
                    index++;
                }
            }
            //c.Dispose();
        }
        private static void SimulateParseRiven()
        {
            var rc = new RivenCleaner();
            var rp = new RivenParser();
            //const string image = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\input.png";
            //var srcBitmap = new Bitmap(image);
            //var bitmap = srcBitmap.Clone(new Rectangle(1757, 463, 582, 831), PixelFormat.DontCare);
            const string imageWhite = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\b2ef63f6434cae7af57a011a1e6645c1_0.png";
            //bitmap.Save(imageWhite);
            //srcBitmap.Dispose();
            //bitmap.Dispose();
            //rc.CleanRiven(imageWhite);
            var riven = rp.ParseRivenImage(imageWhite);
        }

        private static void PrepareRivens()
        {
            var r = new RivenCleaner();
            var p = new RivenParser();

            var totalSw = new Stopwatch();
            var opSw = new Stopwatch();
            var rivens = new List<Riven>();
            foreach (var riven in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\").Where(f => !f.EndsWith("_white.png") && f.EndsWith(".png")))
            {
                Console.WriteLine("\n" + riven.Substring(riven.LastIndexOf("\\") + 1));
                totalSw.Restart();
                opSw.Restart();
                r.PrepareRivenFromFullscreenImage(riven, riven + "_white.png");
                Console.WriteLine("cleanup: " + opSw.Elapsed.TotalSeconds + " seconds");
                opSw.Restart();
                var result = p.ParseRivenImage(riven + "_white.png");
                rivens.Add(result);
                Console.WriteLine("Parsed: " + opSw.Elapsed.TotalSeconds + " seconds");
                opSw.Restart();
                Console.WriteLine(JsonConvert.SerializeObject(result));
                Console.WriteLine("Total: " + totalSw.Elapsed.TotalSeconds + " seconds");
            }

            Console.WriteLine("\n");
            Console.WriteLine(JsonConvert.SerializeObject(rivens));
        }

        private static void FixImages()
        {
            var cleaner = new ImageCleaner();
            cleaner.SaveChatColors(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png");
            var p = new ChatParser();
            var r = p.ParseChatImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png");
            foreach (var line in r)
            {
                Console.WriteLine(line.RawMessage);
            }
        }

        //private static async void MouseTests()
        //{
        //    System.Threading.Thread.Sleep(2000);
        //    var clicker = new MouseHelper();

        //    clicker.MoveTo(4, 768);
        //    //Scroll down for new page of messages
        //    for (int i = 0; i < 27; i++)
        //    {
        //        clicker.ScrollDown();
        //        System.Threading.Thread.Sleep(16);
        //    }
        //    clicker.ScrollUp();//Pause
        //    clicker.MoveTo(0, 0);

        //    var g = new DShowCapture(4096, 2160);
        //    System.Threading.Thread.Sleep(16);
        //    g.GetTradeChatImage("debug.png");
        //    var p = new ChatParser();
        //    var r = p.ParseChatImage("debug.png");
        //    var pos = r.SelectMany(l => l.ClickPoints).First();
        //    clicker.MoveTo(pos.X, pos.Y);
        //    clicker.Click(pos.X, pos.Y);

        //    //clicker.Scroll(1);
        //    //clicker.ScrollUp();
        //    //clicker.MoveCursorTo(0, 0);
        //    //System.Threading.Thread.Sleep(66);
        //    //clicker.MoveCursorTo(1920 / 2, 1080 / 2);
        //}

        //private static void JsonMessagerHelper()
        //{
        //    var r1 = new Riven()
        //    {
        //        Drain = 18,
        //        MasteryRank = 69,
        //        Rolls = 7,
        //        MessagePlacementId = 0,
        //        Modifiers = new string[] { "+50% to skill", "17% fire rate" },
        //        Polarity = Polarity.Madurai,
        //        Rank = 8,
        //        Name = "[Tonkor cri-shaboo]"
        //    };
        //    var r2 = new Riven()
        //    {
        //        Drain = 7,
        //        MessagePlacementId = 1,
        //        Modifiers = new string[] { "-100% damage", "+69% lens flare", "+12% particles" },
        //        Polarity = Polarity.Naramon,
        //        Rank = 0,
        //        MasteryRank = 7,
        //        Name = "[Lenz parti-maker]",
        //        Rolls = 100
        //    };
        //    var r3 = new Riven()
        //    {
        //        Drain = 10,
        //        MasteryRank = 5,
        //        Rolls = 20,
        //        MessagePlacementId = 2,
        //        Modifiers = new string[] { "+50% to skill", "17% fire rate", "-25% likeability" },
        //        Polarity = Polarity.VaZarin,
        //        Rank = 2,
        //        Name = "[Tonkor cri-shaboo]"
        //    };
        //    var m = new ChatMessageModel()
        //    {
        //        Timestamp = "[00:12]",
        //        Author = "joeRivenMan",
        //        Raw = "WTB ||| [Opticor Vandal] ||| WTS [Tonkor cri-shaboo] [[Lenz parti-maker] [Tonkor cri-shaboo] PMO",
        //        Rivens = new Riven[] { r1, r2, r3 },
        //        EnhancedMessage = "WTB ||| [Opticor Vandal] ||| WTS [0][Tonkor cri-shaboo] [1][Lenz parti-maker] [2][Tonkor cri-shaboo] PMO"
        //    };
        //    var json = JsonConvert.SerializeObject(m);
        //    Console.WriteLine(json);
        //}

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

        private static int VerifyNoErrors(int verboseLevel = 0, bool fastFail = false, int xOffset = 4)
        {
            var trainingImages = new List<string>();
            Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
            //Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
            var trainingText = new List<string>();
            Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs").Where(f => f.EndsWith(".txt")).ToList().ForEach(f => trainingText.Add(f));
            //Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith(".txt")).ToList().ForEach(f => trainingText.Add(f));
            //var trainingImages = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".png")).ToArray();
            //var trainingText = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".txt")).ToArray();
            //var trainingImages = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith("e1.png")).ToArray();
            //var trainingText = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith("e1.txt")).ToArray();

            var errorCount = 0;
            for (int k = 0; k < trainingImages.Count; k++)
            {
                var fileInfo = new FileInfo(trainingText[k]);
                Console.WriteLine($"=={fileInfo.Name}==");
                var masterKeyFile = trainingImages[k];
                var correctResults = File.ReadAllLines(trainingText[k]).Select(line => line.Trim()).ToArray();
                var c = new ChatParser();
                var cleaner = new ImageCleaner();
                cleaner.SaveChatColors(masterKeyFile, Path.Combine(outputDir, (new FileInfo(masterKeyFile)).Name));
                var sw = new Stopwatch();
                sw.Restart();
                var result = c.ParseChatImage(masterKeyFile, xOffset, false, false).Select(i => i.RawMessage.Trim()).ToArray();
                Console.WriteLine("Parsed in: " + sw.Elapsed.TotalSeconds + " seconds");
                sw.Stop();

                Console.WriteLine("Expected");
                Console.WriteLine("Recieved");
                Console.WriteLine();

                if (correctResults.Length != result.Length)
                {
                    errorCount += correctResults.Length;
                    return errorCount;
                }
                for (int i = 0; i < result.Length; i++)
                {
                    if (verboseLevel >= 1)
                    {
                        Console.WriteLine(correctResults[i]);
                        Console.WriteLine(result[i]);
                    }
                    if (verboseLevel >= 2)
                    {
                        if (Enumerable.SequenceEqual(correctResults[i], result[i]))
                        {
                            Console.WriteLine("They match!");
                        }
                    }
                    if (!String.Equals(correctResults[i].Trim(), result[i]))
                    {
                        if (verboseLevel >= 2)
                        {
                            if (correctResults[i].Length == result[i].Length)
                            {
                                for (int j = 0; j < correctResults[i].Length; j++)
                                {
                                    if (result[i][j] != correctResults[i][j])
                                    {
                                        Console.WriteLine("^");
                                        break;
                                    }
                                    else
                                    {
                                        Console.Write(" ");
                                    }
                                }
                            }
                            Console.WriteLine("They don't match");
                        }
                        errorCount++;
                    }

                    if (verboseLevel >= 2)
                    {
                        Console.WriteLine();
                    }
                }

                if (errorCount > 0 && fastFail)
                {
                    return errorCount;
                }
            }

            if (verboseLevel >= 2)
            {
                Console.WriteLine("Errors: " + errorCount);
            }
            return errorCount;
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
