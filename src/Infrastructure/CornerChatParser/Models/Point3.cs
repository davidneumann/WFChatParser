using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace RelativeChatParser.Models
{
    public class Point3
    {
        public int X { get; set; }
        public int Y { get; set; }
        public float Z { get; set; }

        public Point3(int x, int y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
