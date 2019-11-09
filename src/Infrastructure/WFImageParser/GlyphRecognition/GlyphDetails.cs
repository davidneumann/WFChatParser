﻿using System;
using System.Collections.Generic;
using System.Text;

namespace WFImageParser.GlyphRecognition
{
    internal class GlyphDetails
    {
        public bool[,] VMask { get; set; }
        public float[,] WeightMappings { get; set; }
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float TotalWeights { get; internal set; }
    }
}
