using MediaBrowser.Model.ApiClient;
using System;
using System.Collections.Generic;

namespace MediaBrowser.ApiInteraction
{
    public interface ICredentialProvider
    {
        /// <summary>
        /// Gets the servers.
        /// </summary>
        /// <returns>List&lt;ServerInfo&gt;.</returns>
        List<ServerInfo> GetServers();

        /// <summary>
        /// Adds the or update server.
        /// </summary>
        /// <param name="server">The server.</param>
        void AddOrUpdateServer(ServerInfo server);

        /// <summary>
        /// Removes the server.
        /// </summary>
        /// <param name="server">The server.</param>
        void RemoveServer(ServerInfo server);

        /// <summary>
        /// Gets the active server identifier.
        /// </summary>
        /// <returns>String.</returns>
        String GetActiveServerId();

        /// <summary>
        /// Sets the active server identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        void SetActiveServerId(string id);
    }
}
