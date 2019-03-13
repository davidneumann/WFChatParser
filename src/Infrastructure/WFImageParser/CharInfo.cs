using System.Collections.Generic;

namespace WFImageParser
{
    public class CharInfo
    {
        public char Character { get; set; }
        public int Width { get; set; }
        public List<List<int>> Slices { get; set; } = new List<List<int>>();
    }
}