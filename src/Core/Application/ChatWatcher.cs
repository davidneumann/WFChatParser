using Application.ChatMessages.Model;
using Application.interfaces;
using Application.Interfaces;
using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Application
{
    public class ChatWatcher
    {
        private IDataSender _dataSender;
        private IImageParser _chatParser;
        private IGameCapture _gameCapture;
        //private IMouseMover _mouseMover;

        public ChatWatcher(IDataSender dataSender, IImageParser chatParser, IGameCapture gameCapture, IMouseMover mouseMover)
        {
            this._dataSender = dataSender;
            this._chatParser = chatParser;
            this._gameCapture = gameCapture;
            //this._mouseMover = mouseMover;
        }

        public async Task MonitorLive(string debugImageDectory = null)
        {
            if (debugImageDectory != null && !Directory.Exists(debugImageDectory))
                Directory.CreateDirectory(debugImageDectory);
            if (!Directory.Exists(Path.Combine(Path.GetTempPath(), "wfchat")))
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "wfchat"));

            var sw = new Stopwatch();
            var sentMessages = new Queue<ChatMessageModel>();
            var sentRedtext = new Queue<string>();
            while (true)
            {
                //_mouseMover.MoveTo(4, 768);
                ////Scroll down for new page of messages
                //for (int i = 0; i < 27; i++)
                //{
                //    _mouseMover.ScrollDown();
                //    await Task.Delay(16);
                //}
                //_mouseMover.ScrollUp();//Pause

                sw.Restart();
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
                var lines = _chatParser.ParseChatImage(image);
                var parseTime = sw.Elapsed.TotalSeconds;
                sw.Restart();
                string debugImageName = null;
                if (debugImageDectory != null)
                    debugImageName = Path.Combine(debugImageDectory, "debug_image_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-fff") + ".png");

                var newMessags = 0;
                var shouldCopyImage = false;
                var badNameRegex = new Regex("[^-A-Za-z0-9._]");
                //[00:00] f: .
                foreach (var line in lines)
                {
                    if(line.LineType == LineParseResult.LineType.RedText && !sentRedtext.Any(m => m == line.RawMessage))
                    {
                        await _dataSender.AsyncSendRedtext(line.RawMessage);
                        sentRedtext.Enqueue(line.RawMessage);
                        while (sentRedtext.Count > 20)
                            sentRedtext.Dequeue();
                    }
                    else if (line.LineType == LineParseResult.LineType.NewMessage)
                    {
                        var message = MakeChatModel(line as LineParseResult.ChatMessageLineResult, badNameRegex);
                        if (!sentMessages.Any(i => i.Author == message.Author && i.Timestamp == message.Timestamp))
                        {
                            newMessags++;
                            var time = String.Format("{0:N2}", parseTime);
                            var str = message.Raw;
                            if (str.Length + time.Length + 4 > Console.BufferWidth)
                                str = str.Substring(0, Console.BufferWidth - time.Length - 4);
                            Console.Write($"\r{parseTime:N2}s: {str}");
                            // Write space to end of line, and then CR with no LF
                            Console.Write("\r".PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
                            sentMessages.Enqueue(message);
                            while (sentMessages.Count > 100)
                                sentMessages.Dequeue();
                            if (message.DEBUGREASON != null)
                            {
                                message.DEBUGIMAGE = debugImageName;
                                shouldCopyImage = true;
                            }

                            await _dataSender.AsyncSendChatMessage(message);
                        }
                        else if (!sentMessages.Any(m => m.Timestamp == message.Timestamp && m.Author == message.Author && m.Raw == message.Raw)
                            && sentMessages.Any(i => i.Timestamp == message.Timestamp && i.Author == message.Author && !String.Equals(i.Raw, message.Raw)))
                        {
                            if (message.DEBUGREASON == null)
                                message.DEBUGREASON = string.Empty;
                            else
                                message.DEBUGREASON = message.DEBUGREASON + " || ";
                            var others = string.Empty;
                            sentMessages.Where(i => i.Timestamp == message.Timestamp && i.Author == message.Author && !String.Equals(i.Raw, message.Raw)).Select(m => m.Raw).ToList().ForEach(str => others += str + "\n ");
                            message.DEBUGREASON += "Message parse differnet error, parse error! Other(s):\n " + others;
                            shouldCopyImage = true;
                            message.DEBUGIMAGE = debugImageName;
                            sentMessages.Enqueue(message);

                            await _dataSender.AsyncSendChatMessage(message);
                        }
                    }
                }
                if (shouldCopyImage)
                {
                    File.Copy(image, debugImageName);
                }
                var transmitTime = sw.Elapsed.TotalSeconds;
                sw.Stop();
                var debugMessage = $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000} New messages {newMessags} {newMessags / parseTime}/s";
                await _dataSender.AsyncSendDebugMessage(debugMessage);
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
