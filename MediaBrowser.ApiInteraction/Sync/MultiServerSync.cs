using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public class MultiServerSync
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger _logger;

        public MultiServerSync(IConnectionManager connectionManager, ILogger logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task Sync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var servers = await _connectionManager.GetAvailableServers(cancellationToken).ConfigureAwait(false);

            await Sync(servers, progress, cancellationToken).ConfigureAwait(false);
        }

        private async Task Sync(List<ServerInfo> servers, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var numComplete = 0;
            double startingPercent = 0;
            double percentPerServer = 1;
            if (servers.Count > 0)
            {
                percentPerServer /= servers.Count;
            }

            foreach (var server in servers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPercent = startingPercent;
                var serverProgress = new DoubleProgress();
                serverProgress.RegisterAction(pct =>
                {
                    var totalProgress = pct*percentPerServer;
                    totalProgress += currentPercent;
                    progress.Report(totalProgress);
                });
                
                await new ServerSync(_connectionManager, _logger)
                    .Sync(server, serverProgress, cancellationToken).ConfigureAwait(false);

                numComplete++;
                startingPercent = numComplete;
                startingPercent /= servers.Count;
                startingPercent *= 100;
                progress.Report(startingPercent);
            }

            progress.Report(100);
        }
    }
}
