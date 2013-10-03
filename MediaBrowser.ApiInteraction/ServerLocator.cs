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
        public Task<IPEndPoint> FindServer(CancellationToken cancellationToken)
        {
            return FindServer(2000, cancellationToken);
        }

        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        public Task<IPEndPoint> FindServer(int timeout, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<IPEndPoint>();

            var timeoutToken = new CancellationTokenSource(timeout).Token;

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);

            linkedTokenSource.Token.Register(() => taskCompletionSource.TrySetCanceled());

            FindServer(taskCompletionSource, timeout);

            return taskCompletionSource.Task;
        }

        private async void FindServer(TaskCompletionSource<IPEndPoint> taskCompletionSource, int timeout)
        {
            // Create a udp client
            using (var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort())))
            {
                client.Client.ReceiveTimeout = timeout;

                // Construct the message the server is expecting
                var bytes = Encoding.UTF8.GetBytes("who is MediaBrowserServer?");

                // Send it - must be IPAddress.Broadcast, 7359
                var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

                // Send the broadcast
                await client.SendAsync(bytes, bytes.Length, targetEndPoint).ConfigureAwait(false);

                try
                {
                    // Get a result back
                    var result = await client.ReceiveAsync().ConfigureAwait(false);

                    if (result.RemoteEndPoint.Port == targetEndPoint.Port)
                    {
                        // Convert bytes to text
                        var text = Encoding.UTF8.GetString(result.Buffer);

                        // Expected response : MediaBrowserServer|192.168.1.1:1234
                        // If the response is what we're expecting, proceed
                        if (text.StartsWith("mediabrowserserver", StringComparison.OrdinalIgnoreCase))
                        {
                            text = text.Split('|')[1];

                            var vals = text.Split(':');

                            var endpoint = new IPEndPoint(IPAddress.Parse(vals[0]), int.Parse(vals[1]));

                            taskCompletionSource.SetResult(endpoint);
                            return;
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
