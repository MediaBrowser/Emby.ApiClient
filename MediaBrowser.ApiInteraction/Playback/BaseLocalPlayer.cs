using MediaBrowser.ApiInteraction.Net;
using MediaBrowser.Model.Dlna;
using System;
using System.Collections.Generic;

namespace MediaBrowser.ApiInteraction.Playback
{
    public abstract class BaseLocalPlayer : ILocalPlayer, IDisposable
    {
        protected readonly INetworkConnection Network;
        protected readonly IAsyncHttpClient HttpClient;

        protected BaseLocalPlayer(INetworkConnection network, IAsyncHttpClient httpClient)
        {
            Network = network;
            HttpClient = httpClient;
            network.NetworkChanged += network_NetworkChanged;
        }

        void network_NetworkChanged(object sender, EventArgs e)
        {
            ClearUrlTestResultCache();
        }

        public abstract bool CanAccessFile(string path);
        public abstract bool CanAccessDirectory(string path);

        public virtual bool CanAccessUrl(string url, bool requiresCustomRequestHeaders)
        {
            if (requiresCustomRequestHeaders)
            {
                return false;
            }

            return true;
            //return CanAccessUrl(url);
        }

        private readonly Dictionary<string, TestResult> _results = new Dictionary<string, TestResult>(StringComparer.OrdinalIgnoreCase);
        private readonly object _resultLock = new object();

        private bool CanAccessUrl(string url)
        {
            var key = GetHostFromUrl(url);
            lock (_resultLock)
            {
                TestResult result;
                if (_results.TryGetValue(url, out result))
                {
                    var timespan = DateTime.UtcNow - result.Date;
                    if (timespan <= TimeSpan.FromMinutes(120))
                    {
                        return result.Success;
                    }
                }
            }

            var canAccess = CanAccessUrlInternal(url);
            lock (_resultLock)
            {
                _results[key] = new TestResult
                {
                    Success = canAccess,
                    Date = DateTime.UtcNow
                };
            }
            return canAccess;
        }

        private bool CanAccessUrlInternal(string url)
        {
            try
            {
                using (var response = HttpClient.GetResponse(new HttpRequest
                {
                    Url = url,
                    Method = "GET",
                    Timeout = 5000

                }).Result)
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected void ClearUrlTestResultCache()
        {
            lock (_resultLock)
            {
                _results.Clear();
            }
        }

        private string GetHostFromUrl(string url)
        {
            var start = url.IndexOf("://", StringComparison.OrdinalIgnoreCase) + 3;
            var len = url.IndexOf('/', start) - start;
            return url.Substring(start, len);
        }

        private class TestResult
        {
            public bool Success;
            public DateTime Date;
        }

        public void Dispose()
        {
            Network.NetworkChanged -= network_NetworkChanged;
        }
    }
}
