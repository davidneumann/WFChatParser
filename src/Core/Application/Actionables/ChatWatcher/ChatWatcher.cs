using Application.Actionables.ChatBots;
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
        private DateTime _lastMessage;
        private BotStates _currentState = BotStates.StartWarframe;
        private bool _requestingControl;

        public override bool IsRequestingControl => _requestingControl;

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
                _currentState = BotStates.StartWarframe;

            switch (_currentState)
            {
                case BotStates.StartWarframe:
                    return StartWarframe();
                case BotStates.WaitForLoadScreen:
                    return WaitForLoadScreen();
                case BotStates.LogIn:
                case BotStates.ClaimReward:
                case BotStates.CloseWarframe:
                case BotStates.NavigateToChat:
                case BotStates.ParseChat:
                default:
                    throw new NotImplementedException();
            }

            ////SHOULD NOT BE HERE
            //CloseWarframe();
            //_requestingControl = false;
            //return;
        }



        private async Task StartWarframe()
        {
            _requestingControl = false;

            var result = await BaseStartWarframe();

            switch (result.State)
            {
                case StartWarframeResult.StartState.Unknown:
                case StartWarframeResult.StartState.LauncherAlreadyRunning:
                case StartWarframeResult.StartState.TimedOutWaitingForLauncher:
                default:
                    _currentState = BotStates.StartWarframe;
                    _requestingControl = true;
                    return;
                case StartWarframeResult.StartState.LaunchedSuccessfully:
                    _warframeProcess = result.WarframeProcess;
                    //Give 15 minutes on a fresh login to allow slow chats to fill up before killing them.
                    _lastMessage = DateTime.Now.AddMinutes(15);
                    _currentState = BotStates.WaitForLoadScreen;
                    _requestingControl = true;
                    break;
            }
        }

        private async Task WaitForLoadScreen()
        {
            try
            {
                await BaseWaitForLoadingScreen();
                _currentState = BotStates.LogIn;
            }
            catch (LoginScreenTimeoutException)
            {
                _currentState = BotStates.CloseWarframe;
            }
            _requestingControl = true;
        }
    }
}