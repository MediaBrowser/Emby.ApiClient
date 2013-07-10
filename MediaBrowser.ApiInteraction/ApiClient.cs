using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Notifications;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Provides api methods centered around an HttpClient
    /// </summary>
    public class ApiClient : BaseApiClient, IApiClient
    {
        public event EventHandler<HttpResponseEventArgs> HttpResponseReceived
        {
            add { HttpClient.HttpResponseReceived += value; }
            remove
            {
                HttpClient.HttpResponseReceived -= value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverApiPort">The server API port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        public ApiClient(string serverHostName, int serverApiPort, string clientName, string deviceName, string deviceId, string applicationVersion)
            : this(new NullLogger(), new AsyncHttpClient(new NullLogger()), serverHostName, serverApiPort, clientName, deviceName, deviceId, applicationVersion)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverApiPort">The server API port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <exception cref="System.ArgumentNullException">httpClient</exception>
        public ApiClient(ILogger logger, IAsyncHttpClient httpClient, string serverHostName, int serverApiPort, string clientName, string deviceName, string deviceId, string applicationVersion)
            : base(logger, new NewtonsoftJsonSerializer(), serverHostName, serverApiPort, clientName, deviceName, deviceId, applicationVersion)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            HttpClient = httpClient;

            var param = AuthorizationParameter;

            if (!string.IsNullOrEmpty(param))
            {
                HttpClient.SetAuthorizationHeader(AuthorizationScheme, param);
            }
        }

        /// <summary>
        /// Gets the HTTP client.
        /// </summary>
        /// <value>The HTTP client.</value>
        protected IAsyncHttpClient HttpClient { get; private set; }

        /// <summary>
        /// Called when [current user changed].
        /// </summary>
        protected override void OnCurrentUserChanged()
        {
            base.OnCurrentUserChanged();

            HttpClient.SetAuthorizationHeader(AuthorizationScheme, AuthorizationParameter);
        }

        /// <summary>
        /// Gets an image stream based on a url
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="System.ArgumentNullException">url</exception>
        public Task<Stream> GetImageStreamAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("url");
            }

            return HttpClient.GetAsync(url, CancellationToken.None);
        }

        /// <summary>
        /// Gets a BaseItem
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public async Task<BaseItemDto> GetItemAsync(string id, string userId)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + id);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the intros async.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{System.String[]}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public async Task<string[]> GetIntrosAsync(string itemId, string userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Intros");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<string[]>(stream);
            }
        }

        public async Task<ItemCounts> GetItemCountsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary { };

            dict.Add("UserId", userId);

            var url = GetApiUrl("Items/Counts", dict);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemCounts>(stream);
            }
        }

        /// <summary>
        /// Gets a BaseItem
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto> GetRootFolderAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/Root");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the users async.
        /// </summary>
        /// <returns>Task{UserDto[]}.</returns>
        public async Task<UserDto[]> GetUsersAsync(UserQuery query)
        {
            var queryString = new QueryStringDictionary();

            queryString.AddIfNotNull("IsDisabled", query.IsDisabled);
            queryString.AddIfNotNull("IsHidden", query.IsHidden);

            var url = GetApiUrl("Users", queryString);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<UserDto[]>(stream);
            }
        }

        public async Task<UserDto[]> GetPublicUsersAsync()
        {
            var url = GetApiUrl("Users/Public");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<UserDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets active client sessions.
        /// </summary>
        /// <returns>Task{SessionInfoDto[]}.</returns>
        public async Task<SessionInfoDto[]> GetClientSessionsAsync()
        {
            var url = GetApiUrl("Sessions");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<SessionInfoDto[]>(stream);
            }
        }

        /// <summary>
        /// Queries for items
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetItemsAsync(ItemQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetItemListUrl(query);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the next up async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetNextUpAsync(NextUpQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var dict = new QueryStringDictionary { };

            if (query.Fields != null)
            {
                dict.Add("fields", query.Fields.Select(f => f.ToString()));
            }

            dict.AddIfNotNull("Limit", query.Limit);

            dict.AddIfNotNull("StartIndex", query.StartIndex);

            dict.Add("UserId", query.UserId);

            var url = GetApiUrl("Shows/NextUp", dict);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the similar movies async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetSimilarMoviesAsync(SimilarItemsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, "Movies");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the similar series async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetSimilarSeriesAsync(SimilarItemsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, "Shows");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the similar games async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetSimilarGamesAsync(SimilarItemsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, "Games");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the similar albums async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<ItemsResult> GetSimilarAlbumsAsync(SimilarItemsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, "Albums");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the people async.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<ItemsResult> GetPeopleAsync(PersonsQuery query)
        {
            var url = GetItemByNameListUrl("Persons", query);

            if (query.PersonTypes != null && query.PersonTypes.Length > 0)
            {
                url += "&PersonTypes=" + string.Join(",", query.PersonTypes);
            }

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets the artists.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<ItemsResult> GetArtistsAsync(ArtistsQuery query)
        {
            var url = GetItemByNameListUrl("Artists", query);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ItemsResult>(stream);
            }
        }

        /// <summary>
        /// Gets a studio
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto> GetStudioAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Studios/" + GetSlugName(name));

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a genre
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto> GetGenreAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Genres/" + GetSlugName(name));

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the music genre async.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public async Task<BaseItemDto> GetMusicGenreAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("MusicGenres/" + GetSlugName(name));

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets the artist async.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public async Task<BaseItemDto> GetArtistAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Artists/" + GetSlugName(name));

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Restarts the server async.
        /// </summary>
        /// <returns>Task.</returns>
        public Task RestartServerAsync()
        {
            var url = GetApiUrl("System/Restart");

            return PostAsync<EmptyRequestResult>(url, new QueryStringDictionary());
        }

        /// <summary>
        /// Gets the system status async.
        /// </summary>
        /// <returns>Task{SystemInfo}.</returns>
        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            var url = GetApiUrl("System/Info");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<SystemInfo>(stream);
            }
        }

        /// <summary>
        /// Gets a person
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto> GetPersonAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            var url = GetApiUrl("Persons/" + GetSlugName(name));

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a year
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto> GetYearAsync(int year)
        {
            var url = GetApiUrl("Years/" + year);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto>(stream);
            }
        }

        /// <summary>
        /// Gets a list of plugins installed on the server
        /// </summary>
        /// <returns>Task{PluginInfo[]}.</returns>
        public async Task<PluginInfo[]> GetInstalledPluginsAsync()
        {
            var url = GetApiUrl("Plugins");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<PluginInfo[]>(stream);
            }
        }

        /// <summary>
        /// Gets the current server configuration
        /// </summary>
        /// <returns>Task{ServerConfiguration}.</returns>
        public async Task<ServerConfiguration> GetServerConfigurationAsync()
        {
            var url = GetApiUrl("System/Configuration");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ServerConfiguration>(stream);
            }
        }

        /// <summary>
        /// Gets the scheduled tasks.
        /// </summary>
        /// <returns>Task{TaskInfo[]}.</returns>
        public async Task<TaskInfo[]> GetScheduledTasksAsync()
        {
            var url = GetApiUrl("ScheduledTasks");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<TaskInfo[]>(stream);
            }
        }

        /// <summary>
        /// Gets the scheduled task async.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{TaskInfo}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public async Task<TaskInfo> GetScheduledTaskAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("ScheduledTasks/" + id);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<TaskInfo>(stream);
            }
        }

        /// <summary>
        /// Gets a user by id
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{UserDto}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public async Task<UserDto> GetUserAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("Users/" + id);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<UserDto>(stream);
            }
        }

        /// <summary>
        /// Gets the parental ratings async.
        /// </summary>
        /// <returns>Task{List{ParentalRating}}.</returns>
        public async Task<List<ParentalRating>> GetParentalRatingsAsync()
        {
            var url = GetApiUrl("Localization/ParentalRatings");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<List<ParentalRating>>(stream);
            }
        }

        /// <summary>
        /// Gets local trailers for an item
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="itemId">The item id.</param>
        /// <returns>Task{ItemsResult}.</returns>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public async Task<BaseItemDto[]> GetLocalTrailersAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/LocalTrailers");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets special features for an item
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="itemId">The item id.</param>
        /// <returns>Task{BaseItemDto[]}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public async Task<BaseItemDto[]> GetSpecialFeaturesAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/SpecialFeatures");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<BaseItemDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets the cultures async.
        /// </summary>
        /// <returns>Task{CultureDto[]}.</returns>
        public async Task<CultureDto[]> GetCulturesAsync()
        {
            var url = GetApiUrl("Localization/Cultures");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<CultureDto[]>(stream);
            }
        }

        /// <summary>
        /// Gets the countries async.
        /// </summary>
        /// <returns>Task{CountryInfo[]}.</returns>
        public async Task<CountryInfo[]> GetCountriesAsync()
        {
            var url = GetApiUrl("Localization/Countries");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<CountryInfo[]>(stream);
            }
        }

        /// <summary>
        /// Marks an item as played or unplayed.
        /// This should not be used to update playstate following playback.
        /// There are separate playstate check-in methods for that. This should be used for a
        /// separate option to reset playstate.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="wasPlayed">if set to <c>true</c> [was played].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task UpdatePlayedStatusAsync(string itemId, string userId, bool wasPlayed)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/PlayedItems/" + itemId);

            if (wasPlayed)
            {
                return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
            }

            return HttpClient.DeleteAsync(url, CancellationToken.None);
        }

        /// <summary>
        /// Updates the favorite status async.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="isFavorite">if set to <c>true</c> [is favorite].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task UpdateFavoriteStatusAsync(string itemId, string userId, bool isFavorite)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/FavoriteItems/" + itemId);

            if (isFavorite)
            {
                return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
            }

            return HttpClient.DeleteAsync(url, CancellationToken.None);
        }

        /// <summary>
        /// Reports to the server that the user has begun playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task ReportPlaybackStartAsync(string itemId, string userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Reports playback progress to the server
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <param name="isPaused">if set to <c>true</c> [is paused].</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task ReportPlaybackProgressAsync(string itemId, string userId, long? positionTicks, bool isPaused)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);
            dict.Add("isPaused", isPaused);

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId + "/Progress", dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Reports to the server that the user has stopped playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task ReportPlaybackStoppedAsync(string itemId, string userId, long? positionTicks)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId, dict);

            return HttpClient.DeleteAsync(url, CancellationToken.None);
        }

        /// <summary>
        /// Instructs antoher client to browse to a library item.
        /// </summary>
        /// <param name="sessionId">The session id.</param>
        /// <param name="itemId">The id of the item to browse to.</param>
        /// <param name="itemName">The name of the item to browse to.</param>
        /// <param name="itemType">The type of the item to browse to.</param>
        /// <param name="context">Optional ui context (movies, music, tv, games, etc). The client is free to ignore this.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// sessionId
        /// or
        /// itemId
        /// or
        /// itemName
        /// or
        /// itemType
        /// </exception>
        public Task SendBrowseCommandAsync(string sessionId, string itemId, string itemName, string itemType, string context)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentNullException("sessionId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }
            if (string.IsNullOrEmpty(itemName))
            {
                throw new ArgumentNullException("itemName");
            }
            if (string.IsNullOrEmpty(itemType))
            {
                throw new ArgumentNullException("itemType");
            }

            var dict = new QueryStringDictionary();
            dict.Add("itemId", itemId);
            dict.Add("itemName", itemName);
            dict.Add("itemType", itemType);
            dict.AddIfNotNullOrEmpty("context", context);

            var url = GetApiUrl("Sessions/" + sessionId + "/Viewing/", dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Sends the play command async.
        /// </summary>
        /// <param name="sessionId">The session id.</param>
        /// <param name="request">The request.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">sessionId
        /// or
        /// request</exception>
        public Task SendPlayCommandAsync(string sessionId, PlayRequest request)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentNullException("sessionId");
            }
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            var dict = new QueryStringDictionary();
            dict.Add("ItemIds", request.ItemIds);
            dict.AddIfNotNull("StartPositionTicks", request.StartPositionTicks);
            dict.Add("PlayCommand", request.PlayCommand.ToString());

            var url = GetApiUrl("Sessions/" + sessionId + "/Playing/", dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Sends the playstate command async.
        /// </summary>
        /// <param name="sessionId">The session id.</param>
        /// <param name="request">The request.</param>
        /// <returns>Task.</returns>
        public Task SendPlaystateCommandAsync(string sessionId, PlaystateRequest request)
        {
            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("SeekPosition", request.SeekPosition);

            var url = GetApiUrl("Sessions/" + sessionId + "/Playing/" + request.Command.ToString(), dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Clears a user's rating for an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task ClearUserItemRatingAsync(string itemId, string userId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Rating");

            return HttpClient.DeleteAsync(url, CancellationToken.None);
        }

        /// <summary>
        /// Updates a user's rating for an item, based on likes or dislikes
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="likes">if set to <c>true</c> [likes].</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public Task UpdateUserItemRatingAsync(string itemId, string userId, bool likes)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var dict = new QueryStringDictionary { };

            dict.Add("likes", likes);

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/Rating", dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        /// <summary>
        /// Authenticates a user and returns the result
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public Task AuthenticateUserAsync(string userId, byte[] sha1Hash)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var password = BitConverter.ToString(sha1Hash).Replace("-", string.Empty);
            var url = GetApiUrl("Users/" + userId + "/Authenticate");

            var args = new Dictionary<string, string>();

            args["password"] = password;

            return PostAsync<EmptyRequestResult>(url, args);
        }

        /// <summary>
        /// Updates the server configuration async.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">configuration</exception>
        public Task UpdateServerConfigurationAsync(ServerConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            var url = GetApiUrl("System/Configuration");

            return PostAsync<ServerConfiguration, EmptyRequestResult>(url, configuration);
        }

        /// <summary>
        /// Updates the scheduled task triggers.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="triggers">The triggers.</param>
        /// <returns>Task{RequestResult}.</returns>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public Task UpdateScheduledTaskTriggersAsync(Guid id, TaskTriggerInfo[] triggers)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            if (triggers == null)
            {
                throw new ArgumentNullException("triggers");
            }

            var url = GetApiUrl("ScheduledTasks/" + id + "/Triggers");

            return PostAsync<TaskTriggerInfo[], EmptyRequestResult>(url, triggers);
        }

        /// <summary>
        /// Gets the display preferences.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>Task{BaseItemDto}.</returns>
        public async Task<DisplayPreferences> GetDisplayPreferencesAsync(string id)
        {
            var url = GetApiUrl("DisplayPreferences/" + id);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<DisplayPreferences>(stream);
            }
        }

        /// <summary>
        /// Updates display preferences for a user
        /// </summary>
        /// <param name="displayPreferences">The display preferences.</param>
        /// <returns>Task{DisplayPreferences}.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public Task UpdateDisplayPreferencesAsync(DisplayPreferences displayPreferences)
        {
            if (displayPreferences == null)
            {
                throw new ArgumentNullException("displayPreferences");
            }

            var url = GetApiUrl("DisplayPreferences/" + displayPreferences.Id);

            return PostAsync<DisplayPreferences, EmptyRequestResult>(url, displayPreferences);
        }

        /// <summary>
        /// Posts a set of data to a url, and deserializes the return stream into T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="args">The args.</param>
        /// <returns>Task{``0}.</returns>
        public async Task<T> PostAsync<T>(string url, Dictionary<string, string> args)
            where T : class
        {
            url = AddDataFormat(url);

            // Create the post body
            var strings = args.Keys.Select(key => string.Format("{0}={1}", key, args[key]));
            var postContent = string.Join("&", strings.ToArray());

            const string contentType = "application/x-www-form-urlencoded";

            using (var stream = await HttpClient.PostAsync(url, contentType, postContent, CancellationToken.None).ConfigureAwait(false))
            {
                return DeserializeFromStream<T>(stream);
            }
        }

        /// <summary>
        /// Posts an object of type TInputType to a given url, and deserializes the response into an object of type TOutputType
        /// </summary>
        /// <typeparam name="TInputType">The type of the T input type.</typeparam>
        /// <typeparam name="TOutputType">The type of the T output type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="obj">The obj.</param>
        /// <returns>Task{``1}.</returns>
        private async Task<TOutputType> PostAsync<TInputType, TOutputType>(string url, TInputType obj)
            where TOutputType : class
        {
            url = AddDataFormat(url);

            const string contentType = "application/json";

            var postContent = SerializeToJson(obj);

            using (var stream = await HttpClient.PostAsync(url, contentType, postContent, CancellationToken.None).ConfigureAwait(false))
            {
                return DeserializeFromStream<TOutputType>(stream);
            }
        }

        /// <summary>
        /// This is a helper around getting a stream from the server that contains serialized data
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Task{Stream}.</returns>
        public Task<Stream> GetSerializedStreamAsync(string url)
        {
            url = AddDataFormat(url);

            return HttpClient.GetAsync(url, CancellationToken.None);
        }

        public async Task<NotificationsSummary> GetNotificationsSummary(string userId)
        {
            var url = GetApiUrl("Notifications/" + userId + "/Summary");

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<NotificationsSummary>(stream);
            }
        }

        public Task<Notification> AddNotification(Notification notification)
        {
            var url = GetApiUrl("Notifications/" + notification.UserId);

            return PostAsync<Notification, Notification>(url, notification);
        }

        public Task MarkNotificationsRead(string userId, IEnumerable<Guid> notificationIdList, bool isRead)
        {
            var url = GetApiUrl("Notifications/" + userId);

            url += isRead ? "/Read" : "/Unread";

            var dict = new QueryStringDictionary();

            var ids = notificationIdList.Select(i => i.ToString("N")).ToArray();

            dict.Add("Ids", string.Join(",", ids));

            url = GetApiUrl(url, dict);

            return PostAsync<EmptyRequestResult>(url, new Dictionary<string, string>());
        }

        public Task UpdateNotification(Notification notification)
        {
            var url = GetApiUrl("Notifications/" + notification.UserId + "/" + notification.Id);

            return PostAsync<Notification, EmptyRequestResult>(url, notification);
        }

        public async Task<NotificationResult> GetNotificationsAsync(NotificationQuery query)
        {
            var url = GetApiUrl("Notifications/" + query.UserId);

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("ItemIds", query.IsRead);
            dict.AddIfNotNull("StartIndex", query.StartIndex);
            dict.AddIfNotNull("Limit", query.Limit);

            url = GetApiUrl(url, dict);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<NotificationResult>(stream);
            }
        }


        public async Task<AllThemeMediaResult> GetAllThemeMediaAsync(string userId, string itemId, bool inheritFromParent)
        {
            var queryString = new QueryStringDictionary();

            queryString.Add("InheritFromParent", inheritFromParent);
            queryString.AddIfNotNullOrEmpty("UserId", userId);

            var url = GetApiUrl("Items/" + itemId + "/ThemeMedia", queryString);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<AllThemeMediaResult>(stream);
            }
        }

        public async Task<ThemeMediaResult> GetThemeSongsAsync(string userId, string itemId, bool inheritFromParent)
        {
            var queryString = new QueryStringDictionary();

            queryString.Add("InheritFromParent", inheritFromParent);
            queryString.AddIfNotNullOrEmpty("UserId", userId);

            var url = GetApiUrl("Items/" + itemId + "/ThemeSongs", queryString);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ThemeMediaResult>(stream);
            }
        }

        public async Task<ThemeMediaResult> GetThemeVideosAsync(string userId, string itemId, bool inheritFromParent)
        {
            var queryString = new QueryStringDictionary();

            queryString.Add("InheritFromParent", inheritFromParent);
            queryString.AddIfNotNullOrEmpty("UserId", userId);

            var url = GetApiUrl("Items/" + itemId + "/ThemeVideos", queryString);

            using (var stream = await GetSerializedStreamAsync(url).ConfigureAwait(false))
            {
                return DeserializeFromStream<ThemeMediaResult>(stream);
            }
        }
    }
}
