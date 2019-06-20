using Application.Actionables.ChatBots;
using Application.ChatMessages.Model;
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
                            var rivens = new List<ChatMessages.Model.Riven>();
                            for (int i = 0; i < 5; i++)
                            {
                                var factor = rand.NextDouble() * 0.5f + 0.5f;
                                var scalingSW = new Stopwatch();
                                scalingSW.Start();
                                using (var cleanedScaledDown = new Bitmap(cleaned, new Size((int)(cleaned.Width * factor), (int)(cleaned.Height * factor))))
                                {
                                    using (var cleanedScaledBack = new Bitmap(cleanedScaledDown, new Size(cleaned.Width, cleaned.Height)))
                                    {
                                        scalingSW.Stop();
                                        _logger.Log(item.Message.Author + "'s riven scaled randomly in: " + scalingSW.ElapsedMilliseconds + " ms.");
                                        var tessParseSW = new Stopwatch();
                                        tessParseSW.Start();
                                        var parsedRiven = parser.ParseRivenTextFromImage(cleanedScaledBack, null);
                                        tessParseSW.Stop();
                                        _logger.Log(item.Message.Author + "'s riven tess parsed in: " + tessParseSW.ElapsedMilliseconds + " ms.");
                                        var imageParseSW = new Stopwatch();
                                        imageParseSW.Start();
                                        //parsedRiven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                                        //parsedRiven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);
                                        imageParseSW.Stop();
                                        _logger.Log(item.Message.Author + "'s riven other stuff parsed in: " + imageParseSW.ElapsedMilliseconds + " ms.");
                                        rivens.Add(parsedRiven);
                                    }
                                }
                            }
                            var combineSW = new Stopwatch();
                            combineSW.Start();
                            var riven = new Riven()
                            {
                                Drain = rivens.Select(p => p.Drain).GroupBy(d => d).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                ImageId = Guid.NewGuid(),
                                MasteryRank = rivens.Select(p => p.MasteryRank).GroupBy(mr => mr).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Modifiers = rivens.Select(p => p.Modifiers).GroupBy(ms => ms.Aggregate("", (key, m) => key + $"{m.Curse}{m.Description}{m.Value}")).OrderByDescending(g => g.Count()).Select(g => g.First()).First(),
                                //Polarity = rivens.Select(p => p.Polarity).GroupBy(p => p).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                //Rank = rivens.Select(p => p.Rank).GroupBy(rank => rank).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Rolls = rivens.Select(p => p.Rolls).GroupBy(rolls => rolls).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Name = rivens.Select(p => p.Name).GroupBy(name => name).OrderByDescending(g => g.Count()).Select(n => n.Key).First()
                            };

                            combineSW.Stop();
                            _logger.Log(item.Message.Author + "'s riven parses combined in: " + combineSW.ElapsedMilliseconds + " ms.");
                            //var riven = parser.ParseRivenTextFromImage(cleaned, r.RivenName);

                            riven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                            riven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);
                            riven.Name = r.RivenName;
                            if (riven.Name.ToLower().Trim() != r.RivenName.ToLower().Trim())
                                success = false;
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
                _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParserFactory.CreateChatParser());
            }

            var controlSw = new Stopwatch();
            while (!c.IsCancellationRequested)
            {
                for (int i = 0; i < _bots.Length; i++)
                {
                    var bot = _bots[i];
                    if (bot.IsRequestingControl)
                    {
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
                            _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParserFactory.CreateChatParser());
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
