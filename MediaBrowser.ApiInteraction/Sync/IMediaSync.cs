using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.ApiClient;

namespace MediaBrowser.ApiInteraction.Sync
{
    public interface IMediaSync
    {
        Task Sync(IApiClient apiClient,
            ServerInfo serverInfo,
            IProgress<double> progress,
            CancellationToken cancellationToken);
    }
}