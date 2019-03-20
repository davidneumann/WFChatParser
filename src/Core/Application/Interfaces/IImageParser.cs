using Application.ChatMessages.Model;
using Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IImageParser
    {
        /// <summary>
        /// Extract the text of an image of the trade chat window and identifies x/y coordinate points to click for riven info.
        /// </summary>
        /// <param name="imagePath">The path to the chat window image.</param>
        /// <returns>The chat window text and click points for rivens.</returns>
        ChatMessageModel[] ParseChatImage(string imagePath);

        /// <summary>
        /// Extracts the text of a riven window.
        /// </summary>
        /// <param name="imagePath">The path to the image of the riven window.</param>
        /// <returns>The text of the riven card.</returns>
        string[] ParseRivenImage(string imagePath);
    }
}
