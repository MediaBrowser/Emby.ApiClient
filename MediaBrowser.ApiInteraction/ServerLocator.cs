using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ServerLocator
    {
        private readonly IJsonSerializer _jsonSerializer = new NewtonsoftJsonSerializer();
        private readonly ILogger _logger;

        public ServerLocator()
            : this(new NullLogger())
        {
        }

        public ServerLocator(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ServerDiscoveryInfo> FindServer(CancellationToken cancellationToken)
        {
            return FindServer(2000, cancellationToken);
        }

        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        public Task<ServerDiscoveryInfo> FindServer(int timeout, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<ServerDiscoveryInfo>();

            var timeoutToken = new CancellationTokenSource(timeout).Token;

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);

            linkedTokenSource.Token.Register(() => taskCompletionSource.TrySetCanceled());

            FindServer(taskCompletionSource, timeout);

            return taskCompletionSource.Task;
        }

        private async void FindServer(TaskCompletionSource<ServerDiscoveryInfo> taskCompletionSource, int timeout)
        {
            // Create a udp client
            using (var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort())))
            {
                client.Client.ReceiveTimeout = timeout;

                // Construct the message the server is expecting
                var bytes = Encoding.UTF8.GetBytes("who is MediaBrowserServer_v2?");

                // Send it - must be IPAddress.Broadcast, 7359
                var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

                try
                {
                    // Send the broadcast
                    await client.SendAsync(bytes, bytes.Length, targetEndPoint).ConfigureAwait(false);

                    // Get a result back
                    var result = await client.ReceiveAsync().ConfigureAwait(false);

                    if (result.RemoteEndPoint.Port == targetEndPoint.Port)
                    {
                        // Convert bytes to text
                        var json = Encoding.UTF8.GetString(result.Buffer);

                        if (!string.IsNullOrEmpty(json))
                        {
                            try
                            {
                                var info = _jsonSerializer.DeserializeFromString<ServerDiscoveryInfo>(json);
                                taskCompletionSource.SetResult(info);
                            }
                            catch (Exception ex)
                            {
                                _logger.ErrorException("Error parsing server discovery info", ex);
                            }
                        }
                    }

                    taskCompletionSource.SetException(new ArgumentException("Unexpected response"));
                }
                catch (Exception ex)
                {
                    taskCompletionSource.TrySetException(ex);
                }
            }
        }

        /// <summary>
        /// Gets a random port number that is currently available
        /// </summary>
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
