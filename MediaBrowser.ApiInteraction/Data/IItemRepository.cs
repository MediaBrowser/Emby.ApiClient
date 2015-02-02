using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Sync;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public interface IItemRepository
    {
        /// <summary>
        /// Adds the or update.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>Task.</returns>
        Task AddOrUpdate(LocalItem item);

        /// <summary>
        /// Gets the item.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Task&lt;BaseItemDto&gt;.</returns>
        Task<LocalItem> Get(string id);

        /// <summary>
        /// Deletes the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Task.</returns>
        Task Delete(string id);

        /// <summary>
        /// Gets the server item ids.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <returns>Task&lt;List&lt;System.String&gt;&gt;.</returns>
        Task<List<string>> GetServerItemIds(string serverId);

        /// <summary>
        /// Queries all items for a server Id and returns a list of unique item types.
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <returns>Task&lt;List&lt;System.String&gt;&gt;.</returns>
        Task<List<string>> GetItemTypes(string serverId);

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Task&lt;List&lt;LocalItem&gt;&gt;.</returns>
        Task<List<LocalItem>> GetItems(LocalItemQuery query);

        /// <summary>
        /// Gets a list of unique AlbumArtist values
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <returns>Task&lt;List&lt;System.String&gt;&gt;.</returns>
        Task<List<string>> GetAlbumArtists(string serverId);

        /// <summary>
        /// Gets a list of unique TvShows values
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <returns>Task&lt;List&lt;System.String&gt;&gt;.</returns>
        Task<List<string>> GetTvShows(string serverId);

        /// <summary>
        /// Gets a list of unique photo albums, by Id
        /// Name = Album property
        /// Value = AlbumId property
        /// </summary>
        /// <param name="serverId">The server identifier.</param>
        /// <returns>Task&lt;List&lt;NameValuePair&gt;&gt;.</returns>
        Task<List<NameValuePair>> GetPhotoAlbums(string serverId);
    }
}
