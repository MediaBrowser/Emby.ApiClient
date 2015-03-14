using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.ApiClient;

namespace MediaBrowser.ApiInteraction.Sync
{
    public interface IServerSync
    {
        Task Sync(ServerInfo server, IProgress<double> progress, CancellationToken cancellationToken = default(CancellationToken));
    }
}