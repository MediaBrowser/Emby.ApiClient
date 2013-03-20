using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Interface IHttpClient
    /// </summary>
    public interface IAsyncHttpClient : IDisposable
    {
        /// <summary>
        /// Sets the authorization header that should be supplied on every request
        /// </summary>
        /// <param name="scheme">The scheme.</param>
        /// <param name="paraneter">The paraneter.</param>
        void SetAuthorizationHeader(string scheme, string paraneter);

        /// <summary>
        /// Removes the authorization header.
        /// </summary>
        void RemoveAuthorizationHeader();
        
        /// <summary>
        /// Gets the stream async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        Task<Stream> GetAsync(string url, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string url, CancellationToken cancellationToken);
        
        /// <summary>
        /// Posts the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="postContent">Content of the post.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        Task<Stream> PostAsync(string url, string contentType, string postContent, CancellationToken cancellationToken);
    }
}
