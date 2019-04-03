using Application.Enums;
using Application.Interfaces;
using Application.Interfaces;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application
{
    public class ChatRivenBot : IDisposable
    {
        private readonly string _launcherPath;
        private readonly IMouseMover _mouse;
        private readonly IScreenStateHandler _screenStateHandler;
        private readonly IGameCapture _gameCapture;
        private readonly IKeyboard _keyboard;
        private readonly bool _usingOBS;
        private readonly ObsSettings _obsSettings;
        private OBSWebsocket _obs;
        private static string _password;

        public ChatRivenBot(string launcherFullPath, IMouseMover mouseMover, IScreenStateHandler screenStateHandler,
            IGameCapture gameCapture,
            ObsSettings obsSettings,
            string password,
            IKeyboard keyboard)
        {
            _launcherPath = launcherFullPath;
            _mouse = mouseMover;
            _screenStateHandler = screenStateHandler;
            _gameCapture = gameCapture;
            _obsSettings = obsSettings;
            _password = password;
            _keyboard = keyboard;

            if (_obsSettings != null)
                ConnectToObs();
        }

        private void ConnectToObs()
        {
            _obs = new OBSWebsocket();
            _obs.Connect(_obsSettings.Url, _obsSettings.Password);
        }

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        public void AsyncRun(CancellationToken cancellationToken)
        {
            //Check if WF is running
            var wfAlreadyRunning = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0;
            if (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length == 0)
            {
                StartWarframe();
            }
            WaitForLoadingScreen(wfAlreadyRunning);

            //Check if on login screen
            LogIn();

            //Check if on daily reward screen
            ClaimDailyReward();
            
            //start an infinite loop
            while (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0)
            {
                //Get to Glyph screen if not already there
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state == Enums.ScreenState.ControllingWarframe)
                    {
                        GoToGlyphScreenAndSetupFilters();
                    }
                    else
                    {
                        ////Tell chat parser to parse and send the next page of results
                    }
                }
                break;
            }
        }

        private void GoToGlyphScreenAndSetupFilters()
        {
            NavigateToGlyphScreen();
            Thread.Sleep(250);
            using (var glyphScreen = _gameCapture.GetFullImage())
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                if (_screenStateHandler.GetScreenState(glyphScreen) == ScreenState.GlyphWindow)
                {
                    //Check if filter is setup with asdf
                    if (!_screenStateHandler.GlyphFiltersPresent(glyphScreen))
                    {
                        //Click clear to be safe
                        _mouse.Click(1125, 263);
                        Thread.Sleep(50);
                        //_mouse.Click(1125, 263);
                        //Thread.Sleep(50);

                        _keyboard.SendPaste("asdf");
                        Thread.Sleep(50);
                    }
                    if (_screenStateHandler.IsChatCollapsed(glyphScreen))
                    {
                        //Click and drag to move chat into place
                        _mouse.ClickAndDrag(new Point(160, 2110), new Point(0, 2160), 100);
                    }
                    else if (!_screenStateHandler.IsChatOpen(glyphScreen))
                        throw new ChatMissingException();
                }
            }
        }

        private void ClaimDailyReward()
        {
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.Click(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                    _mouse.Click(3325, 1951);
            }
        }

        private void LogIn()
        {
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            _mouse.MoveTo(0, 0);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    DisableWarframeGameCapture();
                    _mouse.Click(screen.Width, 0);
                    _mouse.Click(2671, 1239);
                    _keyboard.SendPaste(_password);

                    _mouse.Click(2945, 1333);
                    //Give plenty of time for the screen to transition
                    System.Threading.Thread.Sleep(5000);
                }
            }
            EnableWarframeGameCapture();
        }

        private void WaitForLoadingScreen(bool wfAlreadyRunning)
        {
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            while (!wfAlreadyRunning && DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                _mouse.MoveTo(0, 0);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        //await Task.Delay(1000);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                        break;
                }
            }
        }

        private void StartWarframe()
        {
            ////If not start launcher, click play until WF starts
            while (true)
            {
                var launcher = System.Diagnostics.Process.GetProcessesByName("Launcher").FirstOrDefault();
                if (launcher == null)
                {
                    launcher = new System.Diagnostics.Process()
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = _launcherPath
                        }
                    };
                    launcher.Start();
                    //await Task.Delay(1000);
                    System.Threading.Thread.Sleep(1000);
                    launcher = System.Diagnostics.Process.GetProcessesByName("Launcher").FirstOrDefault();
                    if (launcher == null)
                        continue;
                }
                SetForegroundWindow(launcher.MainWindowHandle);
                Rect launcherRect = new Rect();
                GetWindowRect(launcher.MainWindowHandle, ref launcherRect);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                System.Threading.Thread.Sleep(1000);
                if (launcher.HasExited)
                {
                    DisableWarframeGameCapture();
                    System.Threading.Thread.Sleep(5000);
                    break;
                }
            }
        }

        private void NavigateToGlyphScreen()
        {
            //Ensure we are controlling warframe
            var tries = 0;
            while (true)
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    var state = _screenStateHandler.GetScreenState(screen);
                    if (state != Enums.ScreenState.ControllingWarframe)
                    {
                        _keyboard.SendEscape();
                        System.Threading.Thread.Sleep(125);
                    }
                    else
                        break;
                }
                tries++;
                if (tries > 15)
                    throw new NavigationException(ScreenState.ControllingWarframe);
            }
            //Send escape to open main menu
            _keyboard.SendEscape();
            System.Threading.Thread.Sleep(1000); //Give menu time to animate

            //Check if on Main Menu
            SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.MainMenu)
                {
                    //Click profile
                    SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                    _mouse.Click(728, 937);
                    Thread.Sleep(500);

                    using (var profileMenuImage = _gameCapture.GetFullImage())
                    {
                        if (_screenStateHandler.GetScreenState(profileMenuImage) == Enums.ScreenState.ProfileMenu)
                        {
                            //Click Glyph
                            _mouse.Click(693, 948);
                            Thread.Sleep(500);
                        }
                        else
                            throw new NavigationException(ScreenState.ProfileMenu);
                    }
                }
                else
                    throw new NavigationException(ScreenState.MainMenu);
            }          
        }

        private void EnableWarframeGameCapture()
        {
            if (_obs != null)
            {
                var game = _obs.ListScenes().First().Items.First(i => i.SourceName.ToLower().Contains("warframe"));
                var fields = new JObject();
                fields.Add("item", game.SourceName);
                fields.Add("visible", true);
                _obs.SendRequest("SetSceneItemProperties", fields);
            }
        }

        private void DisableWarframeGameCapture()
        {
            if (_obs != null)
            {
                var game = _obs.ListScenes().First().Items.First(i => i.SourceName.ToLower().Contains("warframe"));
                var fields = new JObject();
                fields.Add("item", game.SourceName);
                fields.Add("visible", false);
                _obs.SendRequest("SetSceneItemProperties", fields);
            }
        }

        public void Dispose()
        {
            if (_obs != null)
                _obs.Disconnect();
        }
    }
}
