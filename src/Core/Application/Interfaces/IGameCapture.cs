using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Interfaces
{
    public interface IGameCapture
    {
        /// <summary>
        /// Capture the game screen and crop it to the chat window
        /// </summary>
        /// <returns>The file path to the image of the chat window</returns>
        Bitmap GetFullImage();
        Bitmap GetRivenImage();
        Bitmap GetChatIcon();
    }
}
