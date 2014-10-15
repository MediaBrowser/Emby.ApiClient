using MediaBrowser.ApiInteraction.Cryptography;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ConnectService
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IAsyncHttpClient _httpClient;
        private readonly ICredentialProvider _credentialProvider;
        private readonly ICryptographyProvider _cryptographyProvider;

        public ConnectService(IJsonSerializer jsonSerializer, ILogger logger, IAsyncHttpClient httpClient, ICredentialProvider credentialProvider, ICryptographyProvider cryptographyProvider)
        {
            _jsonSerializer = jsonSerializer;
            _logger = logger;
            _httpClient = httpClient;
            _credentialProvider = credentialProvider;
            _cryptographyProvider = cryptographyProvider;
        }

        public Task<ConnectAuthenticationResult> Authenticate(string username, string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password ?? string.Empty);

            var hash = BitConverter.ToString(_cryptographyProvider.CreateMD5(bytes))
                .Replace("-", string.Empty);

            var args = new Dictionary<string, string>
            {
                {"userName",username},
                {"password",hash}
            };

            return PostAsync<ConnectAuthenticationResult>(GetConnectUrl("user/authenticate"), args);
        }

        public Task Logout()
        {
            var args = new Dictionary<string, string>();

            return PostAsync<EmptyRequestResult>(GetConnectUrl("user/logout"), args, true);
        }

        public Task<PinCreationResult> CreatePin(string deviceId)
        {
            var args = new Dictionary<string, string>
            {
                {"deviceId",deviceId}
            };

            return PostAsync<PinCreationResult>(GetConnectUrl("pin"), args);
        }

        public async Task<PinStatusResult> GetPinStatus(PinCreationResult pin)
        {
            var dict = new QueryStringDictionary();

            dict.Add("deviceId", pin.DeviceId);
            dict.Add("pin", pin.Pin);

            var url = GetConnectUrl("pin") + "?" + dict.GetQueryString();

            using (var stream = await _httpClient.SendAsync(new HttpRequest
            {
                Method = "GET",
                Url = url

            }).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<PinStatusResult>(stream);
            }
        }

        public Task<PinExchangeResult> ExchangePin(PinCreationResult pin)
        {
            var args = new Dictionary<string, string>
            {
                {"deviceId",pin.DeviceId},
                {"pin",pin.Pin}
            };

            return PostAsync<PinExchangeResult>(GetConnectUrl("pin/authentiate"), args);
        }

        private async Task<T> PostAsync<T>(string url, Dictionary<string, string> args, bool addUserAccessToken = false)
            where T : class
        {
            // Create the post body
            var strings = args.Keys.Select(key => string.Format("{0}={1}", key, args[key]));
            var postContent = string.Join("&", strings.ToArray());

            const string contentType = "application/x-www-form-urlencoded";

            var request = new HttpRequest
            {
                Url = url,
                Method = "POST",
                RequestContentType = contentType,
                RequestContent = postContent
            };

            if (addUserAccessToken)
            {
                await AddUserAccessToken(request).ConfigureAwait(false);
            }

            using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<T>(stream);
            }
        }

        public async Task<ConnectUser> GetConnectUser(ConnectUserQuery query)
        {
            var dict = new QueryStringDictionary();

            if (!string.IsNullOrWhiteSpace(query.Id))
            {
                dict.Add("id", query.Id);
            }
            else if (!string.IsNullOrWhiteSpace(query.Name))
            {
                dict.Add("name", query.Name);
            }
            else if (!string.IsNullOrWhiteSpace(query.Email))
            {
                dict.Add("name", query.Email);
            }

            var url = GetConnectUrl("user") + "?" + dict.GetQueryString();

            var request = new HttpRequest
            {
                Method = "GET",
                Url = url
            };

            await AddUserAccessToken(request).ConfigureAwait(false);

            using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<ConnectUser>(stream);
            }
        }

        public async Task GetServers(string userId)
        {

        }

        private async Task AddUserAccessToken(HttpRequest request)
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            request.RequestHeaders["X-Connect-UserToken"] = credentials.ConnectAccessToken;
        }

        private string GetConnectUrl(string handler)
        {
            return "https://connect.mediabrowser.tv/service/" + handler;
        }
    }
}
