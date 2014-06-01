using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Establishes a web socket connection to the server.
    /// When disconnected, the Closed event will fire.
    /// In addition, this also supports a periodic timer to reconnect if needed
    /// </summary>
    public class ApiWebSocket : BaseApiWebSocket, IDisposable
    {
        /// <summary>
        /// Occurs when [closed].
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly Func<IClientWebSocket> _webSocketFactory;

        /// <summary>
        /// The _current web socket
        /// </summary>
        private IClientWebSocket _currentWebSocket;

        /// <summary>
        /// The _ensure timer
        /// </summary>
        private Timer _ensureTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="applicationName">Name of the application.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="webSocketFactory">The web socket factory.</param>
        public ApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, Func<IClientWebSocket> webSocketFactory)
            : base(logger, jsonSerializer, serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName)
        {
            _webSocketFactory = webSocketFactory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="applicationName">Name of the application.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="webSocketFactory">The web socket factory.</param>
        public ApiWebSocket(string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName, Func<IClientWebSocket> webSocketFactory)
            : this(new NullLogger(), new NewtonsoftJsonSerializer(), serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, deviceName, webSocketFactory)
        {
            _webSocketFactory = webSocketFactory;
        }

        /// <summary>
        /// Creates the specified logger.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="client">The client.</param>
        /// <param name="webSocketFactory">The web socket factory.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{ApiWebSocket}.</returns>
        public static async Task<ApiWebSocket> Create(ILogger logger, IJsonSerializer jsonSerializer, ApiClient client, Func<IClientWebSocket> webSocketFactory, CancellationToken cancellationToken)
        {
            var systemInfo = await client.GetSystemInfoAsync(cancellationToken).ConfigureAwait(false);

            var socket = new ApiWebSocket(client.ServerHostName, systemInfo.WebSocketPortNumber, client.DeviceId,
                                          client.ApplicationVersion, client.ClientName, client.DeviceName, webSocketFactory);

            try
            {
                await socket.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();

                throw;
            }

            client.WebSocketConnection = socket;

            return socket;
        }

        /// <summary>
        /// The _true task result
        /// </summary>
        private readonly Task _trueTaskResult = Task.Factory.StartNew(() => { });

        /// <summary>
        /// Ensures the connection.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task EnsureConnectionAsync(CancellationToken cancellationToken)
        {
            return IsConnected ? _trueTaskResult : ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var url = GetWebSocketUrl(ServerHostName, ServerWebSocketPort);

            try
            {
                var socket = _webSocketFactory();

                Logger.Info("Created new web socket of type {0}", socket.GetType().Name);

                await socket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);

                Logger.Info("Connected to {0}", url);

                socket.OnReceiveBytes = OnMessageReceived;
                socket.OnReceive = OnMessageReceived;

                var idMessage = GetIdentificationMessage();

                Logger.Info("Sending web socket identification message {0}", idMessage);

                socket.Closed += _currentWebSocket_Closed;

                ReplaceSocket(socket);

                await SendAsync(IdentificationMessageName, idMessage).ConfigureAwait(false);

                OnConnected();
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error connecting to {0}", ex, url);

                throw;
            }
        }

        /// <summary>
        /// Changes the server location.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="webSocketPort">The web socket port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ChangeServerLocation(string host, int webSocketPort, CancellationToken cancellationToken)
        {
            StopEnsureConnectionTimer();

            DisposeCurrentSocket();

            ServerHostName = host;
            ServerWebSocketPort = webSocketPort;

            return ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Replaces the socket.
        /// </summary>
        /// <param name="socket">The socket.</param>
        private void ReplaceSocket(IClientWebSocket socket)
        {
            var previousSocket = _currentWebSocket;

            _currentWebSocket = socket;

            if (previousSocket != null)
            {
                previousSocket.Dispose();
            }
        }

        /// <summary>
        /// Handles the Closed event of the _currentWebSocket control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void _currentWebSocket_Closed(object sender, EventArgs e)
        {
            Logger.Warn("Web socket connection closed.");

            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sends the async.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <returns>Task.</returns>
        public Task SendAsync<T>(string messageName, T data)
        {
            return SendAsync(messageName, data, CancellationToken.None);
        }

        /// <summary>
        /// Sends the async.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SendAsync<T>(string messageName, T data, CancellationToken cancellationToken)
        {
            var bytes = GetMessageBytes(messageName, data);

            try
            {
                await _currentWebSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error sending web socket message", ex);

                throw;
            }
        }

        /// <summary>
        /// Sends the server a message indicating what is currently being viewed by the client
        /// </summary>
        /// <param name="itemType">The current item type (if any)</param>
        /// <param name="itemId">The current item id (if any)</param>
        /// <param name="itemName">The current item name (if any)</param>
        /// <param name="context">An optional, client-specific value indicating the area or section being browsed</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task SendContextMessageAsync(string itemType, string itemId, string itemName, string context, CancellationToken cancellationToken)
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

            return SendAsync("Context", string.Join("|", vals.ToArray()), cancellationToken);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
        public override bool IsConnected
        {
            get { return _currentWebSocket != null && _currentWebSocket.State == WebSocketState.Open; }
        }

        /// <summary>
        /// Starts the ensure connection timer.
        /// </summary>
        /// <param name="intervalMs">The interval ms.</param>
        public void StartEnsureConnectionTimer(int intervalMs)
        {
            StopEnsureConnectionTimer();

            _ensureTimer = new Timer(TimerCallback, null, intervalMs, intervalMs);
        }

        /// <summary>
        /// Stops the ensure connection timer.
        /// </summary>
        public void StopEnsureConnectionTimer()
        {
            if (_ensureTimer != null)
            {
                Logger.Debug("Stopping web socket timer");

                _ensureTimer.Dispose();
                _ensureTimer = null;
            }
        }

        /// <summary>
        /// Starts the receiving session updates.
        /// </summary>
        /// <param name="intervalMs">The interval ms.</param>
        /// <returns>Task.</returns>
        public Task StartReceivingSessionUpdates(int intervalMs)
        {
            return SendAsync("SessionsStart", string.Format("{0},{0}", intervalMs));
        }

        /// <summary>
        /// Stops the receiving session updates.
        /// </summary>
        /// <returns>Task.</returns>
        public Task StopReceivingSessionUpdates()
        {
            return SendAsync("SessionsStop", string.Empty);
        }

        /// <summary>
        /// Timers the callback.
        /// </summary>
        /// <param name="state">The state.</param>
        private void TimerCallback(object state)
        {
            EnsureConnectionAsync(CancellationToken.None);
        }

        private void DisposeCurrentSocket()
        {
            var socket = _currentWebSocket;

            if (socket != null)
            {
                Logger.Debug("Disposing client web socket");

                try
                {
                    socket.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error disposing web socket {0}", ex);
                }
                _currentWebSocket = null;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            StopEnsureConnectionTimer();

            DisposeCurrentSocket();
        }
    }
}
