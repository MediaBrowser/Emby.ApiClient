using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ConnectionManager
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ICredentialProvider _credentialProvider;
        private readonly INetworkConnection _networkConnectivity;
        private readonly ILogger _logger;
        private readonly IServerLocator _serverDiscovery;
        private readonly IAsyncHttpClient _httpClient;

        public Dictionary<string, ApiClient> ApiClients { get; private set; }

        public string ActiveServerId { get; private set; }

        public string ApplicationName { get; private set; }
        public string ApplicationVersion { get; private set; }
        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public ClientCapabilities ClientCapabilities { get; private set; }

        public ConnectionManager(ILogger logger,
            IJsonSerializer jsonSerializer,
            ICredentialProvider credentialProvider,
            INetworkConnection networkConnectivity,
            IServerLocator serverDiscovery,
            IAsyncHttpClient httpClient,
            string applicationName,
            string applicationVersion,
            string deviceId,
            string deviceName,
            ClientCapabilities clientCapabilities)
        {
            _jsonSerializer = jsonSerializer;
            _credentialProvider = credentialProvider;
            _networkConnectivity = networkConnectivity;
            _logger = logger;
            _serverDiscovery = serverDiscovery;
            _httpClient = httpClient;
            ClientCapabilities = clientCapabilities;
            DeviceName = deviceName;
            DeviceId = deviceId;
            ApplicationVersion = applicationVersion;
            ApplicationName = applicationName;
            ApiClients = new Dictionary<string, ApiClient>(StringComparer.OrdinalIgnoreCase);
        }

        private ApiClient GetOrAddApiClient(ServerInfo server)
        {
            ApiClient apiClient;

            if (ApiClients.TryGetValue(server.Id, out apiClient))
            {
                return apiClient;
            }

            // TODO: Pass in both local + remote address

            apiClient = new ApiClient(_httpClient, _logger, server.LocalAddress, ApplicationName, DeviceName, DeviceId, ApplicationVersion, ClientCapabilities);

            ApiClients[server.Id] = apiClient;

            return apiClient;
        }

        private async Task<List<ServerInfo>> GetAvailableServers(CancellationToken cancellationToken)
        {
            return _credentialProvider.GetServers();
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

            var activeServerId = _credentialProvider.GetActiveServerId();

            // Try to connect to a server based on the list of saved servers
            var result = await Connect(servers, activeServerId, cancellationToken).ConfigureAwait(false);

            if (result.State != ConnectionState.Unavailable)
            {
                return result;
            }

            servers = await FindServers(cancellationToken).ConfigureAwait(false);

            return await Connect(servers, activeServerId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Loops through a list of servers and returns the first that is available for connection
        /// </summary>
        private async Task<ConnectionResult> Connect(List<ServerInfo> servers, string activeServerId, CancellationToken cancellationToken)
        {
            servers = servers
                .OrderBy(i => (string.Equals(i.Id, activeServerId, StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                .ToList();

            foreach (var server in servers)
            {
                var result = await Connect(server, cancellationToken).ConfigureAwait(false);

                if (result.State != ConnectionState.Unavailable)
                {
                    return result;
                }
            }

            return new ConnectionResult();
        }

        /// <summary>
        /// Attempts to connect to a server
        /// </summary>
        public async Task<ConnectionResult> Connect(ServerInfo server, CancellationToken cancellationToken)
        {
            var result = new ConnectionResult();

            PublicSystemInfo systemInfo = null;
            var isLocalConnection = true;

            if (!string.IsNullOrEmpty(server.LocalAddress) && _networkConnectivity.IsConnectedToLocalNetwork)
            {
                // Try to connect to the local address
                systemInfo = await TryConnect(server.LocalAddress, cancellationToken).ConfigureAwait(false);

                // If that failed, wake the device and retry
                if (systemInfo == null && server.MacAddresses.Count > 0)
                {
                    await WakeServer(server, cancellationToken).ConfigureAwait(false);
                    systemInfo = await TryConnect(server.LocalAddress, cancellationToken).ConfigureAwait(false);
                }
            }

            // If local connection is unavailable, try to connect to the remote address
            if (systemInfo == null && !string.IsNullOrEmpty(server.RemoteAddress))
            {
                systemInfo = await TryConnect(server.RemoteAddress, cancellationToken).ConfigureAwait(false);
                isLocalConnection = false;
            }

            if (systemInfo != null)
            {
                UpdateServerInfo(server, systemInfo);

                if (!string.IsNullOrWhiteSpace(server.AccessToken))
                {
                    await ValidateAuthentication(server, isLocalConnection, cancellationToken).ConfigureAwait(false);
                }

                _credentialProvider.AddOrUpdateServer(server);
                _credentialProvider.SetActiveServerId(server.Id);

                result.ApiClient = GetOrAddApiClient(server);
                result.State = ConnectionState.ServerSignIn;
            }

            return result;
        }

        private async Task ValidateAuthentication(ServerInfo server, bool isLocalConnection, CancellationToken cancellationToken)
        {
            var url = isLocalConnection ? server.LocalAddress : server.RemoteAddress;

            url += "/mediabrowser/system/info";

            var headers = new HttpHeaders();
            headers.SetAccessToken(server.AccessToken);

            try
            {
                using (var stream = await _httpClient.GetAsync(url, headers, cancellationToken).ConfigureAwait(false))
                {
                    var systemInfo = _jsonSerializer.DeserializeFromStream<SystemInfo>(stream);

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
            url += "/mediabrowser/system/info/public";

            try
            {
                using (var stream = await _httpClient.SendAsync(new HttpRequest
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    Timeout = 3000

                }).ConfigureAwait(false))
                {
                    return _jsonSerializer.DeserializeFromStream<PublicSystemInfo>(stream);
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
                server.MacAddresses = new List<string>();

                if (!string.IsNullOrEmpty(fullSystemInfo.MacAddress))
                {
                    server.MacAddresses.Add(fullSystemInfo.MacAddress);
                }
            }
        }

        /// <summary>
        /// Wakes a server
        /// </summary>
        private async Task WakeServer(ServerInfo server, CancellationToken cancellationToken)
        {
            foreach (var address in server.MacAddresses)
            {
                await WakeServer(address, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wakes a device based on mac address
        /// </summary>
        private async Task WakeServer(string macAddress, CancellationToken cancellationToken)
        {
            try
            {
                await _networkConnectivity.SendWakeOnLan(macAddress, cancellationToken).ConfigureAwait(false);
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
    }
}
