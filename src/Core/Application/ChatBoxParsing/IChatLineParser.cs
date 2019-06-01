using Application.LineParseResult;
using System.Drawing;

namespace Application.ChatBoxParsing
{
    public interface IChatLineParser
    {
        BaseLineParseResult ParseLine(Bitmap lineImage);
    }
}