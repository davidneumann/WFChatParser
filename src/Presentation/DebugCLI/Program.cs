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

            TrainOnImages();
            //MonitorChatLive();
            //var c = new ChatImageCleaner();
            //c.SaveGreyscaleImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png");
            //var res = c.ConvertScreenshotToChatTextWithBitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png");
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

        private static void TrainOnImages()
        {
            var trainingImagePaths =
                Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Training Inputs").Where(f => f.EndsWith(".png")).ToArray();
            var trainingTextPaths =
                Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Training Inputs").Where(f => f.EndsWith(".txt")).ToArray();

            var c = new ChatImageCleaner();
            var characters = new List<ChatImageCleaner.TrainingSampleCharacter>();
            for (int i = 0; i < trainingImagePaths.Length; i++)
            {
                var correctText = File.ReadAllLines(trainingTextPaths[i]).Select(line => line.Replace(" ", "").ToArray()).ToList();
                var results = c.TrainOnImage(trainingImagePaths[i], correctText, xOffset: 253);
                results.SelectMany(list => list).ToList().ForEach(t => characters.Add(t));
            }
            var groupedChars = characters.GroupBy(t => t.Character);
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

        private static int VerifyNoErrors(int verboseLevel = 0, bool fastFail = false, int xOffset = 4, float minV = 0.5f, int spaceWidth = 6)
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
                var c = new ChatImageCleaner();
                c.SaveGreyscaleImage(masterKeyFile, Path.Combine(outputDir, (new FileInfo(masterKeyFile)).Name), minV);
                var smallOffset = 183;
                var result = c.ConvertScreenshotToChatTextWithBitmap(masterKeyFile, minV: minV, spaceWidth: spaceWidth, xOffset: xOffset, smallText: false).ToArray();

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
                    if (!Enumerable.SequenceEqual(correctResults[i].Trim(), result[i].Trim()))
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
                Console.WriteLine("V: " + minV + " errors: " + errorCount);
            }
            return errorCount;
        }

        private static void AverageBitmapsSmall(float minV, bool smallText, float threshold)
        {
            var dirs = Directory.GetDirectories(Environment.CurrentDirectory);
            dirs = dirs.Where(d => (new DirectoryInfo(d)).Name.StartsWith("line_")).ToArray();
            var files = new Dictionary<string, List<string>>();
            foreach (var dir in dirs)
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    var name = (new FileInfo(file)).Name;
                    if (!files.ContainsKey(name))
                        files[name] = new List<string>();
                    files[name].Add(file);
                }
            }

            var c = new ChatImageCleaner();
            c.AverageBitmaps(files, minV, threshold, smallText);
        }

        private static void MakeBitmapsSmall(float minV, bool smallSizeText = true)
        {
            var c = new ChatImageCleaner();
            //c.SaveGreyscaleImage(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\test5.png",
            //    Path.Combine(outputDir, "test5.png"), minV: 0.35f);
            //c.FindLineCoords(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\test5.png", smallText: false);
            var trainingImages = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".png")).ToArray();
            var trainingText = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\").Where(f => f.EndsWith(".txt")).ToArray();
            var lineCount = smallSizeText ? 35 : 27;
            var smallOffset = 183;
            var folderOffset = 0;
            for (int i = 0; i < trainingImages.Length; i++)
            {
                var correctResults = File.ReadAllLines(trainingText[i]).Select(str => str.Replace(" ", "")).ToArray();
                for (int j = 0; j < lineCount; j++)
                {
                    c.MakeBitmapDictionary(trainingImages[i], correctResults[j], minV, 252, j, folderOffset, false);
                }
                folderOffset += correctResults.Length;
            }
        }

        private static void VerifyOCR()
        {
            var sw = new Stopwatch();
            sw.Start();
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>(File.ReadAllText("chars.json")));
            sw.Stop();
            Console.WriteLine("Initialize finished in: " + sw.Elapsed.TotalSeconds + "s");
            sw.Reset();
            Console.WriteLine("=Processing files=");
            var fileTimes = new List<double>();
            foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs"))
            {
                var fileInfo = new FileInfo(file);
                var name = file.Substring(file.LastIndexOf('\\') + 1);
                Console.WriteLine($"=={name}==");
                sw.Reset();
                sw.Start();
                p.SaveGreyscaleImage(file, Path.Combine(outputDir, fileInfo.Name), 0.32f);
                var chatText = p.ConvertScreenshotToChatText(file, 0.32f);
                sw.Stop();
                Console.WriteLine("Parsed in: " + sw.Elapsed.TotalSeconds + "s");
                sw.Reset();
                File.WriteAllLines(Path.Combine(outputDir, fileInfo.Name + ".txt"), chatText);

                var correctResults = File.ReadAllLines(file.Replace(".png", ".txt"));
                if (correctResults.Length != chatText.Length)
                    Console.WriteLine($"{fileInfo.Name} test failed");
                else
                {
                    var failed = false;
                    for (int i = 0; i < chatText.Length; i++)
                    {
                        Console.Write("Line " + i + 1 + ": ");
                        if (!Enumerable.SequenceEqual(chatText[i], correctResults[i]))
                        {
                            failed = true;
                            Console.WriteLine(" FAILED");
                        }
                        else
                            Console.WriteLine(" correct");
                    }
                    Console.Write($"File {fileInfo.Name} ");
                    if (failed)
                        Console.WriteLine("FAILED");
                    else
                        Console.WriteLine("PASSED");
                }
            }

            Console.WriteLine("Jobs done");
        }

        private static void MonitorChatLive(float minV = 0.5f, int spaceOffset = 8)
        {
            Console.WriteLine("Starting up game capture");
            _gameCapture = new DShowCapture(4096, 2160);
            //var c = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>("chars.json"));
            Console.WriteLine("Starting up image parser");
            var c = new ChatImageCleaner();
            //var t = new ImageParser();

            Console.WriteLine("Loading config for data sender");
            IConfiguration config = new ConfigurationBuilder()
              .AddJsonFile("appsettings.json", true, true)
              .AddJsonFile("appsettings.development.json", true, true)
              .AddJsonFile("appsettings.production.json", true, true)
              .Build();


            Console.WriteLine("Data sender connecting to: " + config["DataSender:HostName"]);
            var dataSender = new DataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"]);

            dataSender.RequestToKill += (s, e) =>
            {
                CleanUp();
                Environment.Exit(0);
            };
            dataSender.RequestSaveAll += (s, e) =>
            {
                for (int i = 6; i >= 0; i--)
                {
                    var dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves");
                    if (e.Name != null && e.Name.Length > 0)
                        dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves", e.Name);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var curFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + i + ".png");
                    var copyFile = Path.Combine(dir, "capture_" + i + ".png");
                    if (File.Exists(curFile))
                        File.Copy(curFile, copyFile, true);
                }
            };

            Console.WriteLine("Push enter and then switch to warframe");
            Console.ReadLine();
            for (int i = 0; i < 5; i++)
            {
                Console.Write($"\rStarting in {5 - i} seconds...");
                System.Threading.Thread.Sleep(1000);
            }

            string[] messageHistory = new string[100];
            var index = 0;
            var sw = new Stopwatch();
            var badNameRegex = new Regex("[^-A-Za-z0-9._]");
            while (true)
            {
                sw.Restart();
                for (int i = 6; i >= 0; i--)
                {
                    var curFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + i + ".png");
                    var lastFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + (i+1) + ".png");
                    if (File.Exists(lastFile))
                        File.Delete(lastFile);
                    if (File.Exists(curFile))
                        File.Move(curFile, lastFile);
                }
                var image = _gameCapture.GetTradeChatImage(Path.Combine(config["DEBUG:ImageDirectory"], "capture_0.png"));
                var imageTime = sw.Elapsed.TotalSeconds;
                sw.Restart();
                //var processedImagePath = c.ProcessChatImage(image, Environment.CurrentDirectory);
                var messages = c.ConvertScreenshotToChatTextWithBitmap(image);
                var parseTime = sw.Elapsed.TotalSeconds;
                sw.Restart();
                //var text = t.ParseChatImage(processedImagePath);
                var saveImage = false;
                if (!Directory.Exists(config["DEBUG:ImageDirectory"]))
                    Directory.CreateDirectory(config["DEBUG:ImageDirectory"]);
                var debugName = Path.Combine(config["DEBUG:ImageDirectory"], "debug_image_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-fff") + ".png");
                var newMessags = 0;
                foreach (var message in messages)
                {
                    if (!messageHistory.Contains(message))
                    {
                        newMessags++;
                        Console.Write($"\r{parseTime:N2}s: {message}");
                        dataSender.SendChatMessage(message);
                        messageHistory[index++] = message;
                        if (index >= messageHistory.Length)
                            index = 0;
                        var username = message.Substring(8);
                        username = username.Substring(0, username.IndexOf(":"));
                        if (username.Contains(" ") || username.Contains(@"\/") || username.Contains("]") || username.Contains("[") || badNameRegex.Match(username).Success)
                        {
                            dataSender.SendDebugMessage("Bad name: " + username + " see " + debugName);
                            saveImage = true;
                        }
                    }
                }
                if (saveImage)
                {
                    File.Copy(image, debugName);
                }
                var transmitTime = sw.Elapsed.TotalSeconds;
                sw.Stop();
                dataSender.SendTimers(imageTime, parseTime, transmitTime, newMessags);
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
                    sb.Append('.');
                    sb.Append('[');
                    sb.Append(character);
                    sb.AppendLine();
                }
                fout.WriteLine(sb.ToString() + "[");
                //for (int i = 0; i < count; i++)
                //{
                //    sb.Clear();
                //    foreach (var character in chars.OrderBy(x => rand.Next()))
                //    {
                //        sb.Append(character);
                //    }
                //    Console.WriteLine(sb.ToString().Trim() + "[" + "\n");
                //    fout.WriteLine(sb.ToString() + "[");
                //}
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

        private static void AnalyzeImages()
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
                    {
                        var info = new FileInfo(file);
                        File.WriteAllText(Path.Combine(outputDir, info.Name + ".json"), JsonConvert.SerializeObject(charInfoResults));
                        validVs.Add(v);
                    }
                }

                //var processedImagePath = p.AnalysisChatMessage(file, outputDir);
                //Console.WriteLine(processedImagePath);
            }
            Console.WriteLine("Jobs done");
        }

        private static void TrainOCRSmall()
        {
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>(File.ReadAllText("chars.json")));
            //p.FindLineCoordsSmall(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 23\Outputs\test3.png");
            Console.WriteLine("=Processing files=");

            var masterKeyFile = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs\test3.png";
            var correctResults = File.ReadAllLines(masterKeyFile.Replace(".png", ".txt")).Select(str => str.Replace(" ", "")).ToArray();

            var name = masterKeyFile.Substring(masterKeyFile.LastIndexOf('\\') + 1);
            Console.WriteLine($"=={name}==");

            var outputFile = Path.Combine(outputDir, name);

            //var chars = "! # $ % & ' ( ) * + , - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ ` a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ~ – — ’ ‚ [".Replace(" ", "");
            //var chars = @"- t ] ' ^ O z H M f w 6 i 5 @ d 9 2 y Y I F g j ( X v ; ` n _ e , & V 1 * W u U ) l B A c p ~ N 7 S 0 # J m T R | 8 s r q 4 a h , b P + \ { : L ? % - ! Q x D > < k $ o K C 3 - ' E G = } Z . / [".Replace(" ", "");
            //var chars = @"' i - 9 ' ~ ] , \ # q c o $ F m s V 1 y ? & Z = 3 } B n Y v A e M d r C f E P ) < O @ u 4 S + ; - k ` { z J , L T Q U w h . _ p 7 a 5 | I ^ ( W : H > 2 % K R / b l X x t j - g ! G D 8 * N 0 6 [".Replace(" ", "");
            //var chars = @"c M ) P ' h . R ! s e k ‚ ; : j o ( T Z > X B f \ @ + 6 z * L n - 1 w ^ i – x _ O 9 ] 7 — & v ’ ? / ` u D , { % Y = q 5 < A J r ~ $ p 8 S } t V b a C 4 H G N U Q l y # E W d I 3 m K | F 0 2 g [".Replace(" ", "");

            var bestV = 1f;
            var bestCount = 0;
            CharInfo[] bestCharInfo = null;

            for (float v = 0.35f; v > 0.0f; v -= 0.01f)
            {
                Console.WriteLine("Trying v: " + v);
                for (int i = 0; i < 35; i++)
                {
                    var charInfoResults = p.AnalyzeInputSmallFromScreenshot(masterKeyFile, correctResults[i], v, 183, i);
                    var newP = new ChatImageCleaner(charInfoResults);
                    newP.SaveGreyscaleImage(masterKeyFile, Path.Combine(outputDir, masterKeyFile.Substring(masterKeyFile.LastIndexOf('\\') + 1)), v);
                    var chatText = newP.ConvertScreenshotToChatText(masterKeyFile, v, 183);
                    if (chatText.All(str => str.Length == 0))
                        continue;
                    var rightCount = 0;
                    for (int j = 0; j < 35; j++)
                    {
                        if (chatText[j].Length == correctResults[j].Length && Enumerable.SequenceEqual(chatText[j], correctResults[j]))
                        {
                            rightCount++;
                        }
                    }
                    if (rightCount > bestCount)
                    {
                        bestCount = rightCount;
                        bestCharInfo = charInfoResults;
                        bestV = v;
                    }
                }
            }

            var info = new FileInfo(masterKeyFile);
            var path = Path.Combine(outputDir, info.Name + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(bestCharInfo));
            Console.WriteLine($"{info.Name} trained correctly. Output: {path}");
            Console.WriteLine("Best count: " + bestCharInfo);
            Console.WriteLine("best v: " + bestV);
            Debugger.Break();

            //p.SaveGreyscaleImage(masterKeyFile, outputFile, v);
            //var charInfoResults = p.AnalyzeInputSmall(masterKeyFile, chars, v, 183);
            //var newP = new ChatImageCleaner(charInfoResults);
            //var charResults = newP.VerifyInputSmall(masterKeyFile, v, 183);
            //if (!charResults.SequenceEqual(chars))
            //{
            //    continue;
            //}
            //else 
            //{
            //    if(bestCount == 0)
            //    {
            //        bestV = v;
            //        bestCount = 1;
            //    }
            //}
            //var allCorrect = true;
            //foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs"))
            //{
            //    if (!allCorrect)
            //        break;
            //    var fileInfo = new FileInfo(file);
            //    p.SaveGreyscaleImage(file, Path.Combine(outputDir, fileInfo.Name), v);
            //    var chatText = newP.ConvertScreenshotToChatText(file, v, 183);
            //    File.WriteAllLines(Path.Combine(outputDir, fileInfo.Name + ".txt"), chatText);


            //    if (correctResults.Length != chatText.Length)
            //    {
            //        allCorrect = false;
            //        break;
            //    }
            //    else
            //    {
            //        for (int i = 0; i < chatText.Length; i++)
            //        {
            //            Console.WriteLine(correctResults[i]);
            //            Console.WriteLine(chatText[i]);
            //            Console.WriteLine();
            //        }
            //        var countRight = 0;
            //        for (int i = 0; i < chatText.Length; i++)
            //        {
            //            if (!Enumerable.SequenceEqual(chatText[i], correctResults[i]))
            //            {
            //                allCorrect = false;
            //                break;
            //            }
            //        }
            //    }
            //}
            //if (allCorrect)
            //{
            //    Debugger.Break();
            //    var info = new FileInfo(masterKeyFile);
            //    var path = Path.Combine(outputDir, info.Name + ".json");
            //    File.WriteAllText(path, JsonConvert.SerializeObject(charInfoResults));
            //    Console.WriteLine($"{info.Name} trained correctly. Output: {path}");
            //    break;
            //}
            //}

            //var processedImagePath = p.AnalysisChatMessage(file, outputDir);
            //Console.WriteLine(processedImagePath);
            Console.WriteLine("Jobs done");
        }

        private static void TrainOCR()
        {
            var p = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>(File.ReadAllText("chars.json")));
            Console.WriteLine("=Processing files=");
            var masterKeyFile = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Text Inputs\line.png";

            var name = masterKeyFile.Substring(masterKeyFile.LastIndexOf('\\') + 1);
            Console.WriteLine($"=={name}==");

            var outputFile = Path.Combine(outputDir, name);

            //var chars = "! # $ % & ' ( ) * + , - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ ` a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ~ – — ’ ‚ [".Replace(" ", "");
            var chars = @"- t ] ' ^ O z H M f w 6 i 5 @ d 9 2 y Y I F g j ( X v ; ` n _ e , & V 1 * W u U ) l B A c p ~ N 7 S 0 # J m T R | 8 s r q 4 a h , b P + \ { : L ? % - ! Q x D > < k $ o K C 3 - ' E G = } Z . / [".Replace(" ", "");

            for (float v = 0.32f; v > 0.0f; v -= 0.05f)
            {
                p.SaveGreyscaleImage(masterKeyFile, outputFile, v);
                var charInfoResults = p.AnalyzeInput(masterKeyFile, chars, v, 248);
                var newP = new ChatImageCleaner(charInfoResults);
                var charResults = newP.VerifyInput(masterKeyFile, v);
                if (!charResults.SequenceEqual(chars))
                {
                    continue;
                }
                var allCorrect = true;
                foreach (var file in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\OCR Test Inputs"))
                {
                    if (!allCorrect)
                        break;
                    var fileInfo = new FileInfo(file);
                    p.SaveGreyscaleImage(file, Path.Combine(outputDir, fileInfo.Name), v);
                    var chatText = newP.ConvertScreenshotToChatText(file, v);
                    File.WriteAllLines(Path.Combine(outputDir, fileInfo.Name + ".txt"), chatText);

                    var correctResults = File.ReadAllLines(file.Replace(".png", ".txt"));
                    if (correctResults.Length != chatText.Length)
                    {
                        allCorrect = false;
                        break;
                    }
                    else
                    {
                        for (int i = 0; i < chatText.Length; i++)
                        {
                            if (!Enumerable.SequenceEqual(chatText[i], correctResults[i]))
                            {
                                allCorrect = false;
                                break;
                            }
                        }
                    }
                }
                if (allCorrect)
                {
                    Debugger.Break();
                    var info = new FileInfo(masterKeyFile);
                    var path = Path.Combine(outputDir, info.Name + ".json");
                    File.WriteAllText(path, JsonConvert.SerializeObject(charInfoResults));
                    Console.WriteLine($"{info.Name} trained correctly. Output: {path}");
                    break;
                }
            }

            //var processedImagePath = p.AnalysisChatMessage(file, outputDir);
            //Console.WriteLine(processedImagePath);
            Console.WriteLine("Jobs done");
        }
    }
}
