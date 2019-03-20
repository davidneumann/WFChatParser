using System.Collections;
using System.Collections.Generic;
using SixLabors.Primitives;

namespace WFImageParser
{
    /// <summary>
    /// A simple wrapper for using a Dictionary as a sparse multi-dimensional array
    /// </summary>
    public class CoordinateList : IEnumerable<Point>
    {
        private readonly Dictionary<(int x, int y), Point> _coordinates = new Dictionary<(int x, int y), Point>();

        public void Add(Point point)
        {
            _coordinates[(point.X, point.Y)] = point;
        }

        public int Count => _coordinates.Count;

        public bool Exists(Point point)
        {
            return _coordinates.ContainsKey((point.X, point.Y));
        }

        public bool Exists(int x, int y)
        {
            return _coordinates.ContainsKey((x, y));
        }


        public IEnumerator<Point> GetEnumerator()
        {
            return _coordinates.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}