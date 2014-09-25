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

            Logger.Debug("Received {0} status code after {1} ms from {2}: {3}", (int)statusCode, duration.TotalMilliseconds, verb, url);

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

        private PropertyInfo _httpBehaviorPropertyInfo;
        private HttpWebRequest GetRequest(string url, string method, int timeout)
        {
            var request = HttpWebRequest.CreateHttp(url);

            request.AutomaticDecompression = DecompressionMethods.Deflate;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.KeepAlive = true;
            request.Method = method;
            request.Pipelined = true;
            request.Timeout = timeout;

            // This is a hack to prevent KeepAlive from getting disabled internally by the HttpWebRequest
            var sp = request.ServicePoint;
            if (_httpBehaviorPropertyInfo == null)
            {
                _httpBehaviorPropertyInfo = sp.GetType().GetProperty("HttpBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            _httpBehaviorPropertyInfo.SetValue(sp, (byte)0, null);

            return request;
        }

        public async Task<Stream> SendAsync(HttpRequest options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            var httpWebRequest = GetRequest(options.Url, options.Method, options.Timeout);

            ApplyHeaders(options.RequestHeaders, httpWebRequest);

            if (!string.IsNullOrEmpty(options.RequestContent) ||
                string.Equals(options.Method, "post", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = Encoding.UTF8.GetBytes(options.RequestContent ?? string.Empty);

                httpWebRequest.ContentType = options.RequestContentType ?? "application/x-www-form-urlencoded";
                httpWebRequest.SendChunked = false;
                httpWebRequest.ContentLength = bytes.Length;

                var requestStream = await httpWebRequest.GetRequestStreamAsync().ConfigureAwait(false);
                await requestStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }

            Logger.Debug(options.Method + " {0}", options.Url);

            var requestTime = DateTime.Now;

            try
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                using (var response = await httpWebRequest.GetResponseAsync().ConfigureAwait(false))
                {
                    var httpResponse = (HttpWebResponse)response;

                    OnResponseReceived(options.Url, options.Method, httpResponse.StatusCode, requestTime);
                    
                    EnsureSuccessStatusCode(httpResponse);

                    options.CancellationToken.ThrowIfCancellationRequested();

                    return httpResponse.GetResponseStream();
                }
            }
            catch (OperationCanceledException ex)
            {
                var exception = GetCancellationException(options.Url, options.CancellationToken, ex);

                throw exception;
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + options.Url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (WebException ex)
            {
                Logger.ErrorException("Error getting response from " + options.Url, ex);

                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    OnResponseReceived(options.Url, options.Method, response.StatusCode, requestTime);
                }

                throw GetException(ex, options.Url);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error getting response from " + options.Url, ex);

                throw;
            }
        }

        /// <summary>
        /// Gets the exception.
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <param name="url">The URL.</param>
        /// <returns>HttpException.</returns>
        private HttpException GetException(WebException ex, string url)
        {
            Logger.ErrorException("Error getting response from " + url, ex);

            var exception = new HttpException(ex.Message, ex);

            var response = ex.Response as HttpWebResponse;
            if (response != null)
            {
                exception.StatusCode = response.StatusCode;
            }

            return exception;
        }

        private void ApplyHeaders(HttpHeaders headers, HttpWebRequest request)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(headers.AuthorizationScheme))
            {
                var val = new AuthenticationHeaderValue(headers.AuthorizationScheme, headers.AuthorizationParameter).ToString();

                request.Headers.Add("Authorization", val);
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
    }
}
