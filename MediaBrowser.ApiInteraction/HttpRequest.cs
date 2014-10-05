using System.Threading;

namespace MediaBrowser.ApiInteraction
{
    public class HttpRequest
    {
        public string Method { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public string RequestContent { get; set; }
        public string RequestContentType { get; set; }
        public HttpHeaders RequestHeaders { get; set; }
        public string Url { get; set; }

        public int Timeout { get; set; }

        public HttpRequest()
        {
            RequestHeaders = new HttpHeaders();
            Timeout = 30000;
        }
    }
}
