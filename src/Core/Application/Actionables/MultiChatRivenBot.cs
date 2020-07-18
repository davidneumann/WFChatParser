using Application.Actionables.ChatBots;
using Application.ChatMessages.Model;
using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables
{
    public class MultiChatRivenBot
    {
        private WarframeClientInformation[] _warframeCredentials;

        private TradeChatBot[] _bots;

        private IMouse _mouse;
        private IKeyboard _keyboard;
        private IScreenStateHandler _screenStateHandler;
        private IRivenParserFactory _rivenParserFactory;
        private IRivenCleaner _rivenCleaner;
        private IDataSender _dataSender;
        private List<Thread> _rivenQueueWorkers = new List<Thread>();
        private ConcurrentQueue<RivenParseTaskWorkItem> _rivenWorkQueue = new ConcurrentQueue<RivenParseTaskWorkItem>();
        private ILogger _logger;
        private IGameCapture _gameCapture;
        //private IChatParser _chatParser;
        private IChatParserFactory _chatParserFactory;

        public MultiChatRivenBot(WarframeClientInformation[] warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            IRivenParserFactory rivenParserFactory,
            IRivenCleaner rivenCleaner,
            IDataSender dataSender,
            IGameCapture gameCapture,
            ILogger logger,
            IChatParserFactory chatParserFactory)
        {
            _warframeCredentials = warframeCredentials;
            _bots = new TradeChatBot[_warframeCredentials.Length];
            _mouse = mouse;
            _keyboard = keyboard;
            _screenStateHandler = screenStateHandler;
            _rivenParserFactory = rivenParserFactory;
            _rivenCleaner = rivenCleaner;
            _dataSender = dataSender;
            _logger = logger;
            _gameCapture = gameCapture;
            _chatParserFactory = chatParserFactory;
        }

        public void ProcessRivenQueue(CancellationToken c)
        {
            var rand = new Random();
            var parser = _rivenParserFactory.CreateRivenParser();
            while (true)
            {
                if (c.IsCancellationRequested)
                    break;
                if (_rivenWorkQueue.Count > 0)
                    _logger.Log("Worker thread taking new message from queue of " + _rivenWorkQueue.Count + " items");

                RivenParseTaskWorkItem item = null;
                if (!_rivenWorkQueue.TryDequeue(out item) || item == null)
                {
                    Thread.Sleep(250);
                    continue;
                }
                _logger.Log("Worker queue working on: " + item.Message.Author + ":" + item.Message.EnhancedMessage);
                var fullTimeSw = new Stopwatch();
                fullTimeSw.Start();
                var success = true;
                foreach (var r in item.RivenWorkDetails)
                {
                    var cropSW = new Stopwatch();
                    cropSW.Start();
                    using (var croppedCopy = new Bitmap(r.CroppedRivenBitmap))
                    {
                        _logger.Log(item.Message.Author + "'s riven image cropped in: " + cropSW.ElapsedMilliseconds + " ms.");
                        cropSW.Stop();
                        var cleanSW = new Stopwatch();
                        cleanSW.Start();
                        using (var cleaned = _rivenCleaner.CleanRiven(croppedCopy))
                        {
                            _logger.Log(item.Message.Author + "'s riven image cleaned in: " + cleanSW.ElapsedMilliseconds + " ms.");
                            cleanSW.Stop();

                            var riven = parser.ParseRivenTextFromImage(cleaned, r.RivenName);

                            riven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                            riven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);
                            if (r.RivenName != null && r.RivenName.Length > 0)
                            {
                                riven.Name = r.RivenName;
                                if (riven.Name.ToLower().Trim() != r.RivenName.ToLower().Trim())
                                    success = false;
                            }
                            riven.MessagePlacementId = r.RivenIndex;

                            item.Message.Rivens.Add(riven);
                            var outputDir = Path.Combine("riven_images", DateTime.Now.ToString("yyyy_MM_dd"));
                            if (!Directory.Exists(outputDir))
                            {
                                try
                                {
                                    Directory.CreateDirectory(outputDir);
                                }
                                catch { }
                            }
                            try
                            {
                                var saveSW = new Stopwatch();
                                saveSW.Start();
                                croppedCopy.Save(Path.Combine(outputDir, riven.ImageId.ToString() + ".png"));
                                _logger.Log(item.Message.Author + "'s riven image saved to disk in: " + saveSW.ElapsedMilliseconds + " ms.");
                                saveSW.Stop();
                            }
                            catch { }
                            _dataSender.AsyncSendRivenImage(riven.ImageId, croppedCopy);
                        }
                    }
                }
                if (success)
                {
                    item.MessageCache.Enqueue(item.Message.Author + item.Message.EnhancedMessage);
                    item.MessageCacheDetails[item.Message.Author + item.Message.EnhancedMessage] = item.Message;
                    _logger.Log("Riven parsed and added " + item.Message.Author + "'s message to cache in: " + fullTimeSw.ElapsedMilliseconds + " ms.");
                }
                else
                {
                    _dataSender.AsyncSendDebugMessage("Failed to parse riven correctly");
                }
                _dataSender.AsyncSendChatMessage(item.Message);
            }

            if (parser is IDisposable)
                ((IDisposable)parser).Dispose();
        }

        public async Task AsyncRun(CancellationToken c)
        {
            KillAllWarframes();

            if (_rivenQueueWorkers.Count <= 0)
            {
                lock (_rivenQueueWorkers)
                {
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        var thread = new Thread(() => ProcessRivenQueue(c));
                        thread.Start();
                        _rivenQueueWorkers.Add(thread);
                        _logger.Log("New worker thread added. Total: " + _rivenQueueWorkers.Count);
                    }
                }
            }

            for (int i = 0; i < _bots.Length; i++)
            {
                var language = ClientLanguage.English;
                if (_warframeCredentials[i].Region == "T_ZH")
                    language = ClientLanguage.Chinese;

                _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParserFactory.CreateChatParser(language));
            }

            var controlSw = new Stopwatch();
            while (!c.IsCancellationRequested)
            {
                var possibleBadState = true;
                foreach (var bot in _bots)
                {
                    if(bot != null && bot.LastMessage != null && DateTime.UtcNow.Subtract(bot.LastMessage).TotalMinutes < 15)
                    {
                        possibleBadState = false;
                        break;
                    }
                }
                if(possibleBadState)
                {
                    try
                    {
                        _dataSender.AsyncSendDebugMessage("CRITICIAL FAILURE DETECTED! No messages in 15 minutes from any bot! Attempting to restart PC").Wait();
                    }
                    catch { }
                    var shutdown = new System.Diagnostics.Process()
                    {
                        StartInfo = new ProcessStartInfo("shutdown.exe", "/r /f /t 0")
                    };
                    shutdown.Start();
                    break;
                }

                for (int i = 0; i < _bots.Length; i++)
                {
                    _logger.Log($"Looking at bot {i} of {_bots.Length}");
                    var bot = _bots[i];
                    if (bot.IsRequestingControl)
                    {
                        _logger.Log($"bot {i} is requesting control");
                        controlSw.Restart();
                        try
                        {
                            _logger.Log("Giving control to: " + _warframeCredentials[i].StartInfo.UserName + ":" + _warframeCredentials[i].Region);
                            await bot.TakeControl();
                            await Task.Delay(17);
                        }
                        catch (Exception e)
                        {
                            _logger.Log("Exception: " + e.ToString());
                            bot.ShutDown();

                            var language = ClientLanguage.English;
                            if (_warframeCredentials[i].Region == "T_ZH")
                                language = ClientLanguage.Chinese;
                            _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParserFactory.CreateChatParser(language));
                        }
                        _logger.Log(_warframeCredentials[i].StartInfo.UserName + ":" + _warframeCredentials[i].Region + " finished task in: " + controlSw.Elapsed.TotalSeconds + " seconds.");
                        controlSw.Stop();
                    }
                }
            }
        }

        private void KillAllWarframes()
        {
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                var name = process.ProcessName.ToLower();
                if (name.Contains("launcher") || name.Contains("warframe"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
