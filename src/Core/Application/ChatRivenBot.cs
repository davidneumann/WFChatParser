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
using Application.Window;
using Application.Actionables.ChatBots;

namespace Application
{
    public class ChatRivenBot : IDisposable
    {
        private readonly string _launcherPath;
        private readonly IMouse _mouse;
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
        private ConcurrentQueue<RivenParseTaskWorkItem> _rivenWorkQueue = new ConcurrentQueue<RivenParseTaskWorkItem>();
        private ConcurrentQueue<string> _messageCache = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, ChatMessageModel> _messageCacheDetails = new ConcurrentDictionary<string, ChatMessageModel>();
        private List<Thread> _rivenQueueWorkers = new List<Thread>();
        private bool _isRunning = false;
        private System.IO.StreamWriter logStream = null;

        public ChatRivenBot(string launcherFullPath, IMouse mouseMover, IScreenStateHandler screenStateHandler,
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


        public void ProcessRivenQueue(CancellationToken c)
        {
            var parser = _rivenParserFactory.CreateRivenParser();
            while (true)
            {
                if (c.IsCancellationRequested)
                    break;
                RivenParseTaskWorkItem item = null;
                if (!_rivenWorkQueue.TryDequeue(out item) || item == null)
                {
                    Thread.Sleep(250);
                    continue;
                }
                foreach (var r in item.RivenWorkDetails)
                {
                    using (var croppedCopy = new Bitmap(r.CroppedRivenBitmap))
                    {
                        using (var cleaned = _rivenCleaner.CleanRiven(croppedCopy))
                        {
                            using (var cleanedCopy = new Bitmap(cleaned))
                            {
                                var riven = parser.ParseRivenTextFromImage(cleanedCopy, null);
                                riven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                                riven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);

                                riven.MessagePlacementId = r.RivenIndex;
                                riven.Name = r.RivenName;
                                _dataSender.AsyncSendRivenImage(riven.ImageId, croppedCopy);
                                r.CroppedRivenBitmap.Dispose();
                                item.Message.Rivens.Add(riven);
                            }
                        }
                    }
                }
                _messageCache.Enqueue(item.Message.Author + item.Message.EnhancedMessage);
                _messageCacheDetails[item.Message.Author + item.Message.EnhancedMessage] = item.Message;
                _dataSender.AsyncSendChatMessage(item.Message);
            }

            if (parser is IDisposable)
                ((IDisposable)parser).Dispose();
        }

        public void AsyncRun(CancellationToken cancellationToken)
        {
            Log("Running");
            if (!_isRunning)
                _isRunning = true;
            else
                throw new Exception("Bot already running!");

            _redTextParser.OnRedText += async redtext => await _dataSender.AsyncSendRedtext(redtext);

            if (_rivenQueueWorkers.Count <= 0)
            {
                lock (_rivenQueueWorkers)
                {
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        var thread = new Thread(() => ProcessRivenQueue(cancellationToken));
                        thread.Start();
                        _rivenQueueWorkers.Add(thread);
                    }
                }
            }

            var cropper = _rivenParserFactory.CreateRivenParser();
            while (!cancellationToken.IsCancellationRequested)
            {
                //Check if WF is running
                var wfAlreadyRunning = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0;
                if (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length == 0)
                {
                    Log("Starting warframe");
                    StartWarframe();
                }

                WaitForLoadingScreen(wfAlreadyRunning);

                //Check if on login screen
                LogIn();

                //Check if on daily reward screen
                ClaimDailyReward();

                //Wait 45 seconds for all of the notifications to clear out.
                if (!wfAlreadyRunning)
                {
                    Log("Waiting for talking");
                    Thread.Sleep(45 * 1000);
                }

                //Close any annoying windows it opened
                using (var screen = _gameCapture.GetFullImage())
                {
                    if (_screenStateHandler.IsExitable(screen))
                    {
                        _mouse.Click(3816, 2013);
                        Thread.Sleep(30);
                    }
                }

                //Keep parsing chat as long as we are in a good state.
                var lastMessage = DateTime.Now;
                var firstParse = true;
                while (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0 && !cancellationToken.IsCancellationRequested)
                {
                    if (_messageCache.Count > 5000)
                    {
                        lock (_messageCache)
                        {
                            lock (_messageCacheDetails)
                            {
                                while (_messageCache.Count > 5000)
                                {
                                    string key = null;
                                    //Try to get the earliest key entered in
                                    if (_messageCache.TryDequeue(out key) && key != null)
                                    {
                                        ChatMessageModel empty = null;
                                        //If we fail to remove the detail item add the key back to the cache
                                        if (!_messageCacheDetails.TryRemove(key, out empty))
                                            _messageCache.Enqueue(key);
                                    }
                                }
                            }
                        }
                    }

                    if (!firstParse)
                        Log("Running loop");
                    //Close and try again if no messages in 5 minutes
                    if (DateTime.Now.Subtract(lastMessage).TotalMinutes > 5)
                    {
                        Log("Possible chat connection lost, closing WF");
                        CloseWarframe();
                        break;
                    }

                    //Try doing a parse
                    _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                    using (var screen = _gameCapture.GetFullImage())
                    {
                        _mouse.MoveTo(0, 0);
                        Thread.Sleep(17);
                        screen.Save("screen.png");
                        var state = _screenStateHandler.GetScreenState(screen);

                        //Check if we have some weird OK prompt (hotfixes, etc)
                        if (_screenStateHandler.IsPromptOpen(screen))
                        {
                            Log("Unknown prompt detected. Closing.");
                            _mouse.Click(screen.Width / 2, (int)(screen.Height * 0.57));
                            Thread.Sleep(30);
                            continue;
                        }

                        //If we somehow got off the glyph screen get back on it
                        if (state != Enums.ScreenState.GlyphWindow)
                        {
                            Log("Going to glyph screen.");
                            GoToGlyphScreenAndSetupFilters();
                            Thread.Sleep(30);
                            //In the event that we did not keep up with chat and ended up in a bad state we need to scroll to the bottom
                            if (!firstParse)
                            {
                                ScrollToBottomAndPause();
                            }
                            continue;
                        }
                        else if (state == ScreenState.GlyphWindow && _screenStateHandler.IsChatOpen(screen))
                        {
                            //Wait for the scroll bar before even trying to parse
                            if (!_chatParser.IsScrollbarPresent(screen))
                            {
                                Thread.Sleep(100);
                                continue;
                            }

                            //On first parse of a new instance scroll to the top
                            if (firstParse && !wfAlreadyRunning)
                            {
                                //Click top of scroll bar to pause chat
                                if (_chatParser.IsScrollbarPresent(screen))
                                {
                                    Log("Scrollbar found. Starting.");
                                    _mouse.MoveTo(3259, 658);
                                    Thread.Sleep(33);
                                    _mouse.Click(3259, 658);
                                    Thread.Sleep(100);
                                    firstParse = false;
                                    continue;
                                }
                            }
                            //On first parse of existing image jump to bottom and pause
                            else if (firstParse && wfAlreadyRunning)
                            {
                                Log("Scrollbar found. Resuming.");
                                ScrollToBottomAndPause();
                                firstParse = false;
                                continue;
                            }

                            var sw = new Stopwatch();
                            sw.Start();
                            var chatLines = _chatParser.ParseChatImage(screen, true, true, 30);
                            Log($"Found {chatLines.Length} new messages.");
                            foreach (var line in chatLines)
                            {
                                Log("Processing message: " + line.RawMessage);
                                lastMessage = DateTime.Now;
                                if (line is ChatMessageLineResult)
                                {
                                    var processedCorrectly = ProcessChatMessageLineResult(cropper, line);
                                    if (!processedCorrectly)
                                    {
                                        var path = SaveScreenToDebug(screen);
                                        if(path != null)
                                            _dataSender.AsyncSendDebugMessage("Failed to parse correctly. See: " + path);
                                        _chatParser.InvalidCache(line.GetKey());
                                        break;
                                    }
                                }
                                else
                                    Log("Unknown message: " + line.RawMessage);
                            }
                            Thread.Sleep(75);
                            Log($"Processed (not riven parsed) {chatLines.Length} messages in : {sw.Elapsed.TotalSeconds} seconds");
                            sw.Stop();
                        }
                        else
                        {
                            Log("Bad state detected! Restarting!!.");
                            var path = SaveScreenToDebug(screen);
                            _dataSender.AsyncSendDebugMessage("Bad state detected! Restarting!!. See: " + path);
                            //We have no idea what state we are in. Kill the game and pray the next iteration has better luck.
                            CloseWarframe();
                            break;
                        }
                    }

                    //Scroll down to get 27 more messages
                    _mouse.MoveTo(3250, 768);
                    Thread.Sleep(30);
                    //Scroll down for new page of messages
                    for (int i = 0; i < 27; i++)
                    {
                        _mouse.ScrollDown();
                        Thread.Sleep(17);
                    }
                    for (int i = 0; i < 1; i++)
                    {
                        _mouse.ScrollUp();//Pause chat
                        Thread.Sleep(90);
                    }
                    _mouse.MoveTo(0, 0);
                    Thread.Sleep(17);
                }
            }

            Log("Bot loop has been ended");

            _rivenQueueWorkers.Clear();
            _redTextParser.OnRedText -= async redtext => await _dataSender.AsyncSendRedtext(redtext);
            _isRunning = false;

            if (cropper is IDisposable)
                ((IDisposable)cropper).Dispose();
        }

        private string SaveScreenToDebug(Bitmap screen)
        {
            if (!System.IO.Directory.Exists("debug"))
                System.IO.Directory.CreateDirectory("debug");
            var filePath = System.IO.Path.Combine("debug", DateTime.Now.ToFileTime() + ".png");
            try { screen.Save(filePath); return filePath; }
            catch { return null; }
        }

        private void ScrollToBottomAndPause()
        {
            _mouse.ClickAndDrag(new Point(3263, 2085), new Point(3263, 2121), 200);
            Thread.Sleep(100);
            _mouse.MoveTo(3250, 768);
            _mouse.ScrollUp();//Pause chat
            Thread.Sleep(60);
        }

        private static void CloseWarframe()
        {
            System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToList().ForEach(p => p.Kill());
        }

        private static ChatMessageModel MakeChatModel(LineParseResult.ChatMessageLineResult line)
        {
            var badNameRegex = new Regex("[^-A-Za-z0-9._]");
            var m = line.RawMessage;
            string debugReason = null;
            var timestamp = m.Substring(0, 7).Trim();
            var username = line.Username;
            try
            {
                if (username.Contains(" ") || username.Contains(@"\/") || username.Contains("]") || username.Contains("[") || badNameRegex.Match(username).Success)
                {
                    debugReason = "Bade name: " + username;
                }

                if (!Regex.Match(line.RawMessage, @"^(\[\d\d:\d\d\])\s*([-A-Za-z0-9._]+)\s*:?\s*(.+)").Success)
                    debugReason = "Invalid username or timestamp!";
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

        private bool ProcessChatMessageLineResult(IRivenParser cropper, BaseLineParseResult line)
        {
            var clr = line as ChatMessageLineResult;

            if (_messageCacheDetails.ContainsKey(clr.Username + clr.EnhancedMessage))
            {
                Log($"Message cache hit for {clr.Username + clr.EnhancedMessage}");
                var cachedModel = _messageCacheDetails[clr.Username + clr.EnhancedMessage];
                var duplicateModel = new ChatMessageModel()
                {
                    Timestamp = clr.Timestamp,
                    SystemTimestamp = DateTimeOffset.UtcNow,
                    Author = cachedModel.Author,
                    EnhancedMessage = cachedModel.EnhancedMessage,
                    Raw = line.RawMessage,
                    Rivens = cachedModel.Rivens
                };
                _dataSender.AsyncSendChatMessage(duplicateModel);
                return true;
            }

            var chatMessage = MakeChatModel(line as LineParseResult.ChatMessageLineResult);
            if (chatMessage.DEBUGREASON != null && chatMessage.DEBUGREASON.Length > 0)
                return false;
            if (clr.ClickPoints.Count == 0)
                _dataSender.AsyncSendChatMessage(chatMessage);
            else
            {
                var rivenParseDetails = new List<RivenParseTaskWorkItemDetail>();
                foreach (var clickpoint in clr.ClickPoints)
                {
                    //Click riven
                    _mouse.MoveTo(clickpoint.X, clickpoint.Y);
                    Thread.Sleep(17);
                    _mouse.Click(clickpoint.X, clickpoint.Y);
                    Thread.Sleep(17);
                    _mouse.MoveTo(0, 0);
                    Thread.Sleep(17);

                    //Wait for riven to open
                    Bitmap crop = null;
                    var foundRivenWindow = false;
                    for (int tries = 0; tries < 15; tries++)
                    {
                        using (var b = _gameCapture.GetFullImage())
                        {
                            if (_screenStateHandler.GetScreenState(b) == ScreenState.RivenWindow)
                            {
                                foundRivenWindow = true;
                                crop = cropper.CropToRiven(b);

                                _mouse.Click(3816, 2013);
                                Thread.Sleep(17);
                                _mouse.MoveTo(0, 0);
                                Thread.Sleep(17);
                                break;
                            }
                        }
                    }

                    //If something went wrong clear this item from caches so it may be tried again
                    if (!foundRivenWindow || crop == null)
                    {
                        _chatParser.InvalidCache(line.GetKey());
                        if (crop != null)
                        {
                            crop.Dispose();
                        }
                        return false;
                    }

                    //The above click in the bottom right should have closed what ever window we opened.
                    //Give it time to animate but in the event it failed to close try clicking again.
                    for (int tries = 0; tries < 15; tries++)
                    {
                        using (var b = _gameCapture.GetFullImage())
                        {
                            var subState = _screenStateHandler.GetScreenState(b);
                            if (_screenStateHandler.IsChatOpen(b))
                            {
                                break;
                            }
                            else if (tries < 14 && subState == ScreenState.RivenWindow)
                            {
                                Thread.Sleep(17);
                            }
                            else if (tries >= 14 && _screenStateHandler.IsExitable(b))
                            {
                                _mouse.Click(3816, 2013);
                                Thread.Sleep(40);
                                _mouse.MoveTo(0, 0);
                                Thread.Sleep(40);
                            }
                        }
                    }
                    rivenParseDetails.Add(new RivenParseTaskWorkItemDetail() { RivenIndex = clickpoint.Index, RivenName = clickpoint.RivenName, CroppedRivenBitmap = crop });
                }
                _rivenWorkQueue.Enqueue(new RivenParseTaskWorkItem() { Message = chatMessage, RivenWorkDetails = rivenParseDetails });
            }

            return true;
        }

        private void GoToGlyphScreenAndSetupFilters()
        {
            NavigateToGlyphScreen();
            Thread.Sleep(250);
            using (var glyphScreen = _gameCapture.GetFullImage())
            {
                _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
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
                        Thread.Sleep(100);
                    }
                    if (_screenStateHandler.IsChatCollapsed(glyphScreen))
                    {
                        //Click and drag to move chat into place
                        _mouse.ClickAndDrag(new Point(160, 2110), new Point(0, 2160), 1000);
                        Thread.Sleep(100);
                    }
                    else if (!_screenStateHandler.IsChatOpen(glyphScreen))
                        throw new ChatMissingException();
                }
            }
        }

        private void ClaimDailyReward()
        {
            _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.Click(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    Log("Claiming random middle reward");
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                {
                    Log("Claiming unkown plat discount");
                    _mouse.Click(3325, 1951);
                }
            }
        }

        private void LogIn()
        {
            _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.MoveTo(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    Log("Logging in");
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
            Log("Waiting for login screen");
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            while (!wfAlreadyRunning && DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
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
                _screenStateHandler.GiveWindowFocus(launcher.MainWindowHandle);
                Rect launcherRect = _screenStateHandler.GetWindowRectangle(launcher.MainWindowHandle);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                System.Threading.Thread.Sleep(17);
                _keyboard.SendSpace();
                System.Threading.Thread.Sleep(1000);
                if (launcher.HasExited)
                {
                    DisableWarframeGameCapture();
                    System.Threading.Thread.Sleep(5000);
                    break;
                }
            }
        }

        private void NavigateToGlyphScreen(bool retry=true)
        {
            //Ensure we are controlling a warframe
            var tries = 0;
            while (true)
            {
                _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state != Enums.ScreenState.ControllingWarframe)
                    {
                        _keyboard.SendEscape();
                        System.Threading.Thread.Sleep(600);
                    }
                    else
                        break;
                }
                tries++;
                if (tries > 25)
                    throw new NavigationException(ScreenState.ControllingWarframe);
            }
            //Send escape to open main menu
            _keyboard.SendEscape();
            System.Threading.Thread.Sleep(1000); //Give menu time to animate

            //Check if on Main Menu
            _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.MainMenu)
                {
                    //Click profile
                    _screenStateHandler.GiveWindowFocus(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                    _mouse.Click(728, 937);
                    Thread.Sleep(750);

                    using (var profileMenuImage = _gameCapture.GetFullImage())
                    {
                        if (_screenStateHandler.GetScreenState(profileMenuImage) == Enums.ScreenState.ProfileMenu)
                        {
                            //Click Glyph
                            _mouse.Click(693, 948);
                            Thread.Sleep(750);
                        }
                        else if (retry)
                            NavigateToGlyphScreen(false);
                        else if (!retry)
                        {
                            var path = SaveScreenToDebug(screen);
                            _dataSender.AsyncSendDebugMessage("Failed to navigate to profile menu. See: " + path);
                            throw new NavigationException(ScreenState.ProfileMenu);
                        }
                    }
                }
                else if (retry)
                    NavigateToGlyphScreen(false);
                else if (!retry)
                {
                    var path = SaveScreenToDebug(screen);
                    _dataSender.AsyncSendDebugMessage("Failed to navigate to mian menu. See: " + path);
                    throw new NavigationException(ScreenState.MainMenu);
                }
            }
        }

        private void ConnectToObs()
        {
            _obs = new OBSWebsocket();
            _obs.Connect(_obsSettings.Url, _obsSettings.Password);
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

        private void Log(string message)
        {
            if (logStream == null)
                logStream = new System.IO.StreamWriter("log.txt", false);

            _dataSender.AsyncSendLogMessage(message);
            logStream.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.f")}] {message}");
            if (message.Length > Console.BufferWidth)
                message = message.Substring(0, Console.BufferWidth - 1);
            Console.WriteLine(message);
        }

        public void Dispose()
        {
            if (_obs != null)
                _obs.Disconnect();
        }
    }
}
