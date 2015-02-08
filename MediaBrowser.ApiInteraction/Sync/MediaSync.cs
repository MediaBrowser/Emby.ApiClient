using MediaBrowser.ApiInteraction.Data;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Sync;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Sync
{
    public class MediaSync
    {
        private readonly IFileTransferManager _fileTransferManager;
        private readonly LocalAssetManager _localAssetManager;
        private readonly ILogger _logger;

        public MediaSync(LocalAssetManager localAssetManager, ILogger logger, IFileTransferManager fileTransferManager)
        {
            _localAssetManager = localAssetManager;
            _logger = logger;
            _fileTransferManager = fileTransferManager;
        }

        public async Task Sync(IApiClient apiClient,
            ServerInfo serverInfo,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var systemInfo = await apiClient.GetSystemInfoAsync(cancellationToken).ConfigureAwait(false);
            if (!systemInfo.SupportsSync)
            {
                _logger.Debug("Skipping MediaSync because server does not support it.");
                return;
            }

            _logger.Debug("Beginning media sync process with server Id: {0}", serverInfo.Id);

            // First report actions to the server that occurred while offline
            await ReportOfflineActions(apiClient, serverInfo, cancellationToken).ConfigureAwait(false);
            progress.Report(1);

            await SyncData(apiClient, serverInfo, false, cancellationToken).ConfigureAwait(false);
            progress.Report(2);

            // Do the data sync twice so the server knows what was removed from the device
            await SyncData(apiClient, serverInfo, true, cancellationToken).ConfigureAwait(false);
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
            double startingPercent = 0;
            double percentPerItem = 1;
            if (jobItems.Count > 0)
            {
                percentPerItem /= jobItems.Count;
            }

            foreach (var jobItem in jobItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPercent = startingPercent;
                var innerProgress = new DoubleProgress();
                innerProgress.RegisterAction(pct =>
                {
                    var totalProgress = pct * percentPerItem;
                    totalProgress += currentPercent;
                    progress.Report(totalProgress);
                });

                await GetItem(apiClient, server, jobItem, innerProgress, cancellationToken).ConfigureAwait(false);

                numComplete++;
                startingPercent = numComplete;
                startingPercent /= jobItems.Count;
                startingPercent *= 100;
                progress.Report(startingPercent);
            }
        }

        private async Task GetItem(IApiClient apiClient,
            ServerInfo server,
            SyncedItem jobItem,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var libraryItem = jobItem.Item;

            var localItem = _localAssetManager.CreateLocalItem(libraryItem, server, jobItem.OriginalFileName);

            // Create db record
            await _localAssetManager.AddOrUpdate(localItem).ConfigureAwait(false);

            var fileTransferProgress = new DoubleProgress();
            fileTransferProgress.RegisterAction(pct => progress.Report(pct * .92));

            // Download item file
            await _fileTransferManager.GetItemFileAsync(apiClient, server, localItem, jobItem.SyncJobItemId, fileTransferProgress, cancellationToken).ConfigureAwait(false);
            progress.Report(92);

            // Download images
            await GetItemImages(apiClient, localItem, cancellationToken).ConfigureAwait(false);
            progress.Report(95);

            // Download subtitles
            await GetItemSubtitles(apiClient, jobItem, localItem, cancellationToken).ConfigureAwait(false);
            progress.Report(99);

            // Let the server know it was successfully downloaded
            await apiClient.ReportSyncJobItemTransferred(jobItem.SyncJobItemId).ConfigureAwait(false);
            progress.Report(100);
        }

        private async Task GetItemImages(IApiClient apiClient,
            LocalItem item,
            CancellationToken cancellationToken)
        {
            var libraryItem = item.Item;

            if (libraryItem.HasPrimaryImage)
            {
                await DownloadImage(apiClient, item.ServerId, libraryItem.Id, libraryItem.ImageTags[ImageType.Primary], ImageType.Primary, cancellationToken)
                        .ConfigureAwait(false);
            }

            // Container images

            // Series Primary
            if (!string.IsNullOrWhiteSpace(libraryItem.SeriesPrimaryImageTag))
            {
                await DownloadImage(apiClient, item.ServerId, libraryItem.SeriesId, libraryItem.SeriesPrimaryImageTag, ImageType.Primary, cancellationToken)
                        .ConfigureAwait(false);
            }

            // Series Thumb
            if (!string.IsNullOrWhiteSpace(libraryItem.SeriesThumbImageTag))
            {
                await DownloadImage(apiClient, item.ServerId, libraryItem.SeriesId, libraryItem.SeriesThumbImageTag, ImageType.Thumb, cancellationToken)
                        .ConfigureAwait(false);
            }

            // Album Primary
            if (!string.IsNullOrWhiteSpace(libraryItem.AlbumPrimaryImageTag))
            {
                await DownloadImage(apiClient, item.ServerId, libraryItem.AlbumId, libraryItem.AlbumPrimaryImageTag, ImageType.Primary, cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        private async Task DownloadImage(IApiClient apiClient,
            string serverId,
            string itemId,
            string imageTag,
            ImageType imageType,
            CancellationToken cancellationToken)
        {
            var hasImage = await _localAssetManager.HasImage(serverId, itemId, imageTag).ConfigureAwait(false);

            if (hasImage)
            {
                return;
            }

            var url = apiClient.GetImageUrl(itemId, new ImageOptions
            {
                ImageType = imageType,
                Tag = imageTag
            });

            using (var response = await apiClient.GetResponse(url, cancellationToken).ConfigureAwait(false))
            {
                await _localAssetManager.SaveItemImage(serverId, itemId, imageTag, response.Content).ConfigureAwait(false);
            }
        }

        private async Task GetItemSubtitles(IApiClient apiClient,
            SyncedItem jobItem,
            LocalItem item,
            CancellationToken cancellationToken)
        {
            var hasDownloads = false;

            var mediaSource = jobItem.Item.MediaSources.FirstOrDefault();

            if (mediaSource == null)
            {
                _logger.Error("Cannot download subtitles because video has no media source info.");
                return;
            }

            foreach (var file in jobItem.AdditionalFiles.Where(i => i.Type == ItemFileType.Subtitles))
            {
                var subtitleStream = mediaSource.MediaStreams.FirstOrDefault(i => i.Type == MediaStreamType.Subtitle && i.Index == file.Index);

                if (subtitleStream != null)
                {
                    using (var response = await apiClient.GetSyncJobItemAdditionalFile(jobItem.SyncJobItemId, file.Name, cancellationToken).ConfigureAwait(false))
                    {
                        var path = await _localAssetManager.SaveSubtitles(response, subtitleStream.Codec, item, subtitleStream.Language, subtitleStream.IsForced).ConfigureAwait(false);

                        subtitleStream.Path = path;
                    }

                    hasDownloads = true;
                }
                else
                {
                    _logger.Error("Cannot download subtitles because matching stream info wasn't found.");
                }
            }

            // Save the changes to the item
            if (hasDownloads)
            {
                await _localAssetManager.AddOrUpdate(item).ConfigureAwait(false);
            }
        }

        private async Task SyncData(IApiClient apiClient,
            ServerInfo serverInfo,
            bool syncUserItemAccess,
            CancellationToken cancellationToken)
        {
            var localIds = await _localAssetManager.GetServerItemIds(serverInfo.Id).ConfigureAwait(false);

            var result = await apiClient.SyncData(new SyncDataRequest
            {
                TargetId = apiClient.DeviceId,
                LocalItemIds = localIds,
                OfflineUserIds = serverInfo.Users.Select(i => i.Id).ToList()

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

            if (syncUserItemAccess)
            {
                foreach (var item in result.ItemUserAccess)
                {
                    var itemid = item.Key;

                    var localItem = await _localAssetManager.GetLocalItem(serverInfo.Id, itemid).ConfigureAwait(false);

                    if (!localItem.UserIdsWithAccess.SequenceEqual(item.Value, StringComparer.OrdinalIgnoreCase))
                    {
                        localItem.UserIdsWithAccess = item.Value;
                        await _localAssetManager.AddOrUpdate(localItem).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task ReportOfflineActions(IApiClient apiClient,
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
