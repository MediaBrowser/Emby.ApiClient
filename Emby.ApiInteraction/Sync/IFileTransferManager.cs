using System;
using System.Threading;

namespace Emby.ApiInteraction.Sync
{
    public interface IFileTransferManager
    {
        System.Threading.Tasks.Task GetItemFileAsync(MediaBrowser.Model.ApiClient.IApiClient apiClient, MediaBrowser.Model.ApiClient.ServerInfo server, MediaBrowser.Model.Sync.LocalItem item, string syncJobItemId, IProgress<double> transferProgress, System.Threading.CancellationToken cancellationToken = default(CancellationToken));
    }
}
