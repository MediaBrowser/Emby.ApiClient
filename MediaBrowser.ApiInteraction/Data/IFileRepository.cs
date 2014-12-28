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
        /// <returns>Task&lt;List&lt;DeviceFileInfo&gt;&gt;.</returns>
        Task<List<DeviceFileInfo>> GetFileSystemEntries(IEnumerable<string> path);

        /// <summary>
        /// Saves the file.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        Task SaveFile(Stream stream, IEnumerable<string> path);

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        Task DeleteFile(IEnumerable<string> path);

        /// <summary>
        /// Deletes the directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        Task DeleteFolder(IEnumerable<string> path);
        
        /// <summary>
        /// Strips invalid characters from a given name
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        string GetValidFileName(string name);
    }
}
