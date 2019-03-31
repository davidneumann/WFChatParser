using Application.interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application
{
    public class ChatRivenBot
    {
        private readonly string _launcherPath;
        private readonly IMouseMover _mouse;

        public ChatRivenBot(string launcherFullPath, IMouseMover mouseMover)
        {
            _launcherPath = launcherFullPath;
            _mouse = mouseMover;
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

        public async Task AsyncRun(CancellationToken cancellationToken)
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
                        await Task.Delay(1000);
                        launcher = System.Diagnostics.Process.GetProcessesByName("Launcher").FirstOrDefault();
                        if (launcher == null)
                            continue;
                    }
                    SetForegroundWindow(launcher.MainWindowHandle);
                    Rect launcherRect = new Rect();
                    GetWindowRect(launcher.MainWindowHandle, ref launcherRect);
                    _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                        launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                    await Task.Delay(1000);
                    if (launcher.HasExited)
                        break;
                }
            }
            //Check if on login screen
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
    }
}
