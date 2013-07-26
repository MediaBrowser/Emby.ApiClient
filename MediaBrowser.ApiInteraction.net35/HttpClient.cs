using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

namespace MediaBrowser.ApiInteraction.net35
{
    /// <summary>
    /// Class HttpClient
    /// </summary>
    public class HttpClient
    {
        /// <summary>
        /// The default webrequest header collection
        /// </summary>
        private readonly WebHeaderCollection _defaultHeaders;
        
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
            _defaultHeaders = new WebHeaderCollection();
        }

        /// <summary>
        /// Gets or sets the timeout in milliseconds for GET requests.
        /// </summary>
        /// <value>
        /// The timeout.
        /// </value>
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        } private int _timeout = 5000;

        /// <summary>
        /// Gets the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void Get(string url, Action<Stream> onSuccess, Action<Exception> onError)
        {
            var request = CreateRequest(url);
            request.BeginGetResponse(iar =>
            {
                try
                {
                    var response = (HttpWebResponse)((HttpWebRequest)iar.AsyncState).EndGetResponse(iar);
                    if (EnsureSuccessStatusCode(response, onError))
                    {
                        onSuccess(response.GetResponseStream());
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error getting response from " + request.RequestUri, ex);

                    onError(ex);
                    return;
                }
            }, request);
        }

        /// <summary>
        /// Posts the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="postContent">Content of the post.</param>
        /// <param name="onSuccess">The on success.</param>
        /// <param name="onError">The on error.</param>
        public void Post(string url, string contentType, string postContent, Action<Stream> onSuccess, Action<Exception> onError)
        {
            var request = CreateRequest(url, "POST");
            request.ContentType = contentType;
            byte[] data = Encoding.UTF8.GetBytes(postContent);
            request.ContentLength = data.Length;
            request.BeginGetRequestStream(iar =>
            {
                try
                {
                    Stream stream = request.EndGetRequestStream(iar);
                    stream.Write(data, 0, data.Length);
                    stream.Close();

                    var response = (HttpWebResponse) request.GetResponse();
                    if (EnsureSuccessStatusCode(response, onError))
                    {
                        onSuccess(response.GetResponseStream());
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error posting {0}", ex, url);
                    onError(ex);
                }
            }, null);
        }

        /// <summary>
        /// Deletes the specified URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="onError">The on error.</param>
        public void Delete(string url, Action<Exception> onError)
        {
            var request = CreateRequest(url, "DELETE");
            request.BeginGetResponse(iar =>
            {
                try
                {
                    var response = (HttpWebResponse)((HttpWebRequest)iar.AsyncState).EndGetResponse(iar);
                    EnsureSuccessStatusCode(response, onError);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error DELETE {0}", ex, url);
                    onError(ex);
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

        /// <summary>
        /// Creates the request.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        private HttpWebRequest CreateRequest(string url, string method = "GET")
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Headers.Add(_defaultHeaders);
            request.Timeout = _timeout;

            _logger.Debug("{0}: {1}", method, url);

            return request;
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
                _defaultHeaders.Remove(HttpRequestHeader.Authorization);
            }
            else
            {
                _defaultHeaders[HttpRequestHeader.Authorization] = scheme + " " + parameter;
            }
        }

        /// <summary>
        /// Removes the authorization header.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void RemoveAuthorizationHeader()
        {
            _defaultHeaders.Remove(HttpRequestHeader.Authorization);
        }
    }
}
