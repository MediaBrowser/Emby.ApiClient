using MediaBrowser.ApiInteraction.Cryptography;
using MediaBrowser.Model.Connect;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ConnectService
    {
        internal IJsonSerializer JsonSerializer { get; set; }
        private readonly ILogger _logger;
        private readonly IAsyncHttpClient _httpClient;
        private readonly ICredentialProvider _credentialProvider;
        private readonly ICryptographyProvider _cryptographyProvider;

        public ConnectService(IJsonSerializer jsonSerializer, ILogger logger, IAsyncHttpClient httpClient, ICredentialProvider credentialProvider, ICryptographyProvider cryptographyProvider)
        {
            JsonSerializer = jsonSerializer;
            _logger = logger;
            _httpClient = httpClient;
            _credentialProvider = credentialProvider;
            _cryptographyProvider = cryptographyProvider;
        }

        public Task<ConnectAuthenticationResult> Authenticate(string username, string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password ?? string.Empty);

            bytes = _cryptographyProvider.CreateMD5(bytes);

            var hash = BitConverter.ToString(bytes, 0, bytes.Length).Replace("-", string.Empty);

            var args = new Dictionary<string, string>
            {
                {"userName",username},
                {"password",hash}
            };

            return PostAsync<ConnectAuthenticationResult>(GetConnectUrl("user/authenticate"), args);
        }

        public Task Logout()
        {
            var args = new Dictionary<string, string>
            {
            };

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
                return JsonSerializer.DeserializeFromStream<PinStatusResult>(stream);
            }
        }

        public Task<PinExchangeResult> ExchangePin(PinCreationResult pin)
        {
            var args = new Dictionary<string, string>
            {
                {"deviceId",pin.DeviceId},
                {"pin",pin.Pin}
            };

            return PostAsync<PinExchangeResult>(GetConnectUrl("pin/authenticate"), args);
        }

        private async Task<T> PostAsync<T>(string url, Dictionary<string, string> args, bool addUserAccessToken = false)
            where T : class
        {
            var request = new HttpRequest
            {
                Url = url,
                Method = "POST"
            };

            request.SetPostData(args);

            if (addUserAccessToken)
            {
                await AddUserAccessToken(request).ConfigureAwait(false);
            }

            using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return JsonSerializer.DeserializeFromStream<T>(stream);
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
                return JsonSerializer.DeserializeFromStream<ConnectUser>(stream);
            }
        }

        public async Task<ConnectUserServer[]> GetServers(string userId, CancellationToken cancellationToken)
        {
            var dict = new QueryStringDictionary();

            dict.Add("userId", userId);

            var url = GetConnectUrl("servers") + "?" + dict.GetQueryString();

            var request = new HttpRequest
            {
                Method = "GET",
                Url = url,
                CancellationToken = cancellationToken
            };

            await AddUserAccessToken(request).ConfigureAwait(false);

            using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return JsonSerializer.DeserializeFromStream<ConnectUserServer[]>(stream);
            }
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
