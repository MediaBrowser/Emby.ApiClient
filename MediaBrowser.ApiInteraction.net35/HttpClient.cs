using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Net;

namespace MediaBrowser.ApiInteraction.net35
{
    /// <summary>
    /// Class HttpClient
    /// </summary>
    public class HttpClient
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClient"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public HttpClient(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void Get(string url, Action<Stream> onSuccess, Action<Exception> onError)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            Get(request, onSuccess, onError);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void Get(HttpWebRequest request, Action<Stream> onSuccess, Action<Exception> onError)
        {
            _logger.Info("Get {0}", request.RequestUri);
            
            request.BeginGetResponse(iar =>
            {
                HttpWebResponse response;

                try
                {
                    response = (HttpWebResponse)((HttpWebRequest)iar.AsyncState).EndGetResponse(iar);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error getting response from " + request.RequestUri, ex);

                    onError(ex);

                    return;
                }

                if (EnsureSuccessStatusCode(response, onError))
                {
                    onSuccess(response.GetResponseStream());
                }

            }, request);
        }

        /// <summary>
        /// Ensures the success status code.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="onError">The on error.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        private bool EnsureSuccessStatusCode(HttpWebResponse response, Action<Exception> onError)
        {
            var statusCode = response.StatusCode.GetHashCode();

            if (statusCode < 200 || statusCode >= 400)
            {
                onError(new HttpException(response.StatusDescription) { StatusCode = response.StatusCode });

                return false;
            }

            return true;
        }
    }
}
