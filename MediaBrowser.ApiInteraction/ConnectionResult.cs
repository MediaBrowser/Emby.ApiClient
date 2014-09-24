
namespace MediaBrowser.ApiInteraction
{
    public enum ConnectionState
    {
        Unavailable = 1,
        ServerSignIn = 2,
        SignedIn = 3
    }

    public class ConnectionResult
    {
        public ConnectionState State { get; set; }
        public ServerInfo ServerInfo { get; set; }
        public ApiClient ApiClient { get; set; }
    }
}
