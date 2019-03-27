using Application.ChatMessages.Model;

namespace Application.Interfaces
{
    public interface IRivenParser
    {
        /// <summary>
        /// Extracts the text of a riven window.
        /// </summary>
        /// <param name="imagePath">The path to the image of the riven window.</param>
        /// <returns>The text of the riven card.</returns>
        Riven ParseRivenImage(string imagePath);
    }
}