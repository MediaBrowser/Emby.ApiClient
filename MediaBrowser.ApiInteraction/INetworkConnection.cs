using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public interface INetworkConnection
    {
        /// <summary>
        /// Sends the wake on lan.
        /// </summary>
        /// <param name="macAddress">The mac address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task SendWakeOnLan(string macAddress, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the network status.
        /// </summary>
        /// <returns>NetworkStatus.</returns>
        NetworkStatus GetNetworkStatus();
    }

    public class NetworkStatus
    {
        public bool IsNetworkAvailable { get; set; }
        public bool? IsLocalNetworkAvailable { get; set; }
        public bool? IsRemoteNetworkAvailable { get; set; }

        public bool GetIsLocalNetworkAvailable()
        {
            return IsLocalNetworkAvailable.HasValue ? IsLocalNetworkAvailable.Value : IsNetworkAvailable;
        }

        public bool GetIsRemoteNetworkAvailable()
        {
            return IsRemoteNetworkAvailable.HasValue ? IsRemoteNetworkAvailable.Value : IsNetworkAvailable;
        }
    }
}
