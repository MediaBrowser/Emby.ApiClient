using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
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

        public ApiWebSocket(IClientWebSocket webSocket, ILogger logger, IJsonSerializer jsonSerializer)
            : base(logger, jsonSerializer)
        {
            _webSocket = webSocket;
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(string serverHostName, int serverWebSocketPort, string clientName, string deviceId, CancellationToken cancellationToken)
        {
            var url = GetWebSocketUrl(serverHostName, serverWebSocketPort);

            try
            {
                await _webSocket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);

                Logger.Info("Connected to {0}", url);

                _webSocket.OnReceiveDelegate = OnMessageReceived;

                await SendAsync(IdentificationMessageName, GetIdentificationMessage(clientName, deviceId)).ConfigureAwait(false);
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
                await _webSocket.SendAsync(bytes, Model.Net.WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error sending web socket message", ex);

                throw;
            }
        }
    }
}
