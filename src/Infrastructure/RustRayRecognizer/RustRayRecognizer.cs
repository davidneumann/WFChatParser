using Application.ChatLineExtractor;
using Application.Interfaces;
using Application.LineParseResult;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RustRayRecognizer
{
    public class RustRayRecognizer : IChatParser
    {
        private Queue<string> _timeUserCache = new Queue<string>();

        public void InvalidateCache(string key)
        {
            throw new NotImplementedException();
        }

        public bool IsChatFocused(System.Drawing.Bitmap chatIconBitmap)
        {
            var darkPixels = new Point[] { new Point(23, 15), new Point(30, 35), new Point(37, 15), new Point(43, 35) };
            var lightPixles = new Point[] { new Point(17, 25), new Point(24, 12), new Point(26, 19), new Point(32, 24), new Point(40, 32), new Point(30, 43) };
            if (darkPixels.Any(p =>
            {
                var pixel = chatIconBitmap.GetPixel(p.X, p.Y);
                if (pixel.R > 100 || pixel.G > 100 || pixel.G > 100)
                    return true;
                return false;
            }))
                return false;
            if (lightPixles.Any(p =>
            {
                var pixel = chatIconBitmap.GetPixel(p.X, p.Y);
                if (pixel.R < 180 || pixel.G < 180 || pixel.G < 180)
                    return true;
                return false;
            }))
                return false;
            return true;
        }

        public bool IsScrollbarPresent(System.Drawing.Bitmap screenImage)
        {
            if (screenImage.Width != 4096 || screenImage.Height != 2160)
                return false;

            var threshold = (byte)252;
            for (int y = 2097; y > 655; y--)
            {
                var pixel = screenImage.GetPixel(3256, y);
                if (pixel.R > threshold && pixel.G > threshold && pixel.B > threshold)
                    return true;
            }

            return false;
        }

        public ChatMessageLineResult[] ParseChatImage(System.Drawing.Bitmap image, bool useCache, bool isScrolledUp, int lineParseCount)
        {
            //lineParseCount = Math.Min(lineParseCount, LineScanner.LineOffsets.Length);
            var imageCache = new ImageCache(image);
            while (_timeUserCache.Count > 75)
            {
                var removed = _timeUserCache.Dequeue();
                //_logger.Log($"Removed {removed} from parser cache");
            }

            return null;
        }
    }
}
