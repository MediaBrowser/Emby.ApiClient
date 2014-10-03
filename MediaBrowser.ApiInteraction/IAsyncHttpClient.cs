using MediaBrowser.Model.ApiClient;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Interface IHttpClient
    /// </summary>
    public interface IAsyncHttpClient : IDisposable
    {
        /// <summary>
        /// Occurs when [HTTP response received].
        /// </summary>
        event EventHandler<HttpResponseEventArgs> HttpResponseReceived;

        /// <summary>
        /// Sends the asynchronous.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Task&lt;Stream&gt;.</returns>
        Task<Stream> SendAsync(HttpRequest options);
    }
}
