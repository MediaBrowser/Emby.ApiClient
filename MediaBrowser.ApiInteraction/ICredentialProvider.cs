using MediaBrowser.Model.ApiClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public interface ICredentialProvider
    {
        /// <summary>
        /// Gets the server credentials.
        /// </summary>
        /// <returns>ServerCredentialConfiguration.</returns>
        Task<ServerCredentialConfiguration> GetServerCredentials();

        /// <summary>
        /// Saves the server credentials.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        Task SaveServerCredentials(ServerCredentialConfiguration configuration);
    }

    public class ServerCredentialConfiguration
    {
        public string LastServerId { get; set; }
        public List<ServerInfo> Servers { get; set; }

        public ServerCredentialConfiguration()
        {
            Servers = new List<ServerInfo>();
        }

        public void AddOrUpdateServer(ServerInfo server)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }

            var list = Servers.ToList();

            var index = FindIndex(list, server.Id);

            if (index != -1)
            {
                list[index] = server;
            }
            else
            {
                list.Add(server);
            }

            Servers = list;
        }

        private int FindIndex(IEnumerable<ServerInfo> servers, string id)
        {
            var index = 0;

            foreach (var server in servers)
            {
                if (string.Equals(id, server.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

    }
}
