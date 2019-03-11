using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IChatImageProcessor
    {
        /// <summary>
        /// Converts the full color game window into a image of the chat window in grayscale.
        /// </summary>
        /// <param name="imagePath">The path to the game screenshot</param>
        /// <param name="outputDirectory">The directory to save the processed image</param>
        /// <returns>The full path to the processed image</returns>
        string ProcessChatImage(string imagePath, string outputDirectory);
    }
}
