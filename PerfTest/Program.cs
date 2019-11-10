using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Application.Logger;
using WFImageParser;

namespace PerfTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var c = new ChatParser(new FakeLogger(), Path.Combine("ocrdata", "english"));
            var sw = new Stopwatch();
            var times = new List<TimeSpan>();

            foreach (var file in Directory.EnumerateFiles("PerfImages"))
            {
                sw.Restart();
                var messages = c.ParseChatImage(new Bitmap(file));
                var time = sw.Elapsed;
                times.Add(time);
                Console.WriteLine($"{file}: {messages.Length} messages ({time.TotalMilliseconds:N2} ms)");
            }

            var total = times.Sum(e => e.TotalMilliseconds);
            var avg = times.Average(e => e.TotalMilliseconds);
            var min = times.Min(e => e.TotalMilliseconds);
            var max = times.Max(e => e.TotalMilliseconds);

            Console.WriteLine($"total: {total:N2}ms avg: {avg:N2}ms min: {min:N2}ms max: {max:N2}ms");
        }
    }

    internal class FakeLogger : ILogger
    {
        public void Log(string message)
        {
        }
    }
}
