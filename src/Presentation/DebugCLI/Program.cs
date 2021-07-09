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
using System.Collections.ObjectModel;
using Pastel;
using Application;
using AdysTech.CredentialManager;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using Application.Interfaces;
using System.Collections.Concurrent;
using static Application.ChatRivenBot;
using Application.LogParser;
using Application.Logger;
using Application.ChatBoxParsing.ChatLineExtractor;
using Application.ChatBoxParsing;
using Application.ChatBoxParsing.CustomChatParsing;
using Application.Data;
using System.Net;
using HtmlAgilityPack;
using Application.Utils;
using ImageOCR.ComplexRivenParser;
using Application.ChatLineExtractor;
using RelativeChatParser;
using System.Numerics;
using RelativeChatParser.Models;
using RelativeChatParser.Extraction;
using RelativeChatParser.Training;
using System.Reflection;
using WebSocketSharp;
using RelativeChatParser.Database;
using RelativeChatParser.Recognition;
using ImageMagick;
using Application.Actionables.ProfileBots.Models;
using Application.Actionables.ProfileBots;
using Application.Actionables.ChatBots;
using TesseractService.Parsers;
using TesseractService.Factories;
using Application.Models;
using System.Net.Sockets;

namespace DebugCLI
{
    class Program
    {
        static string outputDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Run 23\Outputs";

        private static DShowCapture _gameCapture;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            //if (!Directory.Exists(outputDir))
            //    Directory.CreateDirectory(outputDir);

            //Console.Write("Enter target name [ex, WFBot:Bot2]: ");
            //var target = Console.ReadLine();
            //CredentialShim(target);
            //FindErrorAgain();
            //TestRivenParsing();
            //VerifyNoErrors(2);
            //TestScreenHandler();
            //TestBot();
            //ParseChatImage();
            //TessShim();
            //NewRivenShim();
            //NewChatParsingShim();
            //ChatParsingShim();
            //ChatMovingShim();
            //ParseRivenImage();
            //ChatLineExtractorShim();
            //GenerateCharStrings();
            //TrainOnImages();
            //FindOverlappingLines();
            //TrainSpacesOnImages();
            //ChineseChatShim();
            //ModiDescrShim();
            //GlyphAudit();
            //TestRivens();
            //ComplexRivenShim();
            //GroupShim();
            //NewDataSenderShim();
            //SaveSoftMask();
            //FindOverlappingLines();
            //SaveAllPixelGroups();
            //NewTrainingVerifier();
            //CornerGlyphShim();
            //ParseImageTest();
            //GetCrednetials();

            //LineExtractorTest();

            //RelativeParserWithSpacesShim();

            //GenerateRelativeParserCharacterTrainingData();
            //RelativeParserGlyphTrainer();
            //RelativeParserSpaceTrainer();
            //OverlapExtractingShim();
            //RelativeParserTest();

            //RelativeCacheShim();


            //OverlappGrouperShim();

            //ProfileShim();

            //PostLoginFix();

            //MakeDatFiles();

            //RustServerShim();
            //TestGlyphExtraction();

            DebugNewChat();
        }

        private static void DebugNewChat()
        {
            var filename = "Screenshot (1).png";
            using var b = new Bitmap(filename);
            var ssh = new ScreenStateHandler();
            var state = ssh.GetScreenState(b);
            Console.WriteLine($"{filename} screen state: {state}");
            var rp = new RelativePixelParser(new DummyLogger(), new DummySender());
            Console.WriteLine($"{filename} is chat focused: {rp.IsChatFocused(b)}");
            Console.WriteLine($"{filename} is chat open: {ssh.IsChatOpen(b)}");
            Console.WriteLine($"{filename} is scroll bar present: { rp.IsScrollbarPresent(b)}");
            var results = rp.ParseChatImage(b, false, true, 27);
            Console.WriteLine($"Parsed {results.Length} lines.");
            foreach (var result in results)
            {
                Console.WriteLine($"{result.RawMessage}");
            }
        }

        private static void TestGlyphExtraction()
        {
            var allOffsets = RustRayRecognizer.Extraction.LineScanner.LineOffsets.Select(i => int.MaxValue).ToArray();
            foreach (var input in Directory.GetFiles(Path.Combine("inputs", "character_training")).Select(f => new FileInfo(f)).Where(f => f.Name.StartsWith("characters_") && f.Name.EndsWith(".png")))
            {
                var ic = new ImageCache(new Bitmap(input.FullName));
                var lineOffsets = RustRayRecognizer.Extraction.LineScanner.ExtractLineOffsets(ic);
                if (lineOffsets.Length != RustRayRecognizer.Extraction.LineScanner.LineOffsets.Length)
                {
                    Console.WriteLine($"Line offsets count is not what is expected. Skipping {input.Name}.");
                    continue;
                }
                else
                {
                    for (int i = 0; i < lineOffsets.Length; i++)
                    {
                        allOffsets[i] = Math.Min(lineOffsets[i], allOffsets[i]);
                    }
                }
            }
            for (int i = 0; i < Math.Min(allOffsets.Length, RustRayRecognizer.Extraction.LineScanner.LineOffsets.Length); i++)
            {
                if (allOffsets[i] != RustRayRecognizer.Extraction.LineScanner.LineOffsets[i])
                {
                    //using (var b = new Bitmap(RustRayRecognizer.Extraction.LineScanner.ChatWidth, RustRayRecognizer.Extraction.LineScanner.Lineheight))
                    //{
                    //    for (int x = 0; x < b.Width; x++)
                    //    {
                    //        for (int y = 0; y < b.Height; y++)
                    //        {
                    //            var refX = RustRayRecognizer.Extraction.LineScanner.ChatLeftX + x;
                    //            var refY = allOffsets[i] + y;
                    //            if (ic[refX, refY] > 0)
                    //                b.SetPixel(x, y, Color.White);
                    //            else
                    //                b.SetPixel(x, y, Color.Black);
                    //        }
                    //    }
                    //    b.Save("debug_line.png");
                    //}
                    Console.WriteLine($"Line {i} does not have the expected offset. Found {allOffsets[i]}, expected {RustRayRecognizer.Extraction.LineScanner.LineOffsets[i]}");
                }
            }
            RustRayRecognizer.Extraction.LineScanner.LineOffsets = allOffsets;
        }

        private static void RustServerShim()
        {
            Console.WriteLine($"cd {Environment.CurrentDirectory}");
            var client = new TcpClient("127.0.0.1", 3333);
            using var stream = client.GetStream();
            using var fout = new BinaryWriter(stream);
            using var fin = new BinaryReader(stream);

            var inputDir = Path.Combine("inputs", "character_training");

            var allFiles = Directory.GetFiles(inputDir);
            var inputs = allFiles.Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct();
            var error = false;
            foreach (var input in inputs)
            {
                var pic = input + ".png";
                var txt = input + ".txt";
                if (!allFiles.Contains(pic))
                {
                    Console.WriteLine($"Missing picture for {input}");
                    error = true;
                }
                if (!allFiles.Contains(txt))
                {
                    Console.WriteLine($"Missing text for {input}");
                    error = true;
                }
            }
            if (error)
                return;

            var glyphDict = TrainingDataExtractor.ExtractGlyphs(inputs.Select(input => new TrainingInput(input + ".png", input + ".txt")))
                .ToDictionary(kvp => ((int)kvp.Key).ToString(), kvp => kvp.Value);
            foreach(var pair in glyphDict) {
                Console.WriteLine($"Sending {pair.Value.Count} glyphs to server");
                fout.Write((ushort)pair.Value.Count);


                foreach (var glyph in pair.Value)
                {
                    var arr = new bool[glyph.Width, glyph.Height];
                    var startX = glyph.Width - 1;
                    var endX = 0;
                    var startY = glyph.Height - 1;
                    var endY = 0;
                    for (int y = 0; y < glyph.Height; y++)
                    {
                        for (int x = 0; x < glyph.Width; x++)
                        {
                            arr[x, y] = glyph.RelativeBrights.Any(p => p.X == x && p.Y == y);
                            if (arr[x, y])
                            {
                                startX = Math.Min(startX, x);
                                endX = Math.Max(endX, x);
                                startY = Math.Min(startY, y);
                                endY = Math.Max(endY, y);
                            }
                        }
                    }

                    var width = endX - startX + 1;
                    int height = endY - startY + 1;
                    var trimmedArr = new bool[width, height];
                    for (int x = startX; x <= endX; x++)
                    {
                        for (int y = startY; y <= endY; y++)
                        {
                            trimmedArr[x - startX, y - startY] = arr[x, y];
                        }
                    }
                    // if (width != glyph.Width || height != glyph.Height)
                    //     System.Diagnostics.Debugger.Break();

                    //glyph.Save(debug, false);
                    using (var b = new Bitmap(width, height))
                    {
                        fout.Write((ushort)width);
                        fout.Write((byte)height);
                        fout.Write((byte)glyph.PixelsFromTopOfLine);

                        var packedBytesLen = height * width / 8;
                        if (height * width % 8 != 0)
                            packedBytesLen++;
                        var packedBytes = new byte[packedBytesLen];
                        for (int i = 0; i < packedBytesLen; i++)
                        {
                            for (int j = 0; j < 8; j++)
                            {
                                var x = (i * 8 + j) % width;
                                var y = (i * 8 + j) / width;
                                if (y >= height)
                                    break;
                                if (trimmedArr[x, y])
                                    packedBytes[i] |= (byte)(1 << (7 - j));
                            }
                        }
                        fout.Write(packedBytes);
                        //for (int y = 0; y < height; y++)
                        //{
                        //    for (int x = 0; x < width; x++)
                        //    {
                        //        fout.Write(trimmedArr[x, y]);
                        //        if (trimmedArr[x, y])
                        //            b.SetPixel(x, y, Color.White);
                        //        else
                        //            b.SetPixel(x, y, Color.Black);
                        //    }
                        //}
                    }
                }
                break;
            }
            fout.Flush();
            var response_count = fin.ReadUInt16();
            Console.WriteLine($"Response count: {response_count}");
            for(int i = 0; i < response_count; i++){
                Console.Write($"{fin.ReadChar()}");
            }
            Console.WriteLine();
        }

        private static void MakeDatFiles()
        {
            if (Directory.Exists("dats"))
                Directory.Delete("dats", true);
            // ImageCache.MinV = GlyphDatabase.BrightMinV;
            GlyphExtractor.distanceThreshold = 3;
            var inputDir = Path.Combine("inputs", "character_training");

            var allFiles = Directory.GetFiles(inputDir);
            var inputs = allFiles.Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct();
            var error = false;
            foreach (var input in inputs)
            {
                var pic = input + ".png";
                var txt = input + ".txt";
                if (!allFiles.Contains(pic))
                {
                    Console.WriteLine($"Missing picture for {input}");
                    error = true;
                }
                if (!allFiles.Contains(txt))
                {
                    Console.WriteLine($"Missing text for {input}");
                    error = true;
                }
            }
            if (error)
                return;

            var glyphDict = TrainingDataExtractor.ExtractGlyphs(inputs.Select(input => new TrainingInput(input + ".png", input + ".txt")))
                .ToDictionary(kvp => ((int)kvp.Key).ToString(), kvp => kvp.Value);

            //Overlap stuff
            //const int overlapExtraThreshold = 2;
            //GlyphExtractor.distanceThreshold += overlapExtraThreshold;
            GlyphExtractor.distanceThreshold += 1;
            RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs.RemoveAll(g => g.IsOverlap == true);
            RelativeChatParser.Database.GlyphDatabase.Instance.Init();

            Console.WriteLine("Looking for overlaps");
            foreach (var item in Directory.GetFiles(Path.Combine("inputs", "overlaps")).Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct())
            {
                Console.WriteLine($"={item}=");
                var text = new FileInfo(item + ".txt");
                var image = new FileInfo(item + ".png");
                if (!text.Exists || !image.Exists)
                {
                    Console.WriteLine($"Missing text {text.Exists}. Missing image {image.Exists}.");
                    throw new Exception("File missing");
                }

                using (var b = new Bitmap(image.FullName))
                {
                    var iCache = new ImageCache(b);
                    var expectedChars = File.ReadAllLines(text.FullName).Select(l => l.Replace(" ", "")).ToArray();
                    //var le = LineScanner.ExtractGlyphsFromLine(iCache,
                    for (int line = 0; line < LineScanner.LineOffsets.Length; line++)
                    {
                        var charIndex = 0;
                        var startX = LineScanner.ChatWidth;
                        var endX = LineScanner.ChatWidth;
                        for (int x = LineScanner.ChatLeftX; x < LineScanner.ChatWidth; x++)
                        {
                            for (int y = 0; y < LineScanner.Lineheight; y++)
                            {
                                if(iCache[x, LineScanner.LineOffsets[line] + y] > 0)
                                {
                                    startX = Math.Min(startX, x);
                                    endX = x;
                                }
                            }
                            if(x > endX + 21)
                            {
                                //New end of overlap detected
                                Console.WriteLine($"New glyph block detected {LineScanner.LineOffsets[line]} {startX},{endX - startX + 1}");

                                var rect = new Rectangle(startX, LineScanner.LineOffsets[line], endX-startX+1, LineScanner.Lineheight);
                                var extractedGlyph = LineScanner.ExtractGlyphsFromLine(iCache, rect);
                                if(extractedGlyph.Length != 2)
                                {
                                    //We have detected an overlap
                                    Console.WriteLine("Overlap detected!");
                                    var key = $"{(int)expectedChars[line][charIndex]}_{(int)expectedChars[line][charIndex+1]}";
                                    glyphDict[key] = new List<ExtractedGlyph>(extractedGlyph);
                                }
                                startX = LineScanner.ChatWidth;
                                endX = LineScanner.ChatWidth;
                                charIndex += 2;
                            }
                        }
                    }
                }
                // var overlaps = OverlapExtractor.GetOverlapingGlyphs(text.FullName, image.FullName);
                // foreach (var overlap in overlaps)
                // {
                //     var key = String.Join('_', overlap.ExpectedCharacters.ToCharArray().Select(c => (int)c));
                //     glyphDict[key] = new List<ExtractedGlyph>(new[] { overlap.Extracted });
                // }
                // //glyphDict[(char)0].AddRange(overlaps.Select(o => o.Extracted));
            }






            foreach (var pair in glyphDict)
            {
                // if(pair.Key == (char)0)
                // {
                //     System.Diagnostics.Debugger.Break();
                // }
                var count = 0;
                foreach (var glyph in pair.Value)
                {
                    var outputFile = new FileInfo(Path.Combine("dats", pair.Key, $"{count++}.dat"));
                    if (pair.Key.Contains("_"))
                    {
                        outputFile = new FileInfo(Path.Combine("dats", "overlaps", $"{pair.Key}.dat"));
                    }
                    if (!outputFile.Directory.Exists)
                        Directory.CreateDirectory(outputFile.DirectoryName);

                    var arr = new bool[glyph.Width, glyph.Height];
                    var startX = glyph.Width - 1;
                    var endX = 0;
                    var startY = glyph.Height - 1;
                    var endY = 0;
                    for (int y = 0; y < glyph.Height; y++)
                    {
                        for (int x = 0; x < glyph.Width; x++)
                        {
                            arr[x, y] = glyph.RelativeBrights.Any(p => p.X == x && p.Y == y);
                            if (arr[x, y])
                            {
                                startX = Math.Min(startX, x);
                                endX = Math.Max(endX, x);
                                startY = Math.Min(startY, y);
                                endY = Math.Max(endY, y);
                            }
                        }
                    }

                    var width = endX - startX + 1;
                    int height = endY - startY + 1;
                    var trimmedArr = new bool[width, height];
                    for (int x = startX; x <= endX; x++)
                    {
                        for (int y = startY; y <= endY; y++)
                        {
                            trimmedArr[x - startX, y - startY] = arr[x, y];
                        }
                    }
                    // if (width != glyph.Width || height != glyph.Height)
                    //     System.Diagnostics.Debugger.Break();

                    var debug = outputFile.FullName.Replace(".dat", ".png");
                    //glyph.Save(debug, false);
                    using (var b = new Bitmap(width, height))
                    {
                        using (var fout = new BinaryWriter(outputFile.Open(FileMode.Create, FileAccess.Write)))
                        {
                            fout.Write((ushort)width);
                            fout.Write((byte)height);
                            fout.Write((byte)glyph.PixelsFromTopOfLine);

                            var packedBytesLen = height * width / 8;
                            if (height * width % 8 != 0)
                                packedBytesLen++;
                            var packedBytes = new byte[packedBytesLen];
                            for (int i = 0; i < packedBytesLen; i++)
                            {
                                for (int j = 0; j < 8; j++)
                                {
                                    var x = (i * 8 + j) % width;
                                    var y = (i * 8 + j) / width;
                                    if (y >= height)
                                        break;
                                    if (trimmedArr[x, y])
                                        packedBytes[i] |= (byte)(1 << (7 - j));
                                }
                            }
                            fout.Write(packedBytes);
                            //for (int y = 0; y < height; y++)
                            //{
                            //    for (int x = 0; x < width; x++)
                            //    {
                            //        fout.Write(trimmedArr[x, y]);
                            //        if (trimmedArr[x, y])
                            //            b.SetPixel(x, y, Color.White);
                            //        else
                            //            b.SetPixel(x, y, Color.Black);
                            //    }
                            //}

                        }
                        //b.Save(debug);
                    }
                }
            }

            //Benchmarking
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var glyph_count = 0;
            Parallel.ForEach(glyphDict, item => {
            //foreach (var item in glyphDict) {
                //Console.WriteLine($"Recognizing glyphs for char {item.Key}");
                if(item.Key.Contains("_"))
                    //continue;
                    return;
                glyph_count += item.Value.Count;
                Parallel.ForEach(item.Value, g =>RelativePixelGlyphIdentifier.IdentifyGlyph(g));
                //item.Value.ForEach(g => RelativePixelGlyphIdentifier.IdentifyGlyph(g));
            });
            Console.WriteLine($"Recognized {glyph_count} glyphs. Took: {sw.Elapsed.TotalSeconds}s");
        }

        private class RewardInfo
        {
            public List<Point> BrightPixels = new List<Point>();
            public List<Point> DarkPixels = new List<Point>();
        }

        private static double Distance(Point p1, Point p2)
        {
            return Math.Round(Math.Sqrt(Math.Pow((p2.X - p1.X), 2) + Math.Pow((p2.Y - p1.Y), 2)), 1);
        }

        private static void PostLoginFix()
        {
            //var infos = new List<RewardInfo>();
            //foreach (var b in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\bad_rewards\").Select(f => new Bitmap(f)))
            //{
            //    var info = new RewardInfo();
            //    //2591,325 686x83
            //    for (int x = 2591; x < 2591+686; x++)
            //    {
            //        for (int y = 325; y < 325 + 83; y++)
            //        {
            //            if (b.GetPixel(x, y).ToHsv().Value >= 0.5f)
            //                info.BrightPixels.Add(new Point(x, y));
            //            else
            //                info.DarkPixels.Add(new Point(x, y));
            //        }
            //    }
            //    infos.Add(info);
            //}

            //var bestBrights = new List<Point>();
            //var bestDarks = new List<Point>();
            //var baseI = infos.First();
            //var targetI = infos.Last();
            //foreach (var bright in baseI.BrightPixels)
            //{
            //    //MAke sure the other image contains this bright
            //    if(targetI.BrightPixels.Contains(bright))
            //    {
            //        //Verify that the distance is at least 4 from any dark
            //        var farDarks = baseI.DarkPixels.All(p => Distance(p, bright) > 4) && targetI.DarkPixels.All(p => Distance(p, bright) > 4);
            //        //Verify that the distance to any existing best bright is greater than 4
            //        var farExisting = bestBrights.All(p => Distance(p, bright) > 4);
            //        if (farDarks && farExisting)
            //            bestBrights.Add(bright);
            //    }
            //}
            //foreach (var dark in baseI.DarkPixels)
            //{
            //    //MAke sure the other image contains this dark
            //    if (targetI.DarkPixels.Contains(dark))
            //    {
            //        //Verify that the distance is at least 4 from any bright
            //        var farBrights = baseI.BrightPixels.All(p => Distance(p, dark) > 4) && targetI.BrightPixels.All(p => Distance(p, dark) > 4);
            //        //Verify that the distance to any existing best bright is greater than 4
            //        var farExisting = bestDarks.All(p => Distance(p, dark) > 4);
            //        if (farBrights && farExisting)
            //            bestDarks.Add(dark);
            //    }
            //}

            //Console.Write("var lightPixels = new Point[] { ");
            //foreach (var bright in bestBrights)
            //{
            //    Console.Write($"new Point({bright.X}, {bright.Y}), ");
            //}
            //Console.Write("};\n\n");


            //Console.Write("var darkPixels = new Point[] { ");
            //foreach (var dark in bestDarks.Take(bestBrights.Count))
            //{
            //    Console.Write($"new Point({dark.X}, {dark.Y}), ");
            //}
            //Console.Write("};");

            var path = @"C:\Users\david\OneDrive\Documents\WFChatParser\132569636989417845.png";
            using (var b = new Bitmap(path))
            {
                var ssh = new ScreenStateHandler();
                var debug = ssh.GetScreenState(b);
            }
        }

        private static void ProfileShim()
        {
            IConfiguration config = new ConfigurationBuilder()
              //.AddJsonFile("appsettings.json", true, true)
              //.AddJsonFile("appsettings.development.json", true, true)
              .AddJsonFile("appsettings.production.json", true, true)
              .Build();

            var _dataSender = new ClientWebsocketDataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"],
                config["DataSender:LogMessagePrefix"],
                config["DataSender:LogLineMessagePrefix"]);

            var logger = new DummyLogger(true);
            _dataSender._logger = logger;
            var _ = Task.Run(_dataSender.ConnectAsync);

            var cT = new CancellationTokenSource();
            var fileCreds = File.ReadAllLines("creds.txt");
            var startInfo = new ProcessStartInfo();
            startInfo.UserName = fileCreds[2];
            var password = fileCreds[3];
            System.Security.SecureString ssPwd = new System.Security.SecureString();
            for (int x = 0; x < password.Length; x++)
            {
                ssPwd.AppendChar(password[x]);
            }
            startInfo.Password = ssPwd;
            var info = new FileInfo(@"C:\Program Files (x86)\Steam\steamapps\common\Warframe\Tools\Launcher.exe");
            startInfo.FileName = info.FullName;
            startInfo.Arguments = @"-cluster:public -registry:Steam";
            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = info.Directory.FullName;
            var creds = new WarframeClientInformation()
            {
                Username = fileCreds[0],
                Password = fileCreds[1],
                Region = "debugRegion",
                StartInfo = startInfo
            };
            var bot = new ProfileBot(cT.Token, creds, new MouseHelper(), new KeyboardHelper(), new ScreenStateHandler(), logger, new GameCapture(logger), _dataSender, new LineParserFactory());
            //bot.AddProfileRequest(new ProfileRequest("DavidRivenBot","", ""));
            //bot.AddProfileRequest(new ProfileRequest("magnus","", ""));
            //bot.AddProfileRequest(new ProfileRequest("ayeigui","", ""));
            //bot.AddProfileRequest(new ProfileRequest("gigapatches","", ""));
            //bot.AddProfileRequest(new ProfileRequest("semlar","", ""));
            //bot.AddProfileRequest(new ProfileRequest("unreality101","", ""));

            while (true)
            {
                if (bot.IsRequestingControl)
                    bot.TakeControl().Wait();
                else
                    Thread.Sleep(100);
            }
        }

        private static void OverlappGrouperShim()
        {
            var inputs = Directory.GetFiles(@"overlaps");//.Take(2000).ToArray();
            var list = new List<(int, int)>();
            var totalCount = inputs.Length;
            var count = 0;

            //var fuzzies = new Dictionary<FuzzyGlyph, string>();
            //var history = new Dictionary<FuzzyGlyph, List<ExtractedGlyph>>();
            //var groups = new List<(FuzzyGlyph, List<ExtractedGlyph>)>(inputs.Length);
            var superFastHistory = new Dictionary<FuzzyGlyph, List<ExtractedGlyph>>();
            foreach (var input in inputs)
            {
                count++;
                if (!input.EndsWith(".png"))
                {
                    var y = Console.CursorTop;
                    Console.CursorTop++;
                    Console.CursorLeft = 0;
                    Console.WriteLine($"Unexpected file {input}");
                    Console.CursorTop = y;
                    continue;
                }

                Console.Write($"\r{count} of {totalCount}: {input}");

                using (var b = new Bitmap(input))
                {
                    var ic = new ImageCache(b);
                    list.Add((b.Width, b.Height));

                    var pixels = new List<Point3>();
                    var empies = new List<System.Drawing.Point>();
                    var combined = new List<System.Drawing.Point>();
                    for (int x = 0; x < b.Width; x++)
                    {
                        for (int y = 0; y < b.Height; y++)
                        {
                            var v = ic[x, y];
                            var p = new System.Drawing.Point(x, y);
                            if (v > 0)
                            {
                                pixels.Add(new Point3(x, y, v));
                            }
                            else
                                empies.Add(p);
                            combined.Add(p);
                        }
                    }
                    var fuzzy = new FuzzyGlyph()
                    {
                        AspectRatio = b.Width / (float)b.Height,
                        Character = input,
                        IsOverlap = false,
                        ReferenceGapFromLineTop = 0,
                        ReferenceMaxHeight = b.Height + 2,
                        ReferenceMaxWidth = b.Width + 2,
                        ReferenceMinHeight = b.Height - 2,
                        ReferenceMinWidth = b.Width - 2,
                        RelativeCombinedLocations = combined.ToArray(),
                        RelativeEmptyLocations = empies.ToArray(),
                        RelativePixelLocations = pixels.ToArray()
                    };
                    //fuzzies.Add(fuzzy, input);


                    var extracted = new ExtractedGlyph()
                    {
                        AspectRatio = fuzzy.AspectRatio,
                        PixelsFromTopOfLine = 0,
                        RelativeBrights = fuzzy.RelativeBrights,
                        RelativeEmptyLocations = fuzzy.RelativeEmptyLocations,
                        RelativePixelLocations = fuzzy.RelativePixelLocations,
                        CombinedLocations = fuzzy.RelativeCombinedLocations,
                        Width = b.Width,
                        Height = b.Height
                    };
                    //history[fuzzy] = new List<ExtractedGlyph>();
                    //history[fuzzy].Add(extracted);

                    //groups.Add((fuzzy, new List<ExtractedGlyph>(new[] { extracted })));

                    var didMatch = false;
                    foreach (var superFast in superFastHistory)
                    {
                        var score = RelativePixelGlyphIdentifier.ScoreCandidate(extracted, false, superFast.Key);
                        if (score < RelativePixelGlyphIdentifier.MissedDistancePenalty)
                        {
                            superFast.Value.Add(extracted);
                            var files = superFast.Key.Character + "\n" + fuzzy.Character;
                            var newGlyph = GlyphTrainer.CombineExtractedGlyphs(' ', superFast.Value);
                            newGlyph.RelativeCombinedLocations = newGlyph.RelativePixelLocations.Select(p => new Point(p.X, p.Y)).Union(newGlyph.RelativeEmptyLocations).ToArray();
                            newGlyph.Character = files;
                            superFastHistory.Remove(superFast.Key);
                            superFastHistory[newGlyph] = superFast.Value;
                            didMatch = true;
                            break;
                        }
                    }
                    if (!didMatch)
                        superFastHistory.Add(fuzzy, new List<ExtractedGlyph>(new[] { extracted }));
                }
            }

            //var uniques = new HashSet<(int, int)>(list);
            //var counts = new Dictionary<(int, int), int>();
            //foreach (var unique in uniques)
            //{
            //    counts[unique] = list.Where(i => i == unique).Count();
            //}

            //var sorted = counts.OrderByDescending(i => i.Value).Select(i => ($"({i.Key.Item1},{i.Key.Item2})", i.Value.ToString()));

            //Console.WriteLine("\n");
            //var resultLines = new List<string>();
            //var maxValueWidth = sorted.Max(i => i.Item2.Length);
            //var maxDimenWidth = sorted.Max(i => i.Item1.Length);
            //var combinedWidth = maxDimenWidth + 2 + maxValueWidth + 3;
            //foreach (var item in sorted)
            //{
            //    resultLines.Add($"{item.Item1.PadLeft(maxDimenWidth, ' ')}: {item.Item2.PadLeft(maxValueWidth, ' ')}");
            //}

            //var topOfTable = Console.CursorTop;
            //var columns = Console.BufferWidth / combinedWidth;
            //var rows = (int)Math.Ceiling(sorted.Count() / (float)columns);
            //for (int x = 0; x < columns; x++)
            //{
            //    for (int y = 0; y < rows; y++)
            //    {
            //        var index = x * rows + y;
            //        if (index >= resultLines.Count)
            //            break;
            //        var str = resultLines[index] + " | ";
            //        Console.SetCursorPosition(combinedWidth * x, y + topOfTable);
            //        Console.Write(str);
            //    }
            //}
            //Console.SetCursorPosition(0, 4 + rows);

            //var newSW = new Stopwatch();
            //newSW.Start();
            //for (int x1 = 0; x1 < groups.Count; x1++)
            //{
            //    if (groups[x1].Item1 == null)
            //        continue;

            //    Console.WriteLine($"{x1} of {groups.Count}: Finding matches with {groups[x1].Item1.Character}");
            //    for (int x2 = x1+1; x2 < groups.Count; x2++)
            //    {
            //        if (groups[x2].Item1 == null)
            //            continue;

            //        Console.Write($"\rChecking against {groups[x2].Item1.Character}     ");
            //        Console.CursorLeft -= 5;
            //        var score = RelativePixelGlyphIdentifier.ScoreCandidate(groups[x2].Item2[0], false, groups[x1].Item1);
            //        if (score < RelativePixelGlyphIdentifier.MissedDistancePenalty)
            //        {
            //            Console.Write(" hit!");
            //            groups[x1].Item2.Add(groups[x2].Item2[0]);
            //            var files = groups[x1].Item1.Character + "\n" + groups[x2].Item1.Character;
            //            var newGlyph = GlyphTrainer.CombineExtractedGlyphs(' ', groups[x1].Item2);
            //            newGlyph.RelativeCombinedLocations = newGlyph.RelativePixelLocations.Select(p => new Point(p.X, p.Y)).Union(newGlyph.RelativeEmptyLocations).ToArray();
            //            newGlyph.Character = files;
            //            groups[x1] = (newGlyph, groups[x1].Item2);
            //            groups[x2] = (null, null);
            //        }
            //    }
            //    Console.WriteLine();
            //}
            //newSW.Stop();
            //Console.WriteLine($"Combined in {newSW.Elapsed.TotalSeconds}s.");

            //var db = GlyphDatabase.Instance;
            //db.AllGlyphs = fuzzies.Keys.ToList();
            //db.AllSpaces.Clear();
            //db.Init();
            //var sw = new Stopwatch();
            //sw.Start();

            //var oldCount = 0;
            //var iterationCount = 0;
            //var origCount = history.Count;
            //while (history.Count != oldCount)
            //{
            //    iterationCount++;
            //    oldCount = history.Count;

            //    var checkedCount = 0;
            //    var historyCount = history.Count;
            //    foreach (var glyph in history.Keys.ToArray())
            //    {
            //        Console.Write($"\r[{iterationCount,3}] {++checkedCount} of {historyCount}. Started with {origCount}");//: {fuzzies[glyph]}");
            //        if (!history.ContainsKey(glyph))
            //            continue;
            //        //db.AllGlyphs.Remove(glyph);
            //        db.DenyList.Add(glyph);
            //        //db.Init();

            //        var extracted = new ExtractedGlyph()
            //        {
            //            AspectRatio = glyph.AspectRatio,
            //            PixelsFromTopOfLine = 0,
            //            RelativeBrights = glyph.RelativeBrights,
            //            RelativeEmptyLocations = glyph.RelativeEmptyLocations,
            //            RelativePixelLocations = glyph.RelativePixelLocations,
            //            CombinedLocations = glyph.RelativeCombinedLocations,
            //            Width = (glyph.ReferenceMaxWidth + glyph.ReferenceMinWidth) / 2,
            //            Height = (glyph.ReferenceMaxHeight + glyph.ReferenceMinHeight) / 2
            //        };

            //        var match = RelativePixelGlyphIdentifier.IdentifyGlyph(extracted, false, fastMatch:true);
            //        //Console.WriteLine("Match found");
            //        if (match == null || match.Length != 1 || !history.ContainsKey(match[0]))
            //        {
            //            //db.AllGlyphs.Add(glyph);
            //            db.DenyList.Remove(glyph);
            //            //db.Init();
            //            continue;
            //        }

            //        var score = RelativePixelGlyphIdentifier.ScoreCandidate(extracted, false, match[0]);

            //        if (score < RelativePixelGlyphIdentifier.MissedDistancePenalty)
            //        {
            //            //db.AllGlyphs.Remove(match[0]);

            //            var oldInputs = history[match[0]];
            //            if (history.ContainsKey(glyph))
            //            {
            //                oldInputs.AddRange(history[glyph]);
            //                history.Remove(glyph);
            //            }
            //            var combined = GlyphTrainer.CombineExtractedGlyphs(' ', oldInputs);
            //            history.Remove(match[0]);
            //            db.DenyList.Add(match[0]);
            //            history[combined] = oldInputs;
            //            fuzzies[combined] = fuzzies[glyph] + "\n" + fuzzies[match[0]];
            //            db.AddGlyph(combined);
            //            //db.AllGlyphs = history.Keys.ToList();
            //            //db.Init();
            //        }
            //        else
            //        {
            //            //db.AllGlyphs.Add(glyph);
            //            db.DenyList.Remove(glyph);
            //            //db.Init();
            //        }
            //    }
            //}

            //sw.Stop();
            //Console.WriteLine($"\nFinished in {sw.Elapsed.TotalSeconds}s.");

            var dir = new DirectoryInfo("grouped");
            if (dir.Exists)
            {
                dir.Delete(true);
                Thread.Sleep(500);
            }
            dir.Create();
            Thread.Sleep(500);

            var db = new GlyphDatabase();
            //db.AllGlyphs.Clear();
            //db.AllGlyphs = history.Keys.ToList();
            db.AllGlyphs = superFastHistory.Keys.ToList();
            db.Init();
            File.WriteAllText(Path.Combine(dir.FullName, "groupsDB.json"), JsonConvert.SerializeObject(RelativeChatParser.Database.GlyphDatabase.Instance));

            //var groupCount = 0;
            //foreach (var item in history.Keys)
            //{
            //    var subDir = new DirectoryInfo(Path.Combine(dir.FullName, groupCount.ToString()));
            //    if (!subDir.Exists)
            //    {
            //        subDir.Create();
            //        Thread.Sleep(500);
            //    }

            //    var files = new HashSet<string>(fuzzies[item].Split('\n'));
            //    foreach (var file in files)
            //    {
            //        var fi = new FileInfo(file);
            //        File.Copy(fi.FullName, Path.Combine(subDir.FullName, fi.Name));
            //    }
            //    item.SaveVisualization(Path.Combine(subDir.FullName, "combined.png"), false);
            //    groupCount++;
            //}

            var groupCount = 0;
            foreach (var item in superFastHistory.Keys)
            {
                var subDir = new DirectoryInfo(Path.Combine(dir.FullName, groupCount.ToString()));
                if (!subDir.Exists)
                {
                    subDir.Create();
                    Thread.Sleep(500);
                }

                var files = new HashSet<string>(item.Character.Split('\n'));
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    File.Copy(fi.FullName, Path.Combine(subDir.FullName, fi.Name));
                }
                item.SaveVisualization(Path.Combine(subDir.FullName, "combined.png"), false);
                groupCount++;
            }
        }

        private static void RelativeCacheShim()
        {
            var cp = new RelativePixelParser(new DummyLogger(true), new DummySender());
            var sw = new Stopwatch();
            //const string input = @"bad_overlaps\637306367274895272.png";
            const string input = @"10d43853-5bcb-497b-bd56-2a32b4277a6e.png";
            Console.WriteLine($"Parsing {input}");
            ImageCleaner.SaveSoftMask(input, "debug_screen.png");
            using (var b = new Bitmap(input))
            {
                sw.Start();
                var lines = cp.ParseChatImage(b, true, true, 27);
                sw.Stop();
                Console.WriteLine($"Finished parsing in {sw.Elapsed.TotalSeconds}s.");
                //Console.WriteLine("Timestamp\tUsername");
                foreach (var line in lines)
                {
                    //if(line.Username != null && line.Timestamp != null)
                    //{
                    //    Console.WriteLine($"{line.Timestamp}\t{line.Username}");
                    //}
                    Console.WriteLine($"{line.Timestamp} {line.Username}: {line.EnhancedMessage}");
                }

                var lines2 = cp.ParseChatImage(b, true, true, 27);
            }
        }

        private static void GenerateRelativeParserCharacterTrainingData()
        {
            var chars = GetSupportedCharacters().Replace(" ", "").Trim();
            Console.WriteLine(chars + "\n\n");
            for (int i = 0; i < chars.Length; i++)
            {
                if (i % 27 == 0)
                    Console.WriteLine("\n");
                var line = chars.Substring(i, chars.Length - i) + chars.Substring(0, i);
                var realLine = string.Empty;
                for (int j = 0; j < line.Length; j++)
                {
                    realLine += line[j] + " ";
                }
                realLine += "[";
                Console.WriteLine(realLine);
            }
        }

        private static void RelativeParserWithSpacesShim()
        {
            var ignored = RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs;
            var rp = new RelativePixelParser(new DummyLogger(true), new DummySender());
            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\132378502626565768.png"))
            {
                var chatLines = rp.ParseChatImage(b, true, false, 27);
                foreach (var line in chatLines)
                {
                    //if(line.ClickPoints.Count > 0)
                    //{
                    //    Console.WriteLine(line.EnhancedMessage);
                    //    foreach (var cp in line.ClickPoints)
                    //    {
                    //        Console.WriteLine($"{cp.Index}:{cp.RivenName} {cp.X},{cp.Y}");
                    //    }
                    //}
                    Console.WriteLine(line);
                }
            }

        }

        private static void RelativeParserSpaceTrainer()
        {
            SpaceTrainer.TrainOnSpace(Path.Combine("inputs", "space_training"),
                "RelativeDB_with_spaces.json");
        }

        private static void OverlapExtractingShim()
        {
            const int overlapExtraThreshold = 2;
            GlyphExtractor.distanceThreshold += overlapExtraThreshold;
            RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs.RemoveAll(g => g.IsOverlap == true);
            RelativeChatParser.Database.GlyphDatabase.Instance.Init();
            const string overlapDir = "overlaps";
            if (Directory.Exists(overlapDir))
            {
                Directory.Delete(overlapDir, true);
                Thread.Sleep(1000);
            }
            Directory.CreateDirectory(overlapDir);

            var overlapCount = 0;
            var overlappingGlyphs = new List<FuzzyGlyph>();
            Console.WriteLine("Looking for overlaps");
            foreach (var item in Directory.GetFiles(Path.Combine("inputs", "overlaps")).Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct())
            {
                Console.WriteLine($"={item}=");
                var text = new FileInfo(item + ".txt");
                var image = new FileInfo(item + ".png");
                if (!text.Exists || !image.Exists)
                {
                    Console.WriteLine($"Missing text {text.Exists}. Missing image {image.Exists}.");
                    throw new Exception("File missing");
                }

                var overlaps = OverlapExtractor.GetOverlapingGlyphs(text.FullName, image.FullName);
                var expectedLines = File.ReadAllLines(text.FullName);
                var charI = 0;
                using (var b = new Bitmap(image.FullName))
                {
                    foreach (var overlap in overlaps)
                    {
                        var str = overlap.IdentifiedGlyphs.Aggregate("", (acc, glyph) => acc + glyph.Character);
                        ExtractedGlyph extracted = overlap.Extracted;
                        Console.WriteLine($"Saving overlap {str}/{overlap.ExpectedCharacters} from {extracted.Left},{extracted.Top} {extracted.Width}x{extracted.Height}.");
                        using (var output = new Bitmap(extracted.Width, LineScanner.Lineheight))
                        {
                            for (int x = 0; x < output.Width; x++)
                            {
                                for (int y = 0; y < output.Height; y++)
                                {
                                    var pixel = extracted.RelativePixelLocations.FirstOrDefault(p => p.X == x && p.Y + extracted.PixelsFromTopOfLine == y);
                                    bool emptyValid = extracted.RelativeEmptyLocations.Any(p => p.X == x && p.Y + extracted.PixelsFromTopOfLine == y);

                                    if (pixel != null)
                                    {
                                        var v = (int)(pixel.Z * byte.MaxValue);
                                        if (emptyValid)
                                        {
                                            var c = Color.FromArgb(0, 0, v);
                                            output.SetPixel(x, y, c);
                                        }
                                        else
                                        {
                                            var c = Color.FromArgb(v, v, v);
                                            output.SetPixel(x, y, c);
                                        }
                                    }
                                    else if (emptyValid)
                                    {
                                        output.SetPixel(x, y, Color.Black);
                                    }
                                    else
                                        output.SetPixel(x, y, Color.Magenta);
                                }
                            }
                            output.Save(Path.Combine(overlapDir, (overlapCount++) + ".png"));
                            //output.Save(Path.Combine(overlapDir, "current.png"));
                            //Console.WriteLine("Just saved what is hopefully " + overlap.ExpectedCharacters);
                        }

                        //Console.Write($"Consult {overlapCount - 1}.png. Is this {overlap.ExpectedCharacters}? [y/n]: ");
                        //var input = Console.ReadLine().Trim();
                        //if(input == "y" || input.Length == 0)
                        //    input = overlap.ExpectedCharacters;
                        //else
                        //{
                        //    Console.Write("Enter correct input: ");
                        //    input = Console.ReadLine().Trim();
                        //}
                        var input = overlap.ExpectedCharacters;
                        if (input.Length > 0)
                        {
                            var glyph = new FuzzyGlyph()
                            {
                                AspectRatio = extracted.AspectRatio,
                                Character = input,
                                IsOverlap = true,
                                ReferenceGapFromLineTop = extracted.PixelsFromTopOfLine,
                                ReferenceMaxHeight = extracted.Height + 1,
                                ReferenceMinHeight = extracted.Height - 1,
                                ReferenceMaxWidth = extracted.Width + 1,
                                ReferenceMinWidth = extracted.Width - 1,
                                RelativeEmptyLocations = extracted.RelativeEmptyLocations,
                                RelativePixelLocations = extracted.RelativePixelLocations
                            };
                            overlappingGlyphs.Add(glyph);
                        }
                    }
                }
            }

            //Combine glyphs
            var allGlyphs = new List<FuzzyGlyph>();
            allGlyphs.AddRange(RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs);
            allGlyphs.AddRange(overlappingGlyphs);
            RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs = allGlyphs;
            RelativeChatParser.Database.GlyphDatabase.Instance.Init();
            var json = JsonConvert.SerializeObject(RelativeChatParser.Database.GlyphDatabase.Instance);
            File.WriteAllText("RelativeDB_with_overlaps.json", json);
            GlyphExtractor.distanceThreshold -= overlapExtraThreshold;
        }

        private static void LineExtractorTest(Dictionary<int, int> knownCounts = null)
        {
            var maxCount = 0;
            if (knownCounts != null)
                maxCount = knownCounts.Max(c => c.Value);
            Console.WriteLine("Known counts null: " + (knownCounts == null));
            var lineCounts = new Dictionary<int, int>();
            var count = 0;
            var heightTotal = 0;
            foreach (var file in Directory.GetFiles("debug_lines"))
            {
                File.Delete(file);
            }
            foreach (var file in Directory.GetFiles("tests"))
            {
                Console.WriteLine("Looking at " + file);
                Bitmap b = new Bitmap(file);
                var ic = new ImageCache(b);
                //foreach (var offset in LineScanner.LineOffsets)
                var offset = 765;
                while (offset < 2093)
                {
                    var firstY = 0;
                    for (int y = offset; y < offset + LineScanner.Lineheight; y++)
                    {
                        if (firstY > offset)
                            break;
                        for (int x = LineScanner.ChatLeftX; x < 1700; x++)
                        {
                            if (ic[x, y] > 0)
                            {
                                firstY = y;
                                break;
                            }
                        }
                    }
                    Console.WriteLine("First y: " + firstY);
                    offset = firstY + (int)(LineScanner.Lineheight * 1.2f);
                    if (!lineCounts.ContainsKey(firstY))
                        lineCounts[firstY] = 0;
                    lineCounts[firstY]++;
                    if (knownCounts != null && knownCounts[firstY] < maxCount / 2)
                    {
                        var rect = new Rectangle(4, firstY, LineScanner.ChatWidth, LineScanner.Lineheight);
                        using (var clone = b.Clone(rect, b.PixelFormat))
                        {
                            clone.Save(Path.Combine("debug_lines", (maxCount++) + ".png"));
                        }
                        heightTotal += rect.Height;
                    }
                }
                b.Dispose();
            }

            if (knownCounts == null)
            {
                LineExtractorTest(lineCounts);
                return;
            }
            else
                Console.WriteLine("\r\nHeight: " + heightTotal);

            var lineCountsStrings = lineCounts.OrderBy(o => o.Key).Select(o => $"{o.Key,4} :{o.Value,3}|").ToArray();
            var cTop = Console.CursorTop;
            for (int i = 0; i < lineCountsStrings.Length; i++)
            {
                //var top = i % 8 + cTop;
                //var left = i / 8 * 11;
                //Console.SetCursorPosition(left, top);
                //Console.Write(lineCountsStrings[i]);
                if (Console.CursorLeft + lineCountsStrings[i].Length >= Console.BufferWidth)
                    Console.WriteLine();
                Console.Write(lineCountsStrings[i]);
            }

            Console.WriteLine("\n\n");
            foreach (var line in lineCounts.OrderBy(o => o.Key))
            {
                if (line.Value >= count / 2)
                    Console.WriteLine($"{line.Key,4} {line.Value,4}");
            }

            if (heightTotal > 0)
            {
                //4515
                {
                    var files = Directory.GetFiles("debug_lines");
                    var final = new Bitmap(LineScanner.ChatWidth, heightTotal + files.Length * 2);
                    var top = 0;
                    var colors = new[] { Color.Green, Color.Blue };
                    //var i = 0;
                    foreach (var file in files)
                    {
                        using (var input = new Bitmap(file))
                        {
                            for (int x = 0; x < final.Width; x++)
                            {
                                final.SetPixel(x, top, Color.Red);
                            }
                            top++;
                            for (int x = 0; x < final.Width && x < input.Width; x++)
                            {
                                for (int y = 0; y < input.Height; y++)
                                {
                                    final.SetPixel(x, y + top, input.GetPixel(x, y));
                                }
                            }
                            top += input.Height;
                        }
                    }
                    final.Save("combined_line_misses.png");
                }
            }
        }

        private static void ParseImageTest()
        {
            //var inputs = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\names");
            //var i = 0;
            //foreach (var input in inputs)
            //{
            //    Console.WriteLine(input);
            //    Bitmap b = new Bitmap(input);
            //    var ic = new ImageCache(b);
            //    var output = new Bitmap(ic.Width, ic.Height);
            //    for (int x = 0; x < ic.Width; x++)
            //    {
            //        for (int y = 0; y < ic.Height; y++)
            //        {
            //            if (ic.GetColor(x, y) == ChatColor.ChatTimestampName)
            //                output.SetPixel(x, y, b.GetPixel(x, y));
            //            else
            //                output.SetPixel(x, y, Color.Black);
            //        }
            //    }
            //    output.Save("names_" + (i++) + ".png");
            //    b.Dispose();
            //}

            var input = @"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\garbage.png";
            ImageCleaner.SaveSoftMask(input, "current.png");
            var cp = new RelativePixelParser(new DummyLogger(true), new DummySender());
            var lines = cp.ParseChatImage(new Bitmap(input), false, false, 27);
            foreach (var line in lines)
            {
                Console.WriteLine(line.RawMessage);
            }
        }

        private static void RelativeParserTest()
        {
            var ignore = RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs;
            var parser = new RelativeChatParser.RelativePixelParser(new DummyLogger(), new DummySender());
            var inputDir = Path.Combine("inputs", "character_training");
            var allFiles = Directory.GetFiles(inputDir);
            var sw = new Stopwatch();
            sw.Start();
            var filesDone = 0;
            var errorCount = 0;
            var characterCount = 0;
            var errorsByCharacter = new Dictionary<char, int>();
            foreach (var input in allFiles.Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct())
            {
                filesDone++;
                Console.WriteLine($"={input.Substring(input.LastIndexOf('\\') + 1)}=");
                var inputTxt = input + ".txt";
                var inputImg = input + ".png";
                ImageCleaner.SaveSoftMask(inputImg, "current.png");
                var b = new Bitmap(inputImg);
                var chatLines = parser.ParseChatImage(b, false, false, 27, true).Select(line => line.RawMessage.Replace(" ", "")).ToArray();
                //Console.WriteLine();

                var expectedLines = File.ReadAllLines(inputTxt).Select(line => line.Replace(" ", "").Trim()).ToArray();
                if (chatLines.Length != expectedLines.Length)
                {
                    Console.WriteLine("Expected lines and parsed lines do not line up!");
                }
                for (int i = 0; i < expectedLines.Length; i++)
                {
                    var isError = false;
                    characterCount += expectedLines[i].Length;
                    if (expectedLines[i].Length != chatLines[i].Length)
                    {
                        isError = true;
                        errorCount += Math.Abs(expectedLines[i].Length - chatLines[i].Length);
                        Console.WriteLine($"On line {i} expected {expectedLines[i].Length} characters but got {chatLines[i].Length}.");
                        Console.WriteLine($"{chatLines[i]}\n{expectedLines[i]}\n");
                    }
                    if (!isError)
                    {
                        var errorLine = "";
                        for (int j = 0; j < expectedLines[i].Length; j++)
                        {
                            if (expectedLines[i][j] != chatLines[i][j])
                            {
                                if (!errorsByCharacter.ContainsKey(expectedLines[i][j]))
                                    errorsByCharacter[expectedLines[i][j]] = 1;
                                else
                                    errorsByCharacter[expectedLines[i][j]]++;

                                isError = true;
                                errorLine += "^";
                                errorCount++;
                            }
                            else
                                errorLine += " ";
                        }

                        if (isError)
                        {
                            Console.WriteLine($"Lines {i} do not line up!");
                            Console.WriteLine($"{chatLines[i]}\n{expectedLines[i]}\n{errorLine}");
                        }
                    }
                }
                b.Dispose();
            }
            sw.Stop();
            Console.WriteLine(inputDir);
            Console.WriteLine($"Parsed {filesDone} files in {sw.Elapsed.TotalSeconds}s. {sw.Elapsed.TotalSeconds / filesDone} seconds/file.");
            Console.WriteLine($"Error count: {errorCount} out of {characterCount}. {((float)errorCount / characterCount) * 100f}%");
            foreach (var error in errorsByCharacter.OrderByDescending(kvp => kvp.Value))
            {
                Console.WriteLine($"{error.Key}: {error.Value,3}");
            }
        }

        private static void RelativeParserGlyphTrainer()
        {
            var inputDir = Path.Combine("inputs", "character_training");
            var allFiles = Directory.GetFiles(inputDir);
            var inputs = allFiles.Select(f => f.Substring(0, f.LastIndexOf("."))).Distinct();
            var error = false;
            foreach (var input in inputs)
            {
                var pic = input + ".png";
                var txt = input + ".txt";
                if (!allFiles.Contains(pic))
                {
                    Console.WriteLine($"Missing picture for {input}");
                    error = true;
                }
                if (!allFiles.Contains(txt))
                {
                    Console.WriteLine($"Missing text for {input}");
                    error = true;
                }
            }
            if (error)
                return;

            var glyphDict = TrainingDataExtractor.ExtractGlyphs(inputs.Select(input => new TrainingInput(input + ".png", input + ".txt")));

            Console.WriteLine($"Extracted {glyphDict.Values.SelectMany(g => g).Count()} named glyphs without error.");

            //var finalGlyphs = glyphDict.Select((kvp) => GlyphTrainer.CombineExtractedGlyphsByRects(kvp.Key, kvp.Value)).SelectMany(o => o);
            var finalGlyphs = glyphDict.Select(kvp => GlyphTrainer.CombineExtractedGlyphs(kvp.Key.ToString()[0], kvp.Value)).ToList();
            RelativeChatParser.Database.GlyphDatabase.Instance.AllGlyphs = finalGlyphs;
            RelativeChatParser.Database.GlyphDatabase.Instance.AllSpaces.Clear();
            RelativeChatParser.Database.GlyphDatabase.Instance.Init();
            File.WriteAllText("RelativeDB.json", JsonConvert.SerializeObject(RelativeChatParser.Database.GlyphDatabase.Instance));

            Console.WriteLine("Attempt to save finalGlyphs to debug images");
            var glyphVisualizerDir = @"glyphs";
            if (Directory.Exists(glyphVisualizerDir))
            {
                Directory.Delete(glyphVisualizerDir, true);
                Thread.Sleep(1000);
            }
            Directory.CreateDirectory(glyphVisualizerDir);
            foreach (var glyph in finalGlyphs)
            {
                glyph.SaveVisualization(Path.Combine(glyphVisualizerDir, (int)glyph.Character[0] + ".png"), false);
            }

            //Check if any glyph is taller than line height
            foreach (var glyph in finalGlyphs)
            {
                if (glyph.ReferenceMaxHeight > LineScanner.Lineheight)
                {
                    Console.WriteLine($"Glyph {glyph.Character} has a max height of {glyph.ReferenceMaxHeight} which is above the max height expected of {LineScanner.Lineheight}");
                }
            }
            //var imageCount = 0;
            //foreach (var glyph in finalGlyphs)
            //{
            //    var b = new Bitmap(glyph.Width, glyph.Height);
            //    var pixelColor = Color.White;
            //    var emptyColor = Color.Black;
            //    var missingColor = Color.Magenta;
            //    var bothColor = Color.CornflowerBlue;
            //    for (int x = 0; x < b.Width; x++)
            //    {
            //        for (int y = 0; y < b.Height; y++)
            //        {
            //            bool isPixel = glyph.Pixels.Any(p => p.Key.Item1 == x && p.Key.Item2 == y);
            //            bool isEmpty = glyph.Empties.Any(p => p.Item1 == x && p.Item2 == y);
            //            if (isPixel)
            //            {
            //                var pixel = glyph.Pixels.First(p => p.Key.Item1 == x && p.Key.Item2 == y);
            //                var v = (int)(pixel.Value * byte.MaxValue);
            //                if (isPixel && !isEmpty)
            //                {
            //                    var c = Color.FromArgb(v, v, v);
            //                    b.SetPixel(x, y, c);
            //                }
            //                else if (isEmpty && isPixel)
            //                {
            //                    var c = Color.FromArgb(0, 0, v);
            //                    b.SetPixel(x, y, c);
            //                }
            //            }
            //            else if (isEmpty && !isPixel)
            //                b.SetPixel(x, y, emptyColor);
            //            else
            //                b.SetPixel(x, y, missingColor);
            //        }
            //    }

            //    var fileInfo = new FileInfo(Path.Combine(glyphVisualizerDir, ((int)glyph.Character[0]).ToString(), (imageCount++) + ".png"));
            //    if (!fileInfo.Directory.Exists)
            //        Directory.CreateDirectory(fileInfo.Directory.FullName);
            //    b.Save(fileInfo.FullName);
            //}
        }

        private static Vector2 PointToV2(Point p, int width, int height)
        {
            return new Vector2((float)p.X / (width - 1), (float)p.Y / (height - 1));
        }

        private static void CornerGlyphShim()
        {
            var input = @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Spaces\space_slice_0.png";
            var b = new Bitmap(input);
            var image = new ImageCache(b);
            var sw = new Stopwatch();
            sw.Start();
            var glyphs = new ExtractedGlyph[][] {
                LineScanner.ExtractGlyphsFromLine(image, 13),
                //LineScanner.ExtractGlyphsFromLine(image, 1),
                //LineScanner.ExtractGlyphsFromLine(image, 2),
                //LineScanner.ExtractGlyphsFromLine(image, 3),
                //LineScanner.ExtractGlyphsFromLine(image, 4)
            }.SelectMany(g => g).ToArray();
            sw.Stop();
            Console.WriteLine($"Extracted {glyphs.Length} glyphs in {sw.ElapsedMilliseconds}ms.");
            LineScanner.SaveExtractedGlyphs(image, "glyphs", glyphs);
            b.Dispose();
        }

        //private static void NewTrainingVerifier()
        //{
        //    var inputPaths = new string[] { @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Orig\Overlaps",
        //        @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Orig\Spaces"};
        //    var inputs = new List<string>();
        //    foreach (var inputPath in inputPaths)
        //    {
        //        var allFiles = Directory.GetFiles(inputPath);
        //        foreach (var file in allFiles.Select(f => f.Replace(".png", "").Replace(".txt", "")).Distinct())
        //        {
        //            if (allFiles.Contains(file + ".png") && allFiles.Contains(file + ".txt"))
        //            {
        //                inputs.Add(file);
        //            }
        //        }
        //    }

        //    var cp = new ChatParser(new DummyLogger(), DataHelper.OcrDataPathEnglish);
        //    foreach (var input in inputs)
        //    {
        //        Console.WriteLine($"={input}=");
        //        var b = new System.Drawing.Bitmap(input + ".png");
        //        var lines = cp.ParseChatImage(b).Select(o => o.RawMessage).ToArray();

        //        lines = lines.Select(l => Regex.Replace(l, @"^\[.....\]\s*[^\s]+\s+", "").Trim())
        //            .Where(l => l.ToLower() != "clear").ToArray();
        //        //lines = lines.Select(l => l.Remove(0, l.IndexOf(':')+1).Trim()).ToArray();
        //        var expectedLines = File.ReadAllLines(input + ".txt").Select(line => line.Trim()).ToArray();
        //        if (lines.Length != expectedLines.Length)
        //            Console.WriteLine("Parsed lines and expected lines don't match!");
        //        else
        //        {
        //            for (int i = 0; i < lines.Length; i++)
        //            {
        //                var match = true;
        //                if (lines[i].Length != expectedLines[i].Length)
        //                {
        //                    Console.WriteLine($"Line index {i} does not have expected number of characters");
        //                    match = false;
        //                }
        //                else
        //                {
        //                    for (int j = 0; j < lines[i].Length; j++)
        //                    {
        //                        if (!match)
        //                            break;
        //                        if (lines[i][j] != expectedLines[i][j])
        //                        {
        //                            match = false;
        //                        }
        //                    }
        //                }
        //                if (!match)
        //                {
        //                    Console.WriteLine($"Lines index {i} does not match\n{lines[i]}\n{expectedLines[i]}\n");
        //                }
        //            }
        //        }
        //        b.Dispose();
        //    }
        //}

        //private static void SaveAllPixelGroups()
        //{
        //    var overlapCSV = @"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\overlaps.csv";
        //    OverlapDetector.ExtractPixelGroupsOnImages(overlapCSV,
        //        @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps",
        //        "overlaps");
        //}

        private static void SaveSoftMask()
        {
            //ImageCleaner.SaveSoftMask("LZ.png",
            //    "softmask.png");

            //ImageCleaner.SaveSoftMask(@"C:\Users\david\Downloads\637276927768266587.png", "softmask.png");

            var inputPaths = new string[] { @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Spaces",
                @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps"};
            foreach (var image in inputPaths
                .Select(path => Directory.GetFiles(path).Where(f => f.EndsWith(".png")))
                .SelectMany(f => f))
            {
                Console.WriteLine(image);
                if (!Directory.Exists("softmasks"))
                    Directory.CreateDirectory("softmasks");
                ImageCleaner.SaveSoftMask(image, @"softmasks\" + (new FileInfo(image)).Name);
            }
        }

        private static void NewDataSenderShim()
        {
            IConfiguration config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("appsettings.development.json", true, true)
                 .AddJsonFile("appsettings.production.json", true, true)
                 .Build();
            var dataSender = new ClientWebsocketDataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"],
                config["DataSender:LogMessagePrefix"],
                config["DataSender:LogLineMessagePrefix"]);
            _ = Task.Run(dataSender.ConnectAsync);

            var duck = new Bitmap("17662706-ad79-41a4-8da9-acd4c891a1e4.png");
            var guid = Guid.NewGuid();//Guid.Parse("36001368-7cb1-40f4-81f5-92033dd28928");
            Console.WriteLine(guid);
            dataSender.AsyncSendRivenImage(guid, duck);
            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(1000);
            }
        }

        //private static void ChatParsingShim()
        //{
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\noclick_screen_chat.png"))
        //    {
        //        var lines = cp.ParseChatImage(b);
        //    }
        //}

        private static void GroupShim()
        {
            var dirs = Directory.GetDirectories(@"C:\Users\david\source\repos\WFChatParser\src\Presentation\DebugCLI\bin\Debug\netcoreapp2.2\debug_first").Select(d => new DirectoryInfo(d));
            //var values = dirs.Select(dir => float.Parse(dir.Name));
            var groups = dirs.GroupBy(v => Math.Round(float.Parse(v.Name), 1));
            foreach (var group in groups)
            {
                Directory.CreateDirectory(Path.Combine("debug_grouped", group.Key.ToString()));
                foreach (var dir in group)
                {
                    foreach (var file in Directory.GetFiles(dir.FullName).Select(f => new FileInfo(f)))
                    {
                        File.Copy(file.FullName, Path.Combine("debug_grouped", group.Key.ToString(), file.Name + "_" + (Guid.NewGuid()).ToString() + ".png"));
                    }
                }
            }
        }

        private static void ComplexRivenShim()
        {
            if (!System.IO.Directory.Exists("debug_tess"))
                System.IO.Directory.CreateDirectory("debug_tess");
            else
            {
                System.IO.Directory.GetFiles("debug_tess").ToList().ForEach(f => System.IO.File.Delete(f));
                Directory.GetDirectories("debug_tess").ToList().ForEach(dir => Directory.Delete(dir, true));
            }

            if (!System.IO.Directory.Exists("debug_width"))
                System.IO.Directory.CreateDirectory("debug_width");
            else
            {
                System.IO.Directory.GetFiles("debug_width").ToList().ForEach(f => System.IO.File.Delete(f));
                Directory.GetDirectories("debug_width").ToList().ForEach(dir => Directory.Delete(dir, true));
            }

            if (!System.IO.Directory.Exists("debug_first"))
                System.IO.Directory.CreateDirectory("debug_first");
            else
            {
                System.IO.Directory.GetFiles("debug_first").ToList().ForEach(f => System.IO.File.Delete(f));
                Directory.GetDirectories("debug_first").ToList().ForEach(dir => Directory.Delete(dir, true));
            }

            Color.Black.ToHsv();
            //var sw = new Stopwatch();
            //sw.Start();
            var files = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Riven images\Chinese rivens").Where(f => f.EndsWith(".png")).Select(f => new FileInfo(f)).ToArray();
            var fileQueue = new ConcurrentQueue<FileInfo>(files);
            for (int _ = 0; _ < Environment.ProcessorCount; _++)
            {
                var t = new Thread(() =>
                 {
                     while (fileQueue.Count > 0)
                     {
                         var crp = new ComplexRivenParser(ClientLanguage.English);
                         FileInfo file = null;
                         if (!fileQueue.TryDequeue(out file))
                             continue;

                         using (var b = new Bitmap(file.FullName))
                         {
                             //crp.DebugIdentifyNumbers(b, file.Name);
                             crp.DebugGetFirstCharacterRemove(b, file.Name);
                         }
                     }
                 });
                t.Start();
            }

            while (fileQueue.Count > 0)
            {
                Console.Write($"\rFiles completed: {files.Length - fileQueue.Count}");
                Thread.Sleep(100);
            }

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                Console.WriteLine($"Working on file {i + 1} of {files.Length}");
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static bool AreRivensSame(Riven lhs, Riven rhs)
        {
            if (lhs.Modifiers.Length != rhs.Modifiers.Length)
            {
                Console.WriteLine("Different modifier count");
                return false;
            }
            for (int i = 0; i < lhs.Modifiers.Length; i++)
            {
                if (lhs.Modifiers[i].Value != rhs.Modifiers[i].Value)
                {
                    Console.WriteLine($"Modifier {i} value did not match. {lhs.Modifiers[i].Value} != {rhs.Modifiers[i].Value}");
                    return false;
                }
                if (lhs.Modifiers[i].Description.Replace(" ", "").Replace("%", "").Trim()
                    != rhs.Modifiers[i].Description.Replace(" ", "").Replace("%", "").Trim())
                {
                    Console.WriteLine($"Modifier {i} description did not match. {lhs.Modifiers[i].Description} != {rhs.Modifiers[i].Description}");
                    return false;
                }
                if (lhs.Modifiers[i].Curse != rhs.Modifiers[i].Curse)
                {
                    Console.WriteLine($"Modifier {i} description did not match. {lhs.Modifiers[i].Curse} != {rhs.Modifiers[i].Curse}");
                    return false;
                }
            }
            if (lhs.Drain != rhs.Drain)
            {
                Console.WriteLine($"Drain did not match. {lhs.Drain} != {rhs.Drain}");
                return false;
            }
            if (lhs.MasteryRank != rhs.MasteryRank)
            {
                Console.WriteLine($"MR did not match. {lhs.MasteryRank} != {rhs.MasteryRank}");
                return false;
            }
            if (lhs.Polarity != rhs.Polarity)
            {
                Console.WriteLine($"Polarity did not match. {lhs.Polarity} != {rhs.Polarity}");
                return false;
            }
            if (lhs.Rank != rhs.Rank)
            {
                Console.WriteLine($"Rank did not match. {lhs.Rank} != {rhs.Rank}");
                return false;
            }
            if (lhs.Rolls != rhs.Rolls)
            {
                Console.WriteLine($"Rolls did not match. {lhs.Rolls} != {rhs.Rolls}");
                return false;
            }
            return true;
        }

        private static void TestRivens()
        {
            var valid = new DateTime(2019, 11, 11, 19, 20, 0);
            //string[] allRivens2 = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Riven images\2019_11_11")
            //    .Where(f => (new FileInfo(f)).LastWriteTime > valid).ToArray();
            var knownBads = Directory.GetFiles(@"C:\Users\david\source\repos\WFChatParser\src\Presentation\DebugCLI\bin\Debug\netcoreapp2.2\bad_rivens")
                .Select(f => new FileInfo(f).Name);
            string[] allRivens2 = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Riven images\2019_11_11")
                .Where(f =>
                {
                    var fi = new FileInfo(f);
                    return fi.LastWriteTime > valid
                        && knownBads.Contains(fi.Name);
                }).ToArray();
            var queue = new ConcurrentQueue<string>(allRivens2);
            var threads = new List<Thread>();
            var totalSw = new Stopwatch();
            totalSw.Start();
            var url = File.ReadAllText("secret.txt").Trim();
            var outputQueue = new ConcurrentQueue<string>();
            var badRivens = new ConcurrentQueue<string>();
            for (int j = 0; j < 1; j++)
            {
                Thread thread = new Thread(() =>
                {
                    var rc = new RivenCleaner();
                    var rp = new RivenParser(ClientLanguage.English);
                    var webC = new WebClient();
                    while (queue.Count > 0)
                    {
                        string rawRiven = string.Empty;
                        queue.TryDequeue(out rawRiven);
                        if (rawRiven == null || rawRiven == string.Empty)
                        {
                            Thread.Sleep(500);
                            continue;
                        }

                        var fi = new FileInfo(rawRiven);
                        //if (fi.LastWriteTime > valid)
                        //    continue;
                        if (!rawRiven.EndsWith(".png"))
                            continue;
                        var rivenId = fi.Name.Replace(".png", "");
                        Riven serverRiven = null;
                        for (int j2 = 0; j2 < 10; j2++)
                        {
                            try
                            {
                                serverRiven = JsonConvert.DeserializeObject<Riven>(webC.DownloadString(url + rivenId));
                                break;
                            }
                            catch
                            {

                            }
                        }
                        using (var rawRivenBitmap = new Bitmap(rawRiven))
                        {
                            using (var cleanRivenBitmap = rc.CleanRiven(rawRivenBitmap))
                            {
                                //cleanRivenBitmap.Save("debug_clean_riven.png");
                                var riven = rp.ParseRivenTextFromImage(cleanRivenBitmap, null);
                                riven.Polarity = rp.ParseRivenPolarityFromColorImage(rawRivenBitmap);
                                riven.Rank = rp.ParseRivenRankFromColorImage(rawRivenBitmap);
                                if (!AreRivensSame(serverRiven, riven))
                                {
                                    cleanRivenBitmap.Save("debug_riven_clean.png");
                                    outputQueue.Enqueue($"Rivens are not same! {rawRiven}");
                                    badRivens.Enqueue(rawRiven);
                                }
                                else
                                    outputQueue.Enqueue("Riven " + rivenId + " same");
                                //var lineRects = new List<Rectangle>();
                                //for (int y = 0; y < cleanRivenBitmap.Height;)
                                //{
                                //    var lineRect = GetNextLineRect(y, cleanRivenBitmap);
                                //    if (lineRect == Rectangle.Empty || lineRect.Top >= cleanRivenBitmap.Height)
                                //        break;
                                //    lineRects.Add(lineRect);
                                //    Console.WriteLine($"Adding rect {lineRect.X},{lineRect.Y} : {lineRect.Width}x{lineRect.Height}");
                                //    y = lineRect.Bottom;
                                //}
                            }
                        }
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            if (!Directory.Exists("bad_rivens"))
            {
                Directory.CreateDirectory("bad_rivens");
            }
            var cleaner = new RivenCleaner();
            while (queue.Count > 0)
            {
                string output = string.Empty;
                while (outputQueue.Count > 0)
                {
                    outputQueue.TryDequeue(out output);
                    if (output != null && output != string.Empty)
                    {
                        ClearCurrentConsoleLine();
                        Console.WriteLine(output);
                    }
                }
                output = string.Empty;
                while (badRivens.Count > 0)
                {
                    badRivens.TryDequeue(out output);
                    if (output != null && output != string.Empty)
                    {
                        using (var bitmap = new Bitmap(output))
                        {
                            using (var b = cleaner.CleanRiven(bitmap))
                            {
                                var fi = new FileInfo(output);
                                b.Save(Path.Combine("bad_rivens", fi.Name));
                                ClearCurrentConsoleLine();
                                Console.WriteLine("See bad riven: " + fi.Name);
                            }
                        }
                    }
                }
                var done = allRivens2.Length - queue.Count;
                var left = allRivens2.Length - done;
                ClearCurrentConsoleLine();
                Console.Write($"\rChecked {done} of {allRivens2.Length} rivens in {totalSw.Elapsed.TotalSeconds} seconds. {left * (totalSw.Elapsed.TotalSeconds / done)} seconds left\r");
                Thread.Sleep(500);
            }
        }

        private static Rectangle GetNextLineRect(int lastY, Bitmap bitmap)
        {
            var startingY = -1;
            var endingY = -1;
            var startX = -1;
            var endX = -1;
            for (int y = lastY; y < bitmap.Height; y++)
            {
                var pixelFound = false;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).R < 128)
                    {
                        pixelFound = true;
                        if (x < startX || startX < 0)
                            startX = x;
                        if (x > endX)
                            endX = x;

                        if (startingY < 0 || y < startingY)
                            startingY = y;
                        if (endingY < y)
                            endingY = y;
                    }
                }
                if (!pixelFound && endingY > 0)
                    break;
            }
            endX++;
            endingY++;

            if (startingY > 0 && endingY < 0)
                endingY = bitmap.Height;

            if (endingY - startingY > 65)
                endingY = startingY + (endingY - startingY) / 2;

            if (startingY > 0)
                return new Rectangle(startX, startingY, endX - startX, endingY - startingY);
            else
                return Rectangle.Empty;
        }

        private class GlyphAuditItem
        {
            public float Value { get; set; }
            public string Name { get; set; }
        }
        //private static void GlyphAudit()
        //{
        //    var eGD = new WFImageParser.GlyphRecognition.GlyphDatabase(DataHelper.OcrDataPathEnglish);

        //    //Find smallest known leftmost glyph that can overlap
        //    //Find the lowest connective tissue
        //    GlyphAuditItem smallestLeftmostGlyph = null;
        //    foreach (var glyph in eGD.KnownGlyphs.Where(g => g.Name.Contains(", ")))
        //    {
        //        var leftGlyphName = glyph.Name.Split(',').First();
        //        var leftGlyph = eGD.KnownGlyphs.First(g => g.Name == leftGlyphName);
        //        if (smallestLeftmostGlyph == null || smallestLeftmostGlyph.Value > leftGlyph.Width)
        //        {
        //            smallestLeftmostGlyph = new GlyphAuditItem() { Name = leftGlyph.Name, Value = leftGlyph.Width };
        //        }
        //    }
        //    if (smallestLeftmostGlyph != null)
        //        Console.WriteLine($"{smallestLeftmostGlyph.Name} is smallest leftmost glyph with width {smallestLeftmostGlyph.Value}");

        //    var topLowest = new GlyphAuditItem[] { null, null, null, null, null, null, null, null, null, null };
        //    foreach (var glyph in eGD.KnownGlyphs)
        //    {
        //        if (glyph.Name.Contains(","))
        //            continue;

        //        var lowestTotal = float.NaN;
        //        if (glyph.Width <= smallestLeftmostGlyph.Value)
        //            continue;
        //        for (int x = (int)smallestLeftmostGlyph.Value; x < glyph.Width - 2; x++)
        //        {
        //            var total = 0f;
        //            for (int y = 0; y < glyph.Height; y++)
        //            {
        //                total += glyph.WeightMappings[x, y];
        //            }
        //            if (total <= 0.2f)
        //                continue;
        //            if (float.IsNaN(lowestTotal))
        //                lowestTotal = total;
        //            else
        //                lowestTotal = Math.Min(lowestTotal, total);
        //        }

        //        for (int i = 0; i < topLowest.Length; i++)
        //        {
        //            if (topLowest[i] == null || topLowest[i].Value > lowestTotal)
        //            {
        //                //Shift everyone right;
        //                var oldValue = topLowest[i];
        //                for (int j = i + 1; j < topLowest.Length - 1; j++)
        //                {
        //                    var temp = topLowest[j];
        //                    topLowest[j] = oldValue;
        //                    oldValue = temp;
        //                }
        //                //Store result
        //                topLowest[i] = new GlyphAuditItem() { Name = glyph.Name, Value = lowestTotal };
        //                break;
        //            }
        //        }
        //    }

        //    foreach (var lowest in topLowest)
        //    {
        //        if (lowest == null)
        //            continue;
        //        Console.WriteLine($"{lowest.Name} {lowest.Value}");
        //    }
        //}

        private static string[] NewChatParsingShim(string path = null)
        {
            var sw = new Stopwatch();
            sw.Start();

            var lp = new TessChatLineParser(ClientLanguage.Chinese);
            var cp = new CustomChatLineParser();

            if (path == null)
                path = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chinese_tradechat_2.png";
            var input = new Bitmap(path);

            var cle = new Application.ChatBoxParsing.ChatLineExtractor.ChatLineExtractor();
            var lines = cle.ExtractChatLines(input);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].Save("debug_" + i + ".png");
            }

            var result = new List<string>();
            var messages = new List<BaseLineParseResult>();
            var extraMessages = new List<BaseLineParseResult>();
            var timestampReg = new Regex("^\\[\\d");
            foreach (var image in lines)
            {
                var line = lp.ParseLine(image);
                messages.Add(line);
                extraMessages.Add(cp.ParseLine(image));
                if (!timestampReg.IsMatch(line.RawMessage))
                {
                    var last = result.Last();
                    result.Remove(last);
                    result.Add(last + ' ' + line);
                }
                else
                    result.Add(line.RawMessage);
            }

            File.WriteAllLines("chinese.txt", result);

            sw.Stop();
            Console.WriteLine("Parsed 1 image in: " + sw.Elapsed.TotalSeconds + " seconds.");

            return result.ToArray();

            //var lp = new TessChatLineParser();
            //var input = new Bitmap(@"C:\Users\david\source\repos\WFChatParser\src\Presentation\DebugCLI\bin\Debug\netcoreapp2.2\debug_22.png");
            //var result = lp.ParseLine(input);
            //return null;

            //var clp = new CustomChatLineParser();
            //var result = clp.ParseLine(new Bitmap(@"C:\Users\david\source\repos\WFChatParser\src\Presentation\DebugCLI\bin\Debug\netcoreapp2.2\debug_6.png"));
            //return null;

            //var lp = new LineParser();
            //var b = new Bitmap(@"C:\Users\david\source\repos\WFChatParser\src\Presentation\DebugCLI\bin\Debug\netcoreapp2.2\blackyb.png");
            //Console.WriteLine(lp.ParseLine(b));
            //b.Dispose();
            //return null;
        }

        //private static void ChineseChatShim()
        //{
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathChinese);
        //    const string source = @"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\fake_chinese_wrap_altered.png";
        //    ImageCleaner.SaveSoftMask(source, "lines_white.png");
        //    var lp = new TessChatLineParser(ClientLanguage.Chinese);
        //    using (var b = new Bitmap(source))
        //    {
        //        foreach (var line in Directory.GetFiles(Environment.CurrentDirectory).Where(f => f.StartsWith("line_") && f.EndsWith(".png")))
        //        {
        //            File.Delete(line);
        //        }
        //        var lines = cp.ParseUsernamesFromChatImage(b, false);
        //        for (int i = 0; i < lines.Length; i++)
        //        {
        //            Rectangle rect = lines[i].LineRect;
        //            using (var lineBitmap = new Bitmap(rect.Width, rect.Height))
        //            {
        //                for (int x = 0; x < lineBitmap.Width; x++)
        //                {
        //                    for (int y = 0; y < lineBitmap.Height; y++)
        //                    {
        //                        lineBitmap.SetPixel(x, y, b.GetPixel(rect.Left + x, rect.Top + y));
        //                    }
        //                }
        //                lineBitmap.Save("line_" + i + ".png");
        //            }

        //            var tessLines = WFImageParser.ChatLineExtractor.ExtractChatLines(b, rect);
        //            ChatMessageLineResult fullMessage = null;
        //            for (int j = 0; j < tessLines.Length; j++)
        //            {
        //                tessLines[j].Save("line_" + i + "_" + j + ".png");
        //                var parsedLine = lp.ParseLine(tessLines[j]) as ChatMessageLineResult;
        //                if (fullMessage == null)
        //                {
        //                    fullMessage = parsedLine;
        //                    fullMessage.Username = lines[i].Username;
        //                    fullMessage.Timestamp = lines[i].Timestamp;
        //                    fullMessage.RawMessage = $"{fullMessage.Timestamp} {fullMessage.Username}{fullMessage.RawMessage}";
        //                    fullMessage.EnhancedMessage = $"{fullMessage.Timestamp} {fullMessage.Username}{fullMessage.EnhancedMessage}";
        //                }
        //                else
        //                    fullMessage.Append(parsedLine, 0, 0);
        //            }
        //            fullMessage.MessageBounds = rect;

        //            var debug = JsonConvert.SerializeObject(fullMessage);
        //        }
        //    }
        //}

        private static void ModiDescrShim()
        {
            var modi = new Modifier();
        }

        //private static void ChatLineExtractorShim()
        //{
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chat_new.png");
        //    var lines = cp.ExtractChatLines(b);
        //    for (int i = 0; i < lines.Length; i++)
        //    {
        //        lines[i].Save("line_" + i + ".png");
        //        var username = cp.GetUsernameFromChatLine(lines[i]);
        //        if (username != null)
        //            Console.WriteLine("Username: " + username);
        //    }
        //}

        private static void ChatMovingShim()
        {
            var input = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chat_new.png");
            var samples = LineSampler.GetAllLineSamples(input);
            var clickPoints = new Point[] { new Point(283, 779), new Point(472, 978) };
            var movedScreen = new Bitmap(@"C:\Users\david\Downloads\new_chat_blurr.png");
            foreach (var clickPoint in clickPoints)
            {
                int chatLine = LineSampler.GetLineIndexFromPoint(clickPoint.X, clickPoint.Y);
                var lineSamples = LineSampler.GetLineSamples(movedScreen, chatLine);
                for (int i = 0; i < lineSamples.Length; i++)
                {
                    var origSample = samples[chatLine, i];
                    var sample = lineSamples[i];
                    var rDiff = origSample.R - sample.R;
                    var gDiff = origSample.G - sample.G;
                    var bDiff = origSample.B - sample.B;
                    if ((origSample.R - sample.R) * (origSample.R - sample.R) +
                        (origSample.G - sample.G) * (origSample.G - sample.G) +
                        (origSample.B - sample.B) * (origSample.B - sample.B) > 225)
                    {
                        Console.WriteLine("Image different");
                    }
                }
            }
        }

        private static void NewRivenShim()
        {
            var rc = new RivenCleaner();
            var rp = new RivenParser(ClientLanguage.English);
            //var newRiven = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\new_riven.png");
            var fullImage = new Bitmap(@"\\desktop-3414ubq\Warframes\Bot Client\debug\debug.png");
            var newRiven = rp.CropToRiven(fullImage);
            newRiven.Save("new_riven_cropped.png");
            var result = rc.CleanRiven(newRiven);
            result.Save("new_riven_processed2.png");
            var riven = rp.ParseRivenTextFromImage(result, null);
            riven.Rank = rp.ParseRivenRankFromColorImage(newRiven);
            riven.Polarity = rp.ParseRivenPolarityFromColorImage(newRiven);
            Console.WriteLine("\n" + (int)riven.Polarity + " = " + riven.Polarity.ToString());
            Console.WriteLine(JsonConvert.SerializeObject(riven));
            newRiven.Dispose();
            fullImage.Dispose();
            result.Dispose();
        }

        private static void TessShim()
        {
            //using (var rawImage = new Bitmap(@"C:\Users\david\error.png"))
            //{
            //    var rc = new RivenCleaner();
            //    using (var cleaned = rc.CleanRiven(rawImage))
            //    {

            //    }
            //}
            //var lp = new LineParser();
            //var result = lp.ParseLine(new Bitmap("line.png"));

            var f = new FileInfo(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\4f41dd91-628e-40f4-8b68-2642fd86c4c8.png");
            using (var cropped = new Bitmap(f.FullName))
            {
                var cleaner = new RivenCleaner();
                using (var cleaned = cleaner.CleanRiven(cropped))
                {
                    cleaned.Save("cleaned.png");
                    var parser = new RivenParser(ClientLanguage.English);
                    var riven = parser.ParseRivenTextFromImage(cleaned, null);
                    try
                    {
                        riven.ImageId = Guid.Parse(f.Name.Replace(".png", ""));
                    }
                    catch { }
                }
            }

            //var lp = new LineParser();
            //var wp = new WordParser();
            //var res = lp.ParseLine(new Bitmap("debug.png"));
        }

        //private static void FindErrorAgain()
        //{
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    foreach (var file in Directory.GetFiles(@"\\DESKTOP-BJRVJJQ\ChatLog\debug").Where(f => f.Contains("131992381447623296")))
        //    {
        //        var lines = cp.ParseChatImage(new Bitmap(file));
        //        foreach (var line in lines)
        //        {
        //            var clr = line as ChatMessageLineResult;

        //            var chatMessage = MakeChatModel(line as Application.LineParseResult.ChatMessageLineResult);
        //        }
        //    }
        //}

        private static ChatMessageModel MakeChatModel(Application.LineParseResult.ChatMessageLineResult line)
        {
            var m = line.RawMessage;
            string debugReason, timestamp, username;
            var parsed = GetUsername(line.RawMessage);
            timestamp = parsed.Item1;
            username = parsed.Item2;
            debugReason = parsed.Item3;
            var cm = new ChatMessageModel()
            {
                Raw = m,
                Author = username,
                Timestamp = timestamp,
                SystemTimestamp = DateTimeOffset.UtcNow
            };
            if (debugReason != null)
            {
                cm.DEBUGREASON = debugReason;
            }
            cm.EnhancedMessage = line.EnhancedMessage;
            return cm;
        }

        private static Tuple<string, string, string> GetUsername(string RawMessage)
        {
            var badNameRegex = new Regex("[^-A-Za-z0-9._]");
            string debugReason = null;
            var m = RawMessage;
            var timestamp = m.Substring(0, 7).Trim();
            var username = "Unknown";
            try
            {
                username = m.Substring(8).Trim();
                if (username.IndexOf(":") > 0 && username.IndexOf(":") < username.IndexOf(" "))
                    username = username.Substring(0, username.IndexOf(":"));
                else
                {
                    username = username.Substring(0, username.IndexOf(" "));
                    debugReason = "Bade name: " + username;
                }
                if (username.Contains(" ") || username.Contains(@"\/") || username.Contains("]") || username.Contains("[") || badNameRegex.Match(username).Success)
                {
                    debugReason = "Bade name: " + username;
                }

                if (!Regex.Match(RawMessage, @"^(\[\d\d:\d\d\]) ([-A-Za-z0-9._]+):?\s+(.+)").Success)
                    debugReason = "Invalid username or timestamp!" + "\t\r\n" + RawMessage;
            }
            catch { debugReason = "Bade name: " + username; }

            return new Tuple<string, string, string>(timestamp, username, debugReason);
        }

        private static void ParseRivenImage()
        {
            var rp = new RivenParser(ClientLanguage.English);
            var outputDir = "debug_rivens";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            var sw = new Stopwatch();
            var rc = new RivenCleaner();
            foreach (var riven in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai").Where(f => f.Contains("497cab20-ac01-4a31-96ef-d68a4dd82638")))
            {
                sw.Restart();
                using (var b = new Bitmap(riven))
                {
                    var rivenImage = b;
                    if (b.Width == 4096)
                        rivenImage = rp.CropToRiven(b);
                    var b2 = rc.CleanRiven(rivenImage);
                    b2.Save("debug_riven.png");
                    var text = rp.ParseRivenTextFromImage(b2, null);
                    foreach (var modi in text.Modifiers)
                    {
                        if (!Modifier.PossibleDescriptions["zh"].Contains(modi.Description))
                        {
                            Console.WriteLine("Invalid modifier found!");
                        }
                    }
                    sw.Stop();
                    Console.WriteLine("Finished cleaning and parsing in: " + sw.Elapsed.TotalSeconds + " seconds.");
                    b2.Save(Path.Combine(outputDir, (new FileInfo(riven)).Name));
                    var textFileName = (new FileInfo(riven)).Name;
                    textFileName = Path.Combine(outputDir, textFileName.Substring(0, textFileName.LastIndexOf(".")) + ".json");
                    File.WriteAllText(textFileName, JsonConvert.SerializeObject(text));
                    if (rivenImage != b)
                        rivenImage.Dispose();
                    if (b2 != b)
                        b2.Dispose();
                }
            }
        }

        //private static void ParseChatImage()
        //{
        //    //var filePath = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs\error_blurry1.png";
        //    //foreach (var filePath in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai")
        //    //                            .Select(f => new FileInfo(f))
        //    //                            .Where(f => f.Name.StartsWith("637") && !f.Name.Contains("_white") && f.Name.EndsWith(".png"))
        //    //                            .Select(f => f.FullName))
        //    foreach (var filePath in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps").Where(f => f.EndsWith("_3.png")))
        //    {
        //        using (var bitmap = new Bitmap(filePath))
        //        {
        //            var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //            //ic.SaveSoftMask(filePath, "error_blurry1_white.png");
        //            ImageCleaner.SaveSoftMask(filePath, filePath.Replace(".png", "_white.png"));
        //            var sw = new Stopwatch();
        //            sw.Start();
        //            var lines = cp.ParseChatImage(bitmap);
        //            sw.Stop();
        //            var sb = new StringBuilder();
        //            try
        //            {
        //                var oldDebugs = Directory.GetFiles(Environment.CurrentDirectory).Select(f => new FileInfo(f)).Where(fi => fi.Name.StartsWith("debug_chat_line") && fi.Name.EndsWith(".png"));
        //                foreach (var item in oldDebugs)
        //                {
        //                    File.Delete(item.FullName);
        //                }
        //            }
        //            catch
        //            {

        //            }
        //            for (int i = 0; i < lines.Length; i++)
        //            {
        //                var line = lines[i];
        //                using (var b = new Bitmap(line.MessageBounds.Width, line.MessageBounds.Height))
        //                {
        //                    for (int x = 0; x < b.Width; x++)
        //                    {
        //                        for (int y = 0; y < b.Height; y++)
        //                        {
        //                            b.SetPixel(x, y, bitmap.GetPixel(line.MessageBounds.Left + x, line.MessageBounds.Top + y));
        //                        }
        //                    }
        //                    b.Save("debug_chat_line_" + i + ".png");
        //                }
        //                Console.WriteLine(line.RawMessage);
        //                sb.AppendLine(line.RawMessage);
        //            }
        //            File.WriteAllText(filePath.Replace(".png", ".txt"), sb.ToString());
        //        }
        //    }
        //}

        //private static void TestRedText()
        //{
        //    var input = @"C:\Users\david\OneDrive\Documents\WFChatParser\ErrorImages\Screenshot (175).png";
        //    ImageCleaner.SaveSoftMask(input, "test2.png");
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var lines = cp.ParseChatImage(new Bitmap(input), false, false, 50);
        //}

        private static void AsyncRivenParsingShim()
        {
            var images = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\")/*.SelectMany(f => new string[] { f, f, f, f, f, })*/.Where(f => f.EndsWith(".png")).ToArray();
            Console.WriteLine("Riven inputs: " + images.Length);
            var rc = new RivenCleaner();
            var rp = new RivenParser(ClientLanguage.English);
            var bitmaps = images.Select(f =>
            {
                var bitmap = new Bitmap(f);
                if (bitmap.Width == 4096)
                {
                    var cropped = rp.CropToRiven(bitmap);
                    bitmap.Dispose();
                    return cropped;
                }
                else
                    return bitmap;
            }).Select(b =>
            {
                var cleaned = rc.CleanRiven(b);
                return new { Cleaned = cleaned, Cropped = b };
            }).ToArray();

            //var sw = new Stopwatch();
            ////var serialRivens = new List<Riven>();
            ////sw.Start();
            ////foreach (var bitmap in bitmaps)
            ////{
            ////    var riven = rp.ParseRivenTextFromImage(bitmap.Cleaned, null);
            ////    riven.Rank = rp.ParseRivenRankFromColorImage(bitmap.Cropped);
            ////    riven.Polarity = rp.ParseRivenPolarityFromColorImage(bitmap.Cropped);
            ////    serialRivens.Add(riven);
            ////}
            ////sw.Stop();
            ////Console.WriteLine("Serial parse time: " + sw.Elapsed.TotalSeconds);

            //Console.WriteLine("starting parallel parse");
            //sw.Restart();
            //var tasks = bitmaps.Select(b =>
            //{
            //    var t = new Task<Riven>(() =>
            //    {
            //        using (var parser = new RivenParser())
            //        {
            //            var riven = parser.ParseRivenTextFromImage(b.Cleaned, null);
            //            riven.Polarity = parser.ParseRivenPolarityFromColorImage(b.Cropped);
            //            riven.Rank = parser.ParseRivenRankFromColorImage(b.Cropped);
            //            Console.WriteLine("Task finished");
            //            return riven;
            //        }
            //    });
            //    t.Start();
            //    return t;
            //}).ToArray();
            //Console.WriteLine("Starting sleep");
            //Thread.Sleep(28 * 1000);
            //Console.WriteLine("Sleep finished");
            //var done = Task.WhenAll(tasks).Result;
            //sw.Stop();
            //Console.WriteLine("Parallel parse time: " + sw.Elapsed.TotalSeconds);

            //var queue = new ConcurrentQueue<RivenParseTaskWorkItem>();
            //for (int i = 0; i < images.Length / 5; i++)
            //{
            //    var group = bitmaps.Skip(i * 5).Take(5);
            //    queue.Enqueue(new RivenParseTaskWorkItem()
            //    {
            //        Message = new ChatMessageModel(),
            //        RivenWorkDetails = group.Select(image => new RivenParseTaskWorkItemDetail() { RivenIndex = 0, RivenName = null, CroppedRivenBitmap = image.Cropped }).ToList()
            //    });
            //}

            //var thread = new Thread(() =>
            //{
            //    ChatRivenBot.ProcessRivenQueue(new CancellationToken(), new RivenParserFactory(), new DummySender(), queue, new RivenCleaner());
            //});

            //thread.Start();

            //while (queue.Count > 0)
            //    Thread.Sleep(1000);
        }

        private static void CredentialShim(string target)
        {
            IConfiguration config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("appsettings.development.json", true, true)
                 .AddJsonFile("appsettings.production.json", true, true)
                 .Build();

            var key = config["Credentials:Key"];
            var salt = config["Credentials:Salt"];


            Console.Write("Username: ");
            var username = Console.ReadLine();
            Console.Write("\r\nPassword: ");
            var password = Console.ReadLine();


            string encryptedPassword = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes(password);
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                encryptedPassword = Convert.ToBase64String(ms.ToArray());
            }

            string encryptedUsername = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes(username);
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                encryptedUsername = Convert.ToBase64String(ms.ToArray());
            }

            CredentialManager.SaveCredentials(target, new System.Net.NetworkCredential(encryptedUsername, encryptedPassword));
        }

        private static void GetCrednetials()
        {
            IConfiguration config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("appsettings.development.json", true, true)
                 .AddJsonFile("appsettings.production.json", true, true)
                 .Build();

            var key = config["Credentials:Key"];
            var salt = config["Credentials:Salt"];

            foreach (var i in config.GetSection("Launchers").GetChildren().AsEnumerable())
            {
                var section = config.GetSection(i.Path);

                var Password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value);
                var Username = GetUsername(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value);
                Console.WriteLine($"{Username} : {Password}");
            }
        }

        private static string GetPassword(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static string GetUsername(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.UserName);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        private static void PasswordShim(string key, string salt, string password)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes(password);
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                var encrypted = Convert.ToBase64String(ms.ToArray());

                CredentialManager.SaveCredentials("Test system", new System.Net.NetworkCredential("test username", encrypted));
            }


            var r = CredentialManager.GetCredentials("Test system", CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    var pass = Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        private static void WinOcrTest()
        {
            var rp = new RivenParser(ClientLanguage.English);
            var rc = new RivenCleaner();
            if (!Directory.Exists("riven_stuff"))
                Directory.CreateDirectory("riven_stuff");
            foreach (var error in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs").Select(f => new FileInfo(f)).Where(f => f.Name.EndsWith("13.png")))
            {
                using (var image = new Bitmap(error.FullName))
                {
                    Bitmap cropped = null;
                    if (image.Width == 4096)
                    {
                        cropped = rp.CropToRiven(image);
                        var ss = new ScreenStateHandler();
                        var state = ss.GetScreenState(image);
                        if (state != ScreenState.RivenWindow)
                            System.Diagnostics.Debugger.Break();
                    }
                    else
                        cropped = image;
                    using (var cleaned = rc.CleanRiven(cropped))
                    {
                        cleaned.Save(Path.Combine("riven_stuff", error.Name));
                        var result = rp.ParseRivenTextFromImage(cleaned, null);
                        result.Polarity = rp.ParseRivenPolarityFromColorImage(cropped);
                        result.Rank = rp.ParseRivenRankFromColorImage(cropped);
                        var sb = new StringBuilder();
                        sb.AppendLine(result.Name);
                        if (result.Modifiers != null)
                        {
                            foreach (var modi in result.Modifiers)
                            {
                                sb.AppendLine(modi.ToString());
                            }
                        }
                        sb.AppendLine(result.Drain.ToString());
                        sb.AppendLine(result.MasteryRank + " " + result.Rolls);
                        File.WriteAllText(Path.Combine("riven_stuff", error.Name.Replace(".png", ".txt")), sb.ToString());
                    }
                    cropped.Dispose();
                }
            }
        }

        private static string GetPassword(string key, string salt)
        {
            var r = CredentialManager.GetCredentials("WFChatBot", CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static ObsSettings GetObsSettings(string key, string salt)
        {
            var r = CredentialManager.GetCredentials("OBS", CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    var password = Encoding.UTF8.GetString(ms.ToArray());

                    ms.Seek(0, SeekOrigin.Begin);
                    ms.SetLength(0);
                    input = Convert.FromBase64String(r.UserName);
                    cs.Seek(0, SeekOrigin.Begin);
                    cs.SetLength(0);
                    cs.Write(input, 0, input.Length);
                    var url = Encoding.UTF8.GetString(ms.ToArray());
                    return new ObsSettings() { Url = url, Password = password };
                }
            }

            return null;
        }

        //private static void TestBot()
        //{
        //    IConfiguration config = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", true, true)
        //         .AddJsonFile("appsettings.development.json", true, true)
        //         .AddJsonFile("appsettings.production.json", true, true)
        //         .Build();

        //    var dataSender = new ClientWebsocketDataSender(new Uri(config["DataSender:HostName"]),
        //        config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
        //        config["DataSender:MessagePrefix"],
        //        config["DataSender:DebugMessagePrefix"],
        //        true,
        //        config["DataSender:RawMessagePrefix"],
        //        config["DataSender:RedtextMessagePrefix"],
        //        config["DataSender:RivenImageMessagePrefix"],
        //        config["DataSender:LogMessagePrefix"],
        //        config["DataSender:LogLineMessagePrefix"]);

        //    var pass = Console.ReadLine().Trim();
        //    PasswordShim(config["Credentials:Key"], config["Credentials:Salt"], pass);

        //    var password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"]);
        //    CancellationToken token = new System.Threading.CancellationToken();
        //    var gc = new GameCapture(new Application.Logger.Logger(dataSender, token));
        //    var obs = GetObsSettings(config["Credentials:Key"], config["Credentials:Salt"]);
        //    var logParser = new WarframeLogParser();
        //    var textParser = new AllTextParser(dataSender, logParser);
        //    var bot = new ChatRivenBot(config["LauncherPath"], new MouseHelper(),
        //        new ScreenStateHandler(),
        //        gc,
        //        obs,
        //        password,
        //        new KeyboardHelper(),
        //        new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish),
        //        dataSender,
        //        new RivenCleaner(),
        //        new RivenParserFactory(ClientLanguage.English),
        //        new Application.LogParser.RedTextParser(logParser));
        //    bot.AsyncRun(token);
        //}

        private static void testRivenSplit()
        {
            foreach (var error in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs").Select(f => new FileInfo(f)).Where(f => f.Name.StartsWith("test")))
            {
                using (var cropped = new Bitmap(error.FullName))
                {
                    var cleaner = new RivenCleaner();
                    var rp = new RivenParser(ClientLanguage.English);
                    //var cropped = rp.CropToRiven(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\error.png"));
                    using (var cleaned = cleaner.CleanRiven(cropped))
                    {
                        cleaned.Save("cleaned.png");
                        var r = rp.ParseRivenTextFromImage(cleaned, null);
                        Console.WriteLine(JsonConvert.SerializeObject(r, Formatting.Indented) + "\n");
                    }
                }
            }
            //var cropped = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\error.png");
            //var cleaner = new RivenCleaner();
            //var rp = new RivenParser(ClientLanguage.English);
            ////var cropped = rp.CropToRiven(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\error.png"));
            //var cleaned = cleaner.CleanRiven(cropped);
            //cleaned.Save("cleaned.png");
            //var r = rp.ParseRivenTextFromImage(cleaned, "Akj Cri-vex");

            ////var inputs = new string[] {"+2.6 Punch Through", "+78.7% Multishot", "-10.4% Puncture" };
            ////var modis = inputs.Select(i => Modifier.ParseString(i));
            ////Console.WriteLine(JsonConvert.SerializeObject(new { Modifiers = modis }));
        }

        private static bool PixelIsPurple(Point p, Bitmap bitmap)
        {
            var pixel = bitmap.GetPixel(p.X, p.Y);
            return pixel.R > 155 && pixel.G > 110 && pixel.B > 187;
        }
        private static Polarity GetPolarity(Bitmap croppedRiven)
        {
            //Polarity pixels
            var _dashPixels = new List<Point>();
            for (int x = 537; x < 537 + 3; x++)
            {
                for (int y = 23; y < 23 + 4; y++)
                {
                    _dashPixels.Add(new Point(x, y));
                }
            }
            for (int x = 560; x < 560 + 3; x++)
            {
                for (int y = 23; y < 23 + 4; y++)
                {
                    _dashPixels.Add(new Point(x, y));
                }
            }

            var _dPixels = new List<Point>();
            for (int x = 561; x < 561 + 4; x++)
            {
                for (int y = 32; y < 32 + 7; y++)
                {
                    _dPixels.Add(new Point(x, y));
                }
            }


            var _vPixels = new List<Point>();
            for (int x = 542; x < 542 + 1; x++)
            {
                for (int y = 29; y < 29 + 4; y++)
                {
                    _vPixels.Add(new Point(x, y));
                }
            }
            for (int x = 542; x < 542 + 2; x++)
            {
                for (int y = 18; y < 18 + 3; y++)
                {
                    _vPixels.Add(new Point(x, y));
                }
            }

            var dashMatches = _dashPixels.Count(p => PixelIsPurple(p, croppedRiven));
            var vMatches = _vPixels.Count(p => PixelIsPurple(p, croppedRiven));
            var dMatches = _dPixels.Count(p => PixelIsPurple(p, croppedRiven));

            if (dashMatches > _dashPixels.Count * 0.9)
                return Polarity.Naramon;
            else if (vMatches > _vPixels.Count * 0.9)
                return Polarity.Madurai;
            else if (dMatches > _dashPixels.Count * 0.9)
                return Polarity.Vazarin;
            else
                return Polarity.Unknown;
        }

        private static string ColorString(string input) => input.Pastel("#bea966").PastelBg("#162027");
        private static void UpdateUI()
        {
            var _UIMajorStep = "Test major step";
            var _UIMinorStep = "Test minor step";
            var _UIMessages = new List<String>(new string[] { "Test message 1", "Test message 2", "Test message 3" });
            var _UILastRiven = new Riven()
            {
                Name = "Test riven name",
                Drain = 50,
                MasteryRank = 25,
                Polarity = Polarity.Vazarin,
                Rank = 5,
                Rolls = 10
            };

            Console.SetWindowSize(1, 1);
            Console.SetBufferSize(147, 9);
            Console.SetWindowSize(147, 9);
            var maxWidth = Console.BufferWidth / 2;
            Console.CursorVisible = false;

            var background = " ".Pastel("#162027").PastelBg("#162027");
            for (int y = 0; y < Console.WindowHeight; y++)
            {
                for (int x = 0; x < Console.BufferWidth; x++)
                {
                    Console.SetCursorPosition(x, y);
                    Console.Write(background);
                }
            }
            Console.Clear();

            //Draw left side
            if (_UIMajorStep != null && _UIMajorStep.Length > 0)
            {
                Console.SetCursorPosition(0, 0);
                Console.Write(ColorString(_UIMajorStep.Substring(0, Math.Min(_UIMajorStep.Length, maxWidth))));
            }

            if (_UIMinorStep != null && _UIMinorStep.Length > 0)
            {
                Console.SetCursorPosition(0, 1);
                Console.Write(ColorString(_UIMinorStep.Substring(0, Math.Min(_UIMinorStep.Length, maxWidth))));
            }

            var line = 3;
            var messages = _UIMessages.Count > 3 ? _UIMessages.Skip(_UIMessages.Count - 3).ToList() : _UIMessages;
            foreach (var item in messages)
            {
                Console.SetCursorPosition(0, line);
                Console.WriteLine(ColorString(item.Substring(0, Math.Min(item.Length, maxWidth))));
                line++;
            }

            //Draw right side
            if (_UILastRiven != null)
            {
                if (_UILastRiven.Name != null && _UILastRiven.Name.Length > 0)
                {
                    Console.SetCursorPosition(maxWidth + 2, 0);
                    Console.Write(ColorString(_UILastRiven.Name.Substring(0, Math.Min(_UILastRiven.Name.Length, maxWidth))));
                }
                Console.SetCursorPosition(maxWidth + 2, 1);
                var input = "Polarity: " + _UILastRiven.Polarity;
                Console.Write(SafeColorString(maxWidth, input));
                input = "Rank: " + _UILastRiven.Rank;
                Console.SetCursorPosition(maxWidth + 2, 2);
                Console.Write(SafeColorString(maxWidth, input));
                input = "Mastery rank: " + _UILastRiven.MasteryRank;
                Console.SetCursorPosition(maxWidth + 2, 3);
                Console.Write(SafeColorString(maxWidth, input));
                input = "Rolls: " + _UILastRiven.Rolls;
                Console.SetCursorPosition(maxWidth + 2, 4);
                Console.Write(SafeColorString(maxWidth, input));
                line = 5;
                foreach (var modi in _UILastRiven.Modifiers)
                {
                    if (line >= Console.WindowHeight)
                        return;
                    Console.SetCursorPosition(maxWidth + 2, line);
                    Console.Write(SafeColorString(maxWidth, modi.ToString()));
                    line++;
                }
            }
        }

        private static string SafeColorString(int maxWidth, string input)
        {
            return ColorString(input.Substring(0, Math.Min(input.Length, maxWidth)));
        }

        private static void CLIUITests()
        {
            Console.BackgroundColor = ConsoleColor.DarkCyan;
            Console.Clear();
            var test = "test".Pastel("#bea966").PastelBg("#162027");
            Console.WriteLine(test);
            foreach (var color in Enum.GetValues(typeof(ConsoleColor)).Cast<ConsoleColor>())
            {
                Console.ForegroundColor = color;
                if (color == ConsoleColor.DarkCyan)
                    Console.BackgroundColor = ConsoleColor.White;
                else
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("  " + color.ToString() + "  ");
            }
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        private static void SetupFilters()
        {
            for (int i = 0; i < 5; i++)
            {
                Console.Write("\rSending a lot of ctr+v in " + (5 - i) + "...");
                System.Threading.Thread.Sleep(1000);
            }

            var mouse = new MouseHelper();
            var filters = File.ReadAllLines("Affixcombos.txt");
            TextCopy.Clipboard.SetText(filters.First());
            mouse.Click(33, 678);
            System.Threading.Thread.Sleep(100);
            foreach (var filter in filters)
            {
                TextCopy.Clipboard.SetText(filter);

                mouse.Click(2315, 804);
                System.Threading.Thread.Sleep(66);

                mouse.Click(1574, 811);
                System.Threading.Thread.Sleep(66);

                uint KEYEVENTF_KEYUP = 2;
                byte VK_CONTROL = 0x11;
                keybd_event(VK_CONTROL, 0, 0, 0);
                System.Threading.Thread.Sleep(66);
                keybd_event(0x56, 0, 0, 0);
                System.Threading.Thread.Sleep(66);

                keybd_event(0x56, 0, KEYEVENTF_KEYUP, 0);
                System.Threading.Thread.Sleep(66);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);// 'Left Control Up
                System.Threading.Thread.Sleep(66);

                mouse.Click(2424, 805);
                System.Threading.Thread.Sleep(200);
            }
        }

        //private static void TestCanExit()
        //{
        //    var fullImage = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\Screenshot (117).png");
        //    var ss = new ScreenStateHandler();
        //    var isExitable = ss.IsExitable(fullImage);
        //    fullImage.Dispose();

        //    var chatIcon = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chaticon.png");
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var isChat = cp.IsChatFocused(chatIcon);
        //}

        private static void TestRivenParsing()
        {
            var rp = new RivenParser(ClientLanguage.English);
            var bads = new List<string>();
            //foreach (var name in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs").Where(f => f.EndsWith(".png")))
            //var badIds = new HashSet<string>(File.ReadAllLines(@"C:\users\david\Downloads\bad_rivens.txt"));
            //var files = badIds.Select(f => @"\\desktop-3414ubq\Warframes\Bot Client\riven_images\" + f + ".png").Skip(1311).ToArray();
            //var files = Directory.GetFiles(@"\\desktop-3414ubq\Warframes\Bot Client\riven_images\").Where(f => f.EndsWith(".png")).Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTime).Select(f => f.FullName).ToArray();
            var files = new string[] { @"C:\Users\david\Downloads\riven_test.png" };
            var sw = new Stopwatch();
            var times = new List<double>();
            Console.WindowHeight = 10;
            Console.BufferHeight = 10;
            var empty = "\r" + new string((new Char[Console.BufferWidth - 1]).Select(_ => ' ').ToArray());
            var errors = 0;
            using (var fout = new StreamWriter("errors.txt"))
            {
                var rand = new System.Random();
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    if (!File.Exists(files[fileIndex]))
                        continue;

                    var name = files[fileIndex];
                    sw.Restart();
                    var cropped = new Bitmap(name);
                    if (cropped.Width > 600)
                        cropped = rp.CropToRiven(cropped);
                    //var cropped = rp.CropToRiven(bitmap);
                    cropped.Save("cropped.png");
                    //bitmap.Dispose();
                    var rc = new RivenCleaner();
                    var clean = rc.CleanRiven(cropped);
                    //var resizeDown = new Bitmap(cropped, new Size(cropped.Width / 2, cropped.Height / 2));
                    //var resizeUp = new Bitmap(resizeDown, new Size(cropped.Width, cropped.Height));
                    //var clean = resizeUp;
                    clean.Save("clean.png");
                    //Console.WriteLine(name);
                    var rivens = new List<Application.ChatMessages.Model.Riven>();
                    var scales = 1;
                    for (int i = 1; i <= scales; i++)
                    {
                        //var factor = rand.NextDouble() * 0.5f + 0.5f;
                        var factor = (1.0 / scales) * i;
                        using (var cleanedScaledDown = new Bitmap(clean, new Size((int)(clean.Width * factor), (int)(clean.Height * factor))))
                        {
                            using (var cleanedScaledBack = new Bitmap(cleanedScaledDown, new Size(clean.Width, clean.Height)))
                            {
                                cleanedScaledBack.Save("clean_scaled_" + i + ".png");
                                var parsedRiven = rp.ParseRivenTextFromImage(cleanedScaledBack, null);
                                var imageParseSW = new Stopwatch();
                                imageParseSW.Start();
                                rivens.Add(parsedRiven);
                            }
                        }
                    }
                    var combineSW = new Stopwatch();
                    combineSW.Start();
                    var riven = new Riven();
                    try
                    {
                        riven.Drain = rivens.Select(p => p.Drain).Where(d => d >= 0).GroupBy(d => d).OrderByDescending(g => g.Count()).Select(g => g.Key).First();
                        riven.ImageId = Guid.NewGuid();
                        riven.MasteryRank = rivens.Select(p => p.MasteryRank).Where(mr => mr >= 0).GroupBy(mr => mr).OrderByDescending(g => g.Count()).Select(g => g.Key).First();
                        riven.Modifiers = rivens.Select(p => p.Modifiers).GroupBy(ms => ms.Aggregate("", (key, m) => key + $"{m.Curse}{m.Description}{m.Value}")).OrderByDescending(g => g.Count()).Select(g => g.First()).First();
                        riven.Rolls = rivens.Select(p => p.Rolls).Where(rolls => rolls >= 0).GroupBy(rolls => rolls).OrderByDescending(g => g.Count()).Select(g => g.Key).First();
                    }
                    catch { }
                    riven.Polarity = rp.ParseRivenPolarityFromColorImage(cropped);
                    riven.Rank = rp.ParseRivenRankFromColorImage(cropped);
                    var guid = name.Substring(name.LastIndexOf("\\") + 1);
                    guid = guid.Replace(".png", "");
                    try
                    {
                        riven.ImageId = Guid.Parse(guid);
                    }
                    catch { }
                    //Console.WriteLine(JsonConvert.SerializeObject(result));
                    cropped.Dispose();
                    times.Add(sw.Elapsed.TotalSeconds);
                    Console.Write(empty);
                    Console.Write("\rFinished in: " + sw.Elapsed.TotalSeconds + "s. Average: " + times.Average() + "s.\nTime left: " + ((files.Length - fileIndex) * times.Average() / 60) + " minutes. Checked " + (fileIndex + 1) + " of " + files.Length + ". Errors: " + errors);
                    var errorReason = DoesRivenHaveError(riven);
                    if (errorReason.Length > 0)
                    {
                        var json = JsonConvert.SerializeObject(riven);
                        bads.Add(json);
                        Console.WriteLine("\n" + errorReason);
                        fout.WriteLine("File: " + name + "\nReason: " + errorReason + "\n" + json);
                        fout.Flush();
                        Console.WriteLine(json);
                        //Debugger.Break();

                        var images = Directory.GetFiles(Environment.CurrentDirectory).Where(f => f.Contains("clean_scaled_") && f.EndsWith(".png"))
                            .Select(f => new FileInfo(f)).Where(f => f.Name.StartsWith("clean_scaled_"))
                            .Select(f => new Bitmap(f.FullName)).ToArray();
                        var width = images.Aggregate(0, (prod, next) => prod + next.Width);
                        using (var combinedCleaned = new Bitmap(width, clean.Height))
                        {
                            var offset = 0;
                            for (int i = 0; i < images.Length; i++)
                            {
                                for (int x = 0; x < images[i].Width; x++)
                                {
                                    for (int y = 0; y < images[i].Height && y < clean.Height; y++)
                                    {
                                        combinedCleaned.SetPixel(offset + x, y, images[i].GetPixel(x, y));
                                    }
                                }
                                offset += images[i].Width;
                            }
                            combinedCleaned.Save("combined_cleaned.png");
                        }
                        foreach (var image in images)
                        {
                            image.Dispose();
                        }
                        errors++;
                    }

                    Console.WriteLine("\n" + Newtonsoft.Json.JsonConvert.SerializeObject(riven));
                }
            }
        }

        private static string DoesRivenHaveError(Riven result)
        {
            if (result.Drain < 0)
            {
                return "Bad drain";

            }
            else if (result.MasteryRank < 0)
            {
                return "Bad mastery rank";

            }
            else if (result.Rolls < 0)
            {
                return "Bad rolls [missing]";

            }
            else if (result.Rolls > 255)
                return "Bad rolls [high]";
            else if (result.Polarity == Polarity.Unknown)
            {
                return "Bad polarity";

            }
            else if (result.Modifiers.Any(m => m.Value == 0))
            {
                return "Bad modifier value";

            }
            else if (result.Modifiers.Length > 4)
            {
                return "Bad modifier count [high]";

            }
            else if (result.Modifiers.Length < 2)
                return "Bad modifier count [low]";
            else if (result.Modifiers.Where(m => !m.Curse).Count() > 3)
                return "To many buffs";

            return "";
        }

        //private static void VisualizeClickpoints()
        //{
        //    var cp = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var r = cp.ParseChatImage(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\bad.png"));
        //    var list = new CoordinateList();
        //    r.Where(r1 => r1 is ChatMessageLineResult).Cast<ChatMessageLineResult>().SelectMany(r1 => r1.ClickPoints).ToList().ForEach(p => list.Add(p.X, p.Y));
        //    var ic = new ImageCleaner();
        //    ic.SaveClickMarkers(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\bad.png",
        //        Path.Combine(outputDir, "bad_clicks.png"),
        //        list);
        //}

        private static void TestScreenHandler()
        {
            var c = new GameCapture(new DummyLogger());
            var ss = new ScreenStateHandler();

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\132606924224963827.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is main menu: {state == ScreenState.MainMenu} should be true.");
            }
            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\reward_1.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is reward: {ss.GetScreenState(b) == ScreenState.DailyRewardScreenItem} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_login.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is Login: {ss.GetScreenState(b) == ScreenState.LoginScreen} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_main_menu.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is main menu: {ss.GetScreenState(b) == ScreenState.MainMenu} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_profile_menu.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is profile menu: {ss.GetScreenState(b) == ScreenState.ProfileMenu} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_glyph_screen_no_filter.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine($"Is glyph screen: {ss.GetScreenState(b) == ScreenState.GlyphWindow} should be true. Chat open: {ss.IsChatOpen(b)} should be false. Chat collapsed {ss.IsChatCollapsed(b)} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_glyph_screen_no_filter.png"))
            {
                Console.WriteLine($"Are filters present in chinese glyph screen {ss.GlyphFiltersPresent(b)} should be false.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chinese_glyph_with_filters.png"))
            {
                Console.WriteLine($"Are filters present in chinese glyph screen {ss.GlyphFiltersPresent(b)} should be true.");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\new_glyph_1.png"))
            {
                var state = ss.GetScreenState(b);
                Console.WriteLine("Is GlyphWindow: " + (ss.GetScreenState(b) == (ScreenState.GlyphWindow)) + " should be true. Is chat open: " + ss.IsChatOpen(b) + " should be false");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\new_glyph_1.png"))
            {
                var isLoading = ss.GetScreenState(b);
                Console.WriteLine("Is GlyphWindow: " + (ss.GetScreenState(b) == (ScreenState.GlyphWindow)) + " should be true. Is chat open: " + ss.IsChatOpen(b) + " should be false");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\loading.png"))
            {
                var isLoading = ss.GetScreenState(b) == ScreenState.LoadingScreen;
                Console.WriteLine("Is loading: " + isLoading + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\loading_nobar.png"))
            {
                var isLoading = ss.GetScreenState(b) == ScreenState.LoadingScreen;
                Console.WriteLine("Is loading: " + isLoading + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\login.png"))
            {
                var isLogin = ss.GetScreenState(b) == ScreenState.LoginScreen;
                Console.WriteLine("Is login: " + isLogin + " should be true");
            }

            Console.WriteLine("MISSING TEST DATA FOR NORMAL CLAIM REWARDS SCREEN");

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\plat_claim.png"))
            {
                var isPlat = ss.GetScreenState(b) == ScreenState.DailyRewardScreenPlat;
                Console.WriteLine("Is plat: " + isPlat + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\warframe_pilot1.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe1: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\warframe_pilot2.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe2: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\warframe_pilot3.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe3: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\warframe_pilot4.png"))
            {
                var isPiloting = ss.GetScreenState(b);
                Console.WriteLine("Is piloting warframe4: " + isPiloting + " should be " + ScreenState.ControllingWarframe.ToString() + ". " + (isPiloting == ScreenState.ControllingWarframe));
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\main_menu.png"))
            {
                var isMainMenu = ss.GetScreenState(b) == ScreenState.MainMenu;
                Console.WriteLine("Is main menu: " + isMainMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\main_menu2.png"))
            {
                var isMainMenu = ss.GetScreenState(b) == ScreenState.MainMenu;
                Console.WriteLine("Is main menu2: " + isMainMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\profile_menu.png"))
            {
                var isProfileMenu = ss.GetScreenState(b) == ScreenState.ProfileMenu;
                Console.WriteLine("Is isProfileMenu: " + isProfileMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\glyph_window.png"))
            {
                var isGlyphWindow = ss.GetScreenState(b) == ScreenState.GlyphWindow;
                Console.WriteLine("Is glyph window: " + isGlyphWindow + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\glyph_without_filters.png"))
            {
                var isNoFilters = !ss.GlyphFiltersPresent(b);
                Console.WriteLine("Is glyph filters empty: " + isNoFilters + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\glyph_with_filters.png"))
            {
                var isFilters = ss.GlyphFiltersPresent(b);
                Console.WriteLine("Is glyph filters: " + isFilters + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chat_collapsed.png"))
            {
                var isChatCollapsed = ss.IsChatCollapsed(b);
                Console.WriteLine("Is chat closed: " + isChatCollapsed + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\chat_collapsed2.png"))
            {
                var isChatCollapsed = ss.IsChatCollapsed(b);
                Console.WriteLine("Is chat closed2: " + isChatCollapsed + " should be true");
            }

            using (Bitmap b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chat.png"))
            {
                var isChat = ss.IsChatOpen(b);
                Console.WriteLine("Is chat open: " + isChat + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\riven.png"))
            {
                var isRiven = ss.GetScreenState(b) == ScreenState.RivenWindow;
                Console.WriteLine("Is riven: " + isRiven + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\inbox_open.png"))
            {
                var isExitable = ss.IsExitable(b);
                Console.WriteLine("Is inbox closeable: " + isExitable + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\hotfix_prompt.png"))
            {
                var isPrompt = ss.IsPromptOpen(b);
                Console.WriteLine("Is hotfix prompt open: " + isPrompt + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\fake_hotfix_prompt.png"))
            {
                var isPrompt = ss.IsPromptOpen(b);
                Console.WriteLine("Is fake hotfix prompt open: " + isPrompt + " should be true");
            }
            //glyph_no_chat.png
            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\glyph_no_chat.png"))
            {
                var isChatCollapsed = ss.IsChatCollapsed(b);
                Console.WriteLine("Is chat collapsed icon found: " + isChatCollapsed + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\old_login_reward.png"))
            {
                var isDailyItemReward = ss.GetScreenState(b) == ScreenState.DailyRewardScreenItem;
                Console.WriteLine("Is daily item reward: " + isDailyItemReward + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\unknown_login_reward.png"))
            {
                var isDailyItemReward = ss.GetScreenState(b) == ScreenState.DailyRewardScreenItem;
                Console.WriteLine("Is daily item reward2: " + isDailyItemReward + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Screen States\login_reward_item.png"))
            {
                var isDailyItemReward = ss.GetScreenState(b) == ScreenState.DailyRewardScreenItem;
                Console.WriteLine("Is daily item reward3: " + isDailyItemReward + " should be true");
            }
        }

        //private static void TestRivenStuff()
        //{
        //    var c = new GameCapture(new DummyLogger());
        //    var rp = new RivenParser(ClientLanguage.English);
        //    var ss = new ScreenStateHandler();

        //    var image = "test.png";
        //    var b = c.GetFullImage();
        //    b.Save("test.png");
        //    b.Dispose();

        //    var p = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var results = p.ParseChatImage(new Bitmap(image), true, true, 27).Where(r => r is ChatMessageLineResult).Cast<ChatMessageLineResult>();

        //    var clean = new ImageCleaner();
        //    var coords = new CoordinateList();
        //    results.SelectMany(r => r.ClickPoints).ToList().ForEach(i => coords.Add(i.X, i.Y));
        //    clean.SaveClickMarkers("test.png", "test_marked.png", coords);

        //    var mouse = new MouseHelper();

        //    var index = 0;
        //    var sw = new Stopwatch();
        //    foreach (var clr in results.Where(r => r is ChatMessageLineResult).Cast<ChatMessageLineResult>())
        //    {
        //        foreach (var click in clr.ClickPoints)
        //        {
        //            b = c.GetFullImage();
        //            if (ss.IsChatOpen(b))
        //            {
        //                //Hover over riven
        //                System.Threading.Thread.Sleep(17);
        //                mouse.MoveTo(click.X, click.Y);

        //                //Click riven
        //                System.Threading.Thread.Sleep(17);
        //                mouse.Click(click.X, click.Y);
        //                System.Threading.Thread.Sleep(17);
        //            }

        //            //Move mouse out of the way
        //            mouse.MoveTo(0, 0);
        //            sw.Restart();
        //            var tries = 0;
        //            while (true)
        //            {
        //                try
        //                {
        //                    var bitmap2 = c.GetFullImage();
        //                    if (ss.GetScreenState(bitmap2) == ScreenState.RivenWindow)
        //                    {
        //                        var crop = rp.CropToRiven(bitmap2);
        //                        crop.Save(index.ToString() + ".png");
        //                        crop.Dispose();
        //                        bitmap2.Dispose();
        //                        break;
        //                    }
        //                    bitmap2.Dispose();
        //                }
        //                catch { }
        //                tries++;
        //                if (tries > 15)
        //                {
        //                    Console.WriteLine("Riven not detected! Abort!");
        //                    break;
        //                }
        //            }
        //            Console.WriteLine("Got \"riven\" in " + sw.Elapsed.TotalSeconds + " seconds");

        //            //Hover over exit
        //            System.Threading.Thread.Sleep(33);
        //            mouse.MoveTo(3816, 2013);

        //            //Click exit
        //            var bitmap = c.GetFullImage();
        //            if (ss.GetScreenState(bitmap) == ScreenState.RivenWindow)
        //            {
        //                System.Threading.Thread.Sleep(17);
        //                mouse.Click(3816, 2013);
        //                System.Threading.Thread.Sleep(17);
        //            }
        //            bitmap.Dispose();

        //            //Move mouse out of the way
        //            System.Threading.Thread.Sleep(17);
        //            mouse.MoveTo(0, 0);

        //            System.Threading.Thread.Sleep(17);
        //            index++;
        //        }
        //    }
        //    //c.Dispose();
        //}
        private static void SimulateParseRiven()
        {
            var rc = new RivenCleaner();
            var rp = new RivenParser(ClientLanguage.English);
            //const string image = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\input.png";
            //var srcBitmap = new Bitmap(image);
            //var bitmap = srcBitmap.Clone(new Rectangle(1757, 463, 582, 831), PixelFormat.DontCare);
            const string imageWhite = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\b2ef63f6434cae7af57a011a1e6645c1_0.png";
            //bitmap.Save(imageWhite);
            //srcBitmap.Dispose();
            //bitmap.Dispose();
            //rc.CleanRiven(imageWhite);
            //var riven = rp.ParseRivenImage(imageWhite);
        }

        //private static void PrepareRivens()
        //{
        //    var r = new RivenCleaner();
        //    var p = new RivenParser(ClientLanguage.English);

        //    var totalSw = new Stopwatch();
        //    var opSw = new Stopwatch();
        //    var rivens = new List<Riven>();
        //    foreach (var riven in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\").Where(f => !f.EndsWith("_white.png") && f.EndsWith(".png")))
        //    {
        //        Console.WriteLine("\n" + riven.Substring(riven.LastIndexOf("\\") + 1));
        //        totalSw.Restart();
        //        opSw.Restart();
        //        r.PrepareRivenFromFullscreenImage(riven, riven + "_white.png");
        //        Console.WriteLine("cleanup: " + opSw.Elapsed.TotalSeconds + " seconds");
        //        opSw.Restart();
        //        var result = p.ParseRivenImage(riven + "_white.png");
        //        rivens.Add(result);
        //        Console.WriteLine("Parsed: " + opSw.Elapsed.TotalSeconds + " seconds");
        //        opSw.Restart();
        //        Console.WriteLine(JsonConvert.SerializeObject(result));
        //        Console.WriteLine("Total: " + totalSw.Elapsed.TotalSeconds + " seconds");
        //    }

        //    Console.WriteLine("\n");
        //    Console.WriteLine(JsonConvert.SerializeObject(rivens));
        //}

        //private static void FixImages()
        //{
        //    var cleaner = new ImageCleaner();
        //    cleaner.SaveChatColors(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png");
        //    var p = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //    var r = p.ParseChatImage(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png"));
        //    foreach (var line in r)
        //    {
        //        Console.WriteLine(line.RawMessage);
        //    }
        //}

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

        //private static void TrainSpacesOnImages()
        //{
        //    var spaceTrainer = new OCRSpaceTrainer();
        //    spaceTrainer.TrainOnImages(@"C:\Users\david\OneDrive\Documents\WFChatParser\Training Images", "newnewdata", GetSupportedCharacters().ToCharArray());
        //}

        //private static void TrainOnImages()
        //{
        //    var sourceDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Spaces";
        //    var outputDir = "newEnglishData3";
        //    var trainer = new OCRTrainer();
        //    trainer.TrainOnImages(sourceDir, outputDir);

        //    var spaceTrainer = new OCRSpaceTrainer();
        //    spaceTrainer.TrainOnImages(sourceDir, outputDir, GetSupportedCharacters().ToCharArray());
        //}

        //private static void FindOverlappingLines()
        //{
        //    const string sourceDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps";
        //    //if (!Directory.Exists("overlaps"))
        //    //    Directory.CreateDirectory("overlaps");
        //    //foreach (var image in Directory.GetFiles(sourceDir).Where(f => f.EndsWith(".png")))
        //    //{
        //    //    ImageCleaner.SaveSoftMask(image, Path.Combine("overlaps", (new FileInfo(image)).Name));
        //    //}
        //    OverlapDetector.DetectOverlaps(sourceDir, DataHelper.OcrDataPathEnglish);
        //}

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
        //    Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
        //    //Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Char Spacing\").Where(f => f.EndsWith(".png")).ToList().ForEach(f => trainingImages.Add(f));
        //    var trainingText = new List<string>();
        //    Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\New English\Overlaps").Where(f => f.EndsWith(".txt")).ToList().ForEach(f => trainingText.Add(f));
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

        //        var c = new ChatParser(new FakeLogger(), DataHelper.OcrDataPathEnglish);
        //        var cleaner = new ImageCleaner();
        //        cleaner.SaveChatColors(masterKeyFile, Path.Combine(outputDir, (new FileInfo(masterKeyFile)).Name));

        //        var sw = new Stopwatch();
        //        sw.Restart();
        //        //var fullResults = c.ParseChatImage(new Bitmap(masterKeyFile), xOffset, false, false);
        //        var lines = NewChatParsingShim(masterKeyFile);
        //        var fullResults = lines.Select(l => new ChatMessageLineResult() { RawMessage = l });
        //        //var fullResults = NewChatParsingShim(masterKeyFile);

        //        var m = fullResults.OfType<ChatMessageLineResult>().Select(line => MakeChatModel(line)).ToArray();
        //        var m2 = fullResults.Select(line => GetUsername(line.RawMessage)).ToArray();
        //        var allThere = !m.Select(model => m2.Any(old => old.Item2 == model.Author)).Any(b => !b);
        //        var allThere2 = !m2.Select(old => m.Any(model => model.Author == old.Item2)).Any(b => !b);
        //        if (!allThere || !allThere2)
        //            Debugger.Break();
        //        var newE = m.Select(model => model.EnhancedMessage);
        //        var oldE = fullResults.Select(line => line.RawMessage.Substring(5).Substring(line.RawMessage.Substring(5).IndexOf(":") + 1).Trim());

        //        var result = fullResults.Select(i => i.RawMessage.Trim()).ToArray();
        //        Console.WriteLine("Parsed in: " + sw.Elapsed.TotalSeconds + " seconds");
        //        sw.Stop();

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
        //                if (Enumerable.SequenceEqual(correctResults[i], result[i]))
        //                {
        //                    Console.WriteLine("They match!");
        //                }
        //            }
        //            if (!String.Equals(correctResults[i].Trim(), result[i]))
        //            {
        //                if (verboseLevel >= 2)
        //                {
        //                    if (correctResults[i].Length == result[i].Length)
        //                    {
        //                        for (int j = 0; j < correctResults[i].Length; j++)
        //                        {
        //                            if (result[i][j] != correctResults[i][j])
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

        private class GeneratedPair
        {
            public char Left { get; set; }
            public char Right { get; set; }
        }

        private static void GenerateCharStrings(int count = 35)
        {
            string chars = GetSupportedCharacters();

            var pairs = new List<GeneratedPair>();
            for (int i = 0; i < chars.Length; i++)
            {
                var prefix = chars[i];
                for (int j = 0; j < chars.Length; j++)
                {
                    pairs.Add(new GeneratedPair() { Left = prefix, Right = chars[j] });
                }
            }

            //Add missing (char) [
            //] [ must be the first item
            pairs.Add(new GeneratedPair() { Left = ']', Right = '[' });
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ']')
                    continue;

                var suffix = '[';
                pairs.Add(new GeneratedPair() { Left = chars[i], Right = suffix });
            }

            //Add missing [ (char)
            for (int i = 0; i < chars.Length; i++)
            {
                var prefix = '[';
                if (chars[i] == ']')
                    continue;
                pairs.Add(new GeneratedPair() { Left = prefix, Right = chars[i] });
            }


            SaveSafeOutputToFile(GetSafeOutputFromPairs(pairs, " "), "space_atlas.txt", "space_slice");
            //using (var atlas = new StreamWriter("space_atlas.txt"))
            //{
            //    var slice = 0;
            //    var lines = 0;
            //    string str = GetSafeOutputFromPairs(pairs, ' ');
            //    var sliceContents = new List<string>();
            //    while (str.Length > 80)
            //    {
            //        string line = str.Substring(0, 80);
            //        Console.WriteLine(line);
            //        atlas.WriteLine(line);
            //        sliceContents.Add(line);
            //        lines++;
            //        if (sliceContents.Count >= 27)
            //        {
            //            SaveSlice(slice++, sliceContents, "space_slice");
            //            sliceContents.Clear();
            //        }
            //        str = str.Substring(80);
            //    }
            //    if (str.Length > 0)
            //    {
            //        Console.WriteLine(str);
            //        atlas.WriteLine(str);
            //        sliceContents.Add(str);
            //        lines++;
            //        SaveSlice(slice++, sliceContents, "space_slice");
            //    }
            //}

            //Generate two character combos for overlap detection
            SaveSafeOutputToFile(GetSafeOutputFromPairs(pairs, string.Empty), "overlaps.txt", "overlap_slice");
        }

        private static void SaveSafeOutputToFile(string safeOutput, string outputFileName, string slicePrefix)
        {
            var lines = safeOutput.Split(new char[] { '\n' });
            var groupCount = lines.Length / 27;
            if (lines.Length % 27 != 0)
                groupCount++;
            for (int i = 0; i < groupCount; i++)
            {
                var startIndex = i * 27;
                var count = 27;
                if (startIndex + count >= lines.Length)
                    count = lines.Length - startIndex;
                SaveSlice(i, lines.Skip(startIndex).Take(count).Select(str => str.Trim()), slicePrefix);
            }

            File.WriteAllLines(outputFileName, lines.Select(str => str.Trim()));
        }

        private static string GetSafeOutputFromPairs(List<GeneratedPair> pairs, string seperator)
        {
            var lineCount = 0;
            var sb = new StringBuilder();
            foreach (var pair in pairs)
            {
                var subStr = $"{pair.Left}{seperator}{pair.Right} ";
                if (lineCount + subStr.Length > 80)
                {
                    sb.AppendLine();
                    if (subStr.StartsWith("!") || subStr.StartsWith("/"))
                        subStr = ". " + subStr;
                    lineCount = subStr.Length;
                }
                else
                {
                    if (lineCount == 0 && (subStr.StartsWith("!") || subStr.StartsWith("/")))
                        subStr = ". " + subStr;
                    lineCount += subStr.Length;
                }
                sb.Append(subStr);
            }

            return sb.ToString();
        }

        private static string GetSupportedCharacters()
        {
            return "~ ! # $ % & ' ( ) * + - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ,".Replace(" ", "");
        }

        private static void SaveSlice(int sliceNumber, IEnumerable<string> sliceContents, string sliceType)
        {
            var output = sliceContents;
            if (sliceContents.Count() == 27)
                output = sliceContents.Prepend("CLEAR").Append("CLEAR");
            else
            {
                var emptyNeeded = 27 - sliceContents.Count();
                var newOutput = new List<string>(sliceContents);
                for (int i = 0; i < emptyNeeded; i++)
                {
                    newOutput.Add("EMPTY");
                }
                output = newOutput.Prepend("CLEAR").Append("CLEAR");
            }
            File.WriteAllLines(sliceType.TrimEnd('_') + "_" + sliceNumber.ToString() + ".txt", output);
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

    internal class DummyLogger : ILogger
    {
        private bool _output;

        public DummyLogger(bool sendToConsole)
        {
            _output = sendToConsole;
        }
        public DummyLogger()
        {
            _output = false;
        }
        public void Log(string message, bool writeToConsole = true, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (_output)
                Console.WriteLine(message);
        }
    }

    class DummySender : IDataTxRx
    {
        public event EventHandler<ProfileRequest> ProfileParseRequest;

        public async Task AsyncSendChatMessage(ChatMessageModel message)
        {
        }

        public async Task AsyncSendDebugMessage(string message)
        {
            Console.WriteLine(message);
        }

        public async Task AsyncSendLogLine(LogMessage message)
        {
        }

        public async Task AsyncSendLogMessage(string message)
        {
        }

        public async Task AsyncSendProfileData(Profile profile, string target, string command)
        {
            string json = JsonConvert.SerializeObject(profile);
            Console.WriteLine(json);
            File.WriteAllText(Path.Combine("debug", "profiles", profile.Name, $"{profile.Name}.json"), json);
        }

        public async Task AsyncSendProfileRequestAck(ProfileRequest request, int queueSize)
        {
        }

        public async Task AsyncSendRedtext(string rawMessage)
        {
        }

        public async Task AsyncSendRedtext(RedTextMessage message)
        {
        }

        public async Task AsyncSendRivenImage(Guid imageID, Bitmap image)
        {
        }

        public async Task AsyncSendRivenImage(Guid imageID, string rivenBase64)
        {
        }
    }

    class DummyParser : ILineParser
    {
        public void Dispose()
        {

        }

        public string ParseLine(Bitmap bitmap)
        {
            return "Fake news";
        }
    }

    class DummyParserFactory : ILineParserFactory
    {
        public ILineParser CreateParser(ClientLanguage language)
        {
            return new DummyParser();
        }
    }
}
