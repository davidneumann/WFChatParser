using Application.Actionables.ChatBots;
using Application.Actionables.ProfileBots.Models;
using Application.Actionables.States;
using Application.Enums;
using Application.Extensions;
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

        private string _debugFolder = Path.Combine("debug", "profiles");
        private ILineParserFactory _lineParserFactory;
        private ILineParser _profileTabParser;

        public ProfileBot(
            CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataTxRx dataSender,
            ILineParserFactory lineParserFactory)
            : base(cancellationToken, warframeCredentials, mouse, keyboard, screenStateHandler, logger, gameCapture, dataSender)
        {
            _dataSender.ProfileParseRequest += _dataSender_ProfileParseRequest;
            _lineParserFactory = lineParserFactory;
            _profileTabParser = _lineParserFactory.CreateParser(ClientLanguage.English);
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

            await GiveWarframeFocus();

            OpenMenu();
            OpenChat();
            PasteProfile();
            CloseChat();

            _currentState = ProfileBotState.WaitingForProfile;
            _requestingControl = true;
            return;
        }

        private async Task GiveWarframeFocus()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(17);
                _mouse.Click(0, 0);
                await Task.Delay(17);
            }
        }

        private void OpenMenu()
        {
            //Ensure we are controlling a warframe
            var tries = 0;
            var loopSafeguard = 0;
            while (loopSafeguard++ < 100)
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
            //Give it a few attempts incase the loading is almost done
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
                        Thread.Sleep(750); //Let things truly finish animating

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

            string filename = Path.Combine("debug", DateTime.Now.Ticks + ".png");
            using (var debug = _gameCapture.GetFullImage())
            {
                debug.Save(filename);
            }
            _profileRequestQueue.Enqueue(_currentProfileName);
            _currentState = ProfileBotState.WaitingForBaseBot;
            _requestingControl = true;
            _dataSender.AsyncSendDebugMessage($"Failed to load profile screen. See {filename}.");
            return Task.CompletedTask;
        }

        private async Task ParseProfile()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            _logger.Log($"Starting to parse profile {_currentProfileName}.");

            var profile = ParseProfileTab();
            _logger.Log("Getting equipment tiles");
            var tiles = GetEquipmentTileBitmaps();

            //Close profile
            _logger.Log("Closing profile");
            _keyboard.SendEscape();

            var _ = Task.Run(() =>
            {
                _logger.Log("Parsing equipment tiles");
                var equipment = new EquipmentItem[tiles.Length];
                using (var lineParser = _lineParserFactory.CreateParser(ClientLanguage.English))
                {
                    for (int i = 0; i < tiles.Length; i++)
                    {
                        equipment[i] = ParseEquipmentTile(tiles[i], lineParser);
                        tiles[i].Dispose();
                    }
                }
                profile.Equipment.AddRange(equipment);
                sw.Stop();
                _logger.Log($"{profile.Name}'s profile fully parsed in {sw.Elapsed.TotalSeconds}s.");

                _dataSender.AsyncSendProfileData(profile).Wait();
            });

            // Go back to idling if no more names ot parse otherwise let the next loop take over
            if (_profileRequestQueue.Count <= 0)
                _requestingControl = false;
            else
                _requestingControl = true;

            _currentState = ProfileBotState.WaitingForBaseBot;
        }

        private Bitmap ExtractWhiteBitmapFromRect(Rectangle rect, Bitmap source, int padding = 4, bool trimRect = true, bool strictWhites = false)
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

                result = PrepareBitmapForTess(bitmap, padding);
            }

            return result;
        }

        private static Bitmap PrepareBitmapForTess(Bitmap bitmap, int padding = 4)
        {
            Bitmap result;
            var scale = 48f / bitmap.Height;

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

            return result;
        }

        private Rectangle TrimRect(Rectangle searchSpace, Bitmap bitmap, bool trimDark = true)
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
                    if (trimDark && IsWhite(hsv)
                        || !trimDark && hsv.Value <= 0.5f)
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

            return new Rectangle(left, top, right - left, bottom - top + 1);
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
                        var image = ExtractWhiteBitmapFromRect(trimmed, bitmap);
                        headers.Add(new Header(image, ys[i], _profileTabParser.ParseLine(image)));
                    }
                }
            }
#if DEBUG
            DebugSaveImages(headers.Select(h => h.Bitmap), "profile_headers.png");
#endif
            headers.ForEach(h => h.Bitmap.Dispose());

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
#if DEBUG
                var debugImages = new List<Bitmap>();
#endif
                var sb = new StringBuilder();
                sb.AppendLine(_currentProfileName);
                foreach (var header in headers)
                {
                    switch (header.Value)
                    {
                        case HeaderOption.Accolades:
                            var names = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 230, 1065, 38), bitmap);
                            var descriptions = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 267, 1065, 45), bitmap);
                            string namesText = _profileTabParser.ParseLine(names);
                            string descriptionsText = _profileTabParser.ParseLine(descriptions);
                            sb.AppendLine($"Names: {namesText}\nDescriptions: {descriptionsText}");
#if DEBUG
                            debugImages.Add(names.Clone(new Rectangle(0, 0, names.Width, names.Height), names.PixelFormat));
                            debugImages.Add(descriptions.Clone(new Rectangle(0, 0, descriptions.Width, descriptions.Height), names.PixelFormat));
#endif
                            result.Accolades = new List<Accolades>();
                            result.Accolades.Add(new Accolades() { Name = namesText, Description = descriptionsText });
                            names.Dispose();
                            descriptions.Dispose();
                            break;
                        case HeaderOption.MasteryRank:
                            var mr = ExtractWhiteBitmapFromRect(new Rectangle(3155, header.Anchor + 162, 100, 65), bitmap, strictWhites: true);
                            var title = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 232, 1065, 40), bitmap);
                            var totalXp = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 302, 1065, 38), bitmap);
                            var remaingXp = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 374, 1065, 47), bitmap);
                            string mrText = _profileTabParser.ParseLine(mr);
                            string mrTitleText = _profileTabParser.ParseLine(title);
                            string totalXpText = _profileTabParser.ParseLine(totalXp);
                            string remainingXpText = _profileTabParser.ParseLine(remaingXp);
                            sb.AppendLine($"Mr: {mrText}\nMr Title: {mrTitleText}\nTotal xp: {totalXpText}\nRemaining Xp: {remainingXpText.Split(' ').Last()}");
#if DEBUG
                            debugImages.Add(mr.Clone(new Rectangle(0, 0, mr.Width, mr.Height), mr.PixelFormat));
                            debugImages.Add(title.Clone(new Rectangle(0, 0, title.Width, title.Height), title.PixelFormat));
                            debugImages.Add(totalXp.Clone(new Rectangle(0, 0, totalXp.Width, totalXp.Height), totalXp.PixelFormat));
                            debugImages.Add(remaingXp.Clone(new Rectangle(0, 0, remaingXp.Width, remaingXp.Height), remaingXp.PixelFormat));
#endif
                            result.MasteryRank = mrText;
                            result.MasteryRankTitle = mrTitleText;
                            result.TotalXp = totalXpText;
                            result.XpToLevel = remainingXpText;
                            mr.Dispose();
                            title.Dispose();
                            totalXp.Dispose();
                            remaingXp.Dispose();
                            break;
                        case HeaderOption.Clan:
                            var clan = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 228, 1065, 54), bitmap);
                            string clanText = _profileTabParser.ParseLine(clan);
                            sb.AppendLine($"Name: {clanText}");
#if DEBUG
                            debugImages.Add(clan.Clone(new Rectangle(0, 0, clan.Width, clan.Height), clan.PixelFormat));
#endif
                            result.ClanName = clanText;
                            clan.Dispose();
                            break;
                        case HeaderOption.MarkedForDeathBy:
                            var markedBy = ExtractWhiteBitmapFromRect(new Rectangle(2675, header.Anchor + 185, 1065, 35), bitmap);
                            string markedByText = _profileTabParser.ParseLine(markedBy);
                            sb.AppendLine($"Marked by: {markedByText}");
#if DEBUG
                            debugImages.Add(markedBy.Clone(new Rectangle(0, 0, markedBy.Width, markedBy.Height), markedBy.PixelFormat));
#endif
                            result.MarkedBy = markedByText;
                            markedBy.Dispose();
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
#if DEBUG
                DebugSaveImages(debugImages, Path.Combine(_debugFolder, _currentProfileName, "profile_combined.png"));
#endif
                File.WriteAllText(Path.Combine(_debugFolder, _currentProfileName, "profile_combined.txt"), sb.ToString());
#if DEBUG
                bitmap.Save(Path.Combine(_debugFolder, _currentProfileName, "profile_screen.png"));
#endif
            }

            return result;
        }

        private static Hsv AverageHsv(IEnumerable<Hsv> hsvs)
        {
            var hue = 0f;
            var saturation = 0f;
            var value = 0f;
            var count = 0;
            foreach (var hsv in hsvs)
            {
                hue += hsv.Hue;
                saturation += hsv.Saturation;
                value += hsv.Value;
                count++;
            }

            return new Hsv() { Hue = hue / count, Saturation = saturation / count, Value = value / count };
        }
        private static int[] LocateEquipmentRows(Bitmap bitmap)
        {
            var results = new int[2];
            var resultI = 0;
            bool localIsWhite(Hsv p) => p.Value >= 0.88f && p.Saturation <= 0.1f;
            bool isGray(Hsv p) => p.Saturation <= 0.3f && p.Value <= 0.4f && p.Hue >= 195 && p.Hue <= 210;

            var width = 503;
            var xStart = 337;
            var currentRows = new Hsv[width];
            var up4s = new Hsv[width];
            var up2s = new Hsv[width];
            for (int y = 1155; y < 1858; y++)
            {
                if (resultI >= 2)
                    break;

                for (int x = xStart; x < xStart + width; x++)
                {

                    currentRows[x - xStart] = bitmap.GetPixel(x, y).ToHsv();
                    up4s[x - xStart] = bitmap.GetPixel(x, y - 4).ToHsv();
                    up2s[x - xStart] = bitmap.GetPixel(x, y - 2).ToHsv();
                }

                if (IsHsvBlueBar(AverageHsv(currentRows)) && localIsWhite(AverageHsv(up4s)) && isGray(AverageHsv(up2s)))
                {
                    results[resultI++] = y - 353;
                }
            }

            return results;
        }

        private static bool IsHsvBlueBar(Hsv p) => p.Hue >= 195 && p.Hue <= 210 && p.Value >= 0.85f;

        private Rectangle[] LocateTextLines(Bitmap bitmap)
        {
            var rects = new List<Rectangle>();

            //Approach 1: Fast but uses hard coded values
            ////=Unique locations=
            ////4x305: 577
            ////4x260: 577
            ////4x215: 1

            //var knownLocations = new Point[]
            //{
            //    new Point(15, 215),
            //    new Point(15, 260),
            //    new Point(15, 305)
            //};
            //foreach (var point in knownLocations)
            //{
            //    //Use left and right padding to ensure we have a white background
            //    var valid = true;
            //    const int validSize = 6;
            //    for (int x = 1; x < 1 + validSize; x++)
            //    {
            //        if (bitmap.GetPixel(x, point.Y).ToHsv().Value < 0.88f)
            //        {
            //            valid = false;
            //            break;
            //        }
            //    }
            //    if (!valid)
            //        continue;
            //    for (int x = bitmap.Width - 1; x > bitmap.Width - 1 - validSize; x--)
            //    {
            //        if (bitmap.GetPixel(x, point.Y).ToHsv().Value < 0.88f)
            //        {
            //            valid = false;
            //            break;
            //        }
            //    }
            //    if (!valid)
            //        continue;

            //    var pixelFound = false;
            //    for (int y = point.Y; y < point.Y + 25; y+=2)
            //    {
            //        if (pixelFound)
            //            break;
            //        for (int x = point.X; x < point.X + 6; x++)
            //        {
            //            if(bitmap.GetPixel(x, y).ToHsv().Value <= 0.26f)
            //            {
            //                pixelFound = true;
            //                break;
            //            }
            //        }
            //    }
            //    if (pixelFound)
            //        rects.Add(new Rectangle(1, point.Y - 2, bitmap.Width - 2, 32));
            //}

            //Approach 2: slow but adapts to unknown situations
            var onLine = false;
            var startY = 0;
            for (int y = 340; y >= 0; y--)
            {
                //Use left and right padding to ensure we have a white background
                var valid = true;
                const int validSize = 6;
                for (int x = 1; x < 1 + validSize; x++)
                {
                    if (bitmap.GetPixel(x, y).ToHsv().Value < 0.88f)
                    {
                        valid = false;
                    }
                }
                for (int x = bitmap.Width - 1; x > bitmap.Width - 1 - validSize; x--)
                {
                    if (bitmap.GetPixel(x, y).ToHsv().Value < 0.88f)
                    {
                        valid = false;
                    }
                }
                if (!valid)
                    break;

                var anyFound = false;
                for (int x = 5; x < bitmap.Width - 5; x++)
                {
                    if (bitmap.GetPixel(x, y).ToHsv().Value < 0.26f)
                    {
                        if (!onLine)
                        {
                            onLine = true;
                            startY = y;
                        }
                        anyFound = true;
                        break;
                    }
                }
                if (onLine && !anyFound)
                {
                    onLine = false;
                    rects.Add(new Rectangle(4, y - 2, bitmap.Width - 8, startY - y + 5));
                }
            }

            return rects.Select(r => TrimRect(r, bitmap, false)).ToArray();
        }

        private EquipmentItem ParseEquipmentTile(Bitmap bitmap, ILineParser parser)
        {
            var lines = LocateTextLines(bitmap);
            var texts = new List<string>();
            foreach (var line in lines)
            {
                using (var clean = new Bitmap(line.Width, line.Height))
                {
                    for (int x = 0; x < clean.Width; x++)
                    {
                        for (int y = 0; y < clean.Height; y++)
                        {
                            clean.SetPixel(x, y, bitmap.GetPixel(line.Left + x, line.Top + y));
                        }
                    }
                    //using (var g = Graphics.FromImage(clean))
                    //{
                    //    g.DrawImage(bitmap, new Rectangle(0, 0, line.Width, line.Height), line, GraphicsUnit.Pixel);
                    //}

                    using (var prepped = PrepareBitmapForTess(clean))
                    {
                        texts.Add(parser.ParseLine(prepped));
                    }
                }
            }

            var sb = new StringBuilder();
            foreach (var text in texts.Skip(1).Reverse())
            {
                sb.Append(text + " ");
            }

            var result = new EquipmentItem()
            {
                Rank = texts[0],
                Name = sb.ToString().Trim()
            };

            return result;
        }

        private Bitmap[] GetEquipmentTileBitmaps()
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
            List<Bitmap> tiles = ExtractTiles(ref topLeftRect);

            if (tiles.Count > 0)
            {

#if DEBUG
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
                        g.DrawImage(tiles[i], left, top);

                        if ((i + 1) % 6 == 0)
                        {
                            left = 1;
                            top += topLeftRect.Height + 2;
                        }
                        else
                        {
                            left += topLeftRect.Width + 2;
                        }
                    }

                    debug.Save(Path.Combine(_debugFolder, _currentProfileName, "profile_equipment.jpg"), 90L);
                    _logger.Log("Profile equipment image saved");
                }
#endif
            }

            if (tiles.Count == 0)
                return null;
            return tiles.ToArray();
        }

        private List<Bitmap> ExtractTiles(ref Rectangle topLeftRect)
        {
            var tiles = new List<Bitmap>();
            var onlyOneLine = false;
            var loopSafeguard = 0;
            while (loopSafeguard++ < 1500)
            {
                var unownedDetected = false;

                GiveWarframeFocus().Wait();

                using (var bitmap = _gameCapture.GetFullImage())
                {
                    //Read two rows
                    var ys = LocateEquipmentRows(bitmap);
                    //The rows are not going to be in the right place. Scan down from a y of 1142 and detect blue

                    for (int y = 0; y < 2; y++)
                    {
                        //Skip the top row if only one line scrolled down
                        var curRect = new Rectangle(topLeftRect.Left, !onlyOneLine ? ys[y] : ys[1], topLeftRect.Width, topLeftRect.Height);

                        if (unownedDetected)
                            break;

                        for (int x = 0; x < 6; x++)
                        {
                            //For white check point: Right - 8, Bottom - 20
                            //White is v >= 0.961
                            if (IsTileUnowned(bitmap, curRect))
                            {
                                //_logger.Log("Unowned equipment detected");
                                //bitmap.Save("equipment_unowned_screen.png");
                                unownedDetected = true;
                                break;
                            }

                            var tile = new Bitmap(curRect.Width, curRect.Height);
                            using (var g = Graphics.FromImage(tile))
                            {
                                g.DrawImage(bitmap, new Rectangle(0, 0, tile.Width, tile.Height), curRect, GraphicsUnit.Pixel);
                            }
                            tiles.Add(tile);

                            var p = bitmap.GetPixel(curRect.Left + 16, curRect.Bottom - 11).ToHsv();
                            if (!(p.Hue >= 190 && p.Hue <= 218 && p.Value >= 0.85 && p.Saturation >= 0.72f))
                            {
                                string guid = Guid.NewGuid().ToString();
                                tile.Save($"debug\\bad\\equipment_bad_{guid}_item.png");
                                bitmap.Save($"debug\\bad\\equipment_bad_{guid}_screen.png");
                                _dataSender.AsyncSendDebugMessage($"Bad equipment tile detected. See debug\\bad\\{guid}").Wait();
                                _logger.Log("Bad equipment item detected");
                            }

                            // Gap of 40 pixels between items
                            curRect = new Rectangle(curRect.Right + 40, curRect.Top, curRect.Width, curRect.Height);
                        }
                    }
                }

                if (unownedDetected)
                    break;

                //Scroll twice
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();
                _mouse.Click(0, 0);
                Thread.Sleep(33);
                _mouse.MoveTo(606, 1036);
                Thread.Sleep(33);
                _mouse.Click(3148, 1029);
                Thread.Sleep(66);
                _mouse.ScrollDown();
                Thread.Sleep(33);
                if (IsEquipmentScrolledDown())
                    onlyOneLine = true;
                _mouse.ScrollDown();
                Thread.Sleep(33);
                if (!onlyOneLine && IsEquipmentScrolledDown())
                    onlyOneLine = true;
                _mouse.MoveTo(0, 0);
                Thread.Sleep(Math.Max(0, 250 - (int)timer.ElapsedMilliseconds)); //Let rows animate in partially
                timer.Stop();
            }

            return tiles;
        }

        private bool IsEquipmentScrolledDown()
        {
            using (var bitmap = _gameCapture.GetFullImage())
            {
                return bitmap.GetPixel(3755, 1925).ToHsv().Value >= 0.85f;
            }
        }

        private bool IsTileUnowned(Bitmap bitmap, Rectangle curRect)
        {
            var checkSize = 9;
            var value = 0f;
            var count = 0;
            for (int y = 0; y < checkSize; y++)
            {
                for (int x = 0; x < checkSize; x++)
                {
                    var p = bitmap.GetPixel(curRect.Right - 8 - checkSize / 2, curRect.Bottom - 20 - checkSize / 2).ToHsv();
                    value += p.Value;
                    count++;
                }
            }
            return value / count < 0.8f;
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
