using MediaBrowser.ApiInteraction.Data;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Session;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public class ServerSync
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IFileTransferManager _fileTransferManager;
        private readonly ILogger _logger;
        private readonly ILocalAssetManager _localAssetManager;

        private readonly ClientCapabilities _clientCapabilities;
        private static readonly Dictionary<string, SemaphoreSlim> SemaphoreLocks = new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        public ServerSync(IConnectionManager connectionManager, ILogger logger, ILocalAssetManager localAssetManager, IFileTransferManager fileTransferManager, ClientCapabilities clientCapabilities)
        {
            _connectionManager = connectionManager;
            _fileTransferManager = fileTransferManager;
            _clientCapabilities = clientCapabilities;
            _logger = logger;
            _localAssetManager = localAssetManager;
        }

        public async Task Sync(ServerInfo server, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var semaphore = GetLock(server.Id);

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await SyncInternal(server, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static SemaphoreSlim GetLock(string serverId)
        {
            SemaphoreSlim semaphore;

            if (SemaphoreLocks.TryGetValue(serverId, out semaphore))
            {
                return semaphore;
            }

            semaphore = new SemaphoreSlim(1, 1);
            SemaphoreLocks[serverId] = semaphore;

            return semaphore;
        }

        private async Task SyncInternal(ServerInfo server, IProgress<double> progress, CancellationToken cancellationToken)
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

            if (_clientCapabilities.SupportsOfflineAccess)
            {
                await UpdateOfflineUsers(server, apiClient, cancellationToken).ConfigureAwait(false);
            }

            var syncProgress = new DoubleProgress();
            syncProgress.RegisterAction(p => progress.Report((cameraUploadTotalPercentage * 100) + (p * (1 - cameraUploadTotalPercentage))));

            await new MediaSync(_localAssetManager, _logger, _fileTransferManager)
                .Sync(apiClient, server, uploadProgress, cancellationToken).ConfigureAwait(false);
        }

        private void LogNoAuthentication(ServerInfo server)
        {
            _logger.Info("Skipping sync process for server " + server.Name + ". No server authentication information available.");
        }

        private async Task UpdateOfflineUsers(ServerInfo server, IApiClient apiClient, CancellationToken cancellationToken)
        {
            foreach (var user in server.Users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await SaveOfflineUser(user, apiClient, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Already logged at lower level
                }
            }
        }

        private async Task SaveOfflineUser(ServerUserInfo user, IApiClient apiClient, CancellationToken cancellationToken)
        {
            var deleteUser = false;
            var updateImage = false;

            UserDto offlineUser = null;

            try
            {
                offlineUser = await apiClient.GetOfflineUserAsync(user.Id).ConfigureAwait(false);

                await _localAssetManager.SaveOfflineUser(offlineUser).ConfigureAwait(false);

                updateImage = true;
            }
            catch (HttpException ex)
            {
                _logger.ErrorException("Error getting user info", ex);

                if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
                {
                    deleteUser = true;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting user info", ex);
            }

            if (deleteUser)
            {
                await _localAssetManager.DeleteOfflineUser(user.Id).ConfigureAwait(false);
            }

            if (updateImage && offlineUser != null)
            {
                await UpdateUserImage(offlineUser, apiClient, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task UpdateUserImage(UserDto user, IApiClient apiClient, CancellationToken cancellationToken)
        {
            if (user.HasPrimaryImage)
            {
                var isImageCached = await _localAssetManager.HasImage(user).ConfigureAwait(false);

                if (!isImageCached)
                {
                    var imageUrl = apiClient.GetUserImageUrl(user, new ImageOptions
                    {
                        ImageType = ImageType.Primary
                    });

                    using (var stream = await apiClient.GetImageStreamAsync(imageUrl, cancellationToken).ConfigureAwait(false))
                    {
                        await _localAssetManager.SaveUserImage(user, stream).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await _localAssetManager.DeleteUserImage(user).ConfigureAwait(false);
            }
        }
    }
}
