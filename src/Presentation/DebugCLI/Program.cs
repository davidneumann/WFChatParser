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
using Application.Logger;
using Application.ChatBoxParsing;

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
            //ChatMovingShim();
            //ParseRivenImage();
            //ChatLineExtractorShim();
            //GenerateCharStrings();
            TrainOnImages();
            //TrainSpacesOnImages();
        }

        private static void ChatLineExtractorShim()
        {
            var cp = new ChatParser(new FakeLogger());
            var b = new Bitmap(@"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Inputs\chat_new.png");
            var lines = cp.ExtractChatLines(b);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].Save("line_" + i + ".png");
                var username = cp.GetUsernameFromChatLine(lines[i]);
                if (username != null)
                    Console.WriteLine("Username: " + username);
            }
        }

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
            var rp = new RivenParser();
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
                    var parser = new RivenParser();
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

        private static void FindErrorAgain()
        {
            var cp = new ChatParser(new FakeLogger());
            foreach (var file in Directory.GetFiles(@"\\DESKTOP-BJRVJJQ\ChatLog\debug").Where(f => f.Contains("131992381447623296")))
            {
                var lines = cp.ParseChatImage(new Bitmap(file));
                foreach (var line in lines)
                {
                    var clr = line as ChatMessageLineResult;

                    var chatMessage = MakeChatModel(line as Application.LineParseResult.ChatMessageLineResult);
                }
            }
        }

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
                    debugReason = "Invalid username or timestamp!";
            }
            catch { debugReason = "Bade name: " + username; }

            return new Tuple<string, string, string>(timestamp, username, debugReason);
        }

        private static void ParseRivenImage()
        {
            var rp = new RivenParser();
            var b = Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai").Where(f => f.EndsWith("74062b19-5158-4eb6-b26a-1b809f787994.png")).Select(file => new Bitmap(file)).FirstOrDefault();
            var rc = new RivenCleaner();
            var b2 = rc.CleanRiven(b);
            b2.Save("debug_clean.png");
            var text = rp.ParseRivenTextFromImage(b2, null);
        }

        private static void ParseChatImage()
        {
            //var filePath = @"C:\Users\david\OneDrive\Documents\WFChatParser\Test Runs\Validation Inputs\error_blurry1.png";
            //foreach (var filePath in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai")
            //                            .Select(f => new FileInfo(f))
            //                            .Where(f => f.Name.StartsWith("637") && !f.Name.Contains("_white") && f.Name.EndsWith(".png"))
            //                            .Select(f => f.FullName))
            foreach (var filePath in Directory.GetFiles(@"C:\Users\david\OneDrive\Documents\WFChatParser\Notice Me Senpai").Where(f => f.EndsWith("637086559684964790.png")))
            {
                using (var bitmap = new Bitmap(filePath))
                {
                    var cp = new ChatParser(new FakeLogger());
                    //ic.SaveSoftMask(filePath, "error_blurry1_white.png");
                    ImageCleaner.SaveSoftMask(filePath, filePath.Replace(".png", "_white.png"));
                    var lines = cp.ParseChatImage(bitmap);
                    var sb = new StringBuilder();
                    foreach (var line in lines)
                    {
                        Console.WriteLine(line.RawMessage);
                        sb.AppendLine(line.RawMessage);
                    }
                    File.WriteAllText(filePath.Replace(".png", ".txt"), sb.ToString());
                }
            }
        }

        private static void TestRedText()
        {
            var input = @"C:\Users\david\OneDrive\Documents\WFChatParser\ErrorImages\Screenshot (175).png";
            ImageCleaner.SaveSoftMask(input, "test2.png");
            var cp = new ChatParser(new FakeLogger());
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
                config["DataSender:RivenImageMessagePrefix"],
                config["DataSender:LogMessagePrefix"],
                config["DataSender:LogLineMessagePrefix"]);

            var pass = Console.ReadLine().Trim();
            PasswordShim(config["Credentials:Key"], config["Credentials:Salt"], pass);

            var password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"]);
            CancellationToken token = new System.Threading.CancellationToken();
            var gc = new GameCapture(new Logger(dataSender, token));
            var obs = GetObsSettings(config["Credentials:Key"], config["Credentials:Salt"]);
            var logParser = new WarframeLogParser();
            var textParser = new AllTextParser(dataSender, logParser);
            var bot = new ChatRivenBot(config["LauncherPath"], new MouseHelper(),
                new ScreenStateHandler(),
                gc,
                obs,
                password,
                new KeyboardHelper(),
                new ChatParser(new FakeLogger()),
                dataSender,
                new RivenCleaner(),
                new RivenParserFactory(),
                new Application.LogParser.RedTextParser(logParser));
            bot.AsyncRun(token);
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
            var cp = new ChatParser(new FakeLogger());
            var isChat = cp.IsChatFocused(chatIcon);
        }

        private static void TestRivenParsing()
        {
            var rp = new RivenParser();
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

        private static void VisualizeClickpoints()
        {
            var cp = new ChatParser(new FakeLogger());
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
            var c = new GameCapture(new DummyLogger());
            var ss = new ScreenStateHandler();

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
        }

        private static void TestRivenStuff()
        {
            var c = new GameCapture(new DummyLogger());
            var rp = new RivenParser();
            var ss = new ScreenStateHandler();

            var image = "test.png";
            var b = c.GetFullImage();
            b.Save("test.png");
            b.Dispose();

            var p = new ChatParser(new FakeLogger());
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
            var p = new ChatParser(new FakeLogger());
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

        private static void TrainSpacesOnImages()
        {
            var spaceTrainer = new OCRSpaceTrainer();
            spaceTrainer.TrainOnImages(@"C:\Users\david\OneDrive\Documents\WFChatParser\Training Images", "newnewdata", GetSupportedCharacters().ToCharArray());
        }

        private static void TrainOnImages()
        {
            var sourceDir = @"C:\Users\david\OneDrive\Documents\WFChatParser\Training Inputs\Spaces";
            var outputDir = "newnewnewdata";
            var trainer = new OCRTrainer();
            trainer.TrainOnImages(sourceDir, outputDir);

            var spaceTrainer = new OCRSpaceTrainer();
            spaceTrainer.TrainOnImages(sourceDir, outputDir, GetSupportedCharacters().ToCharArray());
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
                var c = new ChatParser(new FakeLogger());
                var cleaner = new ImageCleaner();
                cleaner.SaveChatColors(masterKeyFile, Path.Combine(outputDir, (new FileInfo(masterKeyFile)).Name));
                var sw = new Stopwatch();
                sw.Restart();
                var fullResults = c.ParseChatImage(new Bitmap(masterKeyFile), xOffset, false, false);

                var m = fullResults.OfType<ChatMessageLineResult>().Select(line => MakeChatModel(line)).ToArray();
                var m2 = fullResults.Select(line => GetUsername(line.RawMessage)).ToArray();
                var allThere = !m.Select(model => m2.Any(old => old.Item2 == model.Author)).Any(b => !b);
                var allThere2 = !m2.Select(old => m.Any(model => model.Author == old.Item2)).Any(b => !b);
                if (!allThere || !allThere2)
                    Debugger.Break();
                var newE = m.Select(model => model.EnhancedMessage);
                var oldE = fullResults.Select(line => line.RawMessage.Substring(5).Substring(line.RawMessage.Substring(5).IndexOf(":") + 1).Trim());

                var result = fullResults.Select(i => i.RawMessage.Trim()).ToArray();
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
            return "! # $ % & ' ( ) * + - . / 0 1 2 3 4 5 6 7 8 9 : ; < = > ? @ A B C D E F G H I J K L M N O P Q R S T U V W X Y Z \\ ] ^ _ a b c d e f g h i j k l m n o p q r s t u v w x y z { | } ,".Replace(" ", "");
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
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }

    internal class FakeLogger : ILogger
    {
        public void Log(string message)
        {
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

        public async Task AsyncSendLogLine(LogMessage message)
        {
        }

        public async Task AsyncSendLogMessage(string message)
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
