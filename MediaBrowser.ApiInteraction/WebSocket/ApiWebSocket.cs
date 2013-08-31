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
        public event EventHandler Closed;

        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly Func<IClientWebSocket> _webSocketFactory;

        private IClientWebSocket _currentWebSocket;

        private Timer _ensureTimer;

        public ApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, Func<IClientWebSocket> webSocketFactory)
            : base(logger, jsonSerializer, serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName)
        {
            _webSocketFactory = webSocketFactory;
        }

        public ApiWebSocket(string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, Func<IClientWebSocket> webSocketFactory)
            : this(new NullLogger(), new NewtonsoftJsonSerializer(), serverHostName, serverWebSocketPort, deviceId, applicationVersion, applicationName, webSocketFactory)
        {
            _webSocketFactory = webSocketFactory;
        }

        private readonly Task _trueTaskResult = Task.Factory.StartNew(() => { });

        public Task EnsureConnection(CancellationToken cancellationToken)
        {
            return IsOpen ? _trueTaskResult : ConnectAsync(cancellationToken);
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

                await socket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);

                Logger.Info("Connected to {0}", url);

                socket.OnReceiveDelegate = OnMessageReceived;

                var idMessage = GetIdentificationMessage(ApplicationName, DeviceId, ApplicationVersion);

                Logger.Info("Sending web socket identification message {0}", idMessage);

                await SendAsync(IdentificationMessageName, idMessage).ConfigureAwait(false);

                socket.Closed += _currentWebSocket_Closed;

                ReplaceSocket(socket);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error connecting to {0}", ex, url);

                throw;
            }
        }

        private void ReplaceSocket(IClientWebSocket socket)
        {
            var previousSocket = _currentWebSocket;

            _currentWebSocket = socket;

            if (previousSocket != null)
            {
                previousSocket.Dispose();
            }
        }

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
        /// Gets a value indicating whether this instance is open.
        /// </summary>
        /// <value><c>true</c> if this instance is open; otherwise, <c>false</c>.</value>
        public bool IsOpen
        {
            get { return _currentWebSocket != null && _currentWebSocket.State == WebSocketState.Open; }
        }

        public void StartEnsureConnectionTimer(int intervalMs)
        {
            StopEnsureConnectionTimer();

            _ensureTimer = new Timer(TimerCallback, null, intervalMs, intervalMs);
        }

        public void StopEnsureConnectionTimer()
        {
            if (_ensureTimer != null)
            {
                _ensureTimer.Dispose();
                _ensureTimer = null;
            }
        }

        private void TimerCallback(object state)
        {
            EnsureConnection(CancellationToken.None);
        }

        public void Dispose()
        {
            StopEnsureConnectionTimer();

            if (_currentWebSocket != null)
            {
                _currentWebSocket.Dispose();
                _currentWebSocket = null;
            }
        }
    }
}
