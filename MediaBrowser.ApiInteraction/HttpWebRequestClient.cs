using System.Net.Cache;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class HttpWebRequestClient
    /// </summary>
    public class HttpWebRequestClient : IAsyncHttpClient
    {
        public event EventHandler<HttpResponseEventArgs> HttpResponseReceived;

        private string _authorizationHeaderValue;

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
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClient" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public HttpWebRequestClient(ILogger logger)
        {
            Logger = logger;
        }

        private string GetHostFromUrl(string url)
        {
            var start = url.IndexOf("://", StringComparison.OrdinalIgnoreCase) + 3;
            var len = url.IndexOf('/', start) - start;
            return url.Substring(start, len);
        }

        private PropertyInfo _httpBehaviorPropertyInfo;
        private HttpWebRequest GetRequest(string url, string method)
        {
            var request = HttpWebRequest.CreateHttp(url);

            request.AutomaticDecompression = DecompressionMethods.Deflate;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate);
            request.ConnectionGroupName = GetHostFromUrl(url);
            request.KeepAlive = true;
            request.Method = method;
            request.Pipelined = true;
            request.Timeout = 30000;

            if (!string.IsNullOrEmpty(_authorizationHeaderValue))
            {
                request.Headers.Add("Authorization", _authorizationHeaderValue);
            }

            // This is a hack to prevent KeepAlive from getting disabled internally by the HttpWebRequest
            var sp = request.ServicePoint;
            if (_httpBehaviorPropertyInfo == null)
            {
                _httpBehaviorPropertyInfo = sp.GetType().GetProperty("HttpBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            _httpBehaviorPropertyInfo.SetValue(sp, (byte)0, null);

            return request;
        }

        private async Task<Stream> SendAsync(string url, string httpMethod, CancellationToken cancellationToken, string content = null, string contentType = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Debug(httpMethod + " {0}", url);

            var httpWebRequest = GetRequest(url, httpMethod);

            if (!string.IsNullOrEmpty(content))
            {
                var bytes = Encoding.UTF8.GetBytes(content);

                httpWebRequest.ContentType = contentType;
                httpWebRequest.ContentLength = bytes.Length;
                httpWebRequest.GetRequestStream().Write(bytes, 0, bytes.Length);
                httpWebRequest.SendChunked = false;
            }

            try
            {
                var requestTime = DateTime.Now;

                var response = await httpWebRequest.GetResponseAsync().ConfigureAwait(false);
                var httpResponse = (HttpWebResponse)response;

                OnResponseReceived(url, httpMethod, httpResponse.StatusCode, requestTime);

                EnsureSuccessStatusCode(httpResponse);

                cancellationToken.ThrowIfCancellationRequested();

                return httpResponse.GetResponseStream();
            }
            catch (OperationCanceledException ex)
            {
                var exception = GetCancellationException(url, cancellationToken, ex);

                throw exception;
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (WebException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw;
            }
        }

        /// <summary>
        /// Gets the stream async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Task<Stream> GetAsync(string url, CancellationToken cancellationToken)
        {
            return SendAsync(url, "GET", cancellationToken);
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
        public Task<Stream> PostAsync(string url, string contentType, string postContent, CancellationToken cancellationToken)
        {
            return SendAsync(url, "POST", cancellationToken, postContent, contentType);
        }

        /// <summary>
        /// Deletes the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Task<Stream> DeleteAsync(string url, CancellationToken cancellationToken)
        {
            return SendAsync(url, "DELETE", cancellationToken);
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

        private void EnsureSuccessStatusCode(HttpWebResponse response)
        {
            var statusCode = response.StatusCode;
            var isSuccessful = statusCode >= HttpStatusCode.OK && statusCode <= (HttpStatusCode)299;

            if (!isSuccessful)
            {
                throw new HttpException(response.StatusDescription) { StatusCode = response.StatusCode };
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Sets the authorization header that should be supplied on every request
        /// </summary>
        /// <param name="scheme">The scheme.</param>
        /// <param name="parameter">The parameter.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SetAuthorizationHeader(string scheme, string parameter)
        {
            if (string.IsNullOrEmpty(parameter))
            {
                Logger.Debug("Removing Authorization http header");

                _authorizationHeaderValue = null;
            }
            else
            {
                Logger.Debug("Applying Authorization http header: {0}", parameter);

                _authorizationHeaderValue = new AuthenticationHeaderValue(scheme, parameter).ToString();
            }
        }

        /// <summary>
        /// Removes the authorization header.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void RemoveAuthorizationHeader()
        {
            _authorizationHeaderValue = null;
        }

    }
}
