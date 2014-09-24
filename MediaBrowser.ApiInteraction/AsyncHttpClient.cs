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

        private HttpRequestMessage GetRequest(HttpMethod method, string url, HttpHeaders headers)
        {
            var msg = new HttpRequestMessage(method, url);

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

        public async Task<Stream> GetAsync(string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Debug("GET {0}", url);

            try
            {
                var requestTime = DateTime.Now;

                using (var request = GetRequest(HttpMethod.Get, url, headers))
                {
                    var msg = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    OnResponseReceived(url, "GET", msg.StatusCode, requestTime);

                    EnsureSuccessStatusCode(msg);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await msg.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (OperationCanceledException ex)
            {
                throw GetCancellationException(url, cancellationToken, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error requesting {0}", ex, url);

                throw;
            }
        }

        /// <summary>
        /// Posts the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="postContent">Content of the post.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public async Task<Stream> PostAsync(string url, string contentType, string postContent, HttpHeaders headers, CancellationToken cancellationToken)
        {
            Logger.Info("POST {0}", url);

            var content = new StringContent(postContent, Encoding.UTF8, contentType);

            try
            {
                var requestTime = DateTime.Now;

                using (var request = GetRequest(HttpMethod.Post, url, headers))
                {
                    request.Content = content;

                    var msg = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    OnResponseReceived(url, "POST", msg.StatusCode, requestTime);

                    EnsureSuccessStatusCode(msg);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await msg.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (OperationCanceledException ex)
            {
                throw GetCancellationException(url, cancellationToken, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error posting {0}", ex, url);

                throw;
            }
        }

        /// <summary>
        /// Deletes the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public async Task<Stream> DeleteAsync(string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Info("DELETE {0}", url);

            try
            {
                var requestTime = DateTime.Now;

                using (var request = GetRequest(HttpMethod.Delete, url, headers))
                {
                    var msg = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    OnResponseReceived(url, "DELETE", msg.StatusCode, requestTime);

                    EnsureSuccessStatusCode(msg);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await msg.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (OperationCanceledException ex)
            {
                throw GetCancellationException(url, cancellationToken, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error requesting {0}", ex, url);

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
                return new HttpException(msg, exception) { IsTimedOut = true };
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
