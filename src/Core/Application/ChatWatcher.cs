using Application.ChatMessages.Model;
using Application.interfaces;
using Application.Interfaces;
using Application.LineParseResult;
using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        public ChatWatcher(IDataSender dataSender, IChatParser chatParser, IGameCapture gameCapture, IMouseMover mouseMover, IRivenCleaner rivenCleaner, IRivenParser rivenParser)
        {
            this._dataSender = dataSender;
            this._chatParser = chatParser;
            this._gameCapture = gameCapture;
            this._mouseMover = mouseMover;
            this._rivenCleaner = rivenCleaner;
            this._rivenParser = rivenParser;
        }

        public async Task MonitorLive(string debugImageDectory = null)
        {
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

            var sw = new Stopwatch();
            var scrollbarFound = false;
            Console.Write("Waiting for scrollbar");
            while (true)
            {
                sw.Restart();
                var image = string.Empty;
                try
                {
                    image = _gameCapture.GetTradeChatImage(Path.Combine(Path.GetTempPath(), "wfchat", "capture_0.png"));
                    if (debugImageDectory != null)
                    {
                        File.Copy(image, Path.Combine(debugImageDectory, "capture_0.png"), true);
                    }
                }
                catch { continue; }
                var imageTime = sw.Elapsed.TotalSeconds;
                sw.Restart();

                ////Wait for scrollbar to be ready
                //if (!scrollbarFound)
                //{
                //    if (_chatParser.IsScrollbarPresent(image))
                //    {
                //        scrollbarFound = true;
                //        _mouseMover.MoveTo(3259, 658);
                //        await Task.Delay(33);
                //        _mouseMover.Click(3259, 658);
                //        await Task.Delay(33);
                //        Console.Write("\rScrollbar found\n");
                //        continue;
                //    }
                //    else
                //        continue;
                //}

                //if (debugImageDectory != null)
                //{
                //    for (int i = 6; i >= 0; i--)
                //    {
                //        try
                //        {
                //            var curFile = Path.Combine(debugImageDectory, "capture_" + i + ".png");
                //            var lastFile = Path.Combine(debugImageDectory, "capture_" + (i + 1) + ".png");
                //            if (File.Exists(lastFile))
                //                File.Delete(lastFile);
                //            if (File.Exists(curFile))
                //                File.Move(curFile, lastFile);
                //        }
                //        catch { }
                //    }
                //}

                sw.Restart();
                var lines = _chatParser.ParseChatImage(image, true, false);
                Console.Write("\rFound " + lines.Count() + " new lines");
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
                foreach (var line in lines)
                {
                    if (line.LineType == LineParseResult.LineType.RedText)
                    {
                        await _dataSender.AsyncSendRedtext(line.RawMessage);
                    }
                    else if (line.LineType == LineParseResult.LineType.NewMessage && line is ChatMessageLineResult)
                    {
                        var clr = line as ChatMessageLineResult;
                        var message = MakeChatModel(line as LineParseResult.ChatMessageLineResult, badNameRegex);
                        newMessags++;

                        for (int i = 0; i < clr.ClickPoints.Count; i++)
                        {
                            var clickpoint = clr.ClickPoints[i];

                            var rivenImage = string.Empty;
                            var originalBytes = Encoding.UTF8.GetBytes(clr.Username);
                            var hashedBytes = hasher.ComputeHash(originalBytes);
                            var usernameHash = new StringBuilder();
                            foreach (Byte hashed in hashedBytes)
                                usernameHash.AppendFormat("{0:x2}", hashed);
                            rivenImage = Path.Combine(Path.GetTempPath(), "wfchat", "rivens", usernameHash.ToString() + "_" + i + ".png");

                            _mouseMover.MoveTo(clickpoint.X, clickpoint.Y);
                            return;
                            await Task.Delay(66);
                            _mouseMover.Click(clickpoint.X, clickpoint.Y);
                            await Task.Delay(66);
                            _gameCapture.GetRivenImage(rivenImage);
                            _rivenCleaner.CleanRiven(rivenImage);
                            var riven = _rivenParser.ParseRivenImage(rivenImage);
                            riven.MessagePlacementId = clickpoint.Index;
                            message.Rivens.Add(riven);

                            File.Delete(rivenImage);

                            _mouseMover.MoveTo(3798, 2005);
                            _mouseMover.Click(3798, 2005);
                        }
                        if (message.DEBUGREASON != null)
                        {
                            message.DEBUGIMAGE = debugImageName;
                            shouldCopyImage = true;
                        }

                        await _dataSender.AsyncSendChatMessage(message);
                    }
                }
                if(lastMessage != null)
                {
                    var time = String.Format("{0:N2}", parseTime);
                    var str = lastMessage.Raw;
                    if (str.Length + time.Length + 4 > Console.BufferWidth)
                        str = str.Substring(0, Console.BufferWidth - time.Length - 4);
                    Console.Write($"\r{parseTime:N2}s: {str}");
                    // Write space to end of line, and then CR with no LF
                    Console.Write("\r".PadLeft(Console.WindowWidth - Console.CursorLeft));
                }
                if (shouldCopyImage)
                {
                    File.Copy(image, debugImageName);
                }
                var transmitTime = sw.Elapsed.TotalSeconds;
                sw.Stop();
                var debugMessage = $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000} New messages {newMessags} {newMessags / parseTime}/s";
                await _dataSender.AsyncSendDebugMessage(debugMessage);

                //Scroll down to get 27 more messages
                _mouseMover.MoveTo(3250, 768);
                //Scroll down for new page of messages
                for (int i = 0; i < 24; i++)
                {
                    _mouseMover.ScrollDown();
                    await Task.Delay(16);
                }
                await Task.Delay(16);
                _mouseMover.ScrollUp();//Pause chat
                _mouseMover.MoveTo(0, 0);
                await Task.Delay(66);
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
