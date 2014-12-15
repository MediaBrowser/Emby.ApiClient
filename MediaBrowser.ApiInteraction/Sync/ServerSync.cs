using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public class ServerSync
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger _logger;

        public ServerSync(IConnectionManager connectionManager, ILogger logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task Sync(ServerInfo server, IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(server.AccessToken) && string.IsNullOrWhiteSpace(server.ExchangeToken))
            {
                LogNoAuthentication(server);
                progress.Report(100);
                return;
            }

            var result = await _connectionManager.Connect(server, cancellationToken).ConfigureAwait(false);

            if (result.State == ConnectionState.SignedIn)
            {
                await SyncInternal(server, result.ApiClient, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                LogNoAuthentication(server);
                progress.Report(100);
            }
        }

        public async Task SyncInternal(ServerInfo server, IApiClient apiClient, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var contentUploader = new ContentUploader(apiClient, _logger);

            await contentUploader.UploadImages(progress, cancellationToken).ConfigureAwait(false);
        }
        
        private void LogNoAuthentication(ServerInfo server)
        {
            _logger.Info("Skipping sync process for server " + server.Name + ". No server authentication information available.");
        }
    }
}
