using MediaBrowser.ApiInteraction.Net;

namespace MediaBrowser.ApiInteraction.Playback
{
    public class PortablePlayer : BaseLocalPlayer
    {
        public PortablePlayer(INetworkConnection network, IAsyncHttpClient httpClient)
            : base(network, httpClient)
        {
        }

        public override bool CanAccessFile(string path)
        {
            return false;
        }

        public override bool CanAccessDirectory(string path)
        {
            return false;
        }
    }
}
