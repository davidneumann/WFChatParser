using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace DataStream
{
    public class DataSender : IDisposable
    {
        private readonly Uri _websocketHostname;
        private readonly string _messagePrefix;
        private readonly string _debugMessagePrefix;
        private WebSocket _webSocket;

        public DataSender(Uri websocketHostname, IEnumerable<string> connectionMessages, string messagePrefix, string debugMessagePrefix)
        {
            _websocketHostname = websocketHostname;
            _messagePrefix = messagePrefix;
            _debugMessagePrefix = debugMessagePrefix;
            ConnectWebsocket(connectionMessages);
        }

        private void ConnectWebsocket(IEnumerable<string> connectionMessages)
        {
            _webSocket = new WebSocket(_websocketHostname.AbsoluteUri);

            _webSocket.OnMessage += _webSocket_OnMessage;

            _webSocket.Connect();
            foreach (var message in connectionMessages)
            {
                _webSocket.Send(message);
            }
        }

        private void _webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Data.Substring(e.Data.LastIndexOf(":")+1).Trim() == "KILL")
                Environment.Exit(0);
        }

        public void Dispose()
        {
            _webSocket.Close();
            ((IDisposable)_webSocket).Dispose();
        }

        public void SendChatMessage(string message)
        {
            if (_messagePrefix != null && _messagePrefix.Length > 0)
                _webSocket.Send(_messagePrefix + message);
            else
                _webSocket.Send(message);
        }

        public void SendTimers(double imageTime, double parseTime, double transmitTime)
        {
            if (_debugMessagePrefix != null)
                _webSocket.Send(_debugMessagePrefix + $"Image capture: {imageTime:00.00} Parse time: {parseTime:00.00} TransmitTime: {transmitTime:0.000}");
        }

        public void SendDebugMessage(string message)
        {
            if (_debugMessagePrefix != null)
                _webSocket.Send(_debugMessagePrefix + message);
        }
    }
}
