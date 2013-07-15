using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MediaBrowser.ApiInteraction.net35
{
    /// <summary>
    /// Class ApiClient
    /// </summary>
    public class ApiClient : BaseApiClient
    {
        /// <summary>
        /// The _HTTP client
        /// </summary>
        private readonly HttpClient _httpClient;

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
        /// Gets the server configuration.
        /// </summary>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void GetServerConfiguration(Action<ServerConfiguration> onSuccess, Action<Exception> onError)
        {
            var url = GetApiUrl("System/Configuration");

            GetSerializedData(url, onSuccess, onError);
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
        /// Authenticates a user and returns the result
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="sha1Hash">The sha1 hash.</param>
        /// <returns>Task.</returns>
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

        private void Post<TInputType, TOutputType>(string url, TInputType obj, Action<TOutputType> onSuccess, Action<Exception> onError)
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
        private void GetSerializedData<T>(string url, Action<T> onSuccess, Action<Exception> onError)
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
