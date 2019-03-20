using Application.ChatMessages.Model;
using Application.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using WebSocketSharp;

namespace DataStream
{
    public class DataSender : IDisposable, IDataSender
    {
        private readonly Uri _websocketHostname;
        private readonly string _messagePrefix;
        private readonly string _debugMessagePrefix;
        private readonly IEnumerable<string> _connectionStrings;
        private WebSocket _webSocket;

        public event EventHandler RequestToKill;
        public event EventHandler<SaveEventArgs> RequestSaveAll;

        private bool _shouldReconnect;

        private DateTimeOffset _lastReconnectTime = DateTimeOffset.MinValue;
        public DataSender(Uri websocketHostname, IEnumerable<string> connectionMessages, string messagePrefix, string debugMessagePrefix, bool shouldReconnect, string rawMessagePrefix)
        {
            _websocketHostname = websocketHostname;
            _messagePrefix = messagePrefix;
            _debugMessagePrefix = debugMessagePrefix;
            _shouldReconnect = shouldReconnect;
            _connectionStrings = connectionMessages;
            _rawMessagePrefix = rawMessagePrefix;

            InitWebsocket();

            //ConnectWebsocket();
        }

        private void InitWebsocket()
        {
            if (DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds < 5)
                return;
            _lastReconnectTime = DateTimeOffset.Now;
            if (_webSocket != null)
            {
                if (_webSocket.ReadyState != WebSocketState.Closed)
                {
                    _webSocket.Close();
                    ((IDisposable)_webSocket).Dispose();
                }
            }
            _webSocket = new WebSocket(_websocketHostname.AbsoluteUri);
            _webSocket.OnMessage += _webSocket_OnMessage;
            _webSocket.OnOpen += _webSocket_OnOpen;
            if (_shouldReconnect)
                _webSocket.OnClose += _webSocket_OnClose;
            _webSocket.Connect();
        }

        private void _webSocket_OnOpen(object sender, EventArgs e)
        {
            foreach (var message in _connectionStrings)
            {
                _webSocket.Send(message);
            }
        }

        //private void ConnectWebsocket()
        //{
        //    if (DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds < 5 || _webSocket.ReadyState == WebSocketState.Open)
        //        return;
        //    if(_webSocket.ReadyState != WebSocketState.Open && DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds >= 5)
        //    { 
        //        InitWebsocket();
        //    }
        //    _lastReconnectTime = DateTimeOffset.Now;

        //    _webSocket.Connect();
        //}

        private BackgroundWorker _reconnectWorker = new BackgroundWorker();
        private string _rawMessagePrefix;
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new ChatMessageModelResolver() };

        private void Reconnect()
        {
            if(!_reconnectWorker.IsBusy && (_webSocket == null || _webSocket.ReadyState == WebSocketState.Closed || _webSocket.ReadyState == WebSocketState.Closing))
            {
                _reconnectWorker.Dispose();
                _reconnectWorker = new BackgroundWorker();
                _reconnectWorker.DoWork += (o, e2) => {
                    if (DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds < 5)
                        System.Threading.Thread.Sleep((int)(5 - DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds) * 1000);
                    if (_webSocket == null || (_webSocket.ReadyState != WebSocketState.Connecting && _webSocket.ReadyState != WebSocketState.Open))
                        InitWebsocket();
                };
                _reconnectWorker.RunWorkerAsync();
            }
        }

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            if (_shouldReconnect)
            {
                Reconnect();
            }
        }

        private void _webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Data.Substring(e.Data.LastIndexOf(":") + 1).Trim() == "KILL")
            {
                RequestToKill?.Invoke(this, EventArgs.Empty);
            }
            else if(e.Data.Substring(e.Data.LastIndexOf(":") + 1).Trim().StartsWith("SAVE"))
            {
                var name = e.Data.Substring(e.Data.IndexOf(":SAVE") + 5).Trim();
                RequestSaveAll?.Invoke(this, new SaveEventArgs(name));
            }
        }

        public void Dispose()
        {
            _shouldReconnect = false;
            _webSocket.Close();
            ((IDisposable)_webSocket).Dispose();
        }

        //public void SendChatMessage(string message)
        //{
        //    if (_webSocket.ReadyState == WebSocketState.Open)
        //    {
        //        if (_messagePrefix != null && _messagePrefix.Length > 0)
        //            _webSocket.Send(_messagePrefix + message);
        //        else
        //            _webSocket.Send(message);
        //    }
        //    else if (_shouldReconnect)
        //        Reconnect();
        //}

        //public void SendTimers(double imageTime, double parseTime, double transmitTime, int newMessageCount)
        //{
        //    if (_debugMessagePrefix != null && _webSocket.ReadyState == WebSocketState.Open)
        //        _webSocket.Send(_debugMessagePrefix + $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000} New messages {newMessageCount} {newMessageCount / parseTime}/s");
        //    else if (_shouldReconnect)
        //        Reconnect();
        //}

        //public void SendDebugMessage(string message)
        //{
        //    if (_debugMessagePrefix != null && _webSocket.ReadyState == WebSocketState.Open)
        //        _webSocket.Send(_debugMessagePrefix + message);
        //    else if (_shouldReconnect)
        //        Reconnect();
        //}

        public async Task AsyncSendChatMessage(ChatMessageModel message)
        {
            if (_webSocket.ReadyState == WebSocketState.Open)
            {
                if (_messagePrefix != null && _messagePrefix.Length > 0)
                    _webSocket.Send(_messagePrefix + JsonConvert.SerializeObject(message, Formatting.Indented, _jsonSettings));
                else
                    _webSocket.Send(JsonConvert.SerializeObject(message, Formatting.Indented, _jsonSettings));
                if (_rawMessagePrefix != null && _rawMessagePrefix.Length > 0)
                    _webSocket.Send(_rawMessagePrefix + message.Raw);
            }
            else if (_shouldReconnect)
                Reconnect();
        }

        public async Task AsyncSendDebugMessage(string message)
        {
            if (_debugMessagePrefix != null && _webSocket.ReadyState == WebSocketState.Open)
                _webSocket.Send(_debugMessagePrefix + message);
            else if (_shouldReconnect)
                Reconnect();
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
