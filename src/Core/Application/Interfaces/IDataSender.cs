using Application.ChatMessages.Model;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IDataSender
    {
        Task AsyncSendChatMessage(ChatMessageModel message);
        Task AsyncSendDebugMessage(string message);
        Task AsyncSendRedtext(string rawMessage);
        Task AsyncSendRivenImage(Guid imageID, Bitmap image);
        Task AsyncSendRivenImage(Guid imageID, string rivenBase64);
    }
}