using MediaBrowser.ApiInteraction.Data;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Playback
{
    public class PlaybackManager
    {
        private readonly ILocalAssetManager _localAssetManager;
        private readonly ILogger _logger;
        private readonly IDevice _device;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackManager" /> class.
        /// </summary>
        /// <param name="localAssetManager">The local asset manager.</param>
        /// <param name="device">The device.</param>
        /// <param name="logger">The logger.</param>
        public PlaybackManager(ILocalAssetManager localAssetManager, IDevice device, ILogger logger)
        {
            _localAssetManager = localAssetManager;
            _device = device;
            _logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackManager"/> class.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="logger">The logger.</param>
        public PlaybackManager(IDevice device, ILogger logger)
            : this(new NullAssetManager(), device, logger)
        {
        }
        
        /// <summary>
        /// Gets the selectable audio streams.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;IEnumerable&lt;MediaStream&gt;&gt;.</returns>
        public async Task<IEnumerable<MediaStream>> GetSelectableAudioStreams(string serverId, VideoOptions options)
        {
            var info = await GetVideoStreamInfo(serverId, options).ConfigureAwait(false);

            return info.MediaSource.MediaStreams.Where(i => i.Type == MediaStreamType.Audio);
        }

        /// <summary>
        /// Gets the selectable subtitle streams.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;IEnumerable&lt;MediaStream&gt;&gt;.</returns>
        public async Task<IEnumerable<MediaStream>> GetSelectableSubtitleStreams(string serverId, VideoOptions options)
        {
            var info = await GetVideoStreamInfo(serverId, options).ConfigureAwait(false);

            return info.MediaSource.MediaStreams.Where(i => i.Type == MediaStreamType.Subtitle);
        }

        /// <summary>
        /// Gets the audio stream information.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;StreamInfo&gt;.</returns>
        public async Task<StreamInfo> GetAudioStreamInfo(string serverId, AudioOptions options)
        {
            var streamBuilder = new StreamBuilder();

            var localItem = await _localAssetManager.GetLocalItem(serverId, options.ItemId);

            if (localItem != null)
            {
                var localMediaSource = localItem.Item.MediaSources[0];

                // Use the local media source, unless a specific server media source was requested
                if (string.IsNullOrWhiteSpace(options.MediaSourceId) ||
                    string.Equals(localMediaSource.Id, options.MediaSourceId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Finally, check to make sure the local file is actually available at this time
                    var fileExists = await _localAssetManager.FileExists(localMediaSource.Path).ConfigureAwait(false);

                    if (fileExists)
                    {
                        options.MediaSources = localItem.Item.MediaSources;

                        var result = streamBuilder.BuildAudioItem(options);
                        result.PlayMethod = PlayMethod.DirectPlay;
                        return result;
                    }
                }
            }

            return streamBuilder.BuildAudioItem(options);
        }

        /// <summary>
        /// Gets the video stream information.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;StreamInfo&gt;.</returns>
        public async Task<StreamInfo> GetVideoStreamInfo(string serverId, VideoOptions options)
        {
            var streamBuilder = new StreamBuilder();

            var localItem = await _localAssetManager.GetLocalItem(serverId, options.ItemId);

            if (localItem != null)
            {
                var localMediaSource = localItem.Item.MediaSources[0];

                // Use the local media source, unless a specific server media source was requested
                if (string.IsNullOrWhiteSpace(options.MediaSourceId) ||
                    string.Equals(localMediaSource.Id, options.MediaSourceId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Finally, check to make sure the local file is actually available at this time
                    var fileExists = await _localAssetManager.FileExists(localMediaSource.Path).ConfigureAwait(false);

                    if (fileExists)
                    {
                        options.MediaSources = localItem.Item.MediaSources;

                        var result = streamBuilder.BuildVideoItem(options);
                        result.PlayMethod = PlayMethod.DirectPlay;
                        return result;
                    }
                }
            }

            return streamBuilder.BuildVideoItem(options);
        }

        /// <summary>
        /// Reports playback start
        /// </summary>
        /// <param name="info">The information.</param>
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="apiClient">The current apiClient. It can be null if offline</param>
        /// <returns>Task.</returns>
        public async Task ReportPlaybackStart(PlaybackStartInfo info, bool isOffline, IApiClient apiClient)
        {
            if (!isOffline)
            {
                await apiClient.ReportPlaybackStartAsync(info).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reports playback progress
        /// </summary>
        /// <param name="info">The information.</param>
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="apiClient">The current apiClient. It can be null if offline</param>
        /// <returns>Task.</returns>
        public async Task ReportPlaybackProgress(PlaybackProgressInfo info, bool isOffline, IApiClient apiClient)
        {
            if (!isOffline)
            {
                await apiClient.ReportPlaybackProgressAsync(info).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reports playback progress
        /// </summary>
        /// <param name="info">The information.</param>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="isVideo">if set to <c>true</c> [is video].</param>
        /// <param name="apiClient">The current apiClient. It can be null if offline</param>
        /// <returns>Task.</returns>
        public async Task ReportPlaybackStopped(PlaybackStopInfo info, string serverId, string userId, bool isOffline, bool isVideo, IApiClient apiClient)
        {
            if (isOffline)
            {
                var action = new UserAction
                {
                    Date = DateTime.UtcNow,
                    ItemId = info.ItemId,
                    PositionTicks = info.PositionTicks,
                    ServerId = serverId,
                    Type = UserActionType.PlayedItem,
                    UserId = userId
                };

                await _localAssetManager.RecordUserAction(action).ConfigureAwait(false);
                return;
            }

            // Put a try/catch here because we need to stop transcoding regardless
            try
            {
                await apiClient.ReportPlaybackStoppedAsync(info).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error in ReportPlaybackStoppedAsync", ex);
            }

            if (isVideo)
            {
                await apiClient.StopTranscodingProcesses(_device.DeviceId).ConfigureAwait(false);
            }
        }
    }
}
