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
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables.ProfileBots
{
    public partial class ProfileBot : BaseWarframeBot, IActionable
    {
        private const float minV = 0.43f;
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

        private Bitmap ExtractBitmapFromRect(Rectangle rect, Bitmap source, int padding = 4, bool trimRect = true, bool strictWhites = false)
        {
            Bitmap result;
            if (trimRect)
                rect = TrimRect(rect, source);

            using (var bitmap = new Bitmap(rect.Width, rect.Height))
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    for (int y = rect.Top; y < rect.Bottom; y++)
                    {
                        var p = source.GetPixel(x, y);
                        var hsv = p.ToHsv();
                        var c = 255;
                        if ((!strictWhites && IsWhite(hsv)) || (strictWhites && hsv.Value >= 0.99f && hsv.Saturation <= 0.01f && hsv.Hue <= 0.01f))
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
                    if (IsWhite(hsv))
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

        private static bool IsWhite(Hsv hsv)
        {
            return hsv.Value >= minV && hsv.Saturation <= 0.1f;
        }

        private int[] LocateWhiteLineTops(Bitmap bitmap)
        {
            var result = new List<int>();

            var x = 2675; // Far left of each box + a little bit of safety
            var last = bitmap.GetPixel(x, 289).ToHsv();
            for (int y = 290; y < 1977; y++) // 1977 is top of the buttons at the bottom
            {
                var p = bitmap.GetPixel(x, y).ToHsv();

                if (p.Value >= 0.98f && p.Hue <= 0.01f && p.Saturation <= 0.01f && last.Value < 0.98f)
                {
                    var nextP = bitmap.GetPixel(x, y + 1).ToHsv();
                    if (nextP.Value >= 0.98f && p.Hue <= 0.01f && p.Saturation <= 0.01f)
                    {
                        result.Add(y);
                        y += 5;
                    }
                }

                last = p;
            }

            return result.ToArray();
        }

        private (int, int) LocateHeaderSides(Bitmap bitmap, int y)
        {
            var left = 3213;
            var right = 3213;
            for (int x = 2670; x < 3213; x++)
            {
                var hsv = bitmap.GetPixel(x, y).ToHsv();
                if (!IsWhite(hsv))
                {
                    left = x + 2;
                    break;
                }
            }
            for (int x = 3748; x > 3213; x--)
            {
                var hsv = bitmap.GetPixel(x, y).ToHsv();
                if (!IsWhite(hsv))
                {
                    right = x - 2;
                    break;
                }
            }

            return (left, right);
        }

        private void DebugSaveImages(IEnumerable<Bitmap> images, string filename)
        {
            var top = 0;
            var arr = images.ToArray();
            using (var output = new Bitmap(arr.Max(i => i.Width), arr.Sum(i => i.Height)))
            {
                using (var g = Graphics.FromImage(output))
                {
                    foreach (var image in images)
                    {
                        g.DrawImage(image, 0, top);
                        top += image.Height;
                    }
                }
                output.Save(filename);
            }
        }

        public void ExtractImages(Bitmap bitmap)
        {
            //using (var bitmap = _gameCapture.GetFullImage())
            {
                //Header extraction POC
                List<Header> headers = ExtractHeaders(bitmap);


                //Rect from header POC
                var debugImages = new List<Bitmap>();
                foreach (var header in headers)
                {
                    Console.WriteLine($"={header.Text}=");
                    switch (header.Value)
                    {
                        case HeaderOption.Accolades:
                            var names = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 230, 1065, 38), bitmap);
                            var descriptions = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 267, 1065, 45), bitmap);
                            Console.WriteLine($"Names: {_lineParser.ParseLine(names)}\nDescriptions: {_lineParser.ParseLine(descriptions)}");
                            debugImages.Add(names);
                            debugImages.Add(descriptions);
                            break;
                        case HeaderOption.MasteryRank:
                            var eMr = ExtractBitmapFromRect(new Rectangle(3155, header.Anchor + 162, 100, 65), bitmap, strictWhites: true);
                            var title = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 232, 1065, 40), bitmap);
                            var totalXp = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 302, 1065, 38), bitmap);
                            var remaingXp = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 374, 1065, 47), bitmap);
                            Console.WriteLine($"Mr: {_lineParser.ParseLine(eMr)}\nMr Title: {_lineParser.ParseLine(title)}\nTotal xp: {_lineParser.ParseLine(totalXp)}\nRemaining Xp: {_lineParser.ParseLine(remaingXp).Split(' ').Last()}");
                            debugImages.Add(eMr);
                            debugImages.Add(title);
                            debugImages.Add(totalXp);
                            debugImages.Add(remaingXp);
                            break;
                        case HeaderOption.Clan:
                            var eClan = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 228, 1065, 54), bitmap);
                            Console.WriteLine($"Name: {_lineParser.ParseLine(eClan)}");
                            debugImages.Add(eClan);
                            break;
                        case HeaderOption.MarkedForDeathBy:
                            var markedBy = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 185, 1065, 35), bitmap);
                            Console.WriteLine($"Marked by: {_lineParser.ParseLine(markedBy)}");
                            debugImages.Add(markedBy);
                            break;
                        case HeaderOption.Unknown:
                        default:
                            _logger.Log("Unknown header detected!");
                            try
                            {
                                string filename = Path.Combine("debug", DateTime.Now.Ticks + ".png");
                                bitmap.Save(filename);
                                _dataSender.AsyncSendDebugMessage($"Unkown profile header detected. See {filename}");
                            }
                            catch { }
                            break;
                    }
                }
                DebugSaveImages(debugImages, "profile_combined.png");




                ////Orig Tess POC with Ayeigui's profile
                ////MR
                //var mr = ExtractBitmapFromRect(TrimRect(new Rectangle(3149, 479, 117, 92), bitmap), bitmap);
                //mr.Save("profile_mr.png");
                //Console.WriteLine($"Mr: {_lineParser.ParseLine(mr)}");

                ////TotalXP
                //var totalXp = ExtractBitmapFromRect(TrimRect(new Rectangle(3135, 641, 149, 34), bitmap), bitmap);
                //totalXp.Save("profile_total.png");
                //Console.WriteLine($"Total Xp: {_lineParser.ParseLine(totalXp)}");

                //var remainingXp = ExtractBitmapFromRect(TrimRect(new Rectangle(3402, 710, 141, 42), bitmap), bitmap);
                //remainingXp.Save("profile_remaining.png");
                //Console.WriteLine($"Remaining Xp: {_lineParser.ParseLine(remainingXp)}");

                //var clanName = ExtractBitmapFromRect(TrimRect(new Rectangle(3011, 1075, 385, 59), bitmap), bitmap);
                //clanName.Save("profile_clan.png");
                //Console.WriteLine($"Clan name: {_lineParser.ParseLine(clanName)}");

                //DebugSaveImages(new Bitmap[] { mr, totalXp, remainingXp, clanName }, "profile_combined.png");
            }

            throw new NotImplementedException();
        }

        private List<Header> ExtractHeaders(Bitmap bitmap)
        {
            var headers = new List<Header>();
            var ys = LocateWhiteLineTops(bitmap);
            using (var debug = new Bitmap(bitmap))
            {
                var g = Graphics.FromImage(debug);
                for (int i = 0; i < ys.Length; i++)
                {
                    g.FillRectangle(Brushes.Red, new Rectangle(2670, ys[i] - 13, 300, 26));
                    if (i % 2 == 0)
                    {
                        var (left, right) = LocateHeaderSides(bitmap, ys[i]);
                        var rect = new Rectangle(left, ys[i] - 28, right - left, 38);
                        var trimmed = TrimRect(rect, bitmap);
                        Bitmap image = ExtractBitmapFromRect(trimmed, bitmap);
                        headers.Add(new Header(image, ys[i], _lineParser.ParseLine(image)));
                    }
                }
                //debug.Save("profile_debug.png");
            }
            //for (int i = 0; i < headers.Count; i++)
            //{
            //    headers[i].Bitmap.Save($"header_{i}.png");
            //    Console.WriteLine($"Header {i}: {headers[i].Text}");
            //}
            DebugSaveImages(headers.Select(h => h.Bitmap), "profile_headers.png");
            return headers;
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
