namespace WFImageParser
{
    internal class TargetMask
    {
        public bool[,] Mask { get; internal set; }
        public int MaxX { get; internal set; }
        public int MinX { get; internal set; }
        public int Width { get; set; }
        public int PixelCount { get; internal set; }

        public TargetMask(bool[,] mask, int maxX, int minX, int width, int pixelCount)
        {
            Mask = mask;
            MaxX = maxX;
            MinX = minX;
            Width = width;
            PixelCount = pixelCount;
        }
    }
}