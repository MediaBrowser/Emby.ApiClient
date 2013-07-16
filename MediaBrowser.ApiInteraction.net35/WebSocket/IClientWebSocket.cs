using MediaBrowser.Model.Net;
using System;
using System.Threading;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Interface IClientWebSocket
    /// </summary>
    public interface IClientWebSocket : IDisposable
    {
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        WebSocketState State { get; }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        void Connect(string url, Action onSuccess, Action<Exception> onError);

        /// <summary>
        /// Gets or sets the receive action.
        /// </summary>
        /// <value>The receive action.</value>
        Action<byte[]> OnReceiveDelegate { get; set; }

        /// <summary>
        /// Sends the async.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="type">The type.</param>
        /// <param name="endOfMessage">if set to <c>true</c> [end of message].</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        void Send(byte[] bytes, WebSocketMessageType type, bool endOfMessage, Action<Exception> onError);
    }
}
