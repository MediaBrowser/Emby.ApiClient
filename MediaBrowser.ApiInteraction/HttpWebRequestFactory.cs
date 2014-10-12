using System;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class HttpWebRequestFactory : IHttpWebRequestFactory
    {
        private static PropertyInfo _httpBehaviorPropertyInfo;
        public HttpWebRequest Create(HttpRequest options)
        {
            var request = HttpWebRequest.CreateHttp(options.Url);

            request.AutomaticDecompression = DecompressionMethods.Deflate;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.KeepAlive = true;
            request.Method = options.Method;
            request.Pipelined = true;
            request.Timeout = options.Timeout;

            // This is a hack to prevent KeepAlive from getting disabled internally by the HttpWebRequest
            var sp = request.ServicePoint;
            if (_httpBehaviorPropertyInfo == null)
            {
                _httpBehaviorPropertyInfo = sp.GetType().GetProperty("HttpBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            _httpBehaviorPropertyInfo.SetValue(sp, (byte)0, null);

            if (!string.IsNullOrEmpty(options.RequestContent) ||
                string.Equals(options.Method, "post", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = Encoding.UTF8.GetBytes(options.RequestContent ?? string.Empty);

                request.SendChunked = false;
                request.ContentLength = bytes.Length;
            }

            return request;
        }

        public void SetContentLength(HttpWebRequest request, long length)
        {
            request.ContentLength = length;
        }

        public Task<WebResponse> GetResponseAsync(HttpWebRequest request)
        {
            return request.GetResponseAsync();
        } 
    }
}
