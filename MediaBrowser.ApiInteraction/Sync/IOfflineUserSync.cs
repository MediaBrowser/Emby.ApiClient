using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.ApiClient;

namespace MediaBrowser.ApiInteraction.Sync
{
    public interface IOfflineUserSync
    {
        Task UpdateOfflineUsers(ServerInfo server, IApiClient apiClient, CancellationToken cancellationToken = default(CancellationToken));
    }
}