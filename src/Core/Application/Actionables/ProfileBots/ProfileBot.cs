using Application.Actionables.ChatBots;
using Application.Actionables.States;
using Application.Interfaces;
using Application.Logger;
using ImageMagick;
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
        private ProfileBotState _currentState;

        public ProfileBot(
            CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataTxRx dataSender)
            : base(cancellationToken, warframeCredentials, mouse, keyboard, screenStateHandler, logger, gameCapture, dataSender)
        {
            _dataSender.ProfileParseRequest += _dataSender_ProfileParseRequest;
        }

        private void _dataSender_ProfileParseRequest(object sender, string e)
        {
            AddProfileRequest(e);
        }

        public void AddProfileRequest(string name)
        {
            _profileRequestQueue.Enqueue(name);
            _requestingControl = true;
        }

        public override Task TakeControl()
        {
            _logger.Log(_warframeCredentials.StartInfo.UserName + ":" + _warframeCredentials.Region + " taking control");
            _logger.Log($"Profile queue size: {_profileRequestQueue.Count}.");
            
            if (_warframeProcess == null || _warframeProcess.HasExited)
            {
                _currentState = ProfileBotState.WaitingForBaseBot;
                _baseState = BaseBotState.StartWarframe;
            }

            _requestingControl = false;

            if (_baseState != BaseBotState.Running)
                return BaseTakeControl();
            else if (_baseState == BaseBotState.Running && _currentState == ProfileBotState.WaitingForBaseBot && _profileRequestQueue.Count > 0)
                _currentState = ProfileBotState.OpenProfile;

            switch (_currentState)
            {
                case ProfileBotState.OpenProfile:
                    _logger.Log("Opening profile page");
                    return OpenProfile();
                case ProfileBotState.WaitingForProfile:
                    _logger.Log("Waiting for profile");
                    return VerifyProfileOpen();
                case ProfileBotState.ParsingProfile:
                    _logger.Log("Parsing profile");
                    return ParseProfile();
                default:
                    break;
            }

            return Task.CompletedTask;
        }

        private Task OpenProfile()
        {
            /*
             * Open the menu to safely expose the chat interface
             * Open the chat window and in a safe tab paste in /profile {name}
             * Needs to be able to close the chat on the profile page after verifying it opened (check header text to verify? Click minimize icon in chat window to close?)
             * Click the Equipment header
             * Click the sort by box and select Progress
             * Extract 2 rows of icons (6 icons per row)
             * Check if the last icon on the bottom row is mostly white or mostly grey. If grey abort.
             * If not grey then scroll down 2 times
             * Save all these images to a folder
             * Click exit
            */

            throw new NotImplementedException();

            OpenMenu();
            OpenChat();
            PasteProfile();
            CloseChat();

            _currentState = ProfileBotState.WaitingForProfile;
            return Task.CompletedTask;
        }

        private void OpenMenu()
        {
            // Verify that we are in warframe mode
            // Hit escape to open menu
            // Verify menu is open
            throw new NotImplementedException();
        }
        private void OpenChat()
        {
            // Verify chat is in expected place
            // Click clan icon to open chat
            // Verify SEND MESSAGE TO CLAN is showing
            throw new NotImplementedException();
        }
        private void PasteProfile()
        {
            // Take a name from the queue. If somehow it's empty abort, set status to WaitingForBaseBot, set requesting control to false

            // Paste /profile {name}
            // Hit enter
            throw new NotImplementedException();
        }

        private void CloseChat()
        {
            // Delay 2 frames
            // Hit escape
            throw new NotImplementedException();
        }

        private Task VerifyProfileOpen()
        {
            // Verify the pixels for Profile are in the right spots
            // Check LOTS of pixels as this stpuid thing animates in
            throw new NotImplementedException();
        }

        private Task ParseProfile()
        {
            throw new NotImplementedException();

            ParseProfileTab();
            ParseEquipmentTab();
            //Make sure to send the data _dataSender!

            // Stop trying to parse more names
            if (_profileRequestQueue.Count <= 0)
                _requestingControl = false;
        }

        private void ParseProfileTab()
        {
            // Set name
            // Verify accoldaes are there and parse them
            // Parse MR box. MR, total XP, XP to level
            // Parse clan name

            throw new NotImplementedException();
        }

        private void ParseEquipmentTab()
        {
            // Click sort by dropdown and choose Progress
            // Some sort of do while that parses all rows
            throw new NotImplementedException();
        }
    }
}
