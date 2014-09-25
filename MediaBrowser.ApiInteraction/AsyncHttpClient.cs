using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class AsyncHttpClient
    /// </summary>
    public class AsyncHttpClient : IAsyncHttpClient
    {
        public event EventHandler<HttpResponseEventArgs> HttpResponseReceived;

        /// <summary>
        /// Called when [response received].
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="verb">The verb.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="requestTime">The request time.</param>
        private void OnResponseReceived(string url, string verb, HttpStatusCode statusCode, DateTime requestTime)
        {
            var duration = DateTime.Now - requestTime;

            Logger.Debug("Received {0} status code after {1} ms from {2}: {3}", statusCode, duration.TotalMilliseconds, verb, url);

            if (HttpResponseReceived != null)
            {
                try
                {
                    HttpResponseReceived(this, new HttpResponseEventArgs
                    {
                        Url = url,
                        StatusCode = statusCode
                    });
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in HttpResponseReceived event handler", ex);
                }
            }
        }

        /// <summary>
        /// Gets or sets the HTTP client.
        /// </summary>
        /// <value>The HTTP client.</value>
        private HttpClient HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AsyncHttpClient(ILogger logger)
        {
            Logger = logger;
            HttpClient = new HttpClient(HttpMessageHandlerFactory.GetHandler());
        }

        private HttpRequestMessage GetRequest(string method, string url, HttpHeaders headers)
        {
            HttpMethod methodValue;

            if (string.Equals(method, "post", StringComparison.OrdinalIgnoreCase))
            {
                methodValue = HttpMethod.Post;
            }
            else if (string.Equals(method, "delete", StringComparison.OrdinalIgnoreCase))
            {
                methodValue = HttpMethod.Delete;
            }
            else
            {
                methodValue = HttpMethod.Get;
            }

            var msg = new HttpRequestMessage(methodValue, url);

            foreach (var header in headers)
            {
                msg.Headers.Add(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(headers.AuthorizationScheme))
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue(headers.AuthorizationScheme, headers.AuthorizationParameter);
            }

            return msg;
        }

        public async Task<Stream> SendAsync(HttpRequest options)
        {
            Logger.Debug(options.Method + " {0}", options.Url);

            try
            {
                var requestTime = DateTime.Now;

                using (var request = GetRequest(options.Method, options.Url, options.RequestHeaders))
                {
                    if (string.Equals(options.Method, "post", StringComparison.OrdinalIgnoreCase) ||
                        !string.IsNullOrEmpty(options.RequestContent))
                    {
                        request.Content = new StringContent(options.RequestContent, Encoding.UTF8, options.RequestContentType);
                    }

                    var msg = await HttpClient.SendAsync(request, options.CancellationToken).ConfigureAwait(false);

                    OnResponseReceived(options.Url, options.Method, msg.StatusCode, requestTime);

                    EnsureSuccessStatusCode(msg);

                    options.CancellationToken.ThrowIfCancellationRequested();

                    return await msg.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + options.Url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (OperationCanceledException ex)
            {
                throw GetCancellationException(options.Url, options.CancellationToken, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error posting {0}", ex, options.Url);

                throw;
            }
        }

        /// <summary>
        /// Throws the cancellation exception.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>Exception.</returns>
        private Exception GetCancellationException(string url, CancellationToken cancellationToken, OperationCanceledException exception)
        {
            // If the HttpClient's timeout is reached, it will cancel the Task internally
            if (!cancellationToken.IsCancellationRequested)
            {
                var msg = string.Format("Connection to {0} timed out", url);

                Logger.Error(msg);

                // Throw an HttpException so that the caller doesn't think it was cancelled by user code
                return new HttpException(msg, exception)
                {
                    IsTimedOut = true
                };
            }

            return exception;
        }

        /// <summary>
        /// Ensures the success status code.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        private void EnsureSuccessStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpException(response.ReasonPhrase) { StatusCode = response.StatusCode };
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                HttpClient.Dispose();
            }
        }
    }
}
