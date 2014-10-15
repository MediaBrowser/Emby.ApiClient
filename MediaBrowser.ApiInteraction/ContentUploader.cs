﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public class ContentUploader
    {
        private readonly IApiClient _apiClient;
        private readonly ILogger _logger;

        public ContentUploader(IApiClient apiClient, ILogger logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task UploadImages(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var device = _apiClient.Device;

            var deviceId = device.DeviceId;

            var config = await _apiClient.GetDevicesOptions().ConfigureAwait(false);

            if (!config.EnabledCameraUploadDevices.Contains(deviceId))
            {
                _logger.Debug("Camera upload is not enabled for this device.");
                return;
            }

            var history = await _apiClient.GetContentUploadHistory(deviceId).ConfigureAwait(false);

            var files = device.GetLocalPhotos()
                .ToList();

            files.AddRange(device.GetLocalVideos());

            files = files
                .Where(i => !history.FilesUploaded.Any(f => string.Equals(f.FullPath, i.FullPath, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var numComplete = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Debug("Uploading {0}", file.FullPath);

                try
                {
                    await device.UploadFile(file, _apiClient, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error uploading file", ex);
                }

                numComplete++;
                double percent = numComplete;
                percent /= files.Count;

                progress.Report(100 * percent);
            }

            progress.Report(100);
        }
    }
}