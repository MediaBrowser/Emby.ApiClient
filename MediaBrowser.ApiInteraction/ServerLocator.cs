using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ServerLocator : IServerLocator
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

        public Task<List<ServerDiscoveryInfo>> FindServers(int timeoutMs, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<List<ServerDiscoveryInfo>>();
            var serversFound = new ConcurrentBag<ServerDiscoveryInfo>();

            _logger.Debug("Searching for servers with timeout of {0} ms", timeoutMs);

            var innerCancellationSource = new CancellationTokenSource();
            var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                innerCancellationSource.Token, cancellationToken);

            BeginFindServer(serversFound, taskCompletionSource, innerCancellationSource);

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(timeoutMs, linkedCancellationSource.Token).ConfigureAwait(false);
                    taskCompletionSource.TrySetResult(serversFound.ToList());
                }
                catch (OperationCanceledException)
                {
                    
                }
            });

            return taskCompletionSource.Task;
        }

        private void BeginFindServer(ConcurrentBag<ServerDiscoveryInfo> serversFound, TaskCompletionSource<List<ServerDiscoveryInfo>> taskCompletionSource, CancellationTokenSource cancellationTokenSource)
        {
            FindServer(serversFound.Add, exception =>
            {
                taskCompletionSource.TrySetException(exception);
                cancellationTokenSource.Cancel();

            }, cancellationTokenSource.Token);
        }

        private async void FindServer(Action<ServerDiscoveryInfo> serverFound, Action<Exception> error, CancellationToken cancellationToken)
        {
            // Create a udp client
            using (var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort())))
            {
                // Construct the message the server is expecting
                var bytes = Encoding.UTF8.GetBytes("who is MediaBrowserServer_v2?");

                // Send it - must be IPAddress.Broadcast, 7359
                var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

                try
                {
                    // Send the broadcast
                    await client.SendAsync(bytes, bytes.Length, targetEndPoint).ConfigureAwait(false);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Get a result back
                        var result = await client.ReceiveAsync().ConfigureAwait(false);

                        if (result.RemoteEndPoint.Port == targetEndPoint.Port)
                        {
                            // Convert bytes to text
                            var json = Encoding.UTF8.GetString(result.Buffer);

                            _logger.Debug("Received response from endpoint: " + result.RemoteEndPoint + ". Response: " + json);

                            if (!string.IsNullOrEmpty(json))
                            {
                                try
                                {
                                    var info = _jsonSerializer.DeserializeFromString<ServerDiscoveryInfo>(json);

                                    info.EndpointAddress = result.RemoteEndPoint.Address.ToString();

                                    serverFound(info);
                                }
                                catch (Exception ex)
                                {
                                    _logger.ErrorException("Error parsing server discovery info", ex);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    error(ex);
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
