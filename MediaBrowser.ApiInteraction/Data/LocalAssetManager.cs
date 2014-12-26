using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Sync;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public class LocalAssetManager
    {
        private readonly IUserActionRepository _userActionRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IFileRepository _fileRepository;

        public LocalAssetManager(IUserActionRepository userActionRepository, IItemRepository itemRepository, IFileRepository fileRepository)
        {
            _userActionRepository = userActionRepository;
            _itemRepository = itemRepository;
            _fileRepository = fileRepository;
        }

        /// <summary>
        /// Creates the specified action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>Task.</returns>
        public Task Create(UserAction action)
        {
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
        public Task AddOrUpdate(BaseItemDto item)
        {
            return _itemRepository.AddOrUpdate(item);
        }

        /// <summary>
        /// Gets the files.
        /// </summary>
        /// <param name="itemId">The item identifier.</param>
        /// <returns>Task&lt;List&lt;ItemFileInfo&gt;&gt;.</returns>
        public async Task<List<ItemFileInfo>> GetFiles(string itemId)
        {
            var list = await _fileRepository.Get(itemId).ConfigureAwait(false);

            foreach (var file in list)
            {
                if (file.Type == ItemFileType.Image)
                {
                    file.ImageType = GetImageType(file.Name);
                }
            }

            return list;
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
        /// <param name="file">The file.</param>
        /// <returns>Task.</returns>
        public Task Delete(ItemFileInfo file)
        {
            return _fileRepository.Delete(file);
        }

        public async Task SaveImage(Stream stream,
            string mimeType,
            string itemId,
            ImageInfo imageInfo)
        {
            var localFiles = await GetFiles(itemId).ConfigureAwait(false);

            var media = localFiles.FirstOrDefault(i => i.Type == ItemFileType.Media);

            if (media == null)
            {
                throw new ArgumentException("Media not found");
            }

            var imageFilename = GetSaveFileName(media.Name, imageInfo) + GetSaveExtension(mimeType);

            await _fileRepository.Save(stream, new ItemFileInfo
            {
                ImageType = imageInfo.ImageType,
                Name = imageFilename,
                ItemId = itemId,
                Type = ItemFileType.Image
            });
        }

        private string GetSaveFileName(string mediaName, ImageInfo imageInfo)
        {
            var name = Path.GetFileNameWithoutExtension(mediaName);

            // TODO: Handle other image types

            return name;
        }

        private string GetSaveExtension(string mimeType)
        {
            return MimeTypes.ToExtension(mimeType);
        }

        public Task SaveMedia(Stream stream, SyncedItem jobItem)
        {
            var libraryItem = jobItem.Item;

            var filename = jobItem.OriginalFileName;

            if (string.IsNullOrEmpty(filename))
            {
                filename = Guid.NewGuid().ToString("N");
            }

            return _fileRepository.Save(stream, new ItemFileInfo
            {
                Name = filename,
                ItemId = libraryItem.Id,
                Type = ItemFileType.Media
            });
        }
    }
}
