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
    /// Class ApiWebSocket
    /// </summary>
    public class ApiWebSocket : BaseApiWebSocket
    {
        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly IClientWebSocket _webSocket;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        public ApiWebSocket(IClientWebSocket webSocket, ILogger logger, IJsonSerializer jsonSerializer)
            : base(logger, jsonSerializer)
        {
            _webSocket = webSocket;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        public ApiWebSocket(IClientWebSocket webSocket)
            : this(webSocket, new NullLogger(), new NewtonsoftJsonSerializer())
        {
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(string serverHostName, int serverWebSocketPort, string clientName, string deviceId, string applicationVersion, CancellationToken cancellationToken)
        {
            var url = GetWebSocketUrl(serverHostName, serverWebSocketPort);

            try
            {
                await _webSocket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);

                Logger.Info("Connected to {0}", url);

                _webSocket.OnReceiveDelegate = OnMessageReceived;

                await SendAsync(IdentificationMessageName, GetIdentificationMessage(clientName, deviceId, applicationVersion)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error connecting to {0}", ex, url);
            }
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(string serverHostName, int serverWebSocketPort, CancellationToken cancellationToken)
        {
            var url = GetWebSocketUrl(serverHostName, serverWebSocketPort);

            try
            {
                await _webSocket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);

                Logger.Info("Connected to {0}", url);

                _webSocket.OnReceiveDelegate = OnMessageReceived;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error connecting to {0}", ex, url);
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
                await _webSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
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
            get { return _webSocket != null && _webSocket.State == WebSocketState.Open; }
        }
    }
}
