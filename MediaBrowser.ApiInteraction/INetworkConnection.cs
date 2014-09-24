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
        /// Gets or sets a value indicating whether this instance is connected to local network.
        /// </summary>
        /// <value><c>true</c> if this instance is connected to local network; otherwise, <c>false</c>.</value>
        bool IsConnectedToLocalNetwork { get; set; }
    }
}
