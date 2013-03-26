using Alchemy;
using Alchemy.Classes;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class AlchemyClientWebSocket
    /// </summary>
    public class AlchemyClientWebSocket : IClientWebSocket
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// The _client
        /// </summary>
        private WebSocketClient _client;

        /// <summary>
        /// Gets or sets the receive action.
        /// </summary>
        /// <value>The receive action.</value>
        public Action<byte[]> OnReceiveDelegate { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlchemyClientWebSocket" /> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        public AlchemyClientWebSocket(ILogManager logManager)
        {
            _logger = logManager.GetLogger("AlchemyClientWebSocket");
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ConnectAsync(string url, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _client = new WebSocketClient(url)
                {
                    OnReceive = OnReceive
                };

                try
                {
                    _client.Connect();
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error connecting to {0}", ex, url);

                    throw;
                }
            });
        }

        /// <summary>
        /// Called when [receive].
        /// </summary>
        /// <param name="context">The context.</param>
        private void OnReceive(UserContext context)
        {
            if (OnReceiveDelegate != null)
            {
                var json = context.DataFrame.ToString();

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(json);

                        OnReceiveDelegate(bytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("Error processing web socket message", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Sends the async.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="type">The type.</param>
        /// <param name="endOfMessage">if set to <c>true</c> [end of message].</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task SendAsync(byte[] bytes, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Task.Run(() => _client.Send(bytes));
        }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public WebSocketState State
        {
            get { 
            
                if (_client == null)
                {
                    return WebSocketState.None;
                }

                switch (_client.ReadyState)
                {
                    case WebSocketClient.ReadyStates.CLOSED:
                        return WebSocketState.Closed;
                    case WebSocketClient.ReadyStates.CLOSING:
                        return WebSocketState.Closed;
                    case WebSocketClient.ReadyStates.CONNECTING:
                        return WebSocketState.Connecting;
                    case WebSocketClient.ReadyStates.OPEN:
                        return WebSocketState.Open;
                    default:
                        return WebSocketState.None;
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _client.Disconnect();
            }
        }
    }
}
