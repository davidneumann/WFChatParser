using Application.Actionables.ChatBots;
using Application.Actionables.ProfileBots.Models;
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
using System.Drawing.Imaging;
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

        private string _debugFolder = Path.Combine("debug", "profiles");
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
            if (!Directory.Exists(_debugFolder))
                Directory.CreateDirectory(_debugFolder);
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
                        Thread.Sleep(750);
                        _keyboard.SendF6();

                        var _ = Task.Run(() =>
                        {
                            Thread.Sleep(2500);
                            if (!Directory.Exists(Path.Combine(_debugFolder, _currentProfileName)))
                                Directory.CreateDirectory(Path.Combine(_debugFolder, _currentProfileName));
                            var newestImage = (new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + @"\Warframe"))
                                            .GetFiles().OrderByDescending(f => f.LastWriteTime).First();

                            string destFilename = Path.Combine(_debugFolder, _currentProfileName, "screenshot" + newestImage.Extension);
                            if (File.Exists(destFilename))
                                File.Delete(destFilename);
                            File.Move(newestImage.FullName, destFilename);

                            Thread.Sleep(250);

                            using (var source = new Bitmap(destFilename))
                            {
                                ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageDecoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);

                                // Create an Encoder object based on the GUID  
                                // for the Quality parameter category.  
                                System.Drawing.Imaging.Encoder myEncoder =
                                    System.Drawing.Imaging.Encoder.Quality;

                                // Create an EncoderParameters object.  
                                // An EncoderParameters object has an array of EncoderParameter  
                                // objects. In this case, there is only one  
                                // EncoderParameter object in the array.  
                                EncoderParameters myEncoderParameters = new EncoderParameters(1);

                                var myEncoderParameter = new EncoderParameter(myEncoder, 65L);
                                myEncoderParameters.Param[0] = myEncoderParameter;
                                source.Save($"{destFilename}_small.jpg", jpgEncoder, myEncoderParameters);
                            }
                        });

                        //using (var crop = new Bitmap(2647, 1819))
                        //{
                        //    for (int x = 0; x < crop.Width; x++)
                        //    {
                        //        for (int y = 0; y < crop.Height; y++)
                        //        {
                        //            crop.SetPixel(x, y, screen.GetPixel(x, y + 280));
                        //        }
                        //    }
                        //    crop.Save("extracted_warframe.png");
                        //}

                        Thread.Sleep(500);

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

        private async Task ParseProfile()
        {
            _logger.Log($"Starting to parse profile {_currentProfileName}.");

            //var profile = ParseProfileTab();
            ParseEquipmentTab();
            //throw new NotImplementedException();

            //await _dataSender.AsyncSendProfileData(profile);

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
            var fi = new FileInfo(filename);
            if (!fi.Directory.Exists)
                fi.Directory.Create();

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

        private Profile ParseProfileTab()
        {
            var result = new Profile();
            result.Name = _currentProfileName;
            using (var bitmap = _gameCapture.GetFullImage())
            {
                //Header extraction POC
                List<Header> headers = ExtractHeaders(bitmap);

                //Rect from header POC
                var debugImages = new List<Bitmap>();
                var sb = new StringBuilder();
                sb.AppendLine(_currentProfileName);
                foreach (var header in headers)
                {
                    switch (header.Value)
                    {
                        case HeaderOption.Accolades:
                            var names = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 230, 1065, 38), bitmap);
                            var descriptions = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 267, 1065, 45), bitmap);
                            string namesText = _lineParser.ParseLine(names);
                            string descriptionsText = _lineParser.ParseLine(descriptions);
                            sb.AppendLine($"Names: {namesText}\nDescriptions: {descriptionsText}");
                            debugImages.Add(names);
                            debugImages.Add(descriptions);
                            result.Accolades = new List<Accolades>();
                            result.Accolades.Add(new Accolades() { Name = namesText, Description = descriptionsText });
                            break;
                        case HeaderOption.MasteryRank:
                            var mr = ExtractBitmapFromRect(new Rectangle(3155, header.Anchor + 162, 100, 65), bitmap, strictWhites: true);
                            var title = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 232, 1065, 40), bitmap);
                            var totalXp = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 302, 1065, 38), bitmap);
                            var remaingXp = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 374, 1065, 47), bitmap);
                            string mrText = _lineParser.ParseLine(mr);
                            string mrTitleText = _lineParser.ParseLine(title);
                            string totalXpText = _lineParser.ParseLine(totalXp);
                            string remainingXpText = _lineParser.ParseLine(remaingXp);
                            sb.AppendLine($"Mr: {mrText}\nMr Title: {mrTitleText}\nTotal xp: {totalXpText}\nRemaining Xp: {remainingXpText.Split(' ').Last()}");
                            debugImages.Add(mr);
                            debugImages.Add(title);
                            debugImages.Add(totalXp);
                            debugImages.Add(remaingXp);
                            result.MasteryRank = mrText;
                            result.MasteryRankTitle = mrTitleText;
                            result.TotalXp = totalXpText;
                            result.XpToLevel = remainingXpText;
                            break;
                        case HeaderOption.Clan:
                            var clan = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 228, 1065, 54), bitmap);
                            string clanText = _lineParser.ParseLine(clan);
                            sb.AppendLine($"Name: {clanText}");
                            debugImages.Add(clan);
                            result.ClanName = clanText;
                            break;
                        case HeaderOption.MarkedForDeathBy:
                            var markedBy = ExtractBitmapFromRect(new Rectangle(2675, header.Anchor + 185, 1065, 35), bitmap);
                            string markedByText = _lineParser.ParseLine(markedBy);
                            sb.AppendLine($"Marked by: {markedByText}");
                            debugImages.Add(markedBy);
                            result.MarkedBy = markedByText;
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
                DebugSaveImages(debugImages, Path.Combine(_debugFolder, _currentProfileName, "profile_combined.png"));
                File.WriteAllText(Path.Combine(_debugFolder, _currentProfileName, "profile_combined.txt"), sb.ToString());
                bitmap.Save(Path.Combine(_debugFolder, _currentProfileName, "profile_screen.png"));
            }

            return result;
        }

        private void ParseEquipmentTab()
        {
            Point clickPoint = GetEquipmentTabLocation();
            _logger.Log($"Attempting to opening equipment tab. Clicking at {clickPoint.X},{clickPoint.Y}.");

            //Click Equipment tab
            _mouse.MoveTo(clickPoint.X, clickPoint.Y);
            Thread.Sleep(66);
            _mouse.Click(clickPoint.X, clickPoint.Y);
            Thread.Sleep(33);
            Thread.Sleep(1500); // Wait for animation

            //Click sort dropdown
            _mouse.Click(3050, 835);
            Thread.Sleep(66);

            //Click sort by progress
            _mouse.Click(3081, 990);
            Thread.Sleep(16);
            //Move mouse out of the way
            _mouse.MoveTo(3723, 961);
            //Wait for animation
            Thread.Sleep(1500);



            var topLeftRect = new Rectangle(323, 915, 530, 365);
            var tiles = new List<Bitmap>();
            var debugC = 0;
            while (true)
            {
                var badDetected = false;

                using (var bitmap = _gameCapture.GetFullImage())
                {
                    //Read two rows
                    var curRect = topLeftRect;

                    for (int y = 0; y < 2; y++)
                    {
                        if (badDetected)
                            break;
                        for (int x = 0; x < 6; x++)
                        {
                            //For white check point: Right - 8, Bottom - 20
                            //White is v >= 0.961
                            if (bitmap.GetPixel(curRect.Right - 8, curRect.Bottom - 20).ToHsv().Value < 0.80f)
                            {
                                badDetected = true;
                                break;
                            }

                            var tileB = new Bitmap(curRect.Width, curRect.Height);
                            using (var g = Graphics.FromImage(tileB))
                            {
                                g.DrawImage(bitmap, new Rectangle(0, 0, tileB.Width, tileB.Height), curRect, GraphicsUnit.Pixel);
                            }
                            tileB.Save("equipment_" + debugC++ + ".png");
                            tiles.Add(tileB);

                            // Gap of 40 pixels between items
                            curRect = new Rectangle(curRect.Right + 40, curRect.Top, curRect.Width, curRect.Height);
                        }

                        // Gap of 39 pixels between items
                        curRect = new Rectangle(topLeftRect.Left, topLeftRect.Bottom + 39, topLeftRect.Width, topLeftRect.Height);
                    }

                    if (badDetected)
                        break;

                    //Scroll twice
                    _mouse.Click(3148, 1029);
                    Thread.Sleep(66);
                    _mouse.ScrollDown();
                    Thread.Sleep(66);
                    _mouse.ScrollDown();
                    Thread.Sleep(66);
                    _mouse.MoveTo(0, 0);
                    Thread.Sleep(150);

                }
            }


            if (tiles.Count > 0)
            {
                _logger.Log($"Attempting to save {tiles.Count} tiles.");
                int rows = (int)Math.Ceiling(tiles.Count / 6f);
                _logger.Log($"Image will have {rows} rows.");
                using (var debug = new Bitmap(topLeftRect.Width * 6 + 12, rows * topLeftRect.Height + rows * 2))
                {
                    var g = Graphics.FromImage(debug);
                    g.FillRectangle(Brushes.Red, 0, 0, debug.Width, debug.Height);
                    var top = 1;
                    var left = 1;
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        var tile = tiles[i];

                        g.DrawImage(tile, left, top);

                        if (i % 6 == 0 && i > 0)
                        {
                            left = 1;
                            top += topLeftRect.Height + 2;
                        }
                        else
                        {
                            left += topLeftRect.Width + 2;
                        }
                    }

                    debug.Save("profile_equipment.png");
                }
            }

            throw new NotImplementedException();
        }

        private Point GetEquipmentTabLocation()
        {
            var clickPoint = Point.Empty;
            //We need to find the equipment tab. It's 162 pixels to the right and 44 pixels up from the bar under Profile
            var hsvs = new float[30];
            using (var bitmap = _gameCapture.GetFullImage())
            {
                for (int x = 716; x < 1982; x++)
                {
                    for (int i = 0; i < hsvs.Length; i++)
                    {
                        hsvs[i] = bitmap.GetPixel(x, 276).ToHsv().Value;
                    }

                    if (hsvs.Average(v => v) >= 0.93f)
                    {
                        //Bar is 217 pixels wide and we just detected the leftmost edge
                        clickPoint = new Point(x + 217 + 162, 276 - 44);
                        break;
                    }

                    for (int i = 0; i < hsvs.Length; i++)
                    {
                        hsvs[i] = 0f;
                    }
                }
            }

            return clickPoint;
        }
    }
}
