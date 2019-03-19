﻿using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace DataStream
{
    public class DataSender : IDisposable
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
        public DataSender(Uri websocketHostname, IEnumerable<string> connectionMessages, string messagePrefix, string debugMessagePrefix, bool shouldReconnect)
        {
            _websocketHostname = websocketHostname;
            _messagePrefix = messagePrefix;
            _debugMessagePrefix = debugMessagePrefix;
            _shouldReconnect = shouldReconnect;
            _connectionStrings = connectionMessages;

            InitWebsocket();

            if (_shouldReconnect)
                _webSocket.OnClose += _webSocket_OnClose;

            ConnectWebsocket();
        }

        private void InitWebsocket()
        {
            if (_webSocket != null)
            {
                _webSocket.Close();
                ((IDisposable)_webSocket).Dispose();
            }
            _webSocket = new WebSocket(_websocketHostname.AbsoluteUri);
            _webSocket.OnMessage += _webSocket_OnMessage;
            _webSocket.OnOpen += _webSocket_OnOpen;
        }

        private void _webSocket_OnOpen(object sender, EventArgs e)
        {
            foreach (var message in _connectionStrings)
            {
                _webSocket.Send(message);
            }
        }

        private void ConnectWebsocket()
        {
            if (DateTimeOffset.Now.Subtract(_lastReconnectTime).TotalSeconds < 5)
                return;
            _lastReconnectTime = DateTimeOffset.Now;

            _webSocket.Connect();
        }

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            if (_shouldReconnect)
            {
                InitWebsocket();
                ConnectWebsocket();
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

        public void SendChatMessage(string message)
        {
            if (!_webSocket.IsAlive && _shouldReconnect)
                ConnectWebsocket();
            if (_messagePrefix != null && _messagePrefix.Length > 0)
                _webSocket.Send(_messagePrefix + message);
            else
                _webSocket.Send(message);
        }

        public void SendTimers(double imageTime, double parseTime, double transmitTime, int newMessageCount)
        {
            if (!_webSocket.IsAlive && _shouldReconnect)
                ConnectWebsocket();
            if (_debugMessagePrefix != null)
                _webSocket.Send(_debugMessagePrefix + $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000} New messages {newMessageCount} {newMessageCount / parseTime}/s");
        }

        public void SendDebugMessage(string message)
        {
            if (!_webSocket.IsAlive && _shouldReconnect)
                ConnectWebsocket();
            if (_debugMessagePrefix != null)
                _webSocket.Send(_debugMessagePrefix + message);
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
