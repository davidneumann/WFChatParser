using Application.ChatMessages.Model;
using Common;
using System;
using System.Collections.Generic;
using System.Text;
using Application.LineParseResult;

namespace Application.Interfaces
{
    public interface IChatParser
    {
        /// <summary>
        /// Extract the text of an image of the trade chat window and identifies x/y coordinate points to click for riven info.
        /// </summary>
        /// <param name="imagePath">The path to the chat window image.</param>
        /// <returns>The chat window text and click points for rivens.</returns>
        LineParseResult.BaseLineParseResult[] ParseChatImage(string imagePath, bool useCache, bool isScrolledUp);

        bool IsScrollbarPresent(string imagePath);
    }
}
