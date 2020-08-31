using Application.Actionables.ChatBots;
using Application.Actionables.States;
using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using Application.Utils;
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
        private ILineParser _lineParser;

        public ProfileBot(
            CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataTxRx dataSender,
            ILineParser lineParser)
            : base(cancellationToken, warframeCredentials, mouse, keyboard, screenStateHandler, logger, gameCapture, dataSender)
        {
            _dataSender.ProfileParseRequest += _dataSender_ProfileParseRequest;
            _lineParser = lineParser;
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


            ExtractImages(null);

            throw new NotImplementedException();
            ParseProfileTab();
            ParseEquipmentTab();
            //Make sure to send the data _dataSender!

            // Stop trying to parse more names
            if (_profileRequestQueue.Count <= 0)
                _requestingControl = false;
        }

        private Bitmap ExtractBitmapFromRect(Rectangle rect, Bitmap source, int padding = 4)
        {
            Bitmap result;

            using (var bitmap = new Bitmap(rect.Width, rect.Height))
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    for (int y = rect.Top; y < rect.Bottom; y++)
                    {
                        var p = source.GetPixel(x, y);
                        var hsv = p.ToHsv();
                        var c = 255;
                        if (hsv.Value >= 0.95f)
                        {
                            var v = 1 - hsv.Value;
                            c = (int)(v * byte.MaxValue);
                        }
                        bitmap.SetPixel(x - rect.Left, y - rect.Top, Color.FromArgb(255, c, c, c));
                    }
                }

                var scale = 48f / bitmap.Height;
                //var width = (int)(bitmap.Width * scale);
                //var height = (int)(bitmap.Height * scale);
                //result = new Bitmap(width + padding * 2, height + padding * 2);
                //var g = Graphics.FromImage(result);
                //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                //g.FillRectangle(Brushes.White, 0, 0, result.Width, result.Height);
                //g.DrawImage(bitmap, padding, padding, width, height);

                using (var scaled = new Bitmap(bitmap, new Size((int)(bitmap.Width * scale), (int)(bitmap.Height * scale))))
                {
                    result = new Bitmap(scaled.Width + padding * 2, scaled.Height + padding * 2);
                    for (int x = 0; x < result.Width; x++)
                    {
                        for (int y = 0; y < result.Height; y++)
                        {
                            if (y < padding || x < padding || x >= scaled.Width + padding || y >= scaled.Height + padding)
                                result.SetPixel(x, y, Color.White);
                            else
                            {
                                var c = scaled.GetPixel(x - padding, y - padding);
                                result.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                            }
                        }
                    }
                }
            }

            return result;
        }

        private Rectangle TrimRect(Rectangle searchSpace, Bitmap bitmap)
        {
            var left = searchSpace.Right;
            var right = searchSpace.Left;
            var top = searchSpace.Bottom;
            var bottom = searchSpace.Top;

            for (int x = searchSpace.Left; x < searchSpace.Right; x++)
            {
                for (int y = searchSpace.Top; y < searchSpace.Bottom; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    var hsv = p.ToHsv();
                    if (hsv.Value >= 0.95f)
                    {
                        if (y < top)
                            top = y;
                        if (y > bottom)
                            bottom = y;
                        if (x < left)
                            left = x;
                        if (x > right)
                            right = x;
                    }
                }
            }

            return new Rectangle(left, top, right - left, bottom - top);
        }

        public void ExtractImages(Bitmap bitmap)
        {
            //using (var bitmap = _gameCapture.GetFullImage())
            {
                //MR
                var mr = ExtractBitmapFromRect(TrimRect(new Rectangle(3149, 479, 117, 92), bitmap), bitmap);
                mr.Save("profile_mr.png");
                Console.WriteLine($"Mr: {_lineParser.ParseLine(mr)}");

                //TotalXP
                var totalXp = ExtractBitmapFromRect(TrimRect(new Rectangle(3135, 641, 149, 34), bitmap), bitmap);
                totalXp.Save("profile_total.png");
                Console.WriteLine($"Total Xp: {_lineParser.ParseLine(totalXp)}");

                var remainingXp = ExtractBitmapFromRect(TrimRect(new Rectangle(3402, 710, 141, 42), bitmap), bitmap);
                remainingXp.Save("profile_remaining.png");
                Console.WriteLine($"Remaining Xp: {_lineParser.ParseLine(remainingXp)}");

                var clanName = ExtractBitmapFromRect(TrimRect(new Rectangle(3011, 1075, 385, 59), bitmap), bitmap);
                clanName.Save("profile_clan.png");
                Console.WriteLine($"Clan name: {_lineParser.ParseLine(clanName)}");

                using (var combined = new Bitmap((int)Math.Max(Math.Max(Math.Max(mr.Width, totalXp.Width), remainingXp.Width), clanName.Width),
                                                 mr.Height + totalXp.Height + remainingXp.Height + clanName.Height))
                {
                    var g = Graphics.FromImage(combined);
                    var top = 0;
                    var sources = new Bitmap[] { mr, totalXp, remainingXp, clanName };
                    for (int i = 0; i < sources.Length; i++)
                    {
                        g.DrawImage(sources[i], 0, top);
                        top += sources[i].Height;
                    }
                    //var source = sources[0];
                    //var sourceI = 0;
                    //var top = 0;
                    //for (int y = 0; y < combined.Height; y++)
                    //{
                    //    for (int x = 0; x < combined.Width; x++)
                    //    {
                    //        if (x >= source.Width)
                    //            break;
                    //        combined.SetPixel(x, y, source.GetPixel(x, y - top));
                    //    }
                    //    if(y - top + 1 >= source.Height && y < combined.Height - 1)
                    //    {
                    //        top += source.Height;
                    //        sourceI++;
                    //        source = sources[sourceI];
                    //    }
                    //}
                    combined.Save("profile_combined.png");
                }
            }

            throw new NotImplementedException();
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
