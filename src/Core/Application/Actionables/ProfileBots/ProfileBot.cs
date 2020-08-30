using Application.Actionables.ChatBots;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables.ProfileBots
{
    public class ProfileBot : BaseWarframeBot, IActionable
    {
        private ConcurrentQueue<string> _profileRequestQueue = new ConcurrentQueue<string>();

        public ProfileBot(
            CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataSender dataSender)
            : base(cancellationToken, warframeCredentials, mouse, keyboard, screenStateHandler, logger, gameCapture, dataSender)
        {
        }

        public void AddProfileRequest(string name)
        {
            _profileRequestQueue.Enqueue(name);
            _requestingControl = true;
        }

        public override Task TakeControl()
        {
            throw new NotImplementedException();
        }
    }
}
