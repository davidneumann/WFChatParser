using Application.Actionables.ChatBots;
using Application.Actionables.States;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables.ChatWatcher
{
    internal class ChatWatcher : BaseWarframeBot
    {
        private IChatParser _chatParser;
        
        public ChatWatcher(CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataSender dataSender,
            IChatParser chatParser) : base(cancellationToken, warframeCredentials, mouse, keyboard, screenStateHandler, logger, gameCapture, dataSender)
        {
            _chatParser = chatParser;
        }

        public override Task TakeControl()
        {
            _logger.Log(this.GetType().Name + ": " + _warframeCredentials.StartInfo.UserName + ":" + _warframeCredentials.Region + " taking control");

            if (_warframeProcess == null || _warframeProcess.HasExited)
            {
                _baseState = BaseBotState.StartWarframe;
            }

            _requestingControl = false;

            if (_baseState != BaseBotState.Running)
                return BaseTakeControl();

            throw new NotImplementedException();

            ////SHOULD NOT BE HERE
            //CloseWarframe();
            //_requestingControl = false;
            //return;
        }
    }
}