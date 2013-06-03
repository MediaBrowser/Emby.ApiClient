using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MediaBrowser.ApiInteraction
{
    public class ServerLocator
    {
        const string broadcastDiscoverMessage = "who is MediaBrowserServer?";
        
        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        public void FindServer(Action<IPEndPoint> onSuccess)
        {
            // Create a udp client
            var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort()));

            // Construct the message the server is expecting
            var bytes = Encoding.UTF8.GetBytes(broadcastDiscoverMessage);

            // Send it - must be IPAddress.Broadcast, 7359
            var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

            // Send it
            client.BeginSend(bytes, bytes.Length, targetEndPoint, iac => {
                    
                //client.EndReceive
                    client.BeginReceive(iar =>
                    {
                        IPEndPoint remoteEndPoint= null;
                        var result = client.EndReceive(iar, ref remoteEndPoint);
                        if (remoteEndPoint.Port == targetEndPoint.Port)
                        {
                            // Convert bytes to text
                            var text = Encoding.UTF8.GetString(result);

                            // Expected response : MediaBrowserServer|192.168.1.1:1234
                            // If the response is what we're expecting, proceed
                            if (text.StartsWith("mediabrowserserver", StringComparison.OrdinalIgnoreCase))
                            {
                                text = text.Split('|')[1];
                                var vals = text.Split(':');
                                onSuccess(new IPEndPoint(IPAddress.Parse(vals[0]), int.Parse(vals[1])));
                            }
                        }
                    }, client);
                }, client);
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
