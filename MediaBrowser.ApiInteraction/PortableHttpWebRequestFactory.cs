using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

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
            request.Headers["Content-Length"] = length.ToString(CultureInfo.InvariantCulture);
        }

        public Task<WebResponse> GetResponseAsync(HttpWebRequest request)
        {
            var tcs = new TaskCompletionSource<WebResponse>();

            try
            {
                request.BeginGetResponse(iar =>
                {
                    try
                    {
                        var response = (HttpWebResponse)request.EndGetResponse(iar);
                        tcs.SetResult(response);
                    }
                    catch (Exception exc)
                    {
                        tcs.SetException(exc);
                    }
                }, null);
            }
            catch (Exception exc)
            {
                tcs.SetException(exc);
            }

            return tcs.Task;
        }
    }
}
