using System.Net;
using System.Net.Cache;
using System.Net.Http;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class HttpMessageHandlerFactory
    /// </summary>
    public static class HttpMessageHandlerFactory
    {
        /// <summary>
        /// Gets the handler.
        /// </summary>
        /// <returns>HttpMessageHandler.</returns>
        public static HttpMessageHandler GetHandler()
        {
            return new WebRequestHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate,
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
                
            };
        }
    }
}
