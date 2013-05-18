using MediaBrowser.Model.Logging;
using System;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ClientWebSocketFactory
    /// </summary>
    public static class ClientWebSocketFactory
    {
        /// <summary>
        /// Creates the web socket.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <returns>IClientWebSocket.</returns>
        public static IClientWebSocket CreateWebSocket(ILogger logger)
        {
            try
            {
                // This is preferred but only supported on windows 8/2012
                return new NativeClientWebSocket(logger);
            }
            catch (NotSupportedException)
            {
                return new WebSocket4NetClientWebSocket();
            }
        }
    }
}
