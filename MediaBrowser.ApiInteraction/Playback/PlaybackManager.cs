using MediaBrowser.ApiInteraction.Data;
using MediaBrowser.ApiInteraction.Net;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Playback
{
    public class PlaybackManager : IPlaybackManager
    {
        private readonly ILocalAssetManager _localAssetManager;
        private readonly ILogger _logger;
        private readonly IDevice _device;
        private readonly ILocalPlayer _localPlayer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackManager" /> class.
        /// </summary>
        /// <param name="localAssetManager">The local asset manager.</param>
        /// <param name="device">The device.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="localPlayer">The local player.</param>
        public PlaybackManager(ILocalAssetManager localAssetManager, IDevice device, ILogger logger, ILocalPlayer localPlayer)
        {
            _localAssetManager = localAssetManager;
            _device = device;
            _logger = logger;
            _localPlayer = localPlayer;
        }

        public PlaybackManager(ILocalAssetManager localAssetManager, IDevice device, ILogger logger, INetworkConnection network, IAsyncHttpClient httpClient)
            : this(localAssetManager, device, logger, new PortablePlayer(network, httpClient))
        {
        }

        /// <summary>
        /// Gets the pre playback selectable audio streams.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;IEnumerable&lt;MediaStream&gt;&gt;.</returns>
        public async Task<IEnumerable<MediaStream>> GetPrePlaybackSelectableAudioStreams(string serverId, VideoOptions options)
        {
            var info = await GetVideoStreamInfoInternal(serverId, options).ConfigureAwait(false);

            return info.GetSelectableAudioStreams();
        }

        /// <summary>
        /// Gets the pre playback selectable subtitle streams.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;IEnumerable&lt;MediaStream&gt;&gt;.</returns>
        public async Task<IEnumerable<MediaStream>> GetPrePlaybackSelectableSubtitleStreams(string serverId, VideoOptions options)
        {
            var info = await GetVideoStreamInfoInternal(serverId, options).ConfigureAwait(false);

            return info.GetSelectableSubtitleStreams();
        }

        /// <summary>
        /// Gets the in playback selectable audio streams.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <returns>IEnumerable&lt;MediaStream&gt;.</returns>
        public IEnumerable<MediaStream> GetInPlaybackSelectableAudioStreams(StreamInfo info)
        {
            return info.GetSelectableAudioStreams();
        }

        /// <summary>
        /// Gets the in playback selectable subtitle streams.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <returns>IEnumerable&lt;MediaStream&gt;.</returns>
        public IEnumerable<MediaStream> GetInPlaybackSelectableSubtitleStreams(StreamInfo info)
        {
            return info.GetSelectableSubtitleStreams();
        }

        /// <summary>
        /// Gets the stream builder.
        /// </summary>
        /// <returns>StreamBuilder.</returns>
        private StreamBuilder GetStreamBuilder()
        {
            return new StreamBuilder(_localPlayer);
        }
        
        /// <summary>
        /// Gets the audio stream information.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="options">The options.</param>
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="apiClient">The API client.</param>
        /// <returns>Task&lt;StreamInfo&gt;.</returns>
        public async Task<StreamInfo> GetAudioStreamInfo(string serverId, AudioOptions options, bool isOffline, IApiClient apiClient)
        {
            var streamBuilder = GetStreamBuilder();

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
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="apiClient">The API client.</param>
        /// <returns>Task&lt;StreamInfo&gt;.</returns>
        public async Task<StreamInfo> GetVideoStreamInfo(string serverId, VideoOptions options, bool isOffline, IApiClient apiClient)
        {
            if (!isOffline)
            {
                var mediaInfo = await apiClient.GetPlaybackInfo(options.ItemId, apiClient.CurrentUserId).ConfigureAwait(false);

                if (mediaInfo.ErrorCode.HasValue)
                {
                    throw new PlaybackException { ErrorCode = mediaInfo.ErrorCode.Value };
                }

                options.MediaSources = mediaInfo.MediaSources;
            }

            return await GetVideoStreamInfoInternal(serverId, options).ConfigureAwait(false);
        }

        private async Task<StreamInfo> GetVideoStreamInfoInternal(string serverId, VideoOptions options)
        {
            var streamBuilder = GetStreamBuilder();

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
        /// <param name="streamInfo">The stream information.</param>
        /// <param name="serverId">The server identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="isOffline">if set to <c>true</c> [is offline].</param>
        /// <param name="apiClient">The current apiClient. It can be null if offline</param>
        /// <returns>Task.</returns>
        public async Task ReportPlaybackStopped(PlaybackStopInfo info, StreamInfo streamInfo, string serverId, string userId, bool isOffline, IApiClient apiClient)
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

            if (streamInfo.MediaType == DlnaProfileType.Video)
            {
                await apiClient.StopTranscodingProcesses(_device.DeviceId).ConfigureAwait(false);
            }
        }
    }
}
