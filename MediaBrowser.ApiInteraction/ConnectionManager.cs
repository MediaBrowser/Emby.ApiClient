using MediaBrowser.ApiInteraction.Cryptography;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Connect;
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
        public event EventHandler<GenericEventArgs<UserDto>> LocalUserSignIn;
        public event EventHandler<GenericEventArgs<ConnectUser>> ConnectUserSignIn;
        public event EventHandler<EventArgs> LocalUserSignOut;
        public event EventHandler<EventArgs> ConnectUserSignOut;
        public event EventHandler<EventArgs> RemoteLoggedOut;

        public event EventHandler<GenericEventArgs<ConnectionResult>> Connected;

        private readonly ICredentialProvider _credentialProvider;
        private readonly INetworkConnection _networkConnectivity;
        private readonly ILogger _logger;
        private readonly IServerLocator _serverDiscovery;
        private readonly IAsyncHttpClient _httpClient;
        private readonly Func<IClientWebSocket> _webSocketFactory;
        private readonly ICryptographyProvider _cryptographyProvider;

        public Dictionary<string, IApiClient> ApiClients { get; private set; }

        public string ApplicationName { get; private set; }
        public string ApplicationVersion { get; private set; }
        public IDevice Device { get; private set; }
        public ClientCapabilities ClientCapabilities { get; private set; }

        public IApiClient CurrentApiClient { get; private set; }

        private readonly ConnectService _connectService;

        public ConnectUser ConnectUser { get; private set; }

        public ConnectionManager(ILogger logger,
            ICredentialProvider credentialProvider,
            INetworkConnection networkConnectivity,
            IServerLocator serverDiscovery,
            string applicationName,
            string applicationVersion,
            IDevice device,
            ClientCapabilities clientCapabilities,
            ICryptographyProvider cryptographyProvider,
            Func<IClientWebSocket> webSocketFactory = null)
        {
            _credentialProvider = credentialProvider;
            _networkConnectivity = networkConnectivity;
            _logger = logger;
            _serverDiscovery = serverDiscovery;
            _httpClient = AsyncHttpClientFactory.Create(logger);
            ClientCapabilities = clientCapabilities;
            _webSocketFactory = webSocketFactory;
            _cryptographyProvider = cryptographyProvider;
            Device = device;
            ApplicationVersion = applicationVersion;
            ApplicationName = applicationName;
            ApiClients = new Dictionary<string, IApiClient>(StringComparer.OrdinalIgnoreCase);
            SaveLocalCredentials = true;

            Device.ResumeFromSleep += Device_ResumeFromSleep;

            var jsonSerializer = new NewtonsoftJsonSerializer();
            _connectService = new ConnectService(jsonSerializer, _logger, _httpClient, _cryptographyProvider);
        }

        public IJsonSerializer JsonSerializer
        {
            get { return _connectService.JsonSerializer; }
            set { _connectService.JsonSerializer = value; }
        }

        public bool SaveLocalCredentials { get; set; }

        async void Device_ResumeFromSleep(object sender, EventArgs e)
        {
            await WakeAllServers(CancellationToken.None).ConfigureAwait(false);
        }

        private IApiClient GetOrAddApiClient(ServerInfo server, ConnectionMode connectionMode)
        {
            IApiClient apiClient;

            if (!ApiClients.TryGetValue(server.Id, out apiClient))
            {
                var address = connectionMode == ConnectionMode.Local ? server.LocalAddress : server.RemoteAddress;

                apiClient = new ApiClient(_logger, address, ApplicationName, Device, ApplicationVersion,
                    ClientCapabilities, _cryptographyProvider)
                {
                    JsonSerializer = JsonSerializer
                };

                ApiClients[server.Id] = apiClient;

                apiClient.Authenticated += apiClient_Authenticated;
            }

            if (string.IsNullOrEmpty(server.AccessToken))
            {
                apiClient.ClearAuthenticationInfo();
            }
            else
            {
                apiClient.SetAuthenticationInfo(server.AccessToken, server.UserId);
            }

            return apiClient;
        }

        void apiClient_Authenticated(object sender, GenericEventArgs<AuthenticationResult> e)
        {
            OnAuthenticated(sender as IApiClient, e.Argument, SaveLocalCredentials);
        }

        private void EnsureWebSocketIfConfigured(IApiClient apiClient)
        {
            if (_webSocketFactory != null)
            {
                ((ApiClient)apiClient).OpenWebSocket(_webSocketFactory);
            }
        }

        public async Task<List<ServerInfo>> GetAvailableServers(CancellationToken cancellationToken)
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            _logger.Debug("{0} servers in saved credentials", credentials.Servers.Count);

            if (_networkConnectivity.GetNetworkStatus().GetIsLocalNetworkAvailable())
            {
                foreach (var server in await FindServers(cancellationToken).ConfigureAwait(false))
                {
                    credentials.AddOrUpdateServer(server);
                }
            }

            if (!string.IsNullOrWhiteSpace(credentials.ConnectAccessToken))
            {
                await EnsureConnectUser(credentials, cancellationToken).ConfigureAwait(false);

                foreach (var server in await GetConnectServers(credentials.ConnectUserId, credentials.ConnectAccessToken, cancellationToken).ConfigureAwait(false))
                {
                    credentials.AddOrUpdateServer(server);
                }
            }

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            return credentials.Servers.ToList();
        }

        private async Task<IEnumerable<ServerInfo>> GetConnectServers(string userId, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                var servers = await _connectService.GetServers(userId, accessToken, cancellationToken).ConfigureAwait(false);

                _logger.Debug("User has {0} connect servers", servers.Length);

                return servers.Select(i => new ServerInfo
                {
                    ExchangeToken = i.AccessKey,
                    Id = i.SystemId,
                    Name = i.Name,
                    RemoteAddress = i.Url,
                    LocalAddress = i.LocalAddress,
                    UserLinkType = string.Equals(i.UserType, "guest", StringComparison.OrdinalIgnoreCase) ? UserLinkType.Guest : UserLinkType.LinkedUser
                });
            }
            catch
            {
                return new List<ServerInfo>();
            }
        }

        private async Task<List<ServerInfo>> FindServers(CancellationToken cancellationToken)
        {
            List<ServerDiscoveryInfo> servers;

            try
            {
                servers = await _serverDiscovery.FindServers(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("No servers found via local discovery.");

                servers = new List<ServerDiscoveryInfo>();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error discovering servers.", ex);

                servers = new List<ServerDiscoveryInfo>();
            }

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

            return await Connect(servers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loops through a list of servers and returns the first that is available for connection
        /// </summary>
        private async Task<ConnectionResult> Connect(List<ServerInfo> servers, CancellationToken cancellationToken)
        {
            servers = servers
               .OrderByDescending(i => i.DateLastAccessed)
               .ToList();

            if (servers.Count == 1)
            {
                _logger.Debug("1 server in the list.");

                var result = await Connect(servers[0], cancellationToken).ConfigureAwait(false);

                if (result.State == ConnectionState.Unavailable)
                {
                    result.State = result.ConnectUser == null ?
                        ConnectionState.ConnectSignIn :
                        ConnectionState.ServerSelection;
                }

                return result;
            }

            foreach (var server in servers)
            {
                // If it has saved credentials, try to use that
                if (!string.IsNullOrEmpty(server.AccessToken))
                {
                    var result = await Connect(server, cancellationToken).ConfigureAwait(false);

                    if (result.State == ConnectionState.SignedIn)
                    {
                        return result;
                    }
                }
            }

            var finalResult = new ConnectionResult
            {
                Servers = servers,
                ConnectUser = ConnectUser
            };

            if (finalResult.State != ConnectionState.SignedIn)
            {
                finalResult.State = servers.Count == 0 && finalResult.ConnectUser == null ?
                    ConnectionState.ConnectSignIn :
                    ConnectionState.ServerSelection;
            }

            return finalResult;
        }

        /// <summary>
        /// Attempts to connect to a server
        /// </summary>
        public async Task<ConnectionResult> Connect(ServerInfo server, CancellationToken cancellationToken)
        {
            var result = new ConnectionResult
            {
                State = ConnectionState.Unavailable
            };

            PublicSystemInfo systemInfo = null;
            var connectionMode = ConnectionMode.Local;

            if (!string.IsNullOrEmpty(server.LocalAddress) && _networkConnectivity.GetNetworkStatus().GetIsLocalNetworkAvailable())
            {
                _logger.Debug("Connecting to local server address...");

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
                _logger.Debug("Connecting to remote server address...");

                systemInfo = await TryConnect(server.RemoteAddress, cancellationToken).ConfigureAwait(false);
                connectionMode = ConnectionMode.Remote;
            }

            if (systemInfo != null)
            {
                server.ImportInfo(systemInfo);

                var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(credentials.ConnectAccessToken))
                {
                    await EnsureConnectUser(credentials, cancellationToken).ConfigureAwait(false);

                    await AddAuthenticationInfoFromConnect(server, connectionMode, credentials, cancellationToken).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(server.AccessToken))
                {
                    await ValidateAuthentication(server, connectionMode, cancellationToken).ConfigureAwait(false);
                }

                credentials.AddOrUpdateServer(server);
                server.DateLastAccessed = DateTime.UtcNow;

                await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

                result.ApiClient = GetOrAddApiClient(server, connectionMode);
                result.State = string.IsNullOrEmpty(server.AccessToken) ?
                    ConnectionState.ServerSignIn :
                    ConnectionState.SignedIn;

                ((ApiClient)result.ApiClient).EnableAutomaticNetworking(server, connectionMode, _networkConnectivity);

                if (result.State == ConnectionState.SignedIn)
                {
                    EnsureWebSocketIfConfigured(result.ApiClient);
                }

                CurrentApiClient = result.ApiClient;

                result.Servers.Add(server);

                if (Connected != null)
                {
                    Connected(this, new GenericEventArgs<ConnectionResult>(result));
                }
            }

            result.ConnectUser = ConnectUser;
            return result;
        }

        private async Task AddAuthenticationInfoFromConnect(ServerInfo server,
            ConnectionMode connectionMode,
            ServerCredentials credentials,
            CancellationToken cancellationToken)
        {
            _logger.Debug("Adding authentication info from Connect");

            var url = connectionMode == ConnectionMode.Local ? server.LocalAddress : server.RemoteAddress;

            url += "/mediabrowser/Connect/Exchange?format=json&ConnectUserId=" + credentials.ConnectUserId;

            var headers = new HttpHeaders();
            headers.SetAccessToken(server.ExchangeToken);

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
                    var auth = JsonSerializer.DeserializeFromStream<ConnectAuthenticationExchangeResult>(stream);

                    server.UserId = auth.LocalUserId;
                    server.AccessToken = auth.AccessToken;
                }
            }
            catch (Exception ex)
            {
                // Already logged at a lower level

                server.UserId = null;
                server.AccessToken = null;
            }
        }

        private async Task EnsureConnectUser(ServerCredentials credentials, CancellationToken cancellationToken)
        {
            if (ConnectUser != null && string.Equals(ConnectUser.Id, credentials.ConnectUserId, StringComparison.Ordinal))
            {
                return;
            }

            ConnectUser = null;

            if (!string.IsNullOrWhiteSpace(credentials.ConnectUserId) && !string.IsNullOrWhiteSpace(credentials.ConnectAccessToken))
            {
                try
                {
                    ConnectUser = await _connectService.GetConnectUser(new ConnectUserQuery
                    {
                        Id = credentials.ConnectUserId

                    }, credentials.ConnectAccessToken, cancellationToken).ConfigureAwait(false);

                    OnConnectUserSignIn(ConnectUser);
                }
                catch
                {
                    // Already logged at lower levels
                }
            }
        }

        private async Task ValidateAuthentication(ServerInfo server, ConnectionMode connectionMode, CancellationToken cancellationToken)
        {
            _logger.Debug("Validating saved authentication");

            var url = connectionMode == ConnectionMode.Local ? server.LocalAddress : server.RemoteAddress;

            var headers = new HttpHeaders();
            headers.SetAccessToken(server.AccessToken);

            var request = new HttpRequest
            {
                CancellationToken = cancellationToken,
                Method = "GET",
                RequestHeaders = headers,
                Url = url + "/mediabrowser/system/info?format=json"
            };

            try
            {
                using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    var systemInfo = JsonSerializer.DeserializeFromStream<SystemInfo>(stream);

                    server.ImportInfo(systemInfo);
                }

                if (!string.IsNullOrEmpty(server.UserId))
                {
                    request.Url = url + "/mediabrowser/users/" + server.UserId + "?format=json";

                    using (var stream = await _httpClient.SendAsync(request).ConfigureAwait(false))
                    {
                        var localUser = JsonSerializer.DeserializeFromStream<UserDto>(stream);

                        OnLocalUserSignIn(localUser);
                    }
                }
            }
            catch (Exception ex)
            {
                // Already logged at a lower level

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
                    Timeout = 2000,
                    Method = "GET"

                }).ConfigureAwait(false))
                {
                    return JsonSerializer.DeserializeFromStream<PublicSystemInfo>(stream);
                }
            }
            catch (Exception ex)
            {
                // Already logged at a lower level

                return null;
            }
        }

        /// <summary>
        /// Wakes all servers.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task WakeAllServers(CancellationToken cancellationToken)
        {
            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            foreach (var server in credentials.Servers.ToList())
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

        public IApiClient GetApiClient(IHasServerId item)
        {
            return GetApiClient(item.ServerId);
        }

        public IApiClient GetApiClient(string serverId)
        {
            return ApiClients.Values.OfType<ApiClient>().FirstOrDefault(i => string.Equals(i.ServerInfo.Id, serverId, StringComparison.OrdinalIgnoreCase)) ?? CurrentApiClient;
        }

        public async Task<ConnectionResult> Connect(string address, CancellationToken cancellationToken)
        {
            address = NormalizeAddress(address);

            var publicInfo = await TryConnect(address, cancellationToken).ConfigureAwait(false);

            if (publicInfo == null)
            {
                return new ConnectionResult
                {
                    State = ConnectionState.Unavailable,
                    ConnectUser = ConnectUser
                };
            }

            var server = new ServerInfo();

            server.ImportInfo(publicInfo);

            return await Connect(server, cancellationToken).ConfigureAwait(false);
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

        private async void OnAuthenticated(IApiClient apiClient,
            AuthenticationResult result,
            bool saveCredentials)
        {
            var systeminfo = await apiClient.GetSystemInfoAsync().ConfigureAwait(false);

            var server = ((ApiClient)apiClient).ServerInfo;
            server.ImportInfo(systeminfo);

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            server.DateLastAccessed = DateTime.UtcNow;

            if (saveCredentials)
            {
                server.UserId = result.User.Id;
                server.AccessToken = result.AccessToken;
            }
            else
            {
                server.UserId = null;
                server.AccessToken = null;
            }

            credentials.AddOrUpdateServer(server);
            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            EnsureWebSocketIfConfigured(apiClient);

            OnLocalUserSignIn(result.User);
        }

        private void OnLocalUserSignIn(UserDto user)
        {

        }

        private void OnConnectUserSignIn(ConnectUser user)
        {
            ConnectUser = user;

            if (ConnectUserSignIn != null)
            {
                ConnectUserSignIn(this, new GenericEventArgs<ConnectUser>(ConnectUser));
            }
        }

        public async Task Logout()
        {
            foreach (var client in ApiClients.Values.ToList())
            {
                if (!string.IsNullOrEmpty(client.AccessToken))
                {
                    await client.Logout().ConfigureAwait(false);
                }
            }

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            var servers = credentials.Servers
                .Where(i => !i.UserLinkType.HasValue || i.UserLinkType.Value != UserLinkType.Guest)
                .ToList();

            foreach (var server in servers)
            {
                server.AccessToken = null;
                server.UserId = null;
                server.ExchangeToken = null;
            }

            credentials.Servers = servers;
            credentials.ConnectAccessToken = null;
            credentials.ConnectUserId = null;

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            ConnectUser = null;

            if (ConnectUserSignOut != null)
            {
                ConnectUserSignOut(this, EventArgs.Empty);
            }
        }

        public async Task LoginToConnect(string username, string password)
        {
            var result = await _connectService.Authenticate(username, password).ConfigureAwait(false);

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            credentials.ConnectAccessToken = result.AccessToken;
            credentials.ConnectUserId = result.User.Id;

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);

            OnConnectUserSignIn(result.User);
        }

        public Task<PinCreationResult> CreatePin()
        {
            return _connectService.CreatePin(Device.DeviceId);
        }

        public Task<PinStatusResult> GetPinStatus(PinCreationResult pin)
        {
            return _connectService.GetPinStatus(pin);
        }

        public async Task ExchangePin(PinCreationResult pin)
        {
            var result = await _connectService.ExchangePin(pin);

            var credentials = await _credentialProvider.GetServerCredentials().ConfigureAwait(false);

            credentials.ConnectAccessToken = result.AccessToken;
            credentials.ConnectUserId = result.UserId;

            await EnsureConnectUser(credentials, CancellationToken.None).ConfigureAwait(false);

            await _credentialProvider.SaveServerCredentials(credentials).ConfigureAwait(false);
        }
    }
}
