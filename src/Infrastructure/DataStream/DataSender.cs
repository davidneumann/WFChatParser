using System;
using WebSocketSharp;

namespace DataStream
{
    public class DataSender : IDisposable
    {
        private readonly Uri _websocketHostname;
        private readonly string _messagePrefix;
        private readonly string _connectionMessage;
        private WebSocket _webSocket;

        public DataSender(Uri websocketHostname, string connectionMessage, string messagePrefix)
        {
            _websocketHostname = websocketHostname;
            _messagePrefix = messagePrefix;
            _connectionMessage = connectionMessage;
            ConnectWebsocket();
        }

        private void ConnectWebsocket()
        {
            _webSocket = new WebSocket(_websocketHostname.AbsoluteUri);

            _webSocket.Connect();
            if (_connectionMessage != null && _connectionMessage.Length > 0)
                _webSocket.Send(_connectionMessage);
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
    }
}
