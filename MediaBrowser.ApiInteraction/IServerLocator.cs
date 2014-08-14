using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.ApiClient;

namespace MediaBrowser.ApiInteraction
{
    public interface IServerLocator
    {
        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        Task<List<ServerDiscoveryInfo>> FindServers(CancellationToken cancellationToken);

        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        Task<List<ServerDiscoveryInfo>> FindServers(int timeout, CancellationToken cancellationToken);
    }
}