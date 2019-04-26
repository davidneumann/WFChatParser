using Application.Actionables.ChatBots;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables
{
    public class MultiChatRivenBot
    {
        private WarframeCredentials[] _warframeCredentials;

        private TradeChatBot[] _bots;

        private IMouse _mouse;
        private IKeyboard _keyboard;
        private IScreenStateHandler _screenStateHandler;
        private IRivenParserFactory _rivenParserFactory;
        private IRivenCleaner _rivenCleaner;
        private IDataSender _dataSender;
        private List<Thread> _rivenQueueWorkers = new List<Thread>();
        private ConcurrentQueue<RivenParseTaskWorkItem> _rivenWorkQueue = new ConcurrentQueue<RivenParseTaskWorkItem>();
        private Application.Logger.Logger _logger;
        private IGameCapture _gameCapture;
        private IChatParser _chatParser;

        public MultiChatRivenBot(WarframeCredentials[] warframeCredentials,
            IMouse mouse, 
            IKeyboard keyboard, 
            IScreenStateHandler screenStateHandler,
            IRivenParserFactory rivenParserFactory,
            IRivenCleaner rivenCleaner,
            IDataSender dataSender,
            IGameCapture gameCapture,
            IChatParser chatParser)
        {
            _warframeCredentials = warframeCredentials;
            _bots = new TradeChatBot[_warframeCredentials.Length];
            _mouse = mouse;
            _keyboard = keyboard;
            _screenStateHandler = screenStateHandler;
            _rivenParserFactory = rivenParserFactory;
            _rivenCleaner = rivenCleaner;
            _dataSender = dataSender;
            _logger = new Application.Logger.Logger(_dataSender);
            _gameCapture = gameCapture;
            _chatParser = chatParser;
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
                //_messageCache.Enqueue(item.Message.Author + item.Message.EnhancedMessage);
                //_messageCacheDetails[item.Message.Author + item.Message.EnhancedMessage] = item.Message;
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
