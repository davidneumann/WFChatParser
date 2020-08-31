using Application.Actionables.ChatBots;
using Application.Actionables.States;
using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables.ProfileBots
{
    public class ProfileBot : BaseWarframeBot, IActionable
    {
        private ConcurrentQueue<string> _profileRequestQueue = new ConcurrentQueue<string>();
        private ProfileBotState _currentState;
        private string _currentProfileName;

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

        private async Task OpenProfile()
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

            if (_warframeProcess == null || _warframeProcess.HasExited)
            {
                _baseState = BaseBotState.StartWarframe;
                _currentState = ProfileBotState.WaitingForBaseBot;
                _requestingControl = true;
                return;
            }

            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
            }

            OpenMenu();
            OpenChat();
            PasteProfile();
            CloseChat();

            _currentState = ProfileBotState.WaitingForProfile;
            _requestingControl = true;
            return;
        }

        private void OpenMenu()
        {
            //Ensure we are controlling a warframe
            var tries = 0;
            while (true)
            {
                if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
                {
                    Thread.Sleep(17);
                    _mouse.Click(0, 0);
                }
                using (var screen = _gameCapture.GetFullImage())
                {
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state != Enums.ScreenState.ControllingWarframe)
                    {
                        _keyboard.SendEscape();
                        Thread.Sleep(600);
                    }
                    else
                        break;
                }
                tries++;
                if (tries > 25)
                {
                    _logger.Log("Failed to navigate to glyph screen");
                    throw new NavigationException(ScreenState.ControllingWarframe);
                }
            }
            //Send escape to open main menu
            _keyboard.SendEscape();
            Thread.Sleep(1000); //Give menu time to animate

            //Check if on Main Menu
            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            using (var screen = _gameCapture.GetFullImage())
            {
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.MainMenu)
                {
                    return;
                }
                else
                {
                    var path = SaveScreenToDebug(screen);
                    _dataSender.AsyncSendDebugMessage("Failed to navigate to main menu. See: " + path).Wait();
                    throw new NavigationException(ScreenState.MainMenu);
                }
            }
        }

        private void OpenChat()
        {
            using (var screen = _gameCapture.GetFullImage())
            {
                if (_screenStateHandler.IsChatCollapsed(screen))
                {
                    _logger.Log("Expanding chat");
                    //Click and drag to move chat into place
                    _mouse.ClickAndDrag(new Point(91, 2122), new Point(0, 2160), 1000);
                    Thread.Sleep(100);
                }
                else if (!_screenStateHandler.IsChatOpen(screen))
                    throw new ChatMissingException();
            }
        }
        private void PasteProfile()
        {
            _logger.Log("Pasting profile command.");
            // Take a name from the queue. If somehow it's empty abort, set status to WaitingForBaseBot, set requesting control to false
            if (!_profileRequestQueue.TryDequeue(out _currentProfileName))
            {
                return;
            }

            // Paste /profile {name}
            _mouse.Click(80, 2120);
            Thread.Sleep(250);
            _keyboard.SendPaste($"/profile {_currentProfileName}");

            // Hit enter
            Thread.Sleep(33);
            _keyboard.SendEnter();
            Thread.Sleep(33);
        }

        private void CloseChat()
        {
            _logger.Log("Closing chat.");
            // Delay 2 frames
            Thread.Sleep(66);

            // Hit escape
            _keyboard.SendEscape();

            _mouse.MoveTo(0, 0);
        }

        private Task VerifyProfileOpen()
        {
            _logger.Log("Verifying that profile is fully visible.");
            // Verify the pixels for Profile are in the right spots
            // Check LOTS of pixels as this stpuid thing animates in
            for (int tries = 0; tries < 15; tries++)
            {
                using (var screen = _gameCapture.GetFullImage())
                {
                    if (_screenStateHandler.GetScreenState(screen) == ScreenState.ProfileScreen)
                    {
                        _currentState = ProfileBotState.ParsingProfile;
                        _requestingControl = true;

                        //Save warframe picture
                        _logger.Log("Saving warframe screenshot");
                        _keyboard.SendF6();
                        using (var crop = new Bitmap(2647, 1819))
                        {
                            for (int x = 0; x < crop.Width; x++)
                            {
                                for (int y = 0; y < crop.Height; y++)
                                {
                                    crop.SetPixel(x, y, screen.GetPixel(x, y + 280));
                                }
                            }
                            crop.Save("extracted_warframe.png");
                        }

                        return Task.CompletedTask;
                    }
                    else
                    {
                        _logger.Log("Still waiting for profile screen to load");
                        Thread.Sleep(250);
                    }
                }
            }

            _profileRequestQueue.Enqueue(_currentProfileName);
            _currentState = ProfileBotState.WaitingForBaseBot;
            _requestingControl = true;
            throw new Exception("Failed to load profile screen");
        }

        private Task ParseProfile()
        {
            _logger.Log($"Starting to parse profile {_currentProfileName}.");
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
