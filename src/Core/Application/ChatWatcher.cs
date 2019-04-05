using Application.ChatMessages.Model;
using Application.Enums;
using Application.Interfaces;
using Application.Interfaces;
using Application.LineParseResult;
using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Pastel;
using ImageMagick;
using Application.LogParser;
using Newtonsoft.Json;

namespace Application
{
    public class ChatWatcher
    {
        private IDataSender _dataSender;
        private IChatParser _chatParser;
        private IGameCapture _gameCapture;
        private IMouseMover _mouseMover;
        private IRivenCleaner _rivenCleaner;
        private IRivenParser _rivenParser;
        private IScreenStateHandler _screenStateHandler;
        private List<string> _UIMessages = new List<string>();
        private string _UIThirdLine;
        private string _UISecondLine;
        private string _UIFirstLine;
        private Riven _UILastRiven = null;
        private RedTextParser _redTextParser;

        public ChatWatcher(IDataSender dataSender, IChatParser chatParser, IGameCapture gameCapture, IMouseMover mouseMover, IRivenCleaner rivenCleaner, IRivenParser rivenParser,
            IScreenStateHandler screenStateHandler,
            RedTextParser redTextParser)
        {
            this._dataSender = dataSender;
            this._chatParser = chatParser;
            this._gameCapture = gameCapture;
            this._mouseMover = mouseMover;
            this._rivenCleaner = rivenCleaner;
            this._rivenParser = rivenParser;
            this._screenStateHandler = screenStateHandler;
            _redTextParser = redTextParser;

            Console.SetWindowSize(1, 1);
            Console.SetBufferSize(147, 10);
            Console.SetWindowSize(147, 9);
            Console.CursorVisible = false;
            Console.Clear();
            UpdateUI();
        }

        private string ColorString(string input) => input.Pastel("#bea966").PastelBg("#151d27");
        private void UpdateUILine(int line, string message, bool leftSide)
        {
            var maxWidth = Console.BufferWidth / 2;
            if (leftSide)
                maxWidth = 88;
            else
                maxWidth = 44;
            if (line >= Console.WindowHeight)
                return;
            if (leftSide)
                Console.SetCursorPosition(0, line);
            else
                Console.SetCursorPosition(103, line);
            //Draw left side
            if (message != null && message.Length > 0)
            {
                message = message.Substring(0, Math.Min(message.Length, maxWidth));
                Console.Write(ColorString(message));
            }
            var endPoint = leftSide ? maxWidth : Console.BufferWidth;
            for (int x = Console.CursorLeft; x < endPoint; x++)
            {
                Console.Write(ColorString(" "));
            }
            Console.SetCursorPosition(0, 0);
        }
        private void UpdateUIFirstLine()
        {
            UpdateUILine(0, _UIFirstLine, true);
        }
        private void UpdateUISecondLine()
        {
            UpdateUILine(1, _UISecondLine, true);
        }
        private void UpdateUIThirdLine()
        {
            UpdateUILine(2, _UIThirdLine, true);
        }
        private void UpdateUIRiven(Riven riven)
        {
            var maxWidth = Console.BufferWidth / 2;
            //Draw right side
            if (riven != null)
            {
                UpdateUILine(0, riven.Name, false);
                UpdateUILine(1, "Polarity: " + riven.Polarity, false);
                UpdateUILine(2, "Rank: " + riven.Rank, false);
                UpdateUILine(3, "Mastery rank: " + riven.MasteryRank, false);
                UpdateUILine(4, "Rolls: " + riven.Rolls, false);
                var line = 5;
                if (riven.Modifiers != null)
                {
                    foreach (var modi in riven.Modifiers)
                    {
                        if (line >= Console.WindowHeight)
                            return;
                        UpdateUILine(line++, modi.ToString(), false);
                    }
                }
                else
                    _dataSender.AsyncSendDebugMessage("FATAL: Missing modifiers for: " + riven.ImageId).Wait();
                while (line < Console.WindowHeight)
                {
                    UpdateUILine(line++, "", false);
                }
            }
            Console.SetCursorPosition(0, 0);
        }
        private void UpdateUI()
        {
            Console.Clear();
            UpdateUISeperators();

            //Draw left side
            UpdateUIFirstLine();
            UpdateUISecondLine();
            UpdateUIThirdLine();

            UpdateUIMessages();

            //Draw right side
            UpdateUIRiven(_UILastRiven);
            Console.SetCursorPosition(0, 0);
        }

        private void UpdateUISeperators()
        {
            //Draw vertical seperators
            for (int x = 89; x < 102; x++)
            {
                for (int y = 0; y < Console.WindowHeight; y++)
                {
                    Console.SetCursorPosition(x, y);
                    Console.Write(ColorString(" "));
                }
            }
            for (int y = 0; y < Console.WindowHeight; y++)
            {
                Console.SetCursorPosition(88, y);
                Console.Write(ColorString("│"));
            }
            for (int y = 0; y < Console.WindowHeight; y++)
            {
                Console.SetCursorPosition(102, y);
                Console.Write(ColorString("│"));
            }

            //Draw message seperator
            for (int x = 0; x < 88; x++)
            {
                Console.SetCursorPosition(x, 3);
                Console.Write(ColorString("─"));
            }
            Console.SetCursorPosition(88, 3);
            Console.Write(ColorString("┤"));
        }

        private void UpdateUIMessages()
        {
            var maxWidth = Console.BufferWidth / 2;
            var line = 4;
            if (_UIMessages.Count > 0)
            {
                _UIMessages = _UIMessages.Skip(Math.Max(0, _UIMessages.Count - Console.WindowHeight - line)).ToList();
                foreach (var item in _UIMessages)
                {
                    UpdateUILine(line++, item, true);
                }
            }
            else
            {
                UpdateUILine(line++, "No chat messages", true);
            }
            while (line < Console.WindowHeight)
            {
                UpdateUILine(line++, "", true);
            }
        }

        private string SafeColorString(int maxWidth, string input)
        {
            return ColorString(input.Substring(0, Math.Min(input.Length, maxWidth)));
        }

        public async Task MonitorLive(string debugImageDectory = null)
        {
            _redTextParser.OnRedText += async redtext => await _dataSender.AsyncSendRedtext(JsonConvert.SerializeObject(redtext));

            if (debugImageDectory != null && !Directory.Exists(debugImageDectory))
                Directory.CreateDirectory(debugImageDectory);
            if (!Directory.Exists(Path.Combine(Path.GetTempPath(), "wfchat")))
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "wfchat"));
            if (!Directory.Exists(Path.Combine(Path.GetTempPath(), "wfchat", "rivens")))
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "wfchat", "rivens"));
            foreach (var oldRiven in Directory.GetFiles(Path.Combine(Path.GetTempPath(), "wfchat", "rivens")))
            {
                File.Delete(oldRiven);
            }

            var newSW = new Stopwatch();
            var sw = new Stopwatch();
            var scrollbarFound = false;
            Bitmap b = null;
            var scrollDots = 0;
            var cachedRivens = new Queue<string>();
            var cachedRivenValues = new Dictionary<string, Riven>();
            while (true)
            {
                UpdateUISeperators();
                _UISecondLine = null;
                UpdateUISecondLine();
                _UIThirdLine = null;
                UpdateUIThirdLine();
                if (!scrollbarFound)
                    _UIFirstLine = "Finding scrollbar";
                else
                    _UIFirstLine = "Getting image";
                UpdateUIFirstLine();

                newSW.Restart();
                sw.Restart();
                //var image = string.Empty;
                //try
                //{
                //    newSW.Restart();
                //    b = _gameCapture.GetFullImage();
                //    b.Save(Path.Combine(Path.GetTempPath(), "wfchat", "capture_0.png"));
                //    b.Dispose();
                //    image = Path.Combine(Path.GetTempPath(), "wfchat", "capture_0.png");
                //    if (debugImageDectory != null)
                //    {
                //        File.Copy(image, Path.Combine(debugImageDectory, "capture_0.png"), true);
                //    }
                //}
                //catch { continue; }
                var imageTime = sw.Elapsed.TotalSeconds;
                sw.Restart();
                
                var image = _gameCapture.GetFullImage();
                if (image == null)
                {
                    _UIFirstLine = "Failed to get image";
                    UpdateUIFirstLine();
                    continue;
                }
                if (!_screenStateHandler.IsChatOpen(image))
                {
                    if (_screenStateHandler.IsExitable(image))
                    {
                        _UIFirstLine = "RECOVERING: clickign exit";
                        UpdateUIFirstLine();

                        //Click exit
                        _mouseMover.Click(3814, 2014);
                        await Task.Delay(30);
                        continue;
                    }
                    await _dataSender.AsyncSendDebugMessage("Help I'm stuck!");
                    await Task.Delay(5000);
                    image.Dispose();
                    continue;
                }

                //Wait for scrollbar to be ready
                if (!scrollbarFound)
                {
                    if (_chatParser.IsScrollbarPresent(image))
                    {
                        scrollbarFound = true;
                        _mouseMover.MoveTo(3259, 658);
                        await Task.Delay(33);
                        _mouseMover.Click(3259, 658);
                        await Task.Delay(100);
                        continue;
                    }
                    else
                    {
                        scrollDots++;
                        if (scrollDots > 4)
                            scrollDots = 0;
                        await Task.Delay(100);
                        continue;
                    }
                }

                sw.Restart();
                newSW.Restart();

                _UIFirstLine = "Parsing chat";
                UpdateUIFirstLine();
                newSW.Restart();
                var lines = _chatParser.ParseChatImage(image, true, true, 27);
                _UIMessages.AddRange(lines.Select(l => l.RawMessage));
                _UIFirstLine = "Parsing chat: " + lines.Length + " new messages. Riven cache count: " + cachedRivens.Count;
                UpdateUIFirstLine();
                UpdateUIMessages();
                var parseTime = sw.Elapsed.TotalSeconds;
                sw.Restart();

                string debugImageName = null;
                if (debugImageDectory != null)
                    debugImageName = Path.Combine(debugImageDectory, "debug_image_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-fff") + ".png");

                var newMessags = 0;
                var shouldCopyImage = false;
                var badNameRegex = new Regex("[^-A-Za-z0-9._]");
                //[00:00] f: .
                ChatMessageModel lastMessage = null;
                var hasher = MD5.Create();
                sw.Restart();
                var linesBad = false;
                var rivenParseTime = 0.0;
                var sendingTime = 0.0;
                var rivenSW = new Stopwatch();
                var sendingSW = new Stopwatch();
                foreach (var line in lines)
                {
                    _UISecondLine = "Handling generic: " + line.RawMessage;
                    UpdateUISecondLine();

                    var lineSw = new Stopwatch();
                    lineSw.Start();
                    if (linesBad)
                    {
                        lines.ToList().ForEach(l => _chatParser.InvalidCache(l.GetKey()));
                        break;
                    }
                    //if (line.LineType == LineParseResult.LineType.RedText)
                    //{
                    //    _UISecondLine = "Handing redtext: " + line.RawMessage;
                    //    UpdateUISecondLine();
                    //    //await _dataSender.AsyncSendRedtext(line.RawMessage);
                    //}
                    if (line.LineType == LineParseResult.LineType.NewMessage && line is ChatMessageLineResult)
                    {
                        _UISecondLine = "Current msg: " + line.RawMessage;
                        UpdateUISecondLine();

                        var chatMessageSw = new Stopwatch();
                        chatMessageSw.Start();
                        newSW.Restart();
                        var clr = line as ChatMessageLineResult;
                        var message = MakeChatModel(line as LineParseResult.ChatMessageLineResult, badNameRegex);
                        newMessags++;

                        for (int i = 0; i < clr.ClickPoints.Count; i++)
                        {
                            var clickPointSw = new Stopwatch();
                            clickPointSw.Start();
                            rivenSW.Restart();
                            var clickpoint = clr.ClickPoints[i];

                            _UIThirdLine = "Parsing riven: " + clickpoint.RivenName + " " + clickpoint.X + "," + clickpoint.Y;
                            UpdateUIThirdLine();

                            if (cachedRivenValues.ContainsKey(clr.Username + clickpoint.RivenName))
                            {
                                
                                var cachedRiven = cachedRivenValues[clr.Username + clickpoint.RivenName];
                                var copiedRiven = new Riven();
                                copiedRiven.Drain = cachedRiven.Drain;
                                copiedRiven.ImageId = cachedRiven.ImageId;
                                copiedRiven.MasteryRank = cachedRiven.MasteryRank;
                                copiedRiven.MessagePlacementId = clickpoint.Index;
                                copiedRiven.Modifiers = cachedRiven.Modifiers;
                                copiedRiven.Name = cachedRiven.Name;
                                copiedRiven.Polarity = cachedRiven.Polarity;
                                copiedRiven.Rank = cachedRiven.Rank;
                                copiedRiven.Rolls = cachedRiven.Rolls;
                                message.Rivens.Add(copiedRiven);
                                rivenParseTime += rivenSW.Elapsed.TotalSeconds;

                                await _dataSender.AsyncSendDebugMessage("Found a riven from cache: " + clr.Username + " " + clickpoint.RivenName);
                                _UISecondLine = "Riven: " + clickpoint.RivenName + " found in cache";
                                _UILastRiven = cachedRiven;
                                UpdateUISecondLine();
                                //UpdateUIRiven(cachedRiven);

                                continue;
                            }

                            var rivenImage = string.Empty;
                            var originalBytes = Encoding.UTF8.GetBytes(clr.Username);
                            var hashedBytes = hasher.ComputeHash(originalBytes);
                            var usernameHash = new StringBuilder();
                            foreach (Byte hashed in hashedBytes)
                                usernameHash.AppendFormat("{0:x2}", hashed);
                            rivenImage = Path.Combine(Path.GetTempPath(), "wfchat", "rivens", usernameHash.ToString() + "_" + i + ".png");
                            b = _gameCapture.GetChatIcon();
                            if (_chatParser.IsChatFocused(b))
                            {
                                _mouseMover.MoveTo(clickpoint.X, clickpoint.Y);
                                await Task.Delay(30);
                                _mouseMover.Click(clickpoint.X, clickpoint.Y);
                                await Task.Delay(17);
                            }
                            else
                            {
                                _chatParser.InvalidCache(line.GetKey());
                                linesBad = true;
                                rivenParseTime += rivenSW.Elapsed.TotalSeconds;
                                break;
                            }
                            b.Dispose();

                            _mouseMover.MoveTo(0, 0);
                            Bitmap crop = null;
                            var foundRiven = false;
                            for (int tries = 0; tries < 15; tries++)
                            {
                                b = _gameCapture.GetFullImage();
                                if (_screenStateHandler.GetScreenState(b) == ScreenState.RivenWindow)
                                {
                                    foundRiven = true;
                                    crop = _rivenParser.CropToRiven(b);
                                    b.Dispose();

                                    _mouseMover.Click(3816, 2013);
                                    await Task.Delay(17);
                                    _mouseMover.MoveTo(0, 0);
                                    await Task.Delay(17);
                                    break;
                                }
                                b.Dispose();
                            }
                            if (!foundRiven || crop == null)
                            {
                                linesBad = true;
                                _chatParser.InvalidCache(line.GetKey());
                                if (crop != null)
                                    crop.Dispose();
                                rivenParseTime += rivenSW.Elapsed.TotalSeconds;
                                break;
                            }

                            var newC = _rivenCleaner.CleanRiven(crop);
                            var riven = _rivenParser.ParseRivenTextFromImage(newC, clickpoint.RivenName);
                            riven.Name = clickpoint.RivenName;
                            riven.Polarity = _rivenParser.ParseRivenPolarityFromColorImage(crop);
                            riven.Rank = _rivenParser.ParseRivenRankFromColorImage(crop);
                            newC.Dispose();
                            //crop.Dispose();
                            if (riven == null)
                            {
                                //crop.Dispose();
                                _chatParser.InvalidCache(line.GetKey());
                                linesBad = true;
                                rivenParseTime += rivenSW.Elapsed.TotalSeconds;
                                break;
                            }

                            riven.MessagePlacementId = clickpoint.Index;

                            if (riven.Drain > 0 && riven.MasteryRank > 0)
                            {
                                cachedRivens.Enqueue(clr.Username + clickpoint.RivenName);
                                cachedRivenValues[clr.Username + clickpoint.RivenName] = riven;
                                while (cachedRivens.Count > 5000)
                                {
                                    var removed = cachedRivens.Dequeue();
                                    cachedRivenValues.Remove(removed);
                                }
                                _UIFirstLine = "Parsing chat: " + lines.Length + " new messages. Riven cache count: " + cachedRivens.Count;
                                UpdateUIFirstLine();
                            }
                            message.Rivens.Add(riven);

                            _UILastRiven = riven;
                            crop.Save(riven.ImageId + ".png");
                            UpdateUIRiven(riven);

                            File.Delete(rivenImage);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            _dataSender.AsyncSendRivenImage(riven.ImageId, crop);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            //await _dataSender.AsyncSendRivenImage(riven.ImageID, rivenBase64);

                            for (int tries = 0; tries < 15; tries++)
                            {
                                b = _gameCapture.GetFullImage();
                                var state = _screenStateHandler.GetScreenState(b);
                                if (_screenStateHandler.IsChatOpen(b))
                                {
                                    b.Dispose();
                                    break;
                                }
                                else if (state == ScreenState.RivenWindow)
                                {
                                    _mouseMover.Click(3816, 2013);
                                    await Task.Delay(17);
                                    _mouseMover.MoveTo(0, 0);
                                    await Task.Delay(17);
                                }
                                b.Dispose();
                            }
                        }
                        if (linesBad)
                        {
                            lines.ToList().ForEach(l => _chatParser.InvalidCache(l.GetKey()));
                            break;
                        }
                        if (message.DEBUGREASON != null)
                        {
                            message.DEBUGIMAGE = debugImageName;
                            shouldCopyImage = true;
                        }

                        rivenParseTime += rivenSW.Elapsed.TotalSeconds;
                        sendingSW.Restart();
                        await _dataSender.AsyncSendChatMessage(message);
                        sendingTime += sendingSW.Elapsed.TotalSeconds;
                    }
                }

                if (shouldCopyImage && debugImageName != null && debugImageName.Length > 0)
                {
                    try
                    {
                        image.Save(debugImageName);
                    }
                    catch { }
                    //File.Copy(image, debugImageName);
                }
                image.Dispose();
                var transmitTime = sw.Elapsed.TotalSeconds;
                sw.Stop();
                var debugMessage = $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000} New messages {newMessags} {newMessags / parseTime}/s";
                await _dataSender.AsyncSendDebugMessage(debugMessage);

                _UIFirstLine = "Scrolling";
                _UISecondLine = null;
                _UIThirdLine = null;
                UpdateUIFirstLine();
                UpdateUISecondLine();
                UpdateUIThirdLine();

                //Scroll down to get 27 more messages
                _mouseMover.MoveTo(3250, 768);
                //Scroll down for new page of messages
                for (int i = 0; i < 27; i++)
                {
                    _mouseMover.ScrollDown();
                    await Task.Delay(17);
                }
                for (int i = 0; i < 1; i++)
                {
                    _mouseMover.ScrollUp();//Pause chat
                    await Task.Delay(17);
                }
                _mouseMover.MoveTo(0, 0);
                await Task.Delay(100);
            }
        }

        private static ChatMessageModel MakeChatModel(LineParseResult.ChatMessageLineResult line, Regex badNameRegex)
        {
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
                SystemTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            if (debugReason != null)
            {
                cm.DEBUGREASON = debugReason;
            }
            cm.EnhancedMessage = line.EnhancedMessage;
            return cm;
        }
    }
}
