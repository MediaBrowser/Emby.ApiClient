using MediaBrowser.Model.Sync;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public interface IFileRepository
    {
        /// <summary>
        /// Gets the file system entries.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task&lt;List&lt;System.String&gt;&gt;.</returns>
        Task<List<ItemFileInfo>> GetFileSystemEntries(string path);

        /// <summary>
        /// Saves the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        Task SaveFile(Stream stream, string path);

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        Task DeleteFile(string path);

        /// <summary>
        /// Strips invalid characters from a given name
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        string GetValidFileName(string name);
    }
}
