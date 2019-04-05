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

            //ParseChatImage();
            //GenerateCharStrings(150);
            //VerifyNoErrors(2);
            //WinOcrTest();
            //AsyncRivenParsingShim();
            //TestScreenHandler();
            TestBot();
        }

        private static void ParseChatImage()
        {
            using (var bitmap = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs\error_blurry1.png"))
            {
                var cp = new ChatParser();
                var lines = cp.ParseChatImage(bitmap);
                foreach (var line in lines)
                {
                    Console.WriteLine(line.RawMessage);
                }
            }
        }

        private static void TestRedText()
        {
            var input = @"C:\Users\david\OneDrive\Documents\WFChatParser\ErrorImages\Screenshot (175).png";
            var cleaner = new ImageCleaner();
            cleaner.SaveChatColors(input, "test.png");
            cleaner.SaveSoftMask(input, "test2.png");
            var cp = new ChatParser();
            var lines = cp.ParseChatImage(new Bitmap(input), false, false, 50);
        }

        private static void AsyncRivenParsingShim()
        {
            var images = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\")/*.SelectMany(f => new string[] { f, f, f, f, f, })*/.Where(f => f.EndsWith(".png")).ToArray();
            Console.WriteLine("Riven inputs: " + images.Length);
            var rc = new RivenCleaner();
            var rp = new RivenParser();
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

            var queue = new ConcurrentQueue<RivenParseTaskWorkItem>();
            for (int i = 0; i < images.Length / 5; i++)
            {
                var group = bitmaps.Skip(i * 5).Take(5);
                queue.Enqueue(new RivenParseTaskWorkItem()
                {
                    Model = new ChatMessageModel(),
                    RivenWorkDetails = group.Select(image => new RivenParseTaskWorkItemDetails() { RivenIndex = 0, RivenName = null, CroppedRivenBitmap = image.Cropped }).ToList()
                });
            }

            var thread = new Thread(() =>
            {
                ChatRivenBot.ProcessRivenQueue(new CancellationToken(), new RivenParserFactory(), new DummySender(), queue, new RivenCleaner());
            });

            thread.Start();

            while (queue.Count > 0)
                Thread.Sleep(1000);
        }

        private static void PasswordShim()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes("xizuD7FLFSLPadxsIMqK", new byte[] { 0x32, 0x31, 0x37, 0x35, 0x32, 0x63, 0x31, 0x35, 0x61, 0x37 });
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var pass = Encoding.UTF8.GetBytes("password");
                    cs.Write(pass, 0, pass.Length);
                    cs.Close();
                }
                var encrypted = Convert.ToBase64String(ms.ToArray());

                CredentialManager.SaveCredentials("Test system", new System.Net.NetworkCredential("test username", encrypted));
            }


            var r = CredentialManager.GetCredentials("test system", CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes("xizuD7FLFSLPadxsIMqK", new byte[] { 0x32, 0x31, 0x37, 0x35, 0x32, 0x63, 0x31, 0x35, 0x61, 0x37 });
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
            var rp = new RivenParser();
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
        private static void TestBot()
        {
            IConfiguration config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", true, true)
                 .AddJsonFile("appsettings.development.json", true, true)
                 .AddJsonFile("appsettings.production.json", true, true)
                 .Build();

            var dataSender = new DataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"]);
            
            var password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"]);
            var gc = new GameCapture();
            //var obs = new ObsSettings() { Url = "ws://localhost:4444/", Password = "password123" };
            var bot = new ChatRivenBot(@"C:\Users\david\AppData\Local\Warframe\Downloaded\Public\Tools\Launcher.exe", new MouseHelper(),
                new ScreenStateHandler(),
                gc,
                null,
                password,
                new KeyboardHelper(),
                new ChatParser(),
                dataSender,
                new RivenCleaner(),
                new RivenParserFactory(),
                new Application.LogParser.RedTextParser());
            bot.AsyncRun(new System.Threading.CancellationToken());
        }

        private static void testRivenSplit()
        {
            foreach (var error in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs").Select(f => new FileInfo(f)).Where(f => f.Name.StartsWith("test")))
            {
                using (var cropped = new Bitmap(error.FullName))
                {
                    var cleaner = new RivenCleaner();
                    var rp = new RivenParser();
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
            //var rp = new RivenParser();
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

        private static void TestCanExit()
        {
            var fullImage = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\Screenshot (117).png");
            var ss = new ScreenStateHandler();
            var isExitable = ss.IsExitable(fullImage);
            fullImage.Dispose();

            var chatIcon = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chaticon.png");
            var cp = new ChatParser();
            var isChat = cp.IsChatFocused(chatIcon);
        }

        private static void TestRivenParsing()
        {
            var rp = new RivenParser();
            var cropped = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Riven Inputs\error9.png");
            //var cropped = rp.CropToRiven(bitmap);
            cropped.Save("cropped.png");
            //bitmap.Dispose();
            var rc = new RivenCleaner();
            var clean = rc.CleanRiven(cropped);
            cropped.Dispose();
            clean.Save("clean.png");
            var result = rp.ParseRivenTextFromImage(clean, null);
        }

        private static void VisualizeClickpoints()
        {
            var cp = new ChatParser();
            var r = cp.ParseChatImage(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\bad.png"));
            var list = new CoordinateList();
            r.Where(r1 => r1 is ChatMessageLineResult).Cast<ChatMessageLineResult>().SelectMany(r1 => r1.ClickPoints).ToList().ForEach(p => list.Add(p.X, p.Y));
            var ic = new ImageCleaner();
            ic.SaveClickMarkers(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\bad.png",
                Path.Combine(outputDir, "bad_clicks.png"),
                list);
        }

        private static void TestScreenHandler()
        {
            var c = new GameCapture();
            var ss = new ScreenStateHandler();

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\loading.png"))
            {
                var isLoading = ss.GetScreenState(b) == ScreenState.LoadingScreen;
                Console.WriteLine("Is loading: " + isLoading + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\loading_nobar.png"))
            {
                var isLoading = ss.GetScreenState(b) == ScreenState.LoadingScreen;
                Console.WriteLine("Is loading: " + isLoading + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\login.png"))
            {
                var isLogin = ss.GetScreenState(b) == ScreenState.LoginScreen;
                Console.WriteLine("Is login: " + isLogin + " should be true");
            }

            Console.WriteLine("MISSING TEST DATA FOR NORMAL CLAIM REWARDS SCREEN");

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\plat_claim.png"))
            {
                var isPlat = ss.GetScreenState(b) == ScreenState.DailyRewardScreenPlat;
                Console.WriteLine("Is plat: " + isPlat + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\warframe_pilot1.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe1: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\warframe_pilot2.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe2: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\warframe_pilot3.png"))
            {
                var isPiloting = ss.GetScreenState(b) == ScreenState.ControllingWarframe;
                Console.WriteLine("Is piloting warframe3: " + isPiloting + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\main_menu.png"))
            {
                var isMainMenu = ss.GetScreenState(b) == ScreenState.MainMenu;
                Console.WriteLine("Is main menu: " + isMainMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\main_menu2.png"))
            {
                var isMainMenu = ss.GetScreenState(b) == ScreenState.MainMenu;
                Console.WriteLine("Is main menu2: " + isMainMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\profile_menu.png"))
            {
                var isProfileMenu = ss.GetScreenState(b) == ScreenState.ProfileMenu;
                Console.WriteLine("Is isProfileMenu: " + isProfileMenu + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\glyph_window.png"))
            {
                var isGlyphWindow = ss.GetScreenState(b) == ScreenState.GlyphWindow;
                Console.WriteLine("Is glyph window: " + isGlyphWindow + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\glyph_without_filters.png"))
            {
                var isNoFilters = !ss.GlyphFiltersPresent(b);
                Console.WriteLine("Is glyph filters empty: " + isNoFilters + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\glyph_with_filters.png"))
            {
                var isFilters = ss.GlyphFiltersPresent(b);
                Console.WriteLine("Is glyph filters: " + isFilters + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\chat_collapsed.png"))
            {
                var isChatCollapsed = ss.IsChatCollapsed(b);
                Console.WriteLine("Is chat closed: " + isChatCollapsed + " should be true");
            }

            using (var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai\Screen States\chat_collapsed2.png"))
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
            var results = p.ParseChatImage(new Bitmap(image), true, true, 27).Where(r => r is ChatMessageLineResult).Cast<ChatMessageLineResult>();

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
                    if (ss.IsChatOpen(b))
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
                            if (ss.GetScreenState(bitmap2) == ScreenState.RivenWindow)
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
            //var riven = rp.ParseRivenImage(imageWhite);
        }

        //private static void PrepareRivens()
        //{
        //    var r = new RivenCleaner();
        //    var p = new RivenParser();

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

        private static void FixImages()
        {
            var cleaner = new ImageCleaner();
            cleaner.SaveChatColors(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png", @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input_white.png");
            var p = new ChatParser();
            var r = p.ParseChatImage(new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\input.png"));
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
                var result = c.ParseChatImage(new Bitmap(masterKeyFile), xOffset, false, false).Select(i => i.RawMessage.Trim()).ToArray();
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
                    fout.WriteLine(sb.ToString() + " [");
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

    class DummySender : IDataSender
    {
        public async Task AsyncSendChatMessage(ChatMessageModel message)
        {
        }

        public async Task AsyncSendDebugMessage(string message)
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
}
