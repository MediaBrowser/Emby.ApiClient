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
        /// Queues the event if not null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The handler.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The args.</param>
        protected override void QueueEventIfNotNull<T>(EventHandler<T> handler, object sender, T args)
        {
            if (handler != null)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        handler(sender, args);
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorException("Error in event handler", ex);
                    }
                });
            }
        }
    }
}
