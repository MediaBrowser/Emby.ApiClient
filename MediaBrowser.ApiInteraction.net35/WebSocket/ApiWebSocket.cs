using MediaBrowser.ApiInteraction.net35;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public class ApiWebSocket : BaseApiWebSocket, IDisposable
    {
        /// <summary>
        /// The web socket
        /// </summary>
        private IClientWebSocket _socket;

        /// <summary>
        /// The _ensure timer
        /// </summary>
        private Timer _ensureTimer;

        /// <summary>
        /// Occurs when socket is connected.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Occurs when socket is disconnected.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="applicationName">Name of the application.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="socket">The socket.</param>
        public ApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, IClientWebSocket socket)
            : base(logger, jsonSerializer, serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName)
        {
            _socket = socket;
            RetryInterval = 15000;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket"/> class.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="applicationName">Name of the application.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="socket">The socket.</param>
        public ApiWebSocket(string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, IClientWebSocket socket)
            : this(new NullLogger(), new NewtonsoftJsonSerializer(), serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName, socket)
        {

        }

        /// <summary>
        /// Gets or sets the retry interval.
        /// </summary>
        /// <value>
        /// The interval.
        /// </value>
        public int RetryInterval { get; set; }

        public override bool IsConnected
        {
            get { return _socket != null && _socket.State == WebSocketState.Open; }
        }

        /// <summary>
        /// Connects the websocket.
        /// </summary>
        /// <param name="ensureConnection">if set to <c>true</c> when the connection is lost it will automatically retry.</param>
        public void Connect(bool ensureConnection = false)
        {
            var url = GetWebSocketUrl(ServerHostName, ServerWebSocketPort);

            _socket.Connect(url, () =>
            {
                Logger.Info("Connected to {0}", url);
                _socket.OnReceiveDelegate = OnMessageReceived;
                Send(IdentificationMessageName, GetIdentificationMessage());
                if (Connected != null)
                    // Signal that the socket is connected
                    Connected.Invoke(this, EventArgs.Empty);
            }
            , e =>
            {
                if (Disconnected != null)
                    // Signal that the socket is disconnected
                    Disconnected.Invoke(this, EventArgs.Empty);
                if (ensureConnection)
                    // if ensure connection is true start a time to try and reconnect
                    StartEnsureConnectionTimer(RetryInterval);
            });
        }

        /// <summary>
        /// Sends the specified message name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        public void Send<T>(string messageName, T data)
        {
            Send(messageName, data, ex => Logger.ErrorException("Error sending web socket message", ex));
        }

        /// <summary>
        /// Sends the specified message name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <param name="onError">The on error.</param>
        public void Send<T>(string messageName, T data, Action<Exception> onError)
        {
            var bytes = GetMessageBytes(messageName, data);
            try
            {
                _socket.Send(bytes, Model.Net.WebSocketMessageType.Binary, true, onError);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error sending web socket message", ex);
                onError(ex);
            }
        }

        /// <summary>
        /// Sends the context message.
        /// </summary>
        /// <param name="itemType">Type of the item.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="itemName">Name of the item.</param>
        /// <param name="context">The context.</param>
        /// <param name="onError">The on error.</param>
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

        /// <summary>
        /// Starts the receiving session updates.
        /// </summary>
        /// <param name="intervalMs">The interval ms.</param>
        /// <param name="onError">The on error.</param>
        public void StartReceivingSessionUpdates(int intervalMs, Action<Exception> onError)
        {
            Send("SessionsStart", string.Format("{0},{0}", intervalMs), onError);
        }

        /// <summary>
        /// Stops the receiving session updates.
        /// </summary>
        /// <param name="onError">The on error.</param>
        public void StopReceivingSessionUpdates(Action<Exception> onError)
        {
            Send("SessionsStop", string.Empty, onError);
        }

        /// <summary>
        /// Ensures the connection.
        /// </summary>
        /// <param name="state">The state.</param>
        protected void EnsureConnection(object state)
        {
            // if the socket is already open do nothing
            if (IsConnected) return;

            // try to connect
            Connect(true);
        }

        /// <summary>
        /// Starts the ensure connection timer.
        /// </summary>
        /// <param name="intervalMs">The interval ms.</param>
        protected void StartEnsureConnectionTimer(int intervalMs)
        {
            StopEnsureConnectionTimer();

            _ensureTimer = new Timer(EnsureConnection, null, intervalMs, Timeout.Infinite);
        }

        /// <summary>
        /// Stops the ensure connection timer.
        /// </summary>
        protected void StopEnsureConnectionTimer()
        {
            if (_ensureTimer == null) return;

            Logger.Debug("Stopping web socket timer");

            _ensureTimer.Dispose();
            _ensureTimer = null;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_socket == null) return;

            Logger.Debug("Disposing client web socket");

            try
            {
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error disposing web socket {0}", ex);
            }

            _socket = null;
        }

        /// <summary>
        /// Creates a new websocket connection for the specified client.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public static void Create(ApiClient client, Action<ApiWebSocket> onSuccess, Action<Exception> onError)
        {
            client.GetSystemInfo(info =>
            {
                try
                {
                    var socket = new ApiWebSocket(client.ServerHostName,
                        info.WebSocketPortNumber,
                        client.DeviceId,
                        client.ApplicationVersion,
                        client.ClientName,
                        client.DeviceName,
                        new WebSocket4NetClientWebSocket());

                    client.WebSocketConnection = socket;
                    onSuccess(socket);
                    socket.Connect(true);
                }
                catch (Exception e)
                {
                    onError(e);
                }
            }, onError);
        }
    }
}
