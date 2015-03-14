using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public interface IMultiServerSync
    {
        Task Sync(IProgress<double> progress, CancellationToken cancellationToken = default(CancellationToken));
    }
}