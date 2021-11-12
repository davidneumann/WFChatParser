using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RustRayRecognizer.Extraction
{
    public static class LineScanner
    {
        public static int[] LineOffsets = new int[] { 891, 936, 981, 1025, 1070, 1115, 1160, 1205, 1249, 1294, 1339, 1384, 1429, 1473, 1518, 1563, 1608, 1653, 1697, 1742, 1787, 1832, 1877, 1921, 1966, 2011 };

        public static readonly int ChatWidth = 3236;
        public static readonly int Lineheight = 38;

        public static readonly int ChatLeftX = 4;

        public static int[] ExtractLineOffsets(ImageCache image)
        {
            var offsets = new List<int>();
            for (int y = 726; y < 2095; y++)
            {
                for (int x = ChatLeftX; x < ChatWidth; x++)
                {
                    if(image[x,y] > 0)
                    {
                        offsets.Add(y);
                        y += Lineheight + 5;
                        break;
                    }
                }
            }

            return offsets.ToArray();
        }
    }
}
