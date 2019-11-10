using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser.GlyphRecognition
{
    public class GlyphDetails
    {
        public bool[,] VMask { get; private set; }
        public float[,] WeightMappings { get; private set; }
        public string Name { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float TotalWeights { get; internal set; }

        public GlyphDetails(bool[,] vMask, float[,] weightMappings, string name, int width, int height, float totalWeight)
        {
            VMask = vMask;
            WeightMappings = weightMappings;
            Name = name;
            Width = width;
            Height = height;
            TotalWeights = totalWeight;
        }
    }
}
