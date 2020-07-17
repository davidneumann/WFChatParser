﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Application.ChatBoxParsing;
using Application.ChatMessages.Model;
using Application.Enums;
using Application.Interfaces;
using Application.LineParseResult;
using Application.Logger;
using static Application.ChatRivenBot;

namespace Application.Actionables.ChatBots
{
    public partial class TradeChatBot : IActionable
    {
        public bool IsRequestingControl => _requestingControl;
        private bool _requestingControl = true;

        private Task _controlTask = null;

        private ConcurrentQueue<RivenParseTaskWorkItem> _workQueue;
        private IRivenParser _rivenCropper;
        private CancellationToken _cancellationToken;
        private WarframeClientInformation _warframeCredentials;
        private BotStates _currentState = BotStates.StartWarframe;

        private IMouse _mouse;
        private IKeyboard _keyboard;
        private IScreenStateHandler _screenStateHandler;
        private ILogger _logger;
        private IGameCapture _gameCapture;
        private Process _warframeProcess;
        private bool firstParse = true;
        private IDataSender _dataSender;
        private IChatParser _chatParser;
        private DateTime _lastMessage = DateTime.UtcNow.AddMinutes(10);

        public DateTime LastMessage { get { return _lastMessage; } }

        private ConcurrentQueue<string> _messageCache = new ConcurrentQueue<string>();
        private ConcurrentDictionary<string, ChatMessageModel> _messageCacheDetails = new ConcurrentDictionary<string, ChatMessageModel>();
        private int _failedPostLoginScreens;

        public TradeChatBot(ConcurrentQueue<RivenParseTaskWorkItem> workQueue,
            IRivenParser rivenCropper,
            CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataSender dataSender,
            IChatParser chatParser)
        {
            _workQueue = workQueue;
            _rivenCropper = rivenCropper;
            _cancellationToken = cancellationToken;
            _warframeCredentials = warframeCredentials;
            _mouse = mouse;
            _keyboard = keyboard;
            _screenStateHandler = screenStateHandler;
            _logger = logger;
            _gameCapture = gameCapture;
            _warframeCredentials = warframeCredentials;
            _dataSender = dataSender;
            _chatParser = chatParser;
        }

        public Task TakeControl()
        {
            _logger.Log(_warframeCredentials.StartInfo.UserName + ":" + _warframeCredentials.Region + " taking control");
            _logger.Log("Cache size: " + _messageCache.Count + ". Cache detail size: " + _messageCacheDetails.Count);
            if (_messageCacheDetails.Count > 0)
            {
                var sample = _messageCacheDetails.Last().Value.EnhancedMessage;
                if (sample.Length > Console.BufferWidth - 20)
                    sample = sample.Substring(0, Console.BufferWidth - 20);
                _logger.Log("Cache sample: " + sample);
            }

            if (_warframeProcess == null || _warframeProcess.HasExited)
                _currentState = BotStates.StartWarframe;

            _requestingControl = false;

            switch (_currentState)
            {
                case BotStates.StartWarframe:
                    _logger.Log("Starting warframe");
                    return StartWarframe();
                case BotStates.WaitForLoadScreen:
                    _logger.Log("Waiting for load screen");
                    return WaitForLoadingScreen();
                case BotStates.LogIn:
                    _logger.Log("Running log in logic");
                    return LogIn();
                case BotStates.ClaimReward:
                    _logger.Log("Claiming reward");
                    return ClaimDailyRewardTask();
                case BotStates.CloseWarframe:
                    _logger.Log("Closing Warframe");
                    return CloseWarframe();
                case BotStates.NavigateToChat:
                    _logger.Log("Navigating to chat");
                    return NavigateToChat();
                case BotStates.ParseChat:
                    _logger.Log("Parsing chat");
                    return ParseChat();
                default:
                    break;
            }
            return Task.Delay(5000);
        }

        private async Task ParseChat()
        {
            var lineFailed = false;

            //Close and try again if no messages in 5 minutes
            if (DateTime.Now.Subtract(_lastMessage).TotalMinutes > 5)
            {
                _logger.Log("Possible chat connection lost, closing WF");
                _currentState = BotStates.CloseWarframe;
                _requestingControl = true;
                return;
            }

            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }

            //Try doing a parse
            using (var screen = _gameCapture.GetFullImage())
            {
                try
                {
                    screen.Save("screen_parsechat.png");
                }
                catch { }
                _mouse.MoveTo(0, 0);
                await Task.Delay(17);
                //screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);

                //Check if we have some weird OK prompt (hotfixes, etc)
                if (_screenStateHandler.IsPromptOpen(screen))
                {
                    _logger.Log("Unknown prompt detected. Closing.");
                    _mouse.Click(screen.Width / 2, (int)(screen.Height * 0.57));
                    await Task.Delay(30);
                }

                //If we somehow got off the glyph screen get back on it
                if (state != Enums.ScreenState.GlyphWindow)
                {
                    _logger.Log("Going to glyph screen.");
                    _currentState = BotStates.NavigateToChat;
                    _requestingControl = true;
                    return;
                }
                else if (state == ScreenState.GlyphWindow && _screenStateHandler.IsChatOpen(screen))
                {
                    //Wait for the scroll bar before even trying to parse
                    if (!_chatParser.IsScrollbarPresent(screen))
                    {
                        var _ = Task.Run(async () =>
                        {
                            await Task.Delay(2000);
                            _currentState = BotStates.ParseChat;
                            _requestingControl = true;
                        });
                        return;
                    }

                    //On first parse of a new instance scroll to the top
                    if (firstParse)
                    {
                        //Click top of scroll bar to pause chat
                        if (_chatParser.IsScrollbarPresent(screen))
                        {
                            _logger.Log("Scrollbar found. Starting.");
                            _mouse.MoveTo(3259, 658);
                            await Task.Delay(33);
                            _mouse.Click(3259, 658);
                            await Task.Delay(100);
                            firstParse = false;

                            _currentState = BotStates.ParseChat;
                            _requestingControl = true;
                            return;
                        }
                    }

                    var sw = new Stopwatch();
                    sw.Start();
                    var chatLines = _chatParser.ParseChatImage(screen, true, true, 30);
                    Rgb[,] samples = null;
                    if (chatLines.Length > 0)
                        samples = LineSampler.GetAllLineSamples(screen);

                    _logger.Log($"Found {chatLines.Length} new messages.");
                    var modelsToSend = new List<ChatMessageModel>();
                    var complexRivensToProcess = new List<RivenParseTaskWorkItem>();
                    for (int i = 0; i < chatLines.Length; i++)
                    {
                        var line = chatLines[i];
                        if (lineFailed)
                        {
                            _chatParser.InvalidateCache(line.GetKey());
                            continue;
                        }
                        _logger.Log("Processing message: " + line.RawMessage);
                        _lastMessage = DateTime.UtcNow;
                        if (line is ChatMessageLineResult clr)
                        {
                            bool success = true;
                            if (_messageCacheDetails.ContainsKey(clr.Username + clr.EnhancedMessage))
                            {
                                _logger.Log("Found in cache, sending that.");
                                modelsToSend.Add(SendCaceResult(line, clr));
                            }
                            else if (clr.ClickPoints.Count == 0)
                            {
                                _logger.Log("No clickpoints found. Sending simple message");
                                var result = await ProcessAndSendSimpleMessage(line);
                                if (result == null)
                                {
                                    _logger.Log("Failed to send simple message. Aborting");
                                    success = false;
                                }
                                else
                                    modelsToSend.Add(result);
                            }
                            else
                            {
                                _logger.Log("Attempting to process click points");
                                var result = await ProcessChatMessageLineResult(_rivenCropper, line);
                                if (result == null)
                                    success = false;
                                else
                                    complexRivensToProcess.Add(result);
                            }

                            if (!success)
                            {
                                var path = SaveScreenToDebug(screen);
                                //if (path != null)
                                //    await _dataSender.AsyncSendDebugMessage("Failed to parse correctly. See: " + path);
                                //else
                                //    await _dataSender.AsyncSendDebugMessage("Failed to parse correctly. Also failed to save image");
                                _chatParser.InvalidateCache(line.GetKey());
                                lineFailed = true;
                                break;
                            }
                        }
                        else
                            _logger.Log("Unknown message: " + line.RawMessage);
                    }
                    await Task.Delay(75);
                    _logger.Log($"Processed (not riven parsed) {chatLines.Length} messages in : {sw.Elapsed.TotalSeconds} seconds");
                    sw.Stop();

                    //Verify the chat didn't move during all of that
                    if (!lineFailed && chatLines.Length > 0)
                    {
                        using (var newScreen = _gameCapture.GetFullImage())
                        {
                            var screenValid = CheckInPlace(newScreen, samples);
                            try
                            {
                                newScreen.Save("screen_chat.png");
                            }
                            catch { }
                            if (!screenValid)
                            {
                                lineFailed = true;
                            }
                        }
                    }

                    if (lineFailed)
                    {
                        foreach (var line in chatLines)
                        {
                            _chatParser.InvalidateCache(line.GetKey());
                        }
                    }
                    else
                    {
                        foreach (var message in modelsToSend)
                        {
                            await _dataSender.AsyncSendChatMessage(message);
                        }
                        foreach (var complex in complexRivensToProcess)
                        {
                            _workQueue.Enqueue(complex);
                        }
                    }
                }
                else
                {
                    _logger.Log("Bad state detected! Restarting!!.");
                    var path = SaveScreenToDebug(screen);
                    await _dataSender.AsyncSendDebugMessage("Bad state detected! Restarting!!. See: " + path);
                    //We have no idea what state we are in. Kill the game and pray the next iteration has better luck.
                    _currentState = BotStates.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
            }

            if (lineFailed)
            {
                //Scroll to bottom
                _mouse.Click(3264, 2089);
                await Task.Delay(50);
            }

            //Scroll down to get 27 more messages
            _mouse.MoveTo(3250, 768);
            await Task.Delay(30);
            //Scroll down for new page of messages
            for (int i = 0; i < 27; i++)
            {
                _mouse.ScrollDown();
                await Task.Delay(17);
            }
            for (int i = 0; i < 1; i++)
            {
                _mouse.ScrollUp();//Pause chat
                await Task.Delay(90);
            }
            _mouse.MoveTo(0, 0);
            await Task.Delay(17);

            _currentState = BotStates.ParseChat;
            _requestingControl = true;
        }

        private async Task<ChatMessageModel> ProcessAndSendSimpleMessage(BaseLineParseResult line)
        {
            var chatMessage = MakeChatModel(line as LineParseResult.ChatMessageLineResult);
            chatMessage.Region = _warframeCredentials.Region;
            if (chatMessage.DEBUGREASON != null && chatMessage.DEBUGREASON.Length > 0)
            {
                try
                {
                    using (var b = _gameCapture.GetFullImage())
                    {
                        chatMessage.DEBUGIMAGE = Path.Combine("debug", DateTime.Now.Ticks + ".png");
                        b.Save(chatMessage.DEBUGIMAGE);
                    }
                }
                catch { }
                if (chatMessage.DEBUGIMAGE == null)
                    chatMessage.DEBUGIMAGE = "Failed to save";
                await _dataSender.AsyncSendDebugMessage("Model incorrect: " + chatMessage.DEBUGREASON + ". See: " + chatMessage.DEBUGIMAGE);
                return null;
            }
            return chatMessage;
        }

        private ChatMessageModel SendCaceResult(BaseLineParseResult line, ChatMessageLineResult clr)
        {
            _logger.Log($"Message cache hit for {clr.Username + clr.EnhancedMessage}");
            var cachedModel = _messageCacheDetails[clr.Username + clr.EnhancedMessage];
            var duplicateModel = new ChatMessageModel()
            {
                Timestamp = clr.Timestamp,
                SystemTimestamp = DateTimeOffset.UtcNow,
                Author = cachedModel.Author,
                EnhancedMessage = cachedModel.EnhancedMessage,
                Raw = line.RawMessage,
                Rivens = cachedModel.Rivens,
                Region = _warframeCredentials.Region
            };
            return duplicateModel;
        }

        private async Task<RivenParseTaskWorkItem> ProcessChatMessageLineResult(IRivenParser cropper, BaseLineParseResult line)
        {
            var clr = line as ChatMessageLineResult;
            var chatMessage = MakeChatModel(clr);
            if (chatMessage.DEBUGREASON != null && chatMessage.DEBUGREASON.Length > 0)
            {
                _logger.Log("Debug reason found on chat message during processing");
                using (var b = _gameCapture.GetFullImage())
                {
                    chatMessage.DEBUGIMAGE = Path.Combine("debug", DateTime.Now.Ticks + ".png");
                    b.Save(chatMessage.DEBUGIMAGE);
                    chatMessage.DEBUGREASON += "\nSee: " + chatMessage.DEBUGIMAGE;
                    await _dataSender.AsyncSendDebugMessage(chatMessage.DEBUGREASON);
                }
            }

            var rivenParseDetails = new List<RivenParseTaskWorkItemDetail>();
            foreach (var clickpoint in clr.ClickPoints)
            {
                _logger.Log($"Attempting to click on click point {clickpoint.X},{clickpoint.Y}");
                //Click riven
                _mouse.MoveTo(clickpoint.X, clickpoint.Y);
                await Task.Delay(17);
                _mouse.Click(clickpoint.X, clickpoint.Y);
                await Task.Delay(17);
                _mouse.MoveTo(0, 0);
                await Task.Delay(17);

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
                            await Task.Delay(17);
                            _mouse.MoveTo(0, 0);
                            await Task.Delay(17);
                            break;
                        }
                        else if (_screenStateHandler.GetScreenState(b) == ScreenState.GlyphWindow && _screenStateHandler.IsChatOpen(b))
                        {
                            //Click riven... again
                            _mouse.MoveTo(clickpoint.X, clickpoint.Y);
                            await Task.Delay(45);
                            _mouse.Click(clickpoint.X, clickpoint.Y);
                            await Task.Delay(45);
                            _mouse.MoveTo(0, 0);
                            await Task.Delay(100);
                        }
                    }
                }

                //If something went wrong clear this item from caches so it may be tried again
                if (!foundRivenWindow || crop == null)
                {
                    if (!foundRivenWindow)
                    {
                        //using (var b = _gameCapture.GetFullImage())
                        //{
                        //    string filename = System.IO.Path.Combine("debug", "riven_" + Guid.NewGuid().ToString() + ".png");
                        //    b.Save(filename);
                        //    _logger.Log("Could not find riven window. See: " + filename);
                        //}
                    }

                    _chatParser.InvalidateCache(line.GetKey());
                    if (crop != null)
                    {
                        crop.Dispose();
                    }
                    //await _dataSender.AsyncSendDebugMessage("Failed to findwindow or crop. Found window: " + foundRivenWindow + ", " + "Crop valid: " + (crop != null));
                    return null;
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
                        else if (tries >= 14 && _screenStateHandler.IsExitable(b))
                        {
                            _mouse.Click(3816, 2013);
                            await Task.Delay(40);
                            _mouse.MoveTo(0, 0);
                            await Task.Delay(40);
                        }
                    }
                }
                var workItem = new RivenParseTaskWorkItemDetail() { RivenIndex = clickpoint.Index, RivenName = clickpoint.RivenName, CroppedRivenBitmap = crop };
                if (_warframeCredentials.Region == "T_ZH")
                    workItem.RivenName = null;
                rivenParseDetails.Add(workItem);
            }

            return new RivenParseTaskWorkItem()
            {
                Message = chatMessage,
                RivenWorkDetails = rivenParseDetails,
                MessageCache = _messageCache,
                MessageCacheDetails = _messageCacheDetails
            };
        }

        private bool CheckInPlace(Bitmap screen, Rgb[,] samples)
        {
            _logger.Log("Verify riven message is still there.");
            var lineSamples = LineSampler.GetAllLineSamples(screen);
            for (int chatLine = 0; chatLine < lineSamples.GetLength(0); chatLine++)
            {
                for (int sampleIndex = 0; sampleIndex < lineSamples.GetLength(1); sampleIndex++)
                {
                    var origSample = samples[chatLine, sampleIndex];
                    var sample = lineSamples[chatLine, sampleIndex];

                    if ((origSample.R - sample.R) * (origSample.R - sample.R) +
                        (origSample.G - sample.G) * (origSample.G - sample.G) +
                        (origSample.B - sample.B) * (origSample.B - sample.B) > 225)
                    {
                        _logger.Log("Chat box has changed. Aborting riven clicks.");
                        return false;
                    }
                }
            }
            _logger.Log("Chat box did not move.");

            return true;
        }

        private bool CheckInPlace(Bitmap screen, BaseLineParseResult line, Rgb[,] samples, ClickPoint clickpoint)
        {
            _logger.Log("Verify riven message is still there.");
            int chatLine = LineSampler.GetLineIndexFromPoint(clickpoint.X, clickpoint.Y);
            var lineSamples = LineSampler.GetLineSamples(screen, chatLine);
            for (int i = 0; i < lineSamples.Length; i++)
            {
                var origSample = samples[chatLine, i];
                var sample = lineSamples[i];

                if ((origSample.R - sample.R) * (origSample.R - sample.R) +
                    (origSample.G - sample.G) * (origSample.G - sample.G) +
                    (origSample.B - sample.B) * (origSample.B - sample.B) > 225)
                {
                    _chatParser.InvalidateCache(line.GetKey());
                    _logger.Log("Chat box has changed. Aborting riven clicks.");
                    return false;
                }
            }
            _logger.Log("Riven message still there.");

            return true;
        }

        private ChatMessageModel MakeChatModel(LineParseResult.ChatMessageLineResult line)
        {
            var badNameRegex = new Regex("[^-A-Za-z0-9._]");
            var m = line.RawMessage;
            string debugReason = string.Empty;
            var timestamp = m.Substring(0, 7).Trim();
            var username = line.Username;
            try
            {
                if (username.Contains(" ") || username.Contains(@"\/") || username.Contains("]") || username.Contains("[") || badNameRegex.Match(username).Success)
                {
                    debugReason += "Bade name: " + username  + "\n";
                }

                if (!Regex.Match(line.RawMessage, @"^(\[\d\d:\d\d\])\s*((?:\[DE\])?[-A-Za-z0-9._]+)[:]\s*(.+)").Success)
                    debugReason += "Invalid username or timestamp!" + "\t\r\n" + line.RawMessage + "\n";

                if (username.Trim().Length < 3)
                    debugReason += $"Name is less than 3 characters: {username}\n";

                if (username.Trim().Contains(' '))
                    debugReason += $"Name contains a space: {username}\n";
            }
            catch { debugReason += "Bade name: " + username + "\n"; }
            var cm = new ChatMessageModel()
            {
                Raw = m,
                Author = username,
                Timestamp = timestamp,
                SystemTimestamp = DateTimeOffset.UtcNow,
                Region = _warframeCredentials.Region
            };
            if (debugReason.Length > 0)
            {
                cm.DEBUGREASON = debugReason;
            }
            cm.EnhancedMessage = line.EnhancedMessage;
            return cm;
        }

        private async Task NavigateToChat()
        {
            if (_warframeProcess == null || _warframeProcess.HasExited)
            {
                _currentState = BotStates.StartWarframe;
                _requestingControl = true;
                return;
            }

            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            await Task.Delay(17);
            _mouse.Click(0, 0);

            using (var screen = _gameCapture.GetFullImage())
            {
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.LoadingScreen || state == Enums.ScreenState.LoginScreen || state == Enums.ScreenState.DailyRewardScreenItem || state == Enums.ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("On a login related screen. Restarting");
                    await _dataSender.AsyncSendDebugMessage("Failed to move past login screen");
                    _currentState = BotStates.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
                else
                {
                    _failedPostLoginScreens = 0;
                }

                //Check if we have some weird OK prompt (hotfixes, etc)
                if (_screenStateHandler.IsPromptOpen(screen))
                {
                    _logger.Log("Unknown prompt detected. Closing.");
                    _mouse.Click(screen.Width / 2, (int)(screen.Height * 0.57));
                    await Task.Delay(30);
                }

                //If we somehow got off the glyph screen get back on it
                if (state != Enums.ScreenState.GlyphWindow)
                {
                    _logger.Log("Going to glyph screen.");
                    await GoToGlyphScreenAndSetupFilters();
                    await Task.Delay(30);
                    //In the event that we did not keep up with chat and ended up in a bad state we need to scroll to the bottom
                    if (!firstParse)
                    {
                        await ScrollToBottomAndPause();
                    }
                }
            }

            //_lastMessage = DateTime.Now;
            _currentState = BotStates.ParseChat;
            _requestingControl = true;
        }

        private async Task ScrollToBottomAndPause()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }
            _mouse.ClickAndDrag(new Point(3263, 2085), new Point(3263, 2121), 200);
            await Task.Delay(100);
            _mouse.MoveTo(3250, 768);
            _mouse.ScrollUp();//Pause chat
            await Task.Delay(60);
        }

        private async Task GoToGlyphScreenAndSetupFilters()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }
            _logger.Log("Navigtating to glyph screen inside SetupFilters");
            await NavigateToGlyphScreen();
            _logger.Log("Should be at glyph screen");
            await Task.Delay(250);
            using (var glyphScreen = _gameCapture.GetFullImage())
            {
                if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
                {
                    await Task.Delay(17);
                    _mouse.Click(0, 0);
                }
                if (_screenStateHandler.GetScreenState(glyphScreen) == ScreenState.GlyphWindow)
                {
                    //Check if filter is setup with asdf
                    if (!_screenStateHandler.GlyphFiltersPresent(glyphScreen))
                    {
                        _logger.Log("Setting up filters");
                        //Click clear to be safe
                        _mouse.Click(1125, 263);
                        await Task.Delay(100);
                        _mouse.Click(0, 0);
                        await Task.Delay(100);
                        _mouse.Click(1125, 263);
                        await Task.Delay(100);
                        //_mouse.Click(1125, 263);
                        //Thread.Sleep(50);

                        _keyboard.SendPaste("asdf");
                        await Task.Delay(100);
                    }
                    if (_screenStateHandler.IsChatCollapsed(glyphScreen))
                    {
                        _logger.Log("Expanding chat");
                        //Click and drag to move chat into place
                        _mouse.ClickAndDrag(new Point(160, 2110), new Point(0, 2160), 1000);
                        await Task.Delay(100);
                    }
                    else if (!_screenStateHandler.IsChatOpen(glyphScreen))
                        throw new ChatMissingException();
                }
            }
        }

        private async Task NavigateToGlyphScreen(bool retry = true)
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }
            //Ensure we are controlling a warframe
            var tries = 0;
            while (true)
            {
                if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
                {
                    await Task.Delay(17);
                    _mouse.Click(0, 0);
                }
                using (var screen = _gameCapture.GetFullImage())
                {
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state == ScreenState.GlyphWindow)
                    {
                        await Task.Delay(30);
                        return;
                    }
                    else if (state != Enums.ScreenState.ControllingWarframe)
                    {
                        _keyboard.SendEscape();
                        await Task.Delay(600);
                    }
                    else
                        break;
                }
                tries++;
                if (tries > 25)
                {
                    _logger.Log("Failed to navigate to glyph screen");
                    throw new NavigationException(ScreenState.ControllingWarframe);
                }
            }
            //Send escape to open main menu
            _keyboard.SendEscape();
            await Task.Delay(1000); //Give menu time to animate

            //Check if on Main Menu
            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            using (var screen = _gameCapture.GetFullImage())
            {
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.MainMenu)
                {
                    _failedPostLoginScreens = 0;

                    //Click profile
                    if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
                    {
                        await Task.Delay(17);
                        _mouse.Click(0, 0);
                        await Task.Delay(17);
                    }

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
                            await NavigateToGlyphScreen(false);
                        else if (!retry)
                        {
                            var path = SaveScreenToDebug(screen);
                            await _dataSender.AsyncSendDebugMessage("Failed to navigate to profile menu. See: " + path);
                            throw new NavigationException(ScreenState.ProfileMenu);
                        }
                    }
                }
                else if (retry)
                    await NavigateToGlyphScreen(false);
                else if (!retry)
                {
                    var path = SaveScreenToDebug(screen);
                    await _dataSender.AsyncSendDebugMessage("Failed to navigate to main menu. See: " + path);
                    throw new NavigationException(ScreenState.MainMenu);
                }
            }
        }

        private string SaveScreenToDebug(Bitmap screen)
        {
            if (!System.IO.Directory.Exists("debug"))
                System.IO.Directory.CreateDirectory("debug");
            var filePath = System.IO.Path.Combine("debug", DateTime.Now.ToFileTime() + ".png");
            try { screen.Save(filePath, System.Drawing.Imaging.ImageFormat.Png); return filePath; }
            catch (Exception e)
            {
                //_dataSender.AsyncSendDebugMessage("Failed to save screen: " + e.ToString());
                return null;
            }
        }

        private Task CloseWarframe()
        {
            _logger.Log("Closing warframe");
            return Task.Run(() =>
            {
                if (_warframeProcess != null)
                {
                    try
                    {
                        _warframeProcess.Kill();
                    }
                    catch
                    { }
                    _warframeProcess = null;
                }

                _currentState = BotStates.StartWarframe;
                _requestingControl = true;
            });
        }

        private async Task StartWarframe()
        {
            _requestingControl = false;
            var existingWarframes = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray();

            var launcher = new System.Diagnostics.Process()
            {
                StartInfo = _warframeCredentials.StartInfo
            };
            launcher.Start();
            await Task.Delay(5000);

            //Check if there was already a launcher running
            if (launcher.HasExited)
            {
                System.Diagnostics.Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("launcher")).ToList().ForEach(p =>
                {
                    try { p.Kill(); } catch { }
                });

                _currentState = BotStates.StartWarframe;
                _requestingControl = true;
                return;
            }

            ////If not start launcher, click play until WF starts
            var start = DateTime.Now;
            while (true)
            {

                //Yield to other tasks after 4 minutes of waiting
                if (DateTime.Now.Subtract(start).TotalMinutes > 4f)
                {
                    _currentState = BotStates.StartWarframe;
                    _requestingControl = true;
                    return;
                }

                _screenStateHandler.GiveWindowFocus(launcher.MainWindowHandle);
                var launcherRect = _screenStateHandler.GetWindowRectangle(launcher.MainWindowHandle);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                await Task.Delay(17);
                _keyboard.SendSpace();
                await Task.Delay(1000);
                if (launcher.HasExited)
                {
                    await Task.Delay(10000);
                    break;
                }
            }

            foreach (var warframe in System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray())
            {
                if (!existingWarframes.Any(eWF => eWF.MainWindowHandle == warframe.MainWindowHandle))
                    _warframeProcess = warframe;
            }

            //Give 15 minutes on a fresh login to allow slow chats to fill up before killing them.
            _lastMessage = DateTime.UtcNow.AddMinutes(15);
            _currentState = BotStates.WaitForLoadScreen;
            _requestingControl = true;
        }

        private async Task WaitForLoadingScreen()
        {
            _requestingControl = false;

            _logger.Log("Waiting for login screen");
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            var atLogin = false;
            while (DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                _mouse.MoveTo(0, 0);
                await Task.Delay(17);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        atLogin = true;
                        break;
                    }
                }
            }

            if (atLogin)
            {
                _currentState = BotStates.LogIn;
            }
            else
                _currentState = BotStates.CloseWarframe;
            _requestingControl = true;
        }

        private async Task LogIn()
        {
            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            await Task.Delay(100);
            _mouse.Click(0, 0);

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    _logger.Log("Logging in");
                    ////Username
                    //_mouse.Click(3041, 1145);
                    //await Task.Delay(17);
                    //_keyboard.SendPaste(_warframeCredentials.Username);
                    //await Task.Delay(17);

                    //Password
                    _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                    await Task.Delay(25);
                    _mouse.Click(2936, 1235);
                    await Task.Delay(250);
                    _mouse.Click(2936, 1235);
                    await Task.Delay(250);
                    _keyboard.SendPaste(_warframeCredentials.Password);
                    await Task.Delay(17);

                    //Login
                    _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                    _mouse.Click(2945, 1333);

                    //Give plenty of time for the screen to transition
                    var startTime = DateTime.Now;
                    while (DateTime.Now.Subtract(startTime).TotalSeconds < 15)
                    {
                        using (var newScreen = _gameCapture.GetFullImage())
                        {
                            if (_screenStateHandler.GetScreenState(newScreen) == ScreenState.LoginScreen)
                                await Task.Delay(1000);
                            else
                                break;
                        }
                    }
                    _logger.Log("Waiting 5 seconds for screen transition");
                    await Task.Delay(5000);
                    _logger.Log("Waiting done");
                }
                else
                {
                    _logger.Log("Login screen not detected. Restarting warframe.");
                    _currentState = BotStates.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
            }

            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            await Task.Delay(100);
            using (var screen = _gameCapture.GetFullImage())
            {
                ScreenState state = _screenStateHandler.GetScreenState(screen);
                if (state == ScreenState.LoginScreen)
                {
                    _logger.Log("Login screen still detected. Restarting warframe.");
                    _currentState = BotStates.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
                else if (state == ScreenState.DailyRewardScreenItem || state == ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("Claiming daily reward.");
                    await ClaimDailyReward();
                    _requestingControl = false;
                }
                else if (_failedPostLoginScreens < 3)
                {
                    _failedPostLoginScreens++;
                }
                else if (_failedPostLoginScreens == 3)
                {
                    //We are stuck on some unkown reward screen or new feature screen.
                    //Time to click wildly

                    //Click blidnly at a continue button
                    _logger.Log("Attempting to click continue button");
                    _mouse.Click((int)(screen.Width - screen.Width * 0.06298828125),
                        (int)(screen.Height - screen.Height * 0.0685185185185185));

                    //Wait and then check to see if that worked
                    await Task.Delay(1000);
                    
                    //If we are still not controlling the warframe then more blind clicking.
                    //NEVER EVER MOVE THE MOUSE WHILE CONTROLLING THE WARFRAME!
                    using (var newScreen = _gameCapture.GetFullImage())
                    {
                        ScreenState newState = _screenStateHandler.GetScreenState(screen);
                        if (newState != ScreenState.ControllingWarframe)
                        {
                            //Click blindly on a possible reward location
                            _mouse.Click((int)(screen.Width - screen.Width * 0.28173828125),
                                (int)(screen.Height - screen.Height * 0.2555555555555556));
                            await Task.Delay(1000);
                        }
                    }
                }


                _logger.Log("Waiting in the background for 45 seconds");
                var _ = Task.Run(async () =>
                {
                    await Task.Delay(45 * 1000);
                    _logger.Log("Done waiting 45 seconds");

                    _currentState = BotStates.NavigateToChat;
                    _requestingControl = true;
                });
            }
        }

        private async Task ClaimDailyReward()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    _logger.Log("Claiming random middle reward");
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("Claiming unkown plat discount");
                    _mouse.Click(3325, 1951);
                }
            }

            await Task.Delay(17);

            CloseUnknownWindow();

            await Task.Delay(1000);
        }

        private void CloseUnknownWindow()
        {
            //Close any annoying windows it opened
            using (var screen = _gameCapture.GetFullImage())
            {
                if (_screenStateHandler.IsExitable(screen))
                {
                    _mouse.Click(3816, 2013);
                    Thread.Sleep(30);
                }
            }
        }

        private Task ClaimDailyRewardTask()
        {
            return Task.Run(async () =>
            {
                await ClaimDailyReward();
                _currentState = BotStates.NavigateToChat;
                _requestingControl = true;
            });
        }

        public void ShutDown()
        {
            if (_warframeProcess != null && !_warframeProcess.HasExited)
            {
                try
                {
                    CloseWarframe().Wait();
                }
                catch
                {
                    _warframeProcess.Close();
                    _warframeProcess = null;
                }
            }
        }
    }
}
