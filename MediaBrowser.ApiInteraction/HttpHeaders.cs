using System.Collections.Generic;

namespace MediaBrowser.ApiInteraction
{
    public class HttpHeaders : Dictionary<string,string>
    {
        /// <summary>
        /// Gets or sets the authorization scheme.
        /// </summary>
        /// <value>The authorization scheme.</value>
        public string AuthorizationScheme { get; set; }
        /// <summary>
        /// Gets or sets the authorization parameter.
        /// </summary>
        /// <value>The authorization parameter.</value>
        public string AuthorizationParameter { get; set; }
    }
}
