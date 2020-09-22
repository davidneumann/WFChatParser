using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Actionables.ChatBots;
using Application.Actionables.ProfileBots.Models;
using Application.ChatMessages.Model;
using Application.Interfaces;
using Application.Logger;
using Application.LogParser;
using Application.Models;
using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace DataStream
{
    public class ClientWebsocketDataSender : IDisposable, IDataTxRx
    {
        private ClientWebSocket _websocket;
        private readonly Uri _uri;
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ICollection<string> _connectMessages;
        private bool _doneConnecting;

        private readonly JsonSerializerSettings _jsonSettings;
        private readonly string _messagePrefix;
        private readonly string _debugMessagePrefix;
        private readonly bool _shouldReconnect;
        private readonly string _rawMessagePrefix;
        private readonly string _redtextMessagePrefix;
        private readonly string _rivenImageMessagePrefix;
        private readonly string _logMessagePrefix;
        private readonly string _logLineMessagePrefix;

        public event Action OnConnected;
        public event Action<string> OnReceive;

        public event EventHandler RequestToKill;
        public event EventHandler<SaveEventArgs> RequestSaveAll;
        public event EventHandler<ProfileRequest> ProfileParseRequest;

        public ILogger _logger;

        public ClientWebsocketDataSender(Uri uri, IEnumerable<string> connectMessages,
            string messagePrefix,
            string debugMessagePrefix,
            bool shouldReconnect,
            string rawMessagePrefix,
            string redtextMessagePrefix,
            string rivenImageMessagePrefix,
            string logMessagePrefix,
            string logLineMessagePrefix)
        {
            _uri = uri;
            _connectMessages = connectMessages.ToList();

            _messagePrefix = messagePrefix;
            _debugMessagePrefix = debugMessagePrefix;
            _shouldReconnect = shouldReconnect;
            _rawMessagePrefix = rawMessagePrefix;
            _redtextMessagePrefix = redtextMessagePrefix;
            _rivenImageMessagePrefix = rivenImageMessagePrefix;
            _logMessagePrefix = logMessagePrefix;
            _logLineMessagePrefix = logLineMessagePrefix;

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CompactDataSenderResolver(),
                Converters =
                {
                    new StringEnumConverter { AllowIntegerValues = false, NamingStrategy = new CamelCaseNamingStrategy() }
                }
            };

            OnReceive += Receive;
        }

        public async Task ConnectAsync()
        {
            if (_logger != null)
                _logger.Log($"Connecting to {_uri}", writeToConsole: false);

            // TODO Use cancellation tokens?
            await ActualConnect();
            _ = Task.Run(ReceiveData);
            _ = Task.Run(SendQueue);
        }

        public void Send(string message)
        {
            _sendQueue.Enqueue(message);
        }

        public Task CloseAsync()
        {
            _cancellationTokenSource.Cancel();
            return _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }

        private async Task SendAsync(string message)
        {
            var bytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(message));
            var length = Encoding.UTF8.GetBytes(message, 0, message.Length, bytes, 0);

            try
            {
                await _websocket.SendAsync(new ArraySegment<byte>(bytes, 0, length), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private async Task SendQueue()
        {
            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                if (_sendQueue.TryDequeue(out var message))
                {
                    // Wait for websocket to become available
                    while (!_doneConnecting || _websocket.State != WebSocketState.Open)
                    {
                        if (token.IsCancellationRequested)
                            return;
                        await Task.Delay(25, token);
                    }

                    try
                    {
                        await SendAsync(message);
                    }
                    catch (Exception e)
                    {
                        // TODO Handle this
                    }
                }
                else
                {
                    await Task.Delay(25, token);
                }
            }
        }

        private async Task ActualConnect()
        {
            _doneConnecting = false;

            do
            {
                _websocket?.Dispose();
                _websocket = new ClientWebSocket();
                _websocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                // This method continuously attempts to connect if a connection could not be established.
                try
                {
                    await _websocket.ConnectAsync(_uri, _cancellationTokenSource.Token);

                    // Send connection messages
                    foreach (var message in _connectMessages)
                    {
                        await SendAsync(message);
                    }
                }
                catch (Exception e)
                {
                    if(_logger != null)
                    {
                        _logger.Log($"{e}", writeToConsole: false);
                    }
                    // TODO What to do here?
                }

                if (_websocket.State != WebSocketState.Open)
                    await Task.Delay(1000);
            } while (_websocket.State != WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested);

            _doneConnecting = true;
            OnConnected?.Invoke();
        }

        private async Task ReceiveData()
        {
            var token = _cancellationTokenSource.Token;
            var buffer = new byte[1024];

            while (!token.IsCancellationRequested)
            {
                // Check if a reconnect is needed
                if (_websocket.State != WebSocketState.Open)
                {
                    if (!_shouldReconnect)
                        return;
                    // Begin reconnect
                    await Task.Delay(5000, token);
                    await ActualConnect();
                }

                _stringBuilder.Clear();
                try
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        _stringBuilder.Append(Encoding.ASCII.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    OnReceive?.Invoke(_stringBuilder.ToString());
                }
                catch (Exception)
                {
                    // TODO
                }
            }
        }

        public void Dispose()
        {
            _websocket?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        public Task AsyncSendChatMessage(ChatMessageModel message)
        {
            if (!string.IsNullOrEmpty(_messagePrefix))
                Send(_messagePrefix + JsonConvert.SerializeObject(message, Formatting.None, _jsonSettings));
            else
                Send(JsonConvert.SerializeObject(message, Formatting.None, _jsonSettings));
            if (!string.IsNullOrEmpty(_rawMessagePrefix))
                Send(_rawMessagePrefix + message.Raw);

            if (_logger != null)
                _logger.Log(JsonConvert.SerializeObject(message, Formatting.None, _jsonSettings));

            return Task.CompletedTask;
        }

        public Task AsyncSendDebugMessage(string message)
        {
            if (_debugMessagePrefix != null)
                Send(_debugMessagePrefix + message);
            return Task.CompletedTask;
        }

        public Task AsyncSendRedtext(RedTextMessage message)
        {
            if (_redtextMessagePrefix != null)
                Send(_redtextMessagePrefix + JsonConvert.SerializeObject(message, _jsonSettings));
            return Task.CompletedTask;
        }

        public Task AsyncSendRivenImage(Guid imageId, Bitmap bitmap)
        {
            try
            {
                bitmap.Save("riven.jpg");
            }
            catch { }
            var memImage = new MemoryStream();
            bitmap.Save(memImage, System.Drawing.Imaging.ImageFormat.Jpeg);
            var rivenBase64 = Convert.ToBase64String(memImage.ToArray());
            memImage.Dispose();
            bitmap.Dispose();
            return AsyncSendRivenImage(imageId, rivenBase64);
        }

        public Task AsyncSendRivenImage(Guid imageId, string rivenBase64)
        {
            if (_rivenImageMessagePrefix != null)
            {
                Send(_rivenImageMessagePrefix + JsonConvert.SerializeObject(new { ImageId = imageId, Image = rivenBase64 }, _jsonSettings));
            }
            return Task.CompletedTask;
        }

        public Task AsyncSendLogMessage(string message)
        {
            if (_logMessagePrefix != null)
                Send($"{_logMessagePrefix} [{DateTime.Now:HH:mm:ss.f}] {message}");
            return Task.CompletedTask;
        }

        private void Receive(string message)
        {
            if (_logger != null)
                _logger.Log($"Datasender received: {message}");

            var split = message.Split(new string[] { " :" }, StringSplitOptions.RemoveEmptyEntries); //:5f5196d9fdd47d3d8df5fede GET RIVENBOT9000 TEST :KILL
            var data = split[0].Split(' ');
            var sender = data[0]; //:5f5196d9fdd47d3d8df5fede
            if (sender.StartsWith(":")) //Only trim 1 ;
                sender = sender.Substring(1);
            var code = data[1]; //GET
            var command = data[3];
            var payload = string.Empty;
            if (split.Length > 1)
                payload = split[1];

            //We only care about GETS
            if (code != "GET")
                return;

            switch (command)
            {
                case "KILL":
                    try { AsyncSendDebugMessage("Kill acknowledged. Requesting a stop.").Wait(); }
                    catch { }
                    RequestToKill?.Invoke(this, EventArgs.Empty);
                    break;
                case "RESTART":
                    try 
                    {
                        AsyncSendDebugMessage("Attempting to restart computer").Wait();
                        var shutdown = new System.Diagnostics.Process()
                        {
                            StartInfo = new ProcessStartInfo("shutdown.exe", "/r /f /t 0")
                        };
                        shutdown.Start();
                    }
                    catch
                    {
                        AsyncSendDebugMessage("FAILED TO RESTART COMPUTER!").Wait();
                    }
                    break;
                case "SAVE":
                    var name = payload;
                    RequestSaveAll?.Invoke(this, new SaveEventArgs(name));
                    break;
                case "BADNAME":
                    var username = message.Substring(message.IndexOf("BADNAME") + 7).Trim();
                    try
                    {
                        lock (TradeChatBot._debugBadNames)
                        {
                            var orig = TradeChatBot._debugBadNames.Count;
                            TradeChatBot._debugBadNames.Add(username.ToLower());
                            File.WriteAllLines(TradeChatBot._debugBadNamesFilename, TradeChatBot._debugBadNames);
                            AsyncSendDebugMessage($"Added {username} to bad names. Old count {orig}. New count {TradeChatBot._debugBadNames.Count}");
                        }
                    }
                    catch (Exception e)
                    {
                        if (_logger != null)
                        {
                            Console.WriteLine(e.ToString());
                            _logger.Log(e.ToString());
                            AsyncSendDebugMessage(e.ToString());
                        }
                    }
                    break;
                case "BASIC":
                case "FULL":
                    if (string.IsNullOrEmpty(payload) || payload.Trim().Length <= 0)
                        break;
                    if(_logger != null)
                    {
                        _logger.Log($"Adding {payload} to profile queue");
                    }
                    else
                        Console.WriteLine($"Adding {payload} to profile queue");
                    if (ProfileParseRequest != null)
                    {
                        ProfileParseRequest?.Invoke(this, new ProfileRequest(payload, sender, command));
                    }
                    else
                    {
                        Send($":PROFILEBOT POST {sender} USERQUEUED :[{payload}, -1]");
                        Send($":BROADCAST PROFILEBOT USERQUEUED :[{payload}, -1]");
                    }
                    break;
                default:
                    break;
            }
        }

        public Task AsyncSendLogLine(LogMessage message)
        {
            if (_logLineMessagePrefix != null)
                Send(_logLineMessagePrefix + JsonConvert.SerializeObject(message, _jsonSettings));
            return Task.CompletedTask;
        }

        public Task AsyncSendProfileData(Profile profile, string target, string command)
        {
            Send($":BROADCAST PROFILEBOT {command} :{JsonConvert.SerializeObject(profile, _jsonSettings)}");
            Send($":PROFILEBOT POST {target} {command} :{JsonConvert.SerializeObject(profile, _jsonSettings)}");
            return Task.CompletedTask;
        }

        public Task AsyncSendProfileRequestAck(ProfileRequest request, int queueSize)
        {
            Send($":PROFILEBOT POST {request.Target} USERQUEUED :[{request.Username}, {queueSize}]");
            Send($":BROADCAST PROFILEBOT USERQUEUED :[{request.Username}, {queueSize}]");
            return Task.CompletedTask;
        }
    }
    public class SaveEventArgs : EventArgs
    {
        public string Name { get; internal set; }

        public SaveEventArgs(string name)
        {
            this.Name = name;
        }
    }
}