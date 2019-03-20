using Application.ChatMessages.Model;
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

        public ChatWatcher(IDataSender dataSender, IImageParser chatParser, IGameCapture gameCapture)
        {
            this._dataSender = dataSender;
            this._chatParser = chatParser;
            this._gameCapture = gameCapture;
        }

        public async Task MonitorLive(string debugImageDectory = null)
        {
            if (debugImageDectory != null && !Directory.Exists(debugImageDectory))
                Directory.CreateDirectory(debugImageDectory);
            if (!Directory.Exists(Path.Combine(Path.GetTempPath(), "wfchat")))
                Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "wfchat"));
            
            var sw = new Stopwatch();
            var sentMessages = new Queue<ChatMessageModel>();
            while (true)
            {
                sw.Restart();
                if (debugImageDectory != null)
                {
                    for (int i = 6; i >= 0; i--)
                    {
                        try
                        {
                            var curFile = Path.Combine(debugImageDectory, "capture_" + i + ".png");
                            var lastFile = Path.Combine(debugImageDectory, "capture_" + (i + 1) + ".png");
                            if (File.Exists(lastFile))
                                File.Delete(lastFile);
                            if (File.Exists(curFile))
                                File.Move(curFile, lastFile);
                        }
                        catch { }
                    }
                }
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
                var messages = _chatParser.ParseChatImage(image);
                var parseTime = sw.Elapsed.TotalSeconds;
                sw.Restart();
                string debugImageName = null;
                if (debugImageDectory != null)
                    debugImageName = Path.Combine(debugImageDectory, "debug_image_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-fff") + ".png");

                var newMessags = 0;
                var shouldCopyImage = false;
                var badNameRegex = new Regex("[^-A-Za-z0-9._]");
                //[00:00] f: .
                var cms = messages.Where(line => line.RawMessage.Length >= 10).Select(result =>
                {
                    var m = result.RawMessage;
                    string debugReason = null;
                    var timestamp = m.Substring(0, 7).Trim();
                    var username = m.Substring(8).Trim();
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
                    return cm;
                });
                foreach (var message in cms)
                {

                    if (!sentMessages.Any(i => i.Author == message.Author && i.Timestamp == message.Timestamp))
                    {
                        newMessags++;
                        Console.Write($"\r{parseTime:N2}s: {message.Raw}");
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
                    else if (sentMessages.Any(i => i.Timestamp == message.Timestamp && i.Author == i.Author && !String.Equals(i.Raw, message.Raw)))
                    {
                        if (message.DEBUGREASON == null)
                            message.DEBUGREASON = string.Empty;
                        else
                            message.DEBUGREASON = message.DEBUGREASON + " || ";
                        message.DEBUGREASON += "Message parse differnet error, parse error!";
                        shouldCopyImage = true;
                        message.DEBUGIMAGE = debugImageName;
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
    }
}
