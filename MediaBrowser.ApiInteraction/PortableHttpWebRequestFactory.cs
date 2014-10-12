using System.Globalization;
using System.Net;

namespace MediaBrowser.ApiInteraction
{
    public class PortableHttpWebRequestFactory : IHttpWebRequestFactory
    {
        public HttpWebRequest Create(HttpRequest options)
        {
            var request = HttpWebRequest.CreateHttp(options.Url);

            request.Method = options.Method;
            
            return request;
        }

        public void SetContentLength(HttpWebRequest request, long length)
        {
            //request.Headers["Content-Length"] = length.ToString(CultureInfo.InvariantCulture);
        }
    }
}
