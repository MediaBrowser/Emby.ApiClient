using MediaBrowser.ApiInteraction.Data;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public class MediaSync
    {
        private readonly LocalAssetManager _localAssetManager;
        private readonly ILogger _logger;

        public MediaSync(LocalAssetManager localAssetManager, ILogger logger)
        {
            _localAssetManager = localAssetManager;
            _logger = logger;
        }

        public async Task Sync(IApiClient apiClient,
            ServerInfo serverInfo,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Debug("Beginning media sync process with server Id: {0}", serverInfo.Id);

            // First report actions to the server that occurred while offline
            await ReportOfflineActions(apiClient, serverInfo, cancellationToken).ConfigureAwait(false);
            progress.Report(1);

            await SyncData(apiClient, serverInfo, cancellationToken).ConfigureAwait(false);
            progress.Report(2);

            // Do the data sync twice so the server knows what was removed from the device
            await SyncData(apiClient, serverInfo, cancellationToken).ConfigureAwait(false);
            progress.Report(3);

            var innerProgress = new DoubleProgress();
            innerProgress.RegisterAction(pct =>
            {
                var totalProgress = pct * .97;
                totalProgress += 1;
                progress.Report(totalProgress);
            });
            await GetNewMedia(apiClient, serverInfo, innerProgress, cancellationToken);
            progress.Report(100);
        }

        private async Task GetNewMedia(IApiClient apiClient,
            ServerInfo server,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var jobItems = await apiClient.GetReadySyncItems(apiClient.DeviceId).ConfigureAwait(false);

            var numComplete = 0;

            foreach (var jobItem in jobItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await GetItem(apiClient, server, jobItem, cancellationToken).ConfigureAwait(false);

                numComplete++;
                double percent = numComplete;
                percent /= jobItems.Count;

                progress.Report(100 * percent);
            }
        }

        private async Task GetItem(IApiClient apiClient,
            ServerInfo server,
            SyncedItem jobItem,
            CancellationToken cancellationToken)
        {
            var libraryItem = jobItem.Item;

            var localItem = _localAssetManager.CreateLocalItem(libraryItem, server, jobItem.OriginalFileName);

            // Download item file
            await _localAssetManager.AddOrUpdate(localItem).ConfigureAwait(false);

            // Download item file
            await GetItemFile(apiClient, server, localItem, jobItem.SyncJobItemId, cancellationToken).ConfigureAwait(false);

            var localFiles = await _localAssetManager.GetFiles(localItem).ConfigureAwait(false);

            // Download images
            await GetItemImages(apiClient, server, localItem, localFiles, cancellationToken).ConfigureAwait(false);

            // Download subtitles
            await GetItemSubtitles(apiClient, localItem, localFiles, cancellationToken).ConfigureAwait(false);

            // Let the server know it was successfully downloaded
            await apiClient.ReportSyncJobItemTransferred(jobItem.SyncJobItemId).ConfigureAwait(false);
        }

        private async Task GetItemFile(IApiClient apiClient,
            ServerInfo server,
            LocalItem item,
            string syncJobItemId,
            CancellationToken cancellationToken)
        {
            _logger.Debug("Downloading media with Id {0} to local repository", item.Item.Id);

            using (var stream = await apiClient.GetSyncJobItemFile(syncJobItemId, cancellationToken).ConfigureAwait(false))
            {
                await _localAssetManager.SaveMedia(stream, item, server).ConfigureAwait(false);
            }
        }

        private async Task GetItemImages(IApiClient apiClient,
            ServerInfo server,
            LocalItem item,
            List<ItemFileInfo> localFiles,
            CancellationToken cancellationToken)
        {
            var libraryItem = item.Item;

            var serverImages = GetServerImages(libraryItem)
                .ToList();

            foreach (var image in localFiles)
            {
                var current = serverImages
                    .FirstOrDefault(i => i.ImageType == image.ImageType);

                // Image not on server anymore (or has been changed)
                if (current == null)
                {
                    await _localAssetManager.DeleteFile(image.Path);
                }
            }

            foreach (var image in serverImages)
            {
                var current = localFiles
                    .FirstOrDefault(i => i.ImageType == image.ImageType);

                // Download image
                if (current == null)
                {
                    await DownloadImage(apiClient, server, item, image, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private IEnumerable<ImageInfo> GetServerImages(BaseItemDto item)
        {
            var list = new List<ImageInfo>();

            if (item.HasPrimaryImage)
            {
                list.Add(new ImageInfo
                {
                    ImageIndex = 0,
                    ImageTag = item.ImageTags[ImageType.Primary],
                    ImageType = ImageType.Primary
                });
            }

            return list;
        }

        private async Task DownloadImage(IApiClient apiClient,
            ServerInfo server,
            LocalItem item,
            ImageInfo image,
            CancellationToken cancellationToken)
        {
            var libraryItem = item.Item;

            var url = apiClient.GetImageUrl(libraryItem, new ImageOptions
            {
                ImageIndex = image.ImageIndex,
                ImageType = image.ImageType
            });

            using (var response = await apiClient.GetResponse(url, cancellationToken).ConfigureAwait(false))
            {
                await _localAssetManager.SaveImage(response.Content, response.ContentType, item, image, server).ConfigureAwait(false);
            }
        }

        private async Task GetItemSubtitles(IApiClient apiClient,
            LocalItem item,
            List<ItemFileInfo> localFiles,
            CancellationToken cancellationToken)
        {

        }

        private async Task SyncData(IApiClient apiClient,
            ServerInfo serverInfo,
            CancellationToken cancellationToken)
        {
            var actions = await _localAssetManager.GetUserActions(serverInfo.Id).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var actionList = actions
                .OrderBy(i => i.Date)
                .ToList();

            _logger.Debug("Reporting {0} offline actions to server {1}",
                actionList.Count,
                serverInfo.Id);

            if (actionList.Count > 0)
            {
                await apiClient.ReportOfflineActions(actionList).ConfigureAwait(false);
            }

            foreach (var action in actionList)
            {
                await _localAssetManager.Delete(action).ConfigureAwait(false);
            }
        }

        private async Task ReportOfflineActions(IApiClient apiClient,
            ServerInfo serverInfo,
            CancellationToken cancellationToken)
        {
            var localIds = await _localAssetManager.GetServerItemIds(serverInfo.Id).ConfigureAwait(false);

            var result = await apiClient.SyncData(new SyncDataRequest
            {
                TargetId = apiClient.DeviceId,
                LocalItemIds = localIds

            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var itemIdToRemove in result.ItemIdsToRemove)
            {
                try
                {
                    await RemoveItem(serverInfo.Id, itemIdToRemove).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error deleting item from device. Id: {0}", ex, itemIdToRemove);
                }
            }
        }

        private async Task RemoveItem(string serverId, string itemId)
        {
            var localItem = await _localAssetManager.GetLocalItem(serverId, itemId);

            if (localItem == null)
            {
                return;
            }

            var files = await _localAssetManager.GetFiles(localItem);

            foreach (var file in files)
            {
                await _localAssetManager.DeleteFile(file.Path).ConfigureAwait(false);
            }

            await _localAssetManager.Delete(localItem).ConfigureAwait(false);
        }
    }
}
