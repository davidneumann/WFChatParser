﻿using Application.Actionables.ChatBots;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables
{
    internal abstract class BaseWarframeBot : IActionable
    {
        protected IKeyboard _keyboard;
        protected IScreenStateHandler _screenStateHandler;
        protected ILogger _logger;
        protected IGameCapture _gameCapture;
        protected CancellationToken _cancellationToken;
        protected WarframeClientInformation _warframeCredentials;
        protected IMouse _mouse;
        protected IDataSender _dataSender;
        protected Process _warframeProcess;

        public abstract bool IsRequestingControl { get;}

        protected BaseWarframeBot(CancellationToken cancellationToken,
            WarframeClientInformation warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture,
            IDataSender dataSender)
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

        protected async Task<StartWarframeResult> BaseStartWarframe()
        {
            _logger.Log("Attempting to start warframe");
            var existingWarframes = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray();

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
                    try { p.Kill(); } catch { }
                });

                return new StartWarframeResult(StartWarframeResult.StartState.LauncherAlreadyRunning, null);
            }

            ////If not start launcher, click play until WF starts
            var start = DateTime.Now;
            while (true)
            {

                //Yield to other tasks after 4 minutes of waiting
                if (DateTime.Now.Subtract(start).TotalMinutes > 4f)
                {
                    return new StartWarframeResult(StartWarframeResult.StartState.TimedOutWaitingForLauncher, null);
                }

                _screenStateHandler.GiveWindowFocus(launcher.MainWindowHandle);
                var launcherRect = _screenStateHandler.GetWindowRectangle(launcher.MainWindowHandle);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                await Task.Delay(17);
                _keyboard.SendSpace();
                await Task.Delay(1000);
                if (launcher.HasExited)
                {
                    await Task.Delay(10000);
                    break;
                }
            }

            foreach (var warframe in System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray())
            {
                if (!existingWarframes.Any(eWF => eWF.MainWindowHandle == warframe.MainWindowHandle))
                    return new StartWarframeResult(StartWarframeResult.StartState.LaunchedSuccessfully, warframe);
            }

            return new StartWarframeResult(StartWarframeResult.StartState.Unknown, null);
        }

        protected class StartWarframeResult
        {
            public StartState State { get; }
            public Process WarframeProcess { get; }

            public StartWarframeResult(StartState state, Process process)
            {
                State = state;
                WarframeProcess = process;
            }

            public enum StartState
            {
                Unknown,
                LauncherAlreadyRunning,
                TimedOutWaitingForLauncher,
                LaunchedSuccessfully
            }
        }


        protected async Task BaseWaitForLoadingScreen()
        {
            _logger.Log("Waiting for loading screen");
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            while (DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                _mouse.MoveTo(0, 0);
                await Task.Delay(17);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        return;
                    }
                }
            }

            //Didn't find the login screen within the time allowed
            throw new LoginScreenTimeoutException();
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
                    _warframeProcess.Close();
                    _warframeProcess = null;
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
            });
        }

        public abstract Task TakeControl();

        [Serializable]
        protected class LoginScreenTimeoutException : Exception
        {
            public LoginScreenTimeoutException() { }
        }
    }
}