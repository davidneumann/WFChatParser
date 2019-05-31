using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.ChatLineExtractor
{
    public interface IChatLineExtractor
    {
        Bitmap[] ExtractChatLines(Bitmap screenshot);
    }
}
