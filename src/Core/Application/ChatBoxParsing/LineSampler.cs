using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.ChatBoxParsing
{
    public static class LineSampler
    {
        private static Point[,] _samplePoints;
        private static readonly int _zoneSize = 25;
        private static readonly int _zoneSizeSquared = _zoneSize * _zoneSize;

        static LineSampler()
        {
            _samplePoints = new Point[27, 3];
            for (int line = 0; line < 27; line++)
            {
                for (int i = 0; i < 3; i++)
                {
                    _samplePoints[line, i] = new Point(162 + i * 200, 770 + line * 50);
                }
            }
        }

        public static Rgb[,] GetAllLineSamples(Bitmap fullImage)
        {
            var results = new Rgb[_samplePoints.GetLength(0), _samplePoints.GetLength(1)];

            for (int lineIndex = 0; lineIndex < _samplePoints.GetLength(0); lineIndex++)
            {
                for (int sampleIndex = 0; sampleIndex < _samplePoints.GetLength(1); sampleIndex++)
                {
                    results[lineIndex, sampleIndex] = GetSample(fullImage, lineIndex, sampleIndex);
                }
            }

            return results;
        }

        private static Rgb GetSample(Bitmap fullImage, int lineIndex, int sampleIndex)
        {
            var rTotal = 0;
            var gTotal = 0;
            var bTotal = 0;
            for (int x = 0; x < _zoneSize; x++)
            {
                for (int y = 0; y < _zoneSize; y++)
                {
                    var p = fullImage.GetPixel(_samplePoints[lineIndex, sampleIndex].X + x,
                        _samplePoints[lineIndex, sampleIndex].Y + y);
                    rTotal += p.R;
                    gTotal += p.G;
                    bTotal += p.B;
                }
            }
            return new Rgb((byte)(rTotal / _zoneSizeSquared), (byte)(gTotal / _zoneSizeSquared), (byte)(bTotal / _zoneSizeSquared));
        }

        public static Rgb[] GetLineSamples(Bitmap fullImage, int line)
        {
            var results = new Rgb[_samplePoints.GetLength(1)];

            for (int sampleIndex = 0; sampleIndex < results.Length; sampleIndex++)
            {
                results[sampleIndex] = GetSample(fullImage, line, sampleIndex);
            }

            return results;
        }
    }
}