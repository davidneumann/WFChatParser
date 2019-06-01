using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.ChatBoxParsing.ChatLineExtractor
{
    public interface IChatLineExtractor
    {
        Bitmap[] ExtractChatLines(Bitmap screenshot);
    }
}
