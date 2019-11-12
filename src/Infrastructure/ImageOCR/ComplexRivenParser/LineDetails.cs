using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageOCR.ComplexRivenParser
{
    public partial class ComplexRivenParser
    {
        private class LineDetails
        {
            public Rectangle LineRect { get; set; }

            private RivenImage _rivenImage;

            private List<Rectangle> _charRects = new List<Rectangle>();
            public List<Rectangle> CharacterRects
            {
                get
                {
                    if (_charRects.Count == 0)
                        _charRects = UpdateCharacterRects();
                    return _charRects;
                }
            }

            public LineDetails(Rectangle lineRect, RivenImage rivenImage)
            {
                LineRect = lineRect;
                _rivenImage = rivenImage;
            }

            public List<Rectangle> UpdateCharacterRects()
            {
                var results = new List<Rectangle>();
                var onChar = false;
                var startX = 0;
                var startY = -1;
                var endY = 0;
                for (int x = LineRect.Left; x < LineRect.Right; x++)
                {
                    if (x < 0 || x >= _rivenImage.Width)
                        continue;
                    var purpleFound = false;
                    for (int y = LineRect.Top; y < LineRect.Bottom; y++)
                    {
                        if (y < 0 || y >= _rivenImage.Height)
                            continue;
                        if(_rivenImage.IsPurple(x,y) || _rivenImage.HasNeighbor(x,y, 1))
                        {
                            purpleFound = true;
                            if (startY == -1 || y < startY)
                                startY = y;
                            if (y > endY)
                                endY = y + 1;
                        }
                    }
                    if(!onChar && purpleFound) //Start of character
                    {
                        startX = x;
                        onChar = true;
                    }
                    else if(onChar && !purpleFound) //Character ended
                    {
                        //Add 1 pixel of spacing around characters
                        var safeXStart = Math.Max(0, startX - 1);
                        var safeYStart = Math.Max(0, startY - 1);
                        var safeWidth = Math.Min(_rivenImage.Width, x + 1 - safeXStart);
                        var safeHeight = Math.Min(_rivenImage.Height, endY + 1 - safeYStart);
                        results.Add(new Rectangle(safeXStart, safeYStart, safeWidth, safeHeight));
                        startX = 0;
                        startY = -1;
                        endY = 0;
                        onChar = false;
                    }
                }
                return results;
            }
        }
    }
}
