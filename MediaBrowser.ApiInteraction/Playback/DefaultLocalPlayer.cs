using MediaBrowser.ApiInteraction.Net;
using MediaBrowser.Model.Dlna;
using System;
using System.Collections.Generic;
using System.IO;

namespace MediaBrowser.ApiInteraction.Playback
{
    public class DefaultLocalPlayer : ILocalPlayer, IDisposable
    {
        private readonly INetworkConnection _network;
        private readonly IAsyncHttpClient _httpClient;

        public DefaultLocalPlayer(INetworkConnection network, IAsyncHttpClient httpClient)
        {
            _network = network;
            _httpClient = httpClient;
            network.NetworkChanged += network_NetworkChanged;
        }

        void network_NetworkChanged(object sender, EventArgs e)
        {
            ClearUrlTestResultCache();
        }

        public bool CanAccessFile(string path)
        {
            return File.Exists(path);
        }

        public bool CanAccessDirectory(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool CanAccessUrl(string url, bool requiresCustomRequestHeaders)
        {
            if (requiresCustomRequestHeaders)
            {
                return false;
            }

            return CanAccessUrl(url);
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
                    if (timespan <= TimeSpan.FromMinutes(3))
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
                using (var response = _httpClient.GetResponse(new HttpRequest
                {
                    Url = url,
                    Method = "HEAD",
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
            _network.NetworkChanged += network_NetworkChanged;
        }
    }
}
