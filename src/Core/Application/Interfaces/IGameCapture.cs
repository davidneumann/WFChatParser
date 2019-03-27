using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IGameCapture
    {
        /// <summary>
        /// Capture the game screen and crop it to the chat window
        /// </summary>
        /// <returns>The file path to the image of the chat window</returns>
        string GetTradeChatImage(string outputPath);
        void GetRivenImage(string rivenImage);
    }
}
