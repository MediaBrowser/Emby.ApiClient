using System;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    public class WebSocket4NetClientWebSocket : IClientWebSocket
    {
        private WebSocket4Net.WebSocket _socket;

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public Model.Net.WebSocketState State
        {
            get
            {
                switch (_socket.State)
                {
                    case WebSocketState.Closed:
                        return Model.Net.WebSocketState.Closed;
                    case WebSocketState.Closing:
                        return Model.Net.WebSocketState.Closed;
                    case WebSocketState.Connecting:
                        return Model.Net.WebSocketState.Connecting;
                    case WebSocketState.None:
                        return Model.Net.WebSocketState.None;
                    case WebSocketState.Open:
                        return Model.Net.WebSocketState.Open;
                    default:
                        return Model.Net.WebSocketState.None;
                }
            }
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public void Connect(string url, Action onSuccess, Action<Exception> onError)
        {
            ThreadPool.QueueUserWorkItem((socket) =>
            {
                try 
                {
                    _socket = new WebSocket4Net.WebSocket(url);
                    _socket.MessageReceived += websocket_MessageReceived;
                    _socket.Open();
                    onSuccess();
                }
                catch(Exception e) 
                {
                    onError(e);
                }
            });
        }       

        /// <summary>
        /// Handles the MessageReceived event of the websocket control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MessageReceivedEventArgs"/> instance containing the event data.</param>
        void websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (OnReceiveDelegate != null)
            {
                OnReceiveDelegate(Encoding.UTF8.GetBytes(e.Message));
            }
        }

        /// <summary>
        /// Gets or sets the receive action.
        /// </summary>
        /// <value>The receive action.</value>
        public Action<byte[]> OnReceiveDelegate { get; set; }

        public void Send(byte[] bytes, Model.Net.WebSocketMessageType type, bool endOfMessage, Action<Exception> onError)
        {
            ThreadPool.QueueUserWorkItem((socket) =>
            {
                try
                {
                    _socket.Send(bytes, 0, bytes.Length);
                }
                catch (Exception e)
                {
                    onError(e);
                }
            });
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }
    }
}
