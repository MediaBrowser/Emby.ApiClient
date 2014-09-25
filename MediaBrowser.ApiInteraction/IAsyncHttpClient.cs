using MediaBrowser.Model.ApiClient;
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

    public static class HttpClientExtensions
    {
        public static Task<Stream> GetAsync(this IAsyncHttpClient client, string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return client.SendAsync(new HttpRequest
            {
                Url = url,
                CancellationToken = cancellationToken,
                RequestHeaders = headers,
                Method = "GET"
            });
        }

        public static Task<Stream> DeleteAsync(this IAsyncHttpClient client, string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return client.SendAsync(new HttpRequest
            {
                Url = url,
                CancellationToken = cancellationToken,
                RequestHeaders = headers,
                Method = "DELETE"
            });
        }

        public static Task<Stream> PostAsync(this IAsyncHttpClient client, string url, string contentType, string postContent, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return client.SendAsync(new HttpRequest
            {
                Url = url,
                CancellationToken = cancellationToken,
                RequestHeaders = headers,
                Method = "POST",
                RequestContentType = contentType,
                RequestContent = postContent
            });
        }

    }
}
