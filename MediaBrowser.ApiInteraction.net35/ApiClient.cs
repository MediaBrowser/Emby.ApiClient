using MediaBrowser.ApiInteraction.WebSocket;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Notifications;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Search;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Users;
using MediaBrowser.Model.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MediaBrowser.ApiInteraction.net35
{
    /// <summary>
    /// Class ApiClient
    /// </summary>
    public class ApiClient : BaseApiClient, IApiClient
    {
        /// <summary>
        /// The _HTTP client
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Gets or sets the web socket connection.
        /// </summary>
        /// <value>The web socket connection.</value>
        public ApiWebSocket WebSocketConnection { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverApiPort">The server API port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        public ApiClient(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverApiPort, string clientName, string deviceName, string deviceId, string applicationVersion)
            : base(logger, jsonSerializer, serverHostName, serverApiPort, clientName, deviceName, deviceId, applicationVersion)
        {
            _httpClient = new HttpClient(logger);

            var param = AuthorizationParameter;

            if (!string.IsNullOrEmpty(param))
            {
                _httpClient.SetAuthorizationHeader(AuthorizationScheme, param);
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
            : this(new NullLogger(), new NewtonsoftJsonSerializer(), serverHostName, serverApiPort, clientName, deviceName, deviceId, applicationVersion)
        {
        }

        protected override void OnAuthorizationInfoChanged()
        {
            base.OnAuthorizationInfoChanged();
            _httpClient.SetAuthorizationHeader(AuthorizationScheme, AuthorizationParameter);
        }

        /// <summary>
        /// Gets the system info.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetSystemInfo(Action<SystemInfo> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("System/Info");

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the users.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetUsers(Action<UserDto[]> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("Users");

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the public users.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetPublicUsers(Action<UserDto[]> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("Users/Public");
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the user.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public void GetUser(string id, Action<UserDto> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("Users/" + id);

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the root folder.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void GetRootFolder(string userId, Action<BaseItemDto> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/Root");

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the item.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void GetItem(string id, string userId, Action<BaseItemDto> onSuccess, Action<Exception> onError)
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

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetItems(ItemQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetItemListUrl(query);

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the name of the item by.
        /// </summary>
        /// <param name="type">the plural type name ea. Artists, Genres, Studios</param>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetItemsByName(string type, ItemsByNameQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            string url = base.GetItemByNameListUrl(type, query);

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the genres.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetGenres(ItemsByNameQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            GetItemsByName("Genres", query, onSuccess, onError);
        }

        /// <summary>
        /// Gets the next up.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetNextUp(NextUpQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
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

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets people by query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetPeople(PersonsQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            var url = GetItemByNameListUrl("Persons", query);

            if (query.PersonTypes != null && query.PersonTypes.Length > 0)
            {
                url += "&PersonTypes=" + string.Join(",", query.PersonTypes);
            }

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets similar items.
        /// </summary>
        /// <param name="type">The type of item.</param>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetSimilarItems(string type, SimilarItemsQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            var url = GetSimilarItemListUrl(query, type);
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the studios.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public void GetStudios(ItemsByNameQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError)
        {
            GetItemsByName("Studios", query, onSuccess, onError);
        }

        /// <summary>
        /// Gets the server configuration.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetServerConfiguration(Action<ServerConfiguration> onSuccess, Action<Exception> onError)
        {
            GetOperation("System/Configuration", onSuccess, onError);
        }

        /// <summary>
        /// Authenticates the user.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="password">The password.</param>
        /// <param name="onResponse">the on response delegate, receives a true value when authentication was successful</param>
        public void AuthenticateUser(string userId, string password, Action<bool> onResponse)
        {
            using (var provider = SHA1.Create())
            {
                var hash = provider.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));
                AuthenticateUser(userId, hash, x => onResponse(true), x => onResponse(false));
            }
        }

        /// <summary>
        /// Authenticates the user by name.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">username</exception>
        public void AuthenticateByName(string username, byte[] sha1Hash, Action<AuthenticationResult> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException("username");
            }

            var password = BitConverter.ToString(sha1Hash).Replace("-", string.Empty);
            var url = GetApiUrl("Users/AuthenticateByName");

            var args = new Dictionary<string, string>();

            args["username"] = Uri.EscapeUriString(username);
            args["password"] = password;

            Post<AuthenticationResult>(url, args, onSuccess, onError);
        }

        /// <summary>
        /// Authenticates a user and returns the result
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        public void AuthenticateUser(string userId, byte[] sha1Hash, Action<EmptyRequestResult> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }
            var password = BitConverter.ToString(sha1Hash).Replace("-", string.Empty);
            var url = GetApiUrl("Users/" + userId + "/Authenticate");

            var args = new Dictionary<string, string>();
            args["password"] = password;

            Post<EmptyRequestResult>(url, args, onSuccess, onError);
        }

        /// <summary>
        /// Reports to the server that the user has begun playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="onResponse">The on response.</param>
        /// <exception cref="System.ArgumentNullException">
        /// itemId
        /// or
        /// userId
        /// </exception>
        public void ReportPlaybackStart(string itemId, string userId, Action<bool> onResponse)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (WebSocketConnection != null && WebSocketConnection.IsOpen)
            {
                WebSocketConnection.Send("PlaybackStart", itemId);
            }

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>(), x => onResponse(true), x => onResponse(false));
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
        public void ReportPlaybackProgress(string itemId, string userId, long? positionTicks, bool isPaused, bool isMuted, Action<bool> onResponse)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (WebSocketConnection != null && WebSocketConnection.IsOpen)
            {
                WebSocketConnection.Send("PlaybackProgress", itemId + "|" + (positionTicks == null ? "" : positionTicks.Value.ToString(CultureInfo.InvariantCulture)) + "|" + isPaused.ToString().ToLower() + "|" + isMuted.ToString().ToLower());
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);
            dict.Add("isPaused", isPaused);
            dict.Add("isMuted", isMuted);

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId + "/Progress", dict);

            Post<EmptyRequestResult>(url, new Dictionary<string, string>(), x => onResponse(true), x => onResponse(false));
        }

        /// <summary>
        /// Reports to the server that the user has stopped playing an item
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="positionTicks">The position ticks.</param>
        /// <returns>Task{UserItemDataDto}.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        public void ReportPlaybackStopped(string itemId, string userId, long? positionTicks, Action<bool> onResponse)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }

            if (WebSocketConnection != null && WebSocketConnection.IsOpen)
            {
                WebSocketConnection.Send("PlaybackStopped", itemId + "|" + (positionTicks == null ? "" : positionTicks.Value.ToString(CultureInfo.InvariantCulture)));
            }

            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("positionTicks", positionTicks);

            var url = GetApiUrl("Users/" + userId + "/PlayingItems/" + itemId, dict);

            _httpClient.Delete(url, x => onResponse(false));
        }

        /// <summary>
        /// Restarts the server.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void RestartServer(Action<EmptyRequestResult> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("System/Restart");

            Post(url, new QueryStringDictionary(), onSuccess, onError);
        }

        /// <summary>
        /// Gets the installed plugins.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetInstalledPlugins(Action<PluginInfo[]> onSuccess, Action<Exception> onError)
        {
            GetOperation("Plugins", onSuccess, onError);
        }

        /// <summary>
        /// Gets the scheduled tasks.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetScheduledTasks(Action<TaskInfo[]> onSuccess, Action<Exception> onError)
        {
            GetOperation("ScheduledTasks", onSuccess, onError);
        }


        /// <summary>
        /// Gets the operation.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="operation">The operation.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        protected void GetOperation<TResponse>(string operation, Action<TResponse> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl(operation);

            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the scheduled task.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">id</exception>
        public void GetScheduledTask(Guid id, Action<TaskInfo> onSuccess, Action<Exception> onError)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            var url = GetApiUrl("ScheduledTasks/" + id);
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the parental ratings.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetParentalRatings(Action<List<ParentalRating>> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("Localization/ParentalRatings");
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the local trailers.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetLocalTrailers(string userId, string itemId, Action<BaseItemDto[]> onSuccess, Action<Exception> onError)
        {
            GetExtras("LocalTrailers", userId, itemId, onSuccess, onError);
        }

        /// <summary>
        /// Gets the special features.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetSpecialFeatures(string userId, string itemId, Action<BaseItemDto[]> onSuccess, Action<Exception> onError)
        {
            GetExtras("SpecialFeatures", userId, itemId, onSuccess, onError);
        }

        /// <summary>
        /// Gets the extras.
        /// </summary>
        /// <param name="extraType">Type of the extra.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">
        /// userId
        /// or
        /// itemId
        /// </exception>
        private void GetExtras(string extraType, string userId, string itemId, Action<BaseItemDto[]> onSuccess, Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException("userId");
            }
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException("itemId");
            }

            var url = GetApiUrl("Users/" + userId + "/Items/" + itemId + "/" + extraType);
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the cultures.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetCultures(Action<CultureDto[]> onSuccess, Action<Exception> onError)
        {
            GetOperation("Localization/Cultures", onSuccess, onError);
        }

        /// <summary>
        /// Gets the countries.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetCountries(Action<CountryInfo[]> onSuccess, Action<Exception> onError)
        {
            GetOperation("Localization/Countries", onSuccess, onError);
        }

        /// <summary>
        /// Gets the notifications summary.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetNotificationsSummary(string userId, Action<NotificationsSummary> onSuccess, Action<Exception> onError)
        {
            GetOperation("Notifications/" + userId + "/Summary", onSuccess, onError);
        }

        /// <summary>
        /// Gets the notifications.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetNotifications(NotificationQuery query, Action<NotificationResult> onSuccess, Action<Exception> onError)
        {
            var dict = new QueryStringDictionary();
            dict.AddIfNotNull("ItemIds", query.IsRead);
            dict.AddIfNotNull("StartIndex", query.StartIndex);
            dict.AddIfNotNull("Limit", query.Limit);

            var url = GetApiUrl("Notifications/" + query.UserId, dict);
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Gets the search hints.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="searchTerm">The search term.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="limit">The limit.</param>
        /// <exception cref="System.ArgumentNullException">searchTerm</exception>
        public void GetSearchHints(string userId, string searchTerm, Action<SearchHintResult> onSuccess, Action<Exception> onError, int? startIndex = null, int? limit = null)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                throw new ArgumentNullException("searchTerm");
            }

            var queryString = new QueryStringDictionary();

            queryString.Add("searchTerm", searchTerm);
            queryString.AddIfNotNullOrEmpty("UserId", userId);
            queryString.AddIfNotNull("startIndex", startIndex);
            queryString.AddIfNotNull("limit", limit);

            var url = GetApiUrl("Search/Hints", queryString);
            
            GetSerializedData(url, onSuccess, onError);
        }

        /// <summary>
        /// Marks the played.
        /// </summary>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="datePlayed">The date played.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">
        /// itemId
        /// or
        /// userId
        /// </exception>
        public void MarkPlayed(string itemId, string userId, DateTime? datePlayed, Action<UserItemDataDto> onSuccess, Action<Exception> onError)
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
            //dict.AddIfNotNull("DatePlayed", datePlayed);
            var url = GetApiUrl("Users/" + userId + "/PlayedItems/" + itemId, dict);

            Post(url, new Dictionary<string, string>(), onSuccess, onError);
        }

        /// <summary>
        /// Marks the unplayed.
        /// </summary>
        /// <param name="itemId">The item identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="onResponse">The on response.</param>
        /// <exception cref="System.ArgumentNullException">
        /// itemId
        /// or
        /// userId
        /// </exception>
        public void MarkUnplayed(string itemId, string userId, Action<bool> onResponse)
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
            _httpClient.Delete(url, x => onResponse(false)); // todo: response object
        }

        /// <summary>
        /// Posts the specified URL.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void Post<T>(string url, Dictionary<string, string> args, Action<T> onSuccess, Action<Exception> onError)
           where T : class
        {
            url = AddDataFormat(url);

            // Create the post body
            var strings = args.Keys.Select(key => string.Format("{0}={1}", key, args[key]));
            var postContent = string.Join("&", strings.ToArray());

            const string contentType = "application/x-www-form-urlencoded";
            _httpClient.Post(url, contentType, postContent, (stream) =>
            {
                T data;
                try
                {
                    data = JsonSerializer.DeserializeFromStream<T>(stream);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error deserializing data from {0}", ex, url);
                    onError(ex);
                    return;
                }
                onSuccess(data);
            }, onError);
        }

        /// <summary>
        /// Posts the specified URL.
        /// </summary>
        /// <typeparam name="TInputType">The type of the input type.</typeparam>
        /// <typeparam name="TOutputType">The type of the output type.</typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="obj">The object.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        protected void Post<TInputType, TOutputType>(string url, TInputType obj, Action<TOutputType> onSuccess, Action<Exception> onError)
            where TOutputType : class
        {
            url = AddDataFormat(url);

            const string contentType = "application/json";

            var postContent = SerializeToJson(obj);

            _httpClient.Post(url, contentType, postContent, (stream) =>
            {
                TOutputType data;
                try
                {
                    data = JsonSerializer.DeserializeFromStream<TOutputType>(stream);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error deserializing data from {0}", ex, url);
                    onError(ex);
                    return;
                }
                onSuccess(data);
            }, onError);
        }

        /// <summary>
        /// Gets the serialized data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The URL.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        /// <exception cref="System.ArgumentNullException">
        /// onSuccess
        /// or
        /// onError
        /// </exception>
        protected void GetSerializedData<T>(string url, Action<T> onSuccess, Action<Exception> onError)
        {
            if (onSuccess == null)
            {
                throw new ArgumentNullException("onSuccess");
            }

            if (onError == null)
            {
                throw new ArgumentNullException("onError");
            }

            url = AddDataFormat(url);

            _httpClient.Get(url, stream =>
            {
                T data;

                try
                {
                    data = JsonSerializer.DeserializeFromStream<T>(stream);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error deserializing data from {0}", ex, url);

                    onError(ex);

                    return;
                }

                onSuccess(data);

            }, onError);
        }
    }
}
