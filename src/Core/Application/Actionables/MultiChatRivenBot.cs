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
        private IChatParser _chatParser;

        public MultiChatRivenBot(WarframeClientInformation[] warframeCredentials,
            IMouse mouse, 
            IKeyboard keyboard, 
            IScreenStateHandler screenStateHandler,
            IRivenParserFactory rivenParserFactory,
            IRivenCleaner rivenCleaner,
            IDataSender dataSender,
            IGameCapture gameCapture,
            IChatParser chatParser,
            ILogger logger)
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
            _chatParser = chatParser;
        }

        public void ProcessRivenQueue(CancellationToken c)
        {
            var rand = new Random();
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
                            var rivens = new List<ChatMessages.Model.Riven>();
                            for (int i = 0; i < 10; i++)
                            {
                                var factor = rand.NextDouble() * 0.5f + 0.5f;
                                using (var cleanedScaledDown = new Bitmap(cleaned, new Size((int)(cleaned.Width * factor), (int)(cleaned.Height * factor))))
                                {
                                    using (var cleanedScaledBack = new Bitmap(cleanedScaledDown, new Size(cleaned.Width, cleaned.Height)))
                                    {
                                        var parsedRiven = parser.ParseRivenTextFromImage(cleanedScaledBack, null);
                                        parsedRiven.Polarity = parser.ParseRivenPolarityFromColorImage(croppedCopy);
                                        parsedRiven.Rank = parser.ParseRivenRankFromColorImage(croppedCopy);
                                        rivens.Add(parsedRiven);
                                    }
                                }
                            }
                            var riven = new Riven()
                            {
                                Drain = rivens.Select(p => p.Drain).GroupBy(d => d).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                ImageId = Guid.NewGuid(),
                                MasteryRank = rivens.Select(p => p.MasteryRank).GroupBy(mr => mr).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Modifiers = rivens.Select(p => p.Modifiers).GroupBy(ms => ms.Aggregate("", (key, m) => key + $"{m.Curse}{m.Description}{m.Value}")).OrderByDescending(g => g.Count()).Select(g => g.First()).First(),
                                Polarity = rivens.Select(p => p.Polarity).GroupBy(p => p).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Rank = rivens.Select(p => p.Rank).GroupBy(rank => rank).OrderByDescending(g => g.Count()).Select(g => g.Key).First(),
                                Rolls = rivens.Select(p => p.Rolls).GroupBy(rolls => rolls).OrderByDescending(g => g.Count()).Select(g => g.Key).First()
                            };
                            riven.Name = r.RivenName;
                            riven.MessagePlacementId = r.RivenIndex;
                            item.Message.Rivens.Add(riven);
                            if(!Directory.Exists("riven_images"))
                            {
                                try
                                {
                                    Directory.CreateDirectory("riven_images");
                                }
                                catch { }
                            }
                            try
                            {
                                croppedCopy.Save(Path.Combine("riven_images", riven.ImageId.ToString() + ".png"));
                            }
                            catch { }
                            _dataSender.AsyncSendRivenImage(riven.ImageId, croppedCopy);
                        }
                    }
                }
                item.MessageCache.Enqueue(item.Message.Author + item.Message.EnhancedMessage);
                item.MessageCacheDetails[item.Message.Author + item.Message.EnhancedMessage] = item.Message;
                _dataSender.AsyncSendChatMessage(item.Message);
            }

            if (parser is IDisposable)
                ((IDisposable)parser).Dispose();
        }

        public async Task AsyncRun(CancellationToken c)
        {
            if (_rivenQueueWorkers.Count <= 0)
            {
                lock (_rivenQueueWorkers)
                {
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        var thread = new Thread(() => ProcessRivenQueue(c));
                        thread.Start();
                        _rivenQueueWorkers.Add(thread);
                    }
                }
            }

            for (int i = 0; i < _bots.Length; i++)
            {
                _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParser);
            }

            while (!c.IsCancellationRequested)
            {
                for (int i = 0; i < _bots.Length; i++)
                {
                    var bot = _bots[i];
                    if (bot.IsRequestingControl)
                    {
                        try
                        {
                            await bot.TakeControl();
                            await Task.Delay(17);
                        }
                        catch (Exception e)
                        {
                            _logger.Log("Exception: " + e.ToString());
                            bot.ShutDown();
                            _bots[i] = new TradeChatBot(_rivenWorkQueue, _rivenParserFactory.CreateRivenParser(), c, _warframeCredentials[i], _mouse, _keyboard, _screenStateHandler, _logger, _gameCapture, _dataSender, _chatParser);
                        }
                    }
                }
            }
        }
    }
}
