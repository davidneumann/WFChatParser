using Application.Interfaces;
using Application.Interfaces;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
        private readonly bool _usingOBS;
        private readonly ObsSettings _obsSettings;
        private OBSWebsocket _obs;

        public ChatRivenBot(string launcherFullPath, IMouseMover mouseMover, IScreenStateHandler screenStateHandler,
            IGameCapture gameCapture,
            ObsSettings obsSettings)
        {
            _launcherPath = launcherFullPath;
            _mouse = mouseMover;
            _screenStateHandler = screenStateHandler;
            _gameCapture = gameCapture;
            _obsSettings = obsSettings;

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
            if (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length == 0)
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
                        break;
                    }
                }
            }
            DisableWarframeGameCapture();
            while (true)
            {
                SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoadingScreen)
                    {
                        //await Task.Delay(1000);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                        break;
                }
            }

            //Check if on login screen
            using (var screen = _gameCapture.GetFullImage())
            {
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    DisableWarframeGameCapture();
                }
            }
            ////https://github.com/Palakis/obs-websocket-dotnet black out the obs screen
            ////If so paste in password and click login
            //Check if on daily reward screen
            ////IF so cilck what ever the middle most item is
            //start an infinite loop
            ////Check if is in Warframe controller mode / not in UI interaction mode
            //////If so open menu 
            //////      -> profile 
            //////      -> glyphs 
            //////      -> Check if chat icon is in default location or already moved location
            ////////         If already moved open chat
            ////////         If in deafult location open chat and move it
            ////////         If somewhere else, crash
            //////      -> check if chat is in the default location and if so move it 
            ////Tell chat parser to parse and send the next page of results
        }

        private void DisableWarframeGameCapture()
        {
            if (_obs != null)
            {
                var game = _obs.ListScenes().First().Items.First(i => i.SourceName.ToLower().Contains("warframe"));
                var fields = new JObject();
                fields.Add("item", game.SourceName);
                fields.Add("visible", "false");
                _obs.SendRequest("SetSceneItemProperties", fields);
            }
        }

        public void Dispose()
        {
            if(_obs != null)
                _obs.Disconnect();
        }
    }
}
