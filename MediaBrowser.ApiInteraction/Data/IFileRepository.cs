using MediaBrowser.Model.Sync;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public interface IFileRepository
    {
        /// <summary>
        /// Gets the specified item identifier.
        /// </summary>
        /// <param name="itemId">The item identifier.</param>
        /// <returns>Task&lt;List&lt;ItemFileInfo&gt;&gt;.</returns>
        Task<List<ItemFileInfo>> Get(string itemId);

        /// <summary>
        /// Saves the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="file">The file.</param>
        /// <returns>Task.</returns>
        Task Save(Stream stream, ItemFileInfo file);

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>Task.</returns>
        Task Delete(ItemFileInfo file);
    }
}
