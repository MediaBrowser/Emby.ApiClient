using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
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
        private HttpWebRequest GetRequest(string url, string method)
        {
            var request = HttpWebRequest.CreateHttp(url);

            request.AutomaticDecompression = DecompressionMethods.Deflate;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate);
            request.KeepAlive = true;
            request.Method = method;
            request.Pipelined = true;
            request.Timeout = 30000;

            // This is a hack to prevent KeepAlive from getting disabled internally by the HttpWebRequest
            var sp = request.ServicePoint;
            if (_httpBehaviorPropertyInfo == null)
            {
                _httpBehaviorPropertyInfo = sp.GetType().GetProperty("HttpBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            _httpBehaviorPropertyInfo.SetValue(sp, (byte)0, null);

            return request;
        }

        private async Task<Stream> SendAsync(string url, string httpMethod, HttpHeaders headers, CancellationToken cancellationToken, string content = null, string contentType = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.Debug(httpMethod + " {0}", url);

            var httpWebRequest = GetRequest(url, httpMethod);

            ApplyHeaders(headers, httpWebRequest);

            if (!string.IsNullOrEmpty(content) || string.Equals(httpMethod, "post", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = Encoding.UTF8.GetBytes(content);

                httpWebRequest.ContentType = contentType;
                httpWebRequest.SendChunked = false;
                httpWebRequest.ContentLength = bytes.Length;
                httpWebRequest.GetRequestStream().Write(bytes, 0, bytes.Length);
            }

            var requestTime = DateTime.Now;

            try
            {
                var response = await httpWebRequest.GetResponseAsync().ConfigureAwait(false);
                var httpResponse = (HttpWebResponse)response;

                OnResponseReceived(url, httpMethod, httpResponse.StatusCode, requestTime);

                EnsureSuccessStatusCode(httpResponse);

                cancellationToken.ThrowIfCancellationRequested();

                return httpResponse.GetResponseStream();
            }
            catch (OperationCanceledException ex)
            {
                throw GetCancellationException(url, cancellationToken, ex);
            }
            catch (HttpRequestException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                throw new HttpException(ex.Message, ex);
            }
            catch (WebException ex)
            {
                Logger.ErrorException("Error getting response from " + url, ex);

                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    OnResponseReceived(url, httpMethod, response.StatusCode, requestTime);
                } 
                
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
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Task<Stream> GetAsync(string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return SendAsync(url, "GET", headers, cancellationToken);
        }

        /// <summary>
        /// Posts the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="postContent">Content of the post.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{Stream}.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Task<Stream> PostAsync(string url, string contentType, string postContent, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return SendAsync(url, "POST", headers, cancellationToken, postContent, contentType);
        }

        /// <summary>
        /// Deletes the async.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="MediaBrowser.Model.Net.HttpException"></exception>
        public Task<Stream> DeleteAsync(string url, HttpHeaders headers, CancellationToken cancellationToken)
        {
            return SendAsync(url, "DELETE", headers, cancellationToken);
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
    }
}
