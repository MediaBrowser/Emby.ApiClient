using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public class ApiWebSocket : BaseApiWebSocket
    {
        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly IClientWebSocket _webSocket;

        public ApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, IClientWebSocket webSocket)
            : base(logger, jsonSerializer, serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName)
        {
            _webSocket = webSocket;
        }

        public ApiWebSocket(string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, IClientWebSocket webSocket)
            : base(new NullLogger(), new NewtonsoftJsonSerializer(), serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName)
        {
            _webSocket = webSocket;
        }
        
        public void Connect(string serverHostName, int serverWebSocketPort, Action<Exception> onError)
        {
            var url = GetWebSocketUrl(serverHostName, serverWebSocketPort);

            try
            {
                _webSocket.Connect(url, () => 
                {
                    Logger.Info("Connected to {0}", url);
                    _webSocket.OnReceiveDelegate = OnMessageReceived;
                    Send(IdentificationMessageName, GetIdentificationMessage(), onError);
                }, onError);
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }

        public void Connect(Action<Exception> onError)
        {
            var url = GetWebSocketUrl(ServerHostName, ServerWebSocketPort);
            try
            {
                _webSocket.Connect(url, () => 
                {
                    Logger.Info("Connected to {0}", url);
                    _webSocket.OnReceiveDelegate = OnMessageReceived;
                }, onError);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error connecting to {0}", ex, url);
            }
        }

        public void Send<T>(string messageName, T data)
        {
            Send(messageName, data, ex => Logger.ErrorException("Error sending web socket message", ex));
        }

        public void Send<T>(string messageName, T data, Action<Exception> onError)
        {
            var bytes = GetMessageBytes(messageName, data);
            try
            {
                _webSocket.Send(bytes, Model.Net.WebSocketMessageType.Binary, true, onError);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error sending web socket message", ex);
                onError(ex);
            }
        }

        public void SendContextMessage(string itemType, string itemId, string itemName, string context, Action<Exception> onError)
        {
            var vals = new List<string>
                {
                    itemType ?? string.Empty, 
                    itemId ?? string.Empty, 
                    itemName ?? string.Empty
                };

            if (!string.IsNullOrEmpty(context))
            {
                vals.Add(context);
            }

            Send("Context", string.Join("|", vals.ToArray()), onError);
        }

        public bool IsOpen
        {
            get { return _webSocket != null && _webSocket.State == WebSocketState.Open; }
        }
    }
}
