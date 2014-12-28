using MediaBrowser.Model.Sync;
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
    }
}
