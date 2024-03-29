﻿using Application.Actionables.ChatBots;
using Application.Actionables.States;
using Application.Enums;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables
{
    public abstract class BaseBot : IActionable
    {
        protected IKeyboard _keyboard;
        protected IScreenStateHandler _screenStateHandler;
        protected ILogger _logger;
        protected IGameCapture _gameCapture;
        protected CancellationToken _cancellationToken;
        protected WarframeClientInformation _warframeCredentials;
        protected IMouse _mouse;
        protected IDataTxRx _dataSender;
        protected Process _warframeProcess;
        protected DateTime _lastMessage = DateTime.UtcNow;

        protected BaseBotState _baseState = BaseBotState.StartWarframe;

        protected bool _requestingControl = true;
        public bool IsRequestingControl => _requestingControl;

        protected int _failedPostLoginScreens;

        private static Dictionary<Process, BaseBot> _claimedWarframes = new Dictionary<Process, BaseBot>();

        protected BaseBot(CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataTxRx dataSender)
        {
            _cancellationToken = cancellationToken;
            _warframeCredentials = warframeCredentials;
            _mouse = mouse;
            _keyboard = keyboard;
            _screenStateHandler = screenStateHandler;
            _logger = logger;
            _gameCapture = gameCapture;
            _warframeCredentials = warframeCredentials;
            _dataSender = dataSender;
        }

        public abstract Task TakeControl();

        protected Task BaseTakeControl()
        {
            KillDeadWarframes();
            switch (_baseState)
            {
                case BaseBotState.StartWarframe:
                    _logger.Log("Starting warframe");
                    return StartWarframe();
                case BaseBotState.WaitForLoadScreen:
                    _logger.Log("Waiting for load screen");
                    return WaitForLoadingScreen();
                case BaseBotState.LogIn:
                    _logger.Log("Running log in logic");
                    return LogIn();
                case BaseBotState.ClaimReward:
                    _logger.Log("Claiming reward");
                    return ClaimDailyRewardTask();
                case BaseBotState.CloseWarframe:
                    _logger.Log("Closing Warframe");
                    return CloseWarframe();
                default:
                    break;
            }

            return Task.CompletedTask;
        }

        protected void KillDeadWarframes()
        {
            var deadWarframes = _claimedWarframes.Where(p => p.Key.HasExited).ToList();
            foreach (var kvp in deadWarframes)
            {
                try
                {
                    kvp.Key.Kill();
                    kvp.Value.CloseWarframe();
                }
                catch { }
            }

            var allIds = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Select(p => p.Id);
            var unclaimedIds = allIds.Where(id => !_claimedWarframes.Keys.Any(p => p.Id == id)).ToList();
            foreach (var id in unclaimedIds)
            {
                try
                {
                    var p = Process.GetProcessById(id);
                    p.Kill();
                }
                catch
                {

                }
            }
        }

        private async Task StartWarframe()
        {
            KillDeadWarframes();

            _requestingControl = false;

            var launcher = new System.Diagnostics.Process()
            {
                StartInfo = _warframeCredentials.StartInfo
            };
            launcher.Start();
            await Task.Delay(5000);

            //Check if there was already a launcher running
            if (launcher.HasExited)
            {
                System.Diagnostics.Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("launcher")).ToList().ForEach(p =>
                {
                    _logger.Log("Found an existing launcher " + p.Id);
                    try { p.Kill(); } catch { }
                });

                _logger.Log("Trying to start again as there was already a launcher");
                _baseState = BaseBotState.StartWarframe;
                _requestingControl = true;
                return;
            }

            ////If not start launcher, click play until WF starts
            var start = DateTime.Now;
            var launcherClosed = false;
            while (true)
            {
                //Yield to other tasks after 4 minutes of waiting
                if (DateTime.Now.Subtract(start).TotalMinutes > 4f)
                {
                    _logger.Log("Yielding after 4 minutes of waiting");
                    _baseState = BaseBotState.StartWarframe;
                    _requestingControl = true;
                    return;
                }

                var didTryMain = false;
                for (int i = 0; i < 2; i++)
                {
                    var handle = launcher.MainWindowHandle;
                    if (!didTryMain)
                        _logger.Log("Trying to interact with main window handle");
                    else
                    {
                        _logger.Log("Trying to interact with other handle");
                        handle = launcher.Handle;
                    }
                    didTryMain = true;
                    _screenStateHandler.GiveWindowFocus(launcher.MainWindowHandle);
                    var launcherRect = _screenStateHandler.GetWindowRectangle(launcher.MainWindowHandle);
                    _logger.Log($"Found launcher {handle.ToString()} at {launcherRect.Left},{launcherRect.Top} {launcherRect.Right - launcherRect.Left}x{launcherRect.Bottom - launcherRect.Top}");
                    _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                        launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                    await Task.Delay(66);
                    _keyboard.SendSpace();
                    await Task.Delay(10000);
                    if (launcher.HasExited)
                    {
                        _logger.Log("Launcher has exited");
                        break;
                    }
                }
                if (launcher.HasExited)
                {
                    launcherClosed = true;
                    _logger.Log("Waiting 30 seconds as launcher has exited");
                    await Task.Delay(30000);
                    break;
                }
            }

            //Launcher failed to close game. Kill all warframes
            if (!launcherClosed)
            {
                _logger.Log("Launcher failed to operate correctly. Killing all warframes");
                foreach (var warframe in System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray())
                {

                    try
                    {
                        warframe.Kill();
                    }
                    catch
                    { }
                }
                return;
            }

            for (int tries = 0; tries < 20; tries++)
            {
                _logger.Log("Trying to find our warframe instance");
                if (_warframeProcess != null || (_warframeProcess != null && !_warframeProcess.HasExited))
                {
                    break;
                }

                foreach (var warframe in System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray())
                {
                    if (!_claimedWarframes.Keys.Any(eWF => eWF.Id == warframe.Id))
                    {
                        _warframeProcess = warframe;
                        _claimedWarframes.Add(warframe, this);
                        _logger.Log("New launcher instance found: " + warframe.Id);
                        break;
                    }
                }

                await Task.Delay(1000);
            }

            //Give 15 minutes on a fresh login to allow slow chats to fill up before killing them.
            _lastMessage = DateTime.UtcNow.AddMinutes(15);
            _baseState = BaseBotState.WaitForLoadScreen;
            _requestingControl = true;
        }

        private async Task WaitForLoadingScreen()
        {
            _requestingControl = false;

            _logger.Log("Waiting for login screen");
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            var atLogin = false;
            while (DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                _mouse.MoveTo(0, 0);
                await Task.Delay(66);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        atLogin = true;
                        break;
                    }
                }
            }

            if (atLogin)
            {
                _baseState = BaseBotState.LogIn;
            }
            else
                _baseState = BaseBotState.CloseWarframe;
            _requestingControl = true;
        }

        private async Task LogIn()
        {
            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            await Task.Delay(100);
            _mouse.Click(0, 0);

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    _logger.Log("Logging in");
                    ////Username
                    //_mouse.Click(3041, 1145);
                    //await Task.Delay(66);
                    //_keyboard.SendPaste(_warframeCredentials.Username);
                    //await Task.Delay(66);

                    //Password
                    _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                    _logger.Log("Clicking password box");
                    await Task.Delay(66);
                    _mouse.Click(2936, 1235);
                    await Task.Delay(66);
                    _mouse.Click(2936, 1235);
                    await Task.Delay(750);

                    _logger.Log("Pasting password");
                    await Task.Delay(350);
                    _keyboard.SendPaste(_warframeCredentials.Password);
                    await Task.Delay(350);

                    //Login
                    _logger.Log("Clicking log in button");
                    _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                    _mouse.Click(2945, 1333);
                    await Task.Delay(66);
                    _mouse.Click(2945, 1333);
                    await Task.Delay(750);

                    //Give plenty of time for the screen to transition
                    var startTime = DateTime.Now;
                    while (DateTime.Now.Subtract(startTime).TotalSeconds < 15)
                    {
                        using (var newScreen = _gameCapture.GetFullImage())
                        {
                            if (_screenStateHandler.GetScreenState(newScreen) == ScreenState.LoginScreen)
                                await Task.Delay(1000);
                            else
                                break;
                        }
                    }
                    _logger.Log("Waiting 5 seconds for screen transition");
                    await Task.Delay(5000);
                    _logger.Log("Waiting done");
                }
                else
                {
                    _logger.Log("Login screen not detected. Restarting warframe.");
                    _baseState = BaseBotState.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
            }

            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            await Task.Delay(100);
            using (var screen = _gameCapture.GetFullImage())
            {
                ScreenState state = _screenStateHandler.GetScreenState(screen);
                
                //_logger.Log("New screen state: " + state.ToString());
                //var path = SaveScreenToDebug(screen);
                //await _dataSender.AsyncSendDebugMessage("New post login screen: " + path);

                if (state == ScreenState.LoginScreen)
                {
                    _logger.Log("Login screen still detected. Restarting warframe.");
                    _baseState = BaseBotState.CloseWarframe;
                    _requestingControl = true;
                    return;
                }
                else if (state == ScreenState.DailyRewardScreenItem || state == ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("Claiming daily reward.");
                    await ClaimDailyReward();
                    _requestingControl = false;
                }
                else if (_failedPostLoginScreens < 3)
                {
                    _logger.Log("Increasing failed login attempt counter");
                    _failedPostLoginScreens++;
                    _logger.Log($"Failed logins {_failedPostLoginScreens}");
                }
                else if (_failedPostLoginScreens >= 3)
                {
                    //We are stuck on some unkown reward screen or new feature screen.
                    //Time to click wildly

                    //Click blidnly at a continue button
                    _logger.Log("Attempting to click continue button");
                    _mouse.Click((int)(screen.Width - screen.Width * 0.06298828125),
                        (int)(screen.Height - screen.Height * 0.0685185185185185));

                    //Wait and then check to see if that worked
                    await Task.Delay(1000);

                    //If we are still not controlling the warframe then more blind clicking.
                    //NEVER EVER MOVE THE MOUSE WHILE CONTROLLING THE WARFRAME!
                    using (var newScreen = _gameCapture.GetFullImage())
                    {
                        ScreenState newState = _screenStateHandler.GetScreenState(screen);
                        if (newState != ScreenState.ControllingWarframe)
                        {
                            //Click blindly on a possible reward location
                            _mouse.Click((int)(screen.Width - screen.Width * 0.28173828125),
                                (int)(screen.Height - screen.Height * 0.2555555555555556));
                            await Task.Delay(1000);
                        }
                    }
                }


                _logger.Log("Waiting in the background for 60 seconds");
                var _ = Task.Run(async () =>
                {
                    await Task.Delay(60 * 1000);
                    _logger.Log("Done waiting 60 seconds");

                    _baseState = BaseBotState.Running;
                    _requestingControl = true;
                });
            }
        }

        private Task ClaimDailyRewardTask()
        {
            return Task.Run(async () =>
            {
                await ClaimDailyReward();
                _baseState = BaseBotState.Running;
                _requestingControl = true;
            });
        }

        private async Task ClaimDailyReward()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
            {
                await Task.Delay(66);
                _mouse.Click(0, 0);
            }

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    _logger.Log("Claiming random middle reward");
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("Claiming unkown plat discount");
                    _mouse.Click(3325, 1951);
                }
            }

            await Task.Delay(66);

            CloseUnknownWindow();

            await Task.Delay(1000);
        }
        private void CloseUnknownWindow()
        {
            //Close any annoying windows it opened
            using (var screen = _gameCapture.GetFullImage())
            {
                if (_screenStateHandler.IsExitable(screen))
                {
                    _mouse.Click(3816, 2013);
                    Thread.Sleep(30);
                }
            }
        }

        protected Task CloseWarframe()
        {
            _logger.Log("Closing warframe");
            return Task.Run(() =>
            {
                if (_warframeProcess != null)
                {
                    try
                    {
                        _warframeProcess.Kill();
                    }
                    catch
                    { }
                    _warframeProcess = null;
                }

                _baseState = BaseBotState.StartWarframe;
                _requestingControl = true;
            });
        }

        public void ShutDown()
        {
            if (_warframeProcess != null && !_warframeProcess.HasExited)
            {
                try
                {
                    CloseWarframe().Wait();
                }
                catch
                {
                    _warframeProcess.Kill();
                    _warframeProcess = null;
                }
            }
        }

        protected string SaveScreenToDebug(Bitmap screen)
        {
            if (!System.IO.Directory.Exists("debug"))
                System.IO.Directory.CreateDirectory("debug");
            var filePath = System.IO.Path.Combine("debug", DateTime.Now.ToFileTime() + ".png");
            try { screen.Save(filePath, System.Drawing.Imaging.ImageFormat.Png); return filePath; }
            catch (Exception e)
            {
                //_dataSender.AsyncSendDebugMessage("Failed to save screen: " + e.ToString());
                return null;
            }
        }
    }
}
