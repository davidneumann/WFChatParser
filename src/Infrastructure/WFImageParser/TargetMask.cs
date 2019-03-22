﻿namespace WFImageParser
{
    internal class TargetMask
    {
        public bool[,] Mask { get; internal set; }
        public int MaxX { get; internal set; }
        public int MinX { get; internal set; }
        public int Width { get; set; }
        public int PixelCount { get; internal set; }
        public float SoftPixelCount { get; }

        public TargetMask(bool[,] mask, int maxX, int minX, int width, int pixelCount, float softPixelCount)
        {
            Mask = mask;
            MaxX = maxX;
            MinX = minX;
            Width = width;
            PixelCount = pixelCount;
            SoftPixelCount = softPixelCount;
        }
    }
}