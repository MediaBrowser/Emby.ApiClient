using MediaBrowser.ApiInteraction.Cryptography;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Sync;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public class LocalAssetManager
    {
        private readonly IUserActionRepository _userActionRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IFileRepository _fileRepository;
        private readonly ICryptographyProvider _cryptographyProvider;
        private readonly ILogger _logger;

        public LocalAssetManager(IUserActionRepository userActionRepository, IItemRepository itemRepository, IFileRepository fileRepository, ICryptographyProvider cryptographyProvider, ILogger logger)
        {
            _userActionRepository = userActionRepository;
            _itemRepository = itemRepository;
            _fileRepository = fileRepository;
            _cryptographyProvider = cryptographyProvider;
            _logger = logger;
        }

        /// <summary>
        /// Records the user action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>Task.</returns>
        public Task RecordUserAction(UserAction action)
        {
            action.Id = Guid.NewGuid().ToString("N");

            return _userActionRepository.Create(action);
        }

        /// <summary>
        /// Deletes the specified action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>Task.</returns>
        public Task Delete(UserAction action)
        {
            return _userActionRepository.Delete(action);
        }

        /// <summary>
        /// Gets all user actions by serverId
        /// </summary>
        /// <param name="serverId"></param>
        /// <returns></returns>
        public Task<IEnumerable<UserAction>> GetUserActions(string serverId)
        {
            return _userActionRepository.Get(serverId);
        }

        /// <summary>
        /// Adds the or update.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>Task.</returns>
        public Task AddOrUpdate(LocalItem item)
        {
            return _itemRepository.AddOrUpdate(item);
        }

        /// <summary>
        /// Gets the files.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>Task&lt;List&lt;ItemFileInfo&gt;&gt;.</returns>
        public async Task<List<ItemFileInfo>> GetFiles(LocalItem item)
        {
            var path = item.LocalPath;
            path = _fileRepository.GetParentDirectoryPath(path);

            var list = await _fileRepository.GetFileSystemEntries(path).ConfigureAwait(false);

            var itemFiles = new List<ItemFileInfo>();

            foreach (var file in list)
            {
                var itemFile = new ItemFileInfo
                {
                    Path = file.Path,
                    Name = file.Name
                };

                if (IsImageFile(file.Name))
                {
                    itemFile.Type = ItemFileType.Image;
                    itemFile.ImageType = GetImageType(file.Name);
                }
                else if (IsSubtitleFile(file.Name))
                {
                    itemFile.Type = ItemFileType.Subtitles;
                }
                else
                {
                    itemFile.Type = ItemFileType.Media;
                }
            }

            return itemFiles;
        }

        private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
        private bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path) ?? string.Empty;

            return SupportedImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static readonly string[] SupportedSubtitleExtensions = { ".srt", ".vtt" };
        private bool IsSubtitleFile(string path)
        {
            var ext = Path.GetExtension(path) ?? string.Empty;

            return SupportedSubtitleExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the type of the image.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>ImageType.</returns>
        private ImageType GetImageType(string filename)
        {
            return ImageType.Primary;
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Task.</returns>
        public Task DeleteFile(string path)
        {
            return _fileRepository.DeleteFile(path);
        }

        public async Task SaveImage(Stream stream,
            string mimeType,
            LocalItem item,
            ImageInfo imageInfo,
            ServerInfo server)
        {
            var path = item.LocalPath;

            var imageFilename = GetSaveFileName(item, imageInfo) + GetSaveExtension(mimeType);

            var parentPath = _fileRepository.GetParentDirectoryPath(path);

            path = Path.Combine(parentPath, imageFilename);

            await _fileRepository.SaveFile(stream, path);
        }

        private string GetSaveFileName(LocalItem item, ImageInfo imageInfo)
        {
            var path = item.LocalPath;

            var libraryItem = item.Item;

            var name = Path.GetFileNameWithoutExtension(path);

            if (libraryItem.IsType("episode"))
            {
                name += "-thumb";
            }

            // TODO: Handle other image types

            return name;
        }

        private string GetSaveExtension(string mimeType)
        {
            return MimeTypes.ToExtension(mimeType);
        }

        public Task SaveMedia(Stream stream, LocalItem localItem, ServerInfo server)
        {
            _logger.Debug("Saving media to " + localItem.LocalPath);
            return _fileRepository.SaveFile(stream, localItem.LocalPath);
        }

        private List<string> GetDirectoryPath(BaseItemDto item, ServerInfo server)
        {
            var parts = new List<string>
            {
                server.Name
            };

            if (item.IsType("movie"))
            {
                parts.Add("Movies");
                parts.Add(item.Name);
            }
            else if (item.IsType("episode"))
            {
                parts.Add("TV");
                parts.Add(item.SeriesName);

                if (!string.IsNullOrWhiteSpace(item.SeasonName))
                {
                    parts.Add(item.SeasonName);
                }
            }
            else if (item.IsVideo)
            {
                parts.Add("Videos");
                parts.Add(item.Name);
            }
            else if (item.IsAudio)
            {
                parts.Add("Music");

                if (!string.IsNullOrWhiteSpace(item.AlbumArtist))
                {
                    parts.Add(item.AlbumArtist);
                }

                if (!string.IsNullOrWhiteSpace(item.Album))
                {
                    parts.Add(item.Album);
                }
            }
            else if (string.Equals(item.MediaType, MediaType.Photo, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Photos");

                if (!string.IsNullOrWhiteSpace(item.Album))
                {
                    parts.Add(item.Album);
                }
            }

            return parts.Select(_fileRepository.GetValidFileName).ToList();
        }

        public LocalItem CreateLocalItem(BaseItemDto libraryItem, ServerInfo server, string originalFileName)
        {
            var path = GetDirectoryPath(libraryItem, server);
            path.Add(GetLocalFileName(libraryItem, originalFileName));

            var localPath = _fileRepository.GetFullLocalPath(path);

            foreach (var mediaSource in libraryItem.MediaSources)
            {
                mediaSource.Path = localPath;
                mediaSource.Protocol = MediaProtocol.File;
            }

            return new LocalItem
            {
                Item = libraryItem,
                ItemId = libraryItem.Id,
                ServerId = server.Id,
                LocalPath = localPath,
                UniqueId = GetLocalId(libraryItem.Id, server.Id)
            };
        }

        private string GetLocalFileName(BaseItemDto item, string originalFileName)
        {
            var filename = originalFileName;

            if (string.IsNullOrEmpty(filename))
            {
                filename = item.Name;
            }

            return _fileRepository.GetValidFileName(filename);
        }

        private string GetLocalId(string serverId, string itemId)
        {
            var bytes = Encoding.UTF8.GetBytes(serverId + itemId);
            bytes = _cryptographyProvider.CreateMD5(bytes);
            return BitConverter.ToString(bytes, 0, bytes.Length).Replace("-", string.Empty);
        }

        public Task<LocalItem> GetLocalItem(string serverId, string itemId)
        {
            return _itemRepository.Get(GetLocalId(serverId, itemId));
        }

        public Task<bool> FileExists(string path)
        {
            return _fileRepository.FileExists(path);
        }
    }
}
