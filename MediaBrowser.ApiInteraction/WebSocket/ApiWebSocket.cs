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
    public class ApiWebSocket
    {
        /// <summary>
        /// The _server host name
        /// </summary>
        private readonly string _serverHostName;
        /// <summary>
        /// The _server web socket port
        /// </summary>
        private readonly int _serverWebSocketPort;

        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly IClientWebSocket _webSocket;
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The _json serializer
        /// </summary>
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="webSocket">The web socket.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        public ApiWebSocket(IClientWebSocket webSocket, string serverHostName, int serverWebSocketPort, ILogger logger, IJsonSerializer jsonSerializer)
        {
            _serverHostName = serverHostName;
            _serverWebSocketPort = serverWebSocketPort;
            _webSocket = webSocket;
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var url = string.Format("ws://{0}:{1}/mediabrowser", _serverHostName, _serverWebSocketPort);

            try
            {
                await _webSocket.ConnectAsync("", cancellationToken).ConfigureAwait(false);

                _logger.Info("Connected to {0}", url);
                
                _webSocket.OnReceiveDelegate = OnMessageReceived;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error connecting to {0}", ex, url);
            }
        }

        /// <summary>
        /// Called when [message received].
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        private void OnMessageReceived(byte[] bytes)
        {
        }
    }
}
