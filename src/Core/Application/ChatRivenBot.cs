using Application.ChatMessages.Model;
using Application.Enums;
using Application.Interfaces;
using Application.Interfaces;
using Application.LineParseResult;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Application.LogParser;
using Newtonsoft.Json;

namespace Application
{
    public class ChatRivenBot : IDisposable
    {
        private readonly string _launcherPath;
        private readonly IMouseMover _mouse;
        private readonly IScreenStateHandler _screenStateHandler;
        private readonly IGameCapture _gameCapture;
        private readonly IKeyboard _keyboard;
        private readonly IChatParser _chatParser;
        private readonly IDataSender _dataSender;
        private readonly IRivenCleaner _rivenCleaner;
        private readonly IRivenParserFactory _rivenParserFactory;
        private readonly ObsSettings _obsSettings;
        private readonly RedTextParser _redTextParser;
        private OBSWebsocket _obs;
        private static string _password;

        public ChatRivenBot(string launcherFullPath, IMouseMover mouseMover, IScreenStateHandler screenStateHandler,
            IGameCapture gameCapture,
            ObsSettings obsSettings,
            string password,
            IKeyboard keyboard,
            IChatParser chatParser,
            IDataSender dataSender,
            IRivenCleaner rivenCleaner,
            IRivenParserFactory rivenParserFactory,
            RedTextParser redTextParser)
        {
            _launcherPath = launcherFullPath;
            _mouse = mouseMover;
            _screenStateHandler = screenStateHandler;
            _gameCapture = gameCapture;
            _obsSettings = obsSettings;
            _password = password;
            _keyboard = keyboard;
            _chatParser = chatParser;
            _dataSender = dataSender;
            _rivenCleaner = rivenCleaner;
            _rivenParserFactory = rivenParserFactory;
            _redTextParser = redTextParser;

            if (_obsSettings != null)
                ConnectToObs();
        }

        private ConcurrentQueue<RivenParseTaskWorkItem> _rivenWorkQueue = new ConcurrentQueue<RivenParseTaskWorkItem>();

        public static void ProcessRivenQueue(CancellationToken c, IRivenParserFactory factory, IDataSender dataSender, ConcurrentQueue<RivenParseTaskWorkItem> queue, IRivenCleaner cleaner)
        {
            var parser = factory.CreateRivenParser();
            while (true)
            {
                if (c.IsCancellationRequested)
                    break;
                RivenParseTaskWorkItem item = null;
                if (!queue.TryDequeue(out item) || item == null)
                {
                    Thread.Sleep(250);
                    continue;
                }
                foreach (var r in item.RivenWorkDetails)
                {
                    using (var croppedCopy = new Bitmap(r.CroppedRivenBitmap))
                    {
                        using (var cleaned = cleaner.CleanRiven(croppedCopy))
                        {
                            using (var cleanedCopy = new Bitmap(cleaned))
                            {
                                var riven = parser.ParseRivenTextFromImage(cleanedCopy, null);
                                riven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                                riven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);

                                riven.MessagePlacementId = r.RivenIndex;
                                riven.Name = r.RivenName;
                                dataSender.AsyncSendRivenImage(riven.ImageId, croppedCopy);
                                r.CroppedRivenBitmap.Dispose();
                                item.Model.Rivens.Add(riven);
                            }
                        }
                    }
                    dataSender.AsyncSendChatMessage(item.Model);
                }
            }

            if (parser is IDisposable)
                ((IDisposable)parser).Dispose();
        }

        private void ConnectToObs()
        {
            _obs = new OBSWebsocket();
            _obs.Connect(_obsSettings.Url, _obsSettings.Password);
        }

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        public void AsyncRun(CancellationToken cancellationToken)
        {
            _redTextParser.OnRedText += async redtext => await _dataSender.AsyncSendRedtext(redtext);

            //Check if WF is running
            var wfAlreadyRunning = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0;
            if (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length == 0)
            {
                StartWarframe();
            }
            WaitForLoadingScreen(wfAlreadyRunning);

            //Check if on login screen
            LogIn();

            //Check if on daily reward screen
            ClaimDailyReward();

            var cropper = _rivenParserFactory.CreateRivenParser();
            //start an infinite loop
            while (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0)
            {
                //Get to Glyph screen if not already there
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state != Enums.ScreenState.GlyphWindow)
                    {
                        GoToGlyphScreenAndSetupFilters();
                    }
                    else if (state == ScreenState.GlyphWindow && _screenStateHandler.IsChatOpen(screen))
                    {
                        var chatLines = _chatParser.ParseChatImage(screen, true, true, 30);
                        var workItems = new List<RivenParseTaskWorkItem>();
                        foreach (var line in chatLines)
                        {
                            //if (line.LineType == LineParseResult.LineType.RedText)
                            //{
                            //    //_dataSender.AsyncSendRedtext(line.RawMessage).Wait();
                            //}
                            if (line.LineType == LineParseResult.LineType.NewMessage && line is ChatMessageLineResult)
                            {
                                var clr = line as ChatMessageLineResult;
                                var chatModel = MakeChatModel(line as LineParseResult.ChatMessageLineResult);
                                if (clr.ClickPoints.Count == 0)
                                    _dataSender.AsyncSendChatMessage(chatModel);
                                else
                                {
                                    var crops = new List<RivenParseTaskWorkItemDetails>();
                                    foreach (var clickpoint in clr.ClickPoints)
                                    {
                                        //Click riven
                                        _mouse.MoveTo(clickpoint.X, clickpoint.Y);
                                        Thread.Sleep(17);
                                        _mouse.Click(clickpoint.X, clickpoint.Y);
                                        Thread.Sleep(45);
                                        _mouse.MoveTo(0, 0);
                                        Thread.Sleep(45);

                                        //Wait for riven to open
                                        Bitmap crop = null;
                                        var foundRiven = false;
                                        for (int tries = 0; tries < 6; tries++)
                                        {
                                            var sw = new Stopwatch();
                                            sw.Start();
                                            using (var b = _gameCapture.GetFullImage())
                                            {
                                                Console.WriteLine("Got capture in: " + sw.Elapsed.TotalSeconds);
                                                sw.Stop();
                                                if (_screenStateHandler.GetScreenState(b) == ScreenState.RivenWindow)
                                                {
                                                    Console.WriteLine("found riven after: " + (tries + 1) + " tries");
                                                    foundRiven = true;
                                                    crop = cropper.CropToRiven(b);

                                                    _mouse.Click(3816, 2013);
                                                    Thread.Sleep(17);
                                                    _mouse.MoveTo(0, 0);
                                                    Thread.Sleep(17);
                                                    break;
                                                }
                                            }
                                        }
                                        if (!foundRiven || crop == null)
                                        {
                                            _chatParser.InvalidCache(line.GetKey());
                                            if (crop != null)
                                                crop.Dispose();
                                            break;
                                        }
                                        for (int tries = 0; tries < 6; tries++)
                                        {
                                            using (var b = _gameCapture.GetFullImage())
                                            {
                                                var subState = _screenStateHandler.GetScreenState(b);
                                                if (_screenStateHandler.IsChatOpen(b))
                                                {
                                                    break;
                                                }
                                                else if (tries < 5 && subState == ScreenState.RivenWindow)
                                                {
                                                    Thread.Sleep(17);
                                                    //_mouse.Click(3816, 2013);
                                                    //Thread.Sleep(17);
                                                    //_mouse.MoveTo(0, 0);
                                                    //Thread.Sleep(80);
                                                }
                                                else if (tries >= 5 && _screenStateHandler.IsExitable(b))
                                                {
                                                    _mouse.Click(3816, 2013);
                                                    Thread.Sleep(17);
                                                    _mouse.MoveTo(0, 0);
                                                    Thread.Sleep(17);
                                                }
                                            }
                                        }
                                        crops.Add(new RivenParseTaskWorkItemDetails() { RivenIndex = clickpoint.Index, RivenName = clickpoint.RivenName, CroppedRivenBitmap = crop });
                                    }
                                    workItems.Add(new RivenParseTaskWorkItem() { Model = chatModel, RivenWorkDetails = crops });
                                }
                            }
                        }
                        workItems.ForEach(item =>
                        {
                            var modelTask = new Task(() =>
                            {
                                var tasks = item.RivenWorkDetails.Select(r =>
                                {
                                    var t = new Task<Riven>(() =>
                                    {
                                        using (var cleaned = _rivenCleaner.CleanRiven(r.CroppedRivenBitmap))
                                        {
                                            var parser = _rivenParserFactory.CreateRivenParser();
                                            var riven = parser.ParseRivenTextFromImage(cleaned, null);
                                            riven.Polarity = parser.ParseRivenPolarityFromColorImage(r.CroppedRivenBitmap);
                                            riven.Rank = parser.ParseRivenRankFromColorImage(r.CroppedRivenBitmap);
                                            riven.MessagePlacementId = r.RivenIndex;
                                            riven.Name = r.RivenName;
                                            _dataSender.AsyncSendRivenImage(riven.ImageId, r.CroppedRivenBitmap);
                                            //r.CroppedRivenBitmap.Dispose();

                                            if (parser is IDisposable)
                                                ((IDisposable)parser).Dispose();
                                            return riven;
                                        }
                                    });
                                    t.Start();
                                    return t;
                                });
                                var rivens = Task.WhenAll(tasks).Result.ToList();
                                rivens.ForEach(r => item.Model.Rivens.Add(r));
                                _dataSender.AsyncSendChatMessage(item.Model);
                            });
                            modelTask.Start();
                        });
                    }
                }
                //Scroll down to get 27 more messages
                _mouse.MoveTo(3250, 768);
                //Scroll down for new page of messages
                for (int i = 0; i < 27; i++)
                {
                    _mouse.ScrollDown();
                    Thread.Sleep(17);
                }
                for (int i = 0; i < 1; i++)
                {
                    _mouse.ScrollUp();//Pause chat
                    Thread.Sleep(17);
                }
                _mouse.MoveTo(0, 0);
                Thread.Sleep(100);
            }
            if (cropper is IDisposable)
                ((IDisposable)cropper).Dispose();
        }

        public class RivenParseTaskWorkItem
        {
            public ChatMessageModel Model { get; set; }
            public List<RivenParseTaskWorkItemDetails> RivenWorkDetails { get; set; } = new List<RivenParseTaskWorkItemDetails>();
        }

        public class RivenParseTaskWorkItemDetails
        {
            public string RivenName { get; set; }
            public int RivenIndex { get; set; }
            public Bitmap CroppedRivenBitmap { get; set; }
        }

        private static ChatMessageModel MakeChatModel(LineParseResult.ChatMessageLineResult line)
        {
            var badNameRegex = new Regex("[^-A-Za-z0-9._]");
            var m = line.RawMessage;
            string debugReason = null;
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
            }
            catch { debugReason = "Bade name: " + username; }
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

        private void GoToGlyphScreenAndSetupFilters()
        {
            NavigateToGlyphScreen();
            Thread.Sleep(250);
            using (var glyphScreen = _gameCapture.GetFullImage())
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                if (_screenStateHandler.GetScreenState(glyphScreen) == ScreenState.GlyphWindow)
                {
                    //Check if filter is setup with asdf
                    if (!_screenStateHandler.GlyphFiltersPresent(glyphScreen))
                    {
                        //Click clear to be safe
                        _mouse.Click(1125, 263);
                        Thread.Sleep(50);
                        //_mouse.Click(1125, 263);
                        //Thread.Sleep(50);

                        _keyboard.SendPaste("asdf");
                        Thread.Sleep(50);
                    }
                    if (_screenStateHandler.IsChatCollapsed(glyphScreen))
                    {
                        //Click and drag to move chat into place
                        _mouse.ClickAndDrag(new Point(160, 2110), new Point(0, 2160), 100);
                    }
                    else if (!_screenStateHandler.IsChatOpen(glyphScreen))
                        throw new ChatMissingException();
                }
            }
        }

        private void ClaimDailyReward()
        {
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.Click(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                    _mouse.Click(3325, 1951);
            }
        }

        private void LogIn()
        {
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.MoveTo(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    DisableWarframeGameCapture();
                    _mouse.Click(screen.Width, 0);
                    _mouse.Click(2671, 1239);
                    _keyboard.SendPaste(_password);

                    _mouse.Click(2945, 1333);
                    //Give plenty of time for the screen to transition
                    System.Threading.Thread.Sleep(5000);
                }
            }
            EnableWarframeGameCapture();
        }

        private void WaitForLoadingScreen(bool wfAlreadyRunning)
        {
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            while (!wfAlreadyRunning && DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                _mouse.MoveTo(0, 0);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        //await Task.Delay(1000);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                        break;
                }
            }
        }

        private void StartWarframe()
        {
            ////If not start launcher, click play until WF starts
            while (true)
            {
                var launcher = System.Diagnostics.Process.GetProcessesByName("Launcher").FirstOrDefault();
                if (launcher == null)
                {
                    launcher = new System.Diagnostics.Process()
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = _launcherPath
                        }
                    };
                    launcher.Start();
                    //await Task.Delay(1000);
                    System.Threading.Thread.Sleep(1000);
                    launcher = System.Diagnostics.Process.GetProcessesByName("Launcher").FirstOrDefault();
                    if (launcher == null)
                        continue;
                }
                SetForegroundWindow(launcher.MainWindowHandle);
                Rect launcherRect = new Rect();
                GetWindowRect(launcher.MainWindowHandle, ref launcherRect);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                System.Threading.Thread.Sleep(1000);
                if (launcher.HasExited)
                {
                    DisableWarframeGameCapture();
                    System.Threading.Thread.Sleep(5000);
                    break;
                }
            }
        }

        private void NavigateToGlyphScreen()
        {
            //Ensure we are controlling warframe
            var tries = 0;
            while (true)
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state != Enums.ScreenState.ControllingWarframe)
                    {
                        _keyboard.SendEscape();
                        System.Threading.Thread.Sleep(125);
                    }
                    else
                        break;
                }
                tries++;
                if (tries > 15)
                    throw new NavigationException(ScreenState.ControllingWarframe);
            }
            //Send escape to open main menu
            _keyboard.SendEscape();
            System.Threading.Thread.Sleep(1000); //Give menu time to animate

            //Check if on Main Menu
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.MainMenu)
                {
                    //Click profile
                    SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                    _mouse.Click(728, 937);
                    Thread.Sleep(500);

                    using (var profileMenuImage = _gameCapture.GetFullImage())
                    {
                        if (_screenStateHandler.GetScreenState(profileMenuImage) == Enums.ScreenState.ProfileMenu)
                        {
                            //Click Glyph
                            _mouse.Click(693, 948);
                            Thread.Sleep(500);
                        }
                        else
                            throw new NavigationException(ScreenState.ProfileMenu);
                    }
                }
                else
                    throw new NavigationException(ScreenState.MainMenu);
            }
        }

        private void EnableWarframeGameCapture()
        {
            if (_obs != null)
            {
                var game = _obs.ListScenes().First().Items.First(i => i.SourceName.ToLower().Contains("warframe"));
                var fields = new JObject();
                fields.Add("item", game.SourceName);
                fields.Add("visible", true);
                _obs.SendRequest("SetSceneItemProperties", fields);
            }
        }

        private void DisableWarframeGameCapture()
        {
            if (_obs != null)
            {
                var game = _obs.ListScenes().First().Items.First(i => i.SourceName.ToLower().Contains("warframe"));
                var fields = new JObject();
                fields.Add("item", game.SourceName);
                fields.Add("visible", false);
                _obs.SendRequest("SetSceneItemProperties", fields);
            }
        }

        public void Dispose()
        {
            if (_obs != null)
                _obs.Disconnect();
        }
    }
}
