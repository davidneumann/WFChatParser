using Application.Actionables.ChatBots;
using Application.Interfaces;
using Application.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Actionables
{
    internal abstract class BaseWarframeBot : IActionable
    {
        private IKeyboard _keyboard;
        private IScreenStateHandler _screenStateHandler;
        private ILogger _logger;
        private IGameCapture _gameCapture;
        private CancellationToken _cancellationToken;
        private WarframeClientInformation _warframeCredentials;
        private IMouse _mouse;
        private IDataSender _dataSender;
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

        protected async Task<StartWarframeResult> StartWarframe()
        {
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
            protected StartState State { get; }
            protected Process WarframeProcess { get; }

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
        private Task CloseWarframe()
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
    }
}
