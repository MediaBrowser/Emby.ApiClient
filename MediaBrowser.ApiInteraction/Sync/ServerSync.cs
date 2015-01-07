using MediaBrowser.ApiInteraction.Data;
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
        private readonly LocalAssetManager _userActionAssetManager;

        public ServerSync(IConnectionManager connectionManager, ILogger logger, LocalAssetManager userActionAssetManager)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _userActionAssetManager = userActionAssetManager;
        }

        public async Task Sync(ServerInfo server, IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(server.AccessToken) && string.IsNullOrWhiteSpace(server.ExchangeToken))
            {
                LogNoAuthentication(server);
                progress.Report(100);
                return;
            }

            // Don't need these here
            var result = await _connectionManager.Connect(server, new ConnectionOptions
            {
                EnableWebSocket = false,
                ReportCapabilities = false

            }, cancellationToken).ConfigureAwait(false);

            if (result.State == ConnectionState.SignedIn)
            {
                await SyncInternal(server, result.ApiClient, progress, cancellationToken).ConfigureAwait(false);
                progress.Report(100);
            }
            else
            {
                LogNoAuthentication(server);
                progress.Report(100);
            }
        }

        private async Task SyncInternal(ServerInfo server, IApiClient apiClient, IProgress<double> progress, CancellationToken cancellationToken)
        {
            const double cameraUploadTotalPercentage = .25;

            var uploadProgress = new DoubleProgress();
            uploadProgress.RegisterAction(p => progress.Report(p * cameraUploadTotalPercentage));
            await new ContentUploader(apiClient, _logger)
                .UploadImages(uploadProgress, cancellationToken).ConfigureAwait(false);

            var syncProgress = new DoubleProgress();
            syncProgress.RegisterAction(p => progress.Report((cameraUploadTotalPercentage * 100) + (p * (1 - cameraUploadTotalPercentage))));

            await new MediaSync(_userActionAssetManager, _logger)
                .Sync(apiClient, server, uploadProgress, cancellationToken).ConfigureAwait(false);
        }
        
        private void LogNoAuthentication(ServerInfo server)
        {
            _logger.Info("Skipping sync process for server " + server.Name + ". No server authentication information available.");
        }
    }
}
