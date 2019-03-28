using Application;
using DataStream;
using ImageOCR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WarframeDriver;
using WFGameCapture;
using WFImageParser;

namespace ChatLoggerCLI
{
    public class Program
    {
        private static List<IDisposable> _disposables = new List<IDisposable>();

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            
            var rivenParser = new RivenParser();
            _disposables.Add(rivenParser);
            //var c = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>("chars.json"));
            Console.WriteLine("Starting up image parser");
            var c = new ChatParser();
            //var t = new ImageParser();

            Console.WriteLine("Loading config for data sender");
            IConfiguration config = new ConfigurationBuilder()
              .AddJsonFile("appsettings.json", true, true)
              .AddJsonFile("appsettings.development.json", true, true)
              .AddJsonFile("appsettings.production.json", true, true)
              .Build();


            Console.WriteLine("Data sender connecting");
            var dataSender = new DataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"]);

            dataSender.RequestToKill += (s, e) =>
            {
                Console_CancelKeyPress(null, null);
            };
            dataSender.RequestSaveAll += (s, e) =>
            {
                try
                {
                    for (int i = 6; i >= 0; i--)
                    {
                        var dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves");
                        if (e.Name != null && e.Name.Length > 0)
                            dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves", e.Name);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        var curFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + i + ".png");
                        var copyFile = Path.Combine(dir, "capture_" + i + ".png");
                        if (File.Exists(curFile))
                            File.Copy(curFile, copyFile, true);
                    }
                }
                catch { }
            };

            var watcher = new ChatWatcher(dataSender, c, new GameCapture(), new MouseHelper(), new RivenCleaner(), rivenParser, new ScreenStateHandler());
            Task t =  Task.Run(() => watcher.MonitorLive(config["DEBUG:ImageDirectory"]));
            while(true)
            {
                if (t.IsFaulted || t.Exception != null)
                {
                    Console.WriteLine("\n" + t.Exception);
                    try
                    {
                        dataSender.AsyncSendDebugMessage(t.Exception.ToString());
                        System.Threading.Thread.Sleep(2000);
                    }
                    catch
                    { }
                    break;
                }
                else if (t.IsCompleted || t.IsCanceled)
                    break;
                //var debug = progress.GetAwaiter().IsCompleted;
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            foreach (var item in _disposables)
            {
                if(item != null)
                    item.Dispose();
            }
            Environment.Exit(0);
        }
    }
}
