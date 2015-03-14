using MediaBrowser.ApiInteraction.Net;
using System.IO;

namespace MediaBrowser.ApiInteraction.Playback
{
    public class LocalPlayer : BaseLocalPlayer
    {
        public LocalPlayer(INetworkConnection network, IAsyncHttpClient httpClient)
            : base(network, httpClient)
        {
        }

        public override bool CanAccessFile(string path)
        {
            return File.Exists(path);
        }

        public override bool CanAccessDirectory(string path)
        {
            return Directory.Exists(path);
        }
    }
}
