using MediaBrowser.ApiInteraction.WebSocket;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ConnectionManager : IConnectionManager
    {
        public event EventHandler<GenericEventArgs<ConnectionResult>> Connected;

        private readonly ICredentialProvider _credentialProvider;
        private readonly INetworkConnection _networkConnectivity;
        private readonly ILogger _logger;
        private readonly IServerLocator _serverDiscovery;
        private readonly IAsyncHttpClient _httpClient;
        private readonly Func<IClientWebSocket> _webSocketFactory;

        public Dictionary<string, IApiClient> ApiClients { get; private set; }
        public IJsonSerializer JsonSerializer { get; set; }

        public string ApplicationName { get; private set; }
        public string ApplicationVersion { get; private set; }
        public IDevice Device { get; private set; }
        public ClientCapabilities ClientCapabilities { get; private set; }

        public ConnectionManager(ILogger logger,
            ICredentialProvider credentialProvider,
            INetworkConnection networkConnectivity,
            IServerLocator serverDiscovery,
            IHttpWebRequestFactory httpRequestFactory,
            string applicationName,
            string applicationVersion,
            IDevice device,
            ClientCapabilities clientCapabilities,
            Func<IClientWebSocket> webSocketFactory = null)
        {
            _credentialProvider = credentialProvider;
            _networkConnectivity = networkConnectivity;
            _logger = logger;
            _serverDiscovery = serverDiscovery;
            _httpClient = new HttpWebRequestClient(_logger, httpRequestFactory);
            ClientCapabilities = clientCapabilities;
            _webSocketFactory = webSocketFactory;
            Device = device;
            ApplicationVersion = applicationVersion;
            ApplicationName = applicationName;
            ApiClients = new Dictionary<string, IApiClient>(StringComparer.OrdinalIgnoreCase);

            Device.ResumeFromSleep += Device_ResumeFromSleep;
        }

        async void Device_ResumeFromSleep(object sender, EventArgs e)
        {
            await WakeAllServers(CancellationToken.None).ConfigureAwait(false);
        }

        private IApiClient GetOrAddApiClient(ServerInfo server)
        {
            IApiClient apiClient;

            if (ApiClients.TryGetValue(server.Id, out apiClient))
            {
                return apiClient;
            }

            apiClient = new ApiClient(_httpClient, _logger, server.LocalAddress, ApplicationName, Device.DeviceName, Device.DeviceId, ApplicationVersion, ClientCapabilities);

            ApiClients[server.Id] = apiClient;

            if (string.IsNullOrEmpty(server.AccessToken))
            {
                apiClient.ClearAuthenticationInfo();
            }
            else
            {
                apiClient.SetAuthenticationInfo(server.AccessToken, server.UserId);

                EnsureWebSocketIfConfigured(apiClient);
            }

            return apiClient;
        }

        private void EnsureWebSocketIfConfigured(IApiClient apiClient)
        {
            if (_webSocketFactory != null)
            {
                ((ApiClient)apiClient).OpenWebSocket(_webSocketFactory);
            }
        }

        private async Task<List<ServerInfo>> GetAvailableServers(CancellationToken cancellationToken)
        {
            return _credentialProvider.GetServerCredentials().Servers;
        }

        private async Task<List<ServerInfo>> FindServers(CancellationToken cancellationToken)
        {
            var servers = await _serverDiscovery.FindServers(2000, cancellationToken).ConfigureAwait(false);

            return servers.Select(i => new ServerInfo
            {
                Id = i.Id,
                LocalAddress = i.Address,
                Name = i.Name
            })
            .ToList();
        }

        public async Task<ConnectionResult> Connect(CancellationToken cancellationToken)
        {
            var servers = await GetAvailableServers(cancellationToken).ConfigureAwait(false);

            var lastServerId = _credentialProvider.GetServerCredentials().LastServerId;

            // Try to connect to a server based on the list of saved servers
            var result = await Connect(servers, lastServerId, cancellationToken).ConfigureAwait(false);

            if (result.State != ConnectionState.Unavailable)
            {
                return result;
            }

            servers = await FindServers(cancellationToken).ConfigureAwait(false);

            return await Connect(servers, lastServerId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loops through a list of servers and returns the first that is available for connection
        /// </summary>
        private async Task<ConnectionResult> Connect(List<ServerInfo> servers, string lastServerId, CancellationToken cancellationToken)
        {
            servers = servers
                .OrderBy(i => (string.Equals(i.Id, lastServerId, StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                .ToList();

            foreach (var server in servers)
            {
                var result = await Connect(server, cancellationToken).ConfigureAwait(false);

                if (result.State != ConnectionState.Unavailable)
                {
                    return result;
                }
            }

            return new ConnectionResult
            {
                State = ConnectionState.Unavailable
            };
        }

        /// <summary>
        /// Attempts to connect to a server
        /// </summary>
        public async Task<ConnectionResult> Connect(ServerInfo server, CancellationToken cancellationToken)
        {
            var result = new ConnectionResult();

            PublicSystemInfo systemInfo = null;
            var connectionMode = ConnectionMode.Local;

            if (!string.IsNullOrEmpty(server.LocalAddress) && _networkConnectivity.GetNetworkStatus().GetIsLocalNetworkAvailable())
            {
                // Try to connect to the local address
                systemInfo = await TryConnect(server.LocalAddress, cancellationToken).ConfigureAwait(false);

                // If that failed, wake the device and retry
                if (systemInfo == null && server.WakeOnLanInfos.Count > 0)
                {
                    await WakeServer(server, cancellationToken).ConfigureAwait(false);
                    systemInfo = await TryConnect(server.LocalAddress, cancellationToken).ConfigureAwait(false);
                }
            }

            // If local connection is unavailable, try to connect to the remote address
            if (systemInfo == null && !string.IsNullOrEmpty(server.RemoteAddress))
            {
                systemInfo = await TryConnect(server.RemoteAddress, cancellationToken).ConfigureAwait(false);
                connectionMode = ConnectionMode.Remote;
            }

            if (systemInfo != null)
            {
                UpdateServerInfo(server, systemInfo);

                if (!string.IsNullOrWhiteSpace(server.AccessToken))
                {
                    await ValidateAuthentication(server, connectionMode, cancellationToken).ConfigureAwait(false);
                }

                var credentials = _credentialProvider.GetServerCredentials();

                credentials.AddOrUpdateServer(server);
                credentials.LastServerId = server.Id;
                _credentialProvider.SaveServerCredentials(credentials);

                result.ApiClient = GetOrAddApiClient(server);
                result.State = string.IsNullOrEmpty(server.AccessToken) ?
                    ConnectionState.ServerSignIn :
                    ConnectionState.SignedIn;

                if (result.State == ConnectionState.SignedIn)
                {
                    EnsureWebSocketIfConfigured(result.ApiClient);
                }

                ((ApiClient)result.ApiClient).EnableAutomaticNetworking(server, connectionMode, _networkConnectivity);
            }

            return result;
        }

        private async Task ValidateAuthentication(ServerInfo server, ConnectionMode connectionMode, CancellationToken cancellationToken)
        {
            var url = connectionMode == ConnectionMode.Local ? server.LocalAddress : server.RemoteAddress;

            url += "/mediabrowser/system/info?format=json";

            var headers = new HttpHeaders();
            headers.SetAccessToken(server.AccessToken);

            try
            {
                using (var stream = await _httpClient.SendAsync(new HttpRequest
                {
                    CancellationToken = cancellationToken,
                    Method = "GET",
                    RequestHeaders = headers,
                    Url = url

                }).ConfigureAwait(false))
                {
                    var systemInfo = JsonSerializer.DeserializeFromStream<SystemInfo>(stream);

                    UpdateServerInfo(server, systemInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting response from " + url, ex);

                server.UserId = null;
                server.AccessToken = null;
            }
        }

        private async Task<PublicSystemInfo> TryConnect(string url, CancellationToken cancellationToken)
        {
            url += "/mediabrowser/system/info/public?format=json";

            try
            {
                using (var stream = await _httpClient.SendAsync(new HttpRequest
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    Timeout = 3000,
                    Method = "GET"

                }).ConfigureAwait(false))
                {
                    return JsonSerializer.DeserializeFromStream<PublicSystemInfo>(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting response from " + url, ex);

                return null;
            }
        }

        /// <summary>
        /// Updates the server information.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="systemInfo">The system information.</param>
        private void UpdateServerInfo(ServerInfo server, PublicSystemInfo systemInfo)
        {
            server.Name = systemInfo.ServerName;
            server.Id = systemInfo.Id;

            server.LocalAddress = systemInfo.LocalAddress;
            server.RemoteAddress = systemInfo.WanAddress;

            var fullSystemInfo = systemInfo as SystemInfo;

            if (fullSystemInfo != null)
            {
                server.WakeOnLanInfos = new List<WakeOnLanInfo>();

                if (!string.IsNullOrEmpty(fullSystemInfo.MacAddress))
                {
                    server.WakeOnLanInfos.Add(new WakeOnLanInfo
                    {
                        MacAddress = fullSystemInfo.MacAddress
                    });
                }
            }
        }

        /// <summary>
        /// Wakes all servers.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task WakeAllServers(CancellationToken cancellationToken)
        {
            foreach (var server in _credentialProvider.GetServerCredentials().Servers.ToList())
            {
                await WakeServer(server, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wakes a server
        /// </summary>
        private async Task WakeServer(ServerInfo server, CancellationToken cancellationToken)
        {
            foreach (var info in server.WakeOnLanInfos)
            {
                await WakeServer(info, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wakes a device based on mac address
        /// </summary>
        private async Task WakeServer(WakeOnLanInfo info, CancellationToken cancellationToken)
        {
            try
            {
                await _networkConnectivity.SendWakeOnLan(info.MacAddress, info.Port, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error sending wake on lan command", ex);
            }
        }

        public void Dispose()
        {
            foreach (var client in ApiClients.Values.ToList())
            {
                client.Dispose();
            }
        }

        public IApiClient GetApiClient(BaseItemDto item)
        {
            return GetApiClient("");
        }

        public IApiClient GetApiClient(string serverId)
        {
            return ApiClients.Values.FirstOrDefault();
        }

        public Task<ConnectionResult> Connect(string address, CancellationToken cancellationToken)
        {
            return Connect(new ServerInfo
            {
                RemoteAddress = NormalizeAddress(address)

            }, cancellationToken);
        }

        private string NormalizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentNullException("address");
            }

            if (!address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                address = "http://" + address;
            }

            return address;
        }

        public Task<AuthenticationResult> Authenticate(ServerInfo server, string username, byte[] hash, bool rememberLogin)
        {
            var client = GetApiClient(server.Id);

            return Authenticate(server, client, username, hash, rememberLogin);
        }

        public Task<AuthenticationResult> Authenticate(IApiClient apiClient, string username, byte[] hash, bool rememberLogin)
        {
            var server = ((ApiClient)apiClient).ServerInfo;

            return Authenticate(server, apiClient, username, hash, rememberLogin);
        }

        private async Task<AuthenticationResult> Authenticate(ServerInfo server, IApiClient apiClient, string username, byte[] hash, bool rememberLogin)
        {
            var result = await apiClient.AuthenticateUserAsync(username, hash).ConfigureAwait(false);
            var systeminfo = await apiClient.GetSystemInfoAsync().ConfigureAwait(false);

            UpdateServerInfo(server, systeminfo);

            var credentials = _credentialProvider.GetServerCredentials();
            credentials.LastServerId = server.Id;

            if (rememberLogin)
            {
                server.UserId = result.User.Id;
                server.AccessToken = result.AccessToken;
            }

            credentials.AddOrUpdateServer(server);
            _credentialProvider.SaveServerCredentials(credentials);

            EnsureWebSocketIfConfigured(apiClient);

            return result;
        }

        public async Task<ConnectionResult> Logout()
        {
            foreach (var client in ApiClients.Values.ToList())
            {
                if (!string.IsNullOrEmpty(client.AccessToken))
                {
                    await client.Logout().ConfigureAwait(false);
                }
            }

            var credentials = _credentialProvider.GetServerCredentials();

            foreach (var server in credentials.Servers)
            {
                server.AccessToken = null;
                server.UserId = null;
            }
            _credentialProvider.SaveServerCredentials(credentials);

            return await Connect(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
