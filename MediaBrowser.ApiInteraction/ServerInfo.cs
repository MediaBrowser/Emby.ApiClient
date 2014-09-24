using System;
using System.Collections.Generic;

namespace MediaBrowser.ApiInteraction
{
    public class ServerInfo
    {
        public String Name { get; set; }
        public String Id { get; set; }
        public String LocalAddress { get; set; }
        public String RemoteAddress { get; set; }
        public String UserId { get; set; }
        public String AccessToken { get; set; }
        public List<string> MacAddresses { get; set; }

        public ServerInfo()
        {
            MacAddresses = new List<string>();
        }
    }
}
