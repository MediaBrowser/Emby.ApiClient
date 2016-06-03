using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public interface IMultiServerSync
    {
        Task Sync(IProgress<double> progress, 
            List<string> cameraUploadServers,
            bool syncOnlyOnLocalNetwork,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}