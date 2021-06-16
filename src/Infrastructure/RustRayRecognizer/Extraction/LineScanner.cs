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
        public static int[] LineOffsets = new int[] { 767, 816, 866, 915, 965, 1014, 1064, 1114, 1163, 1213, 1262, 1312, 1362, 1411, 1461, 1510, 1560, 1610, 1659, 1709, 1758, 1808, 1857, 1907, 1957, 2006, 2056 };
        
        public static readonly int ChatWidth = 3236;
        public static readonly int Lineheight = 38;

        public static readonly int ChatLeftX = 4;

        public static int[] ExtractLineOffsets(ImageCache image)
        {
            var offsets = new List<int>();
            for (int y = 660; y < 2095; y++)
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
