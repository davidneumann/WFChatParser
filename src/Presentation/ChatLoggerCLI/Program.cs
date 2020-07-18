using AdysTech.CredentialManager;
using Application;
using Application.Actionables;
using Application.Actionables.ChatBots;
using Application.Data;
using Application.Enums;
using Application.LogParser;
using DataStream;
using ImageOCR;
using Microsoft.Extensions.Configuration;
using RelativeChatParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WarframeDriver;
using WFGameCapture;

namespace ChatLoggerCLI
{
    public class Program
    {
        private static List<IDisposable> _disposables = new List<IDisposable>();
        private static CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private static ClientWebsocketDataSender _dataSender;
        private static bool _cleanExitRequested = false;

        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        public static void Main(string[] args)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Console.CancelKeyPress += Console_CancelKeyPress;

            const ClientLanguage language = ClientLanguage.English;
            //var rivenParser = new RivenParser(language);
            //_disposables.Add(rivenParser);
            //Console.WriteLine("Starting up image parser");

            Console.WriteLine("Loading config for data sender");
            IConfiguration config = new ConfigurationBuilder()
              //.AddJsonFile("appsettings.json", true, true)
              //.AddJsonFile("appsettings.development.json", true, true)
              .AddJsonFile("appsettings.production.json", true, true)
              .Build();

            _dataSender = new ClientWebsocketDataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"],
                config["DataSender:LogMessagePrefix"],
                config["DataSender:LogLineMessagePrefix"]);
            _ = Task.Run(_dataSender.ConnectAsync);

            var logger = new Application.Logger.Logger(_dataSender, _cancellationSource.Token);

            try
            {
                var launchers = config.GetSection("Launchers").GetChildren().AsEnumerable();
                var warframeCredentials = launchers.Select(i =>
                {
                    var section = config.GetSection(i.Path);
                    var startInfo = new ProcessStartInfo();
                    startInfo.UserName = section.GetSection("Username").Value;
                    var password = section.GetSection("Password").Value;
                    System.Security.SecureString ssPwd = new System.Security.SecureString();
                    for (int x = 0; x < password.Length; x++)
                    {
                        ssPwd.AppendChar(password[x]);
                    }
                    startInfo.Password = ssPwd;
                    var info = new FileInfo(section.GetSection("LauncherPath").Value);
                    startInfo.FileName = info.FullName;
                    startInfo.UseShellExecute = false;
                    startInfo.WorkingDirectory = info.Directory.FullName;

                    var credentials = new WarframeClientInformation()
                    {
                        StartInfo = startInfo,
                        Password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value),
                        Username = GetUsername(config["Credentials:Key"], config["Credentials:Salt"], section.GetSection("WarframeCredentialsTarget").Value),
                        Region = section.GetSection("Region").Value
                    };
                    return credentials;
                }).ToArray();

                Console.WriteLine("Data sender connecting");
                SendOldErrorLog();

                _dataSender.RequestToKill += (s, e) =>
                {
                    logger.Log("Request to kill received");
                    Console_CancelKeyPress(null, null);
                };
                _dataSender.RequestSaveAll += (s, e) =>
                {
                    try
                    {
                        for (int i = 6; i >= 0; i--)
                        {
                            var dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves");
                            if (e.Name != null && e.Name.Length > 0)
                                dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves", e.Name);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            var curFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + i + ".png");
                            var copyFile = Path.Combine(dir, "capture_" + i + ".png");
                            if (File.Exists(curFile))
                                File.Copy(curFile, copyFile, true);
                        }
                    }
                    catch { }
                };

                //var password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"]);

                var logParser = new WarframeLogParser();
                var redtextthing = new RedTextParser(logParser);
                redtextthing.OnRedText += Redtextthing_OnRedText;
                logParser.OnNewMessage += LogParser_OnNewMessage;

                var gc = new GameCapture(logger);
                var obs = GetObsSettings(config["Credentials:Key"], config["Credentials:Salt"]);
                logger.Log("Starting bot. Expected " + warframeCredentials.Length + " clients");
                var bot = new MultiChatRivenBot(warframeCredentials, new MouseHelper(),
                    new KeyboardHelper(),
                    new ScreenStateHandler(),
                    new RivenParserFactory(language),
                    new RivenCleaner(),
                    _dataSender,
                    gc,
                    logger,
                    new RelativePixelParserFactory(logger));

                var drive = DriveInfo.GetDrives().First(d => d.Name == Path.GetPathRoot(Environment.CurrentDirectory));
                logger.Log("Starting bot on drive: " + Path.GetPathRoot(Environment.CurrentDirectory) + ". Available space: " + drive.AvailableFreeSpace + " bytes");

                Task t = bot.AsyncRun(_cancellationSource.Token);
                var lastDate = DateTime.Today.Subtract(TimeSpan.FromDays(1));
                while (!t.IsCanceled && !t.IsCompleted && !t.IsCompletedSuccessfully && !t.IsFaulted)
                {
                    //Delete old files
                    if (lastDate != DateTime.Today)
                    {
                        logger.Log("Deleting old files");
                        if (Directory.Exists("riven_images"))
                        {
                            foreach (var folder in Directory.GetDirectories("riven_images"))
                            {
                                var folderInfo = new DirectoryInfo(folder);
                                logger.Log("Looking at: " + folderInfo.Name);
                                var splits = folderInfo.Name.Split('_');
                                try
                                {
                                    var time = new DateTime(int.Parse(splits[0]), int.Parse(splits[1]), int.Parse(splits[2]));
                                    if (DateTime.Today.Subtract(time).TotalDays > 3)
                                    {
                                        logger.Log("Deleting: " + folderInfo.Name);
                                        Directory.Delete(folderInfo.FullName, true);
                                    }
                                }
                                catch { }
                            }
                        }
                        else
                            logger.Log("Failed to find riven_images folder for deletion");
                        lastDate = DateTime.Today;
                    }

                    //var debug = progress.GetAwaiter().IsCompleted;
                    System.Threading.Thread.Sleep(1000);
                }

                if (t.IsFaulted || t.Exception != null)
                {
                    Console.WriteLine("\n" + t.Exception);
                    try
                    {
                        _dataSender.AsyncSendDebugMessage(t.Exception.ToString()).Wait();
                        System.Threading.Thread.Sleep(2000);
                    }
                    catch
                    {
                        _cancellationSource.Cancel();
                        logger.Log("Bad state detected: IsFaulted: " + t.IsFaulted + " Exception: " + t.Exception);
                        _dataSender.AsyncSendDebugMessage("Bad state detected: IsFaulted: " + t.IsFaulted + " Exception: " + t.Exception).Wait();
                    }
                }
            }
            catch (Exception e)
            {
                _dataSender.AsyncSendDebugMessage(e.ToString()).Wait();
            }

            if (!_cleanExitRequested)
            {
                //Failure state detected! Try to clean up images in case that was the issue
                try
                {
                    var cuttoffTime = DateTime.Now.Subtract(TimeSpan.FromDays(2));
                    foreach (var file in Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "riven_images")))
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            if (info.LastWriteTime < cuttoffTime)
                                File.Delete(file);
                        }
                        catch { }
                    }
                }
                catch { }

                var shutdown = new System.Diagnostics.Process()
                {
                    StartInfo = new ProcessStartInfo("shutdown.exe", "/r /f /t 0")
                };
                shutdown.Start();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string fatalMessage = "Fatal exception: " + e.ToString() + "\n\r" + e.ExceptionObject.ToString();
            try
            {
                _dataSender.AsyncSendDebugMessage(fatalMessage).Wait();
            }
            catch
            { }

            try
            {
                File.WriteAllText("Fatal_error.txt", fatalMessage);
                Console.WriteLine(fatalMessage);
            }
            catch { }
        }

        private static void SendOldErrorLog()
        {
            try
            {
                const string oldPath = "error.old.txt";
                if (File.Exists(oldPath))
                {
                    Console.WriteLine("Found old error");
                    var lastWrite = File.GetLastWriteTime("error.old.txt");
                    using (var fs = new FileStream("error.old.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var sr = new StreamReader(fs, Encoding.Default))
                        {
                            var raw = new StringBuilder();
                            var error = new StringBuilder();
                            while (!sr.EndOfStream)
                            {
                                var line = sr.ReadLine();
                                raw.AppendLine(line);
                                if (!line.Contains("WARNING! LEAK!"))
                                    error.AppendLine(line);
                            }


                            var errorImages = Directory.GetFiles(Environment.CurrentDirectory)
                                .Where(f => f.EndsWith(".png")).ToArray();
                            if (errorImages.Length > 0)
                            {
                                raw.AppendLine("\n=Debug images=");
                                error.AppendLine("\n=Debug images=");
                                var debugDir = Path.Combine("debug", "errors_" + lastWrite.Ticks);
                                Directory.CreateDirectory(debugDir);
                                foreach (var file in errorImages
                                    .Select(f => new FileInfo(f)))
                                {
                                    try
                                    {
                                        var newPath = Path.Combine(debugDir, file.Name);
                                        File.Copy(file.FullName, newPath);
                                        error.AppendLine(newPath);
                                        raw.AppendLine(newPath);
                                    }
                                    catch { }
                                }
                            }
                            if (error.Length > 3)
                                _dataSender.AsyncSendDebugMessage("Past client failed catastrophically.\n " + error.ToString()).Wait();

                            Console.WriteLine(raw.ToString());
                            File.WriteAllText("error_last_rebuild.txt", raw.ToString());
                        }
                    }
                    //File.Delete(oldPath);
                }
            }
            catch { }
        }

        private static void LogParser_OnNewMessage(LogMessage msg)
        {
            _dataSender.AsyncSendLogLine(msg).Wait();
        }

        private static void Redtextthing_OnRedText(RedTextMessage msg)
        {
            _dataSender.AsyncSendRedtext(msg).Wait();
        }

        private static string GetPassword(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static string GetUsername(string key, string salt, string target)
        {
            var r = CredentialManager.GetCredentials(target, CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.UserName);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static ObsSettings GetObsSettings(string key, string salt)
        {
            var r = CredentialManager.GetCredentials("OBS", CredentialManager.CredentialType.Generic);
            string password = null;
            string url = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    password = Encoding.UTF8.GetString(ms.ToArray());
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.UserName);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    url = Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            if (password != null && url != null)
                return new ObsSettings() { Url = url, Password = password };
            else
                return null;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _cleanExitRequested = true;
            _cancellationSource.Cancel();
            foreach (var item in _disposables)
            {
                if (item != null)
                    item.Dispose();
            }
            Console.WriteLine("Shutting down...");
        }
    }
}
