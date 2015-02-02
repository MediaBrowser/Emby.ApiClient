using MediaBrowser.ApiInteraction.Cryptography;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dlna;
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
        private readonly IUserRepository _userRepository;
        private readonly IImageRepository _imageRepository;

        public LocalAssetManager(IUserActionRepository userActionRepository, IItemRepository itemRepository, IFileRepository fileRepository, ICryptographyProvider cryptographyProvider, ILogger logger, IUserRepository userRepository, IImageRepository iImageRepository)
        {
            _userActionRepository = userActionRepository;
            _itemRepository = itemRepository;
            _fileRepository = fileRepository;
            _cryptographyProvider = cryptographyProvider;
            _logger = logger;
            _userRepository = userRepository;
            _imageRepository = iImageRepository;
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
        /// Deletes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>Task.</returns>
        public Task Delete(LocalItem item)
        {
            return _itemRepository.Delete(item.Id);
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

            var name = Path.GetFileNameWithoutExtension(item.LocalPath);

            foreach (var file in list.Where(f => f.Name.Contains(name)))
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

                itemFiles.Add(itemFile);
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
            ImageInfo imageInfo)
        {
            var path = item.LocalPath;

            var imageFilename = GetSaveFileName(item, imageInfo) + GetSaveExtension(mimeType);

            var parentPath = _fileRepository.GetParentDirectoryPath(path);

            path = Path.Combine(parentPath, imageFilename);

            await _fileRepository.SaveFile(stream, path);
        }

        public async Task<string> SaveSubtitles(Stream stream,
            string format,
            LocalItem item,
            string language,
            bool isForced)
        {
            var path = item.LocalPath;

            var filename = GetSubtitleSaveFileName(item, language, isForced) + "." + format.ToLower();

            var parentPath = _fileRepository.GetParentDirectoryPath(path);

            path = Path.Combine(parentPath, filename);

            await _fileRepository.SaveFile(stream, path);

            return path;
        }

        private string GetSubtitleSaveFileName(LocalItem item, string language, bool isForced)
        {
            var path = item.LocalPath;

            var name = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrWhiteSpace(language))
            {
                name += "." + language.ToLower();
            }

            if (isForced)
            {
                name += ".foreign";
            }

            return name;
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

            if (item.IsType("episode"))
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
                Id = GetLocalId(server.Id, libraryItem.Id)
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

        public Task<LocalItem> GetLocalItem(string localId)
        {
            return _itemRepository.Get(localId);
        }

        public Task<LocalItem> GetLocalItem(string serverId, string itemId)
        {
            return GetLocalItem(GetLocalId(serverId, itemId));
        }

        public Task<bool> FileExists(string path)
        {
            return _fileRepository.FileExists(path);
        }

        public Task<List<string>> GetServerItemIds(string serverId)
        {
            return _itemRepository.GetServerItemIds(serverId);
        }

        public Task<Stream> GetFileStream(StreamInfo info)
        {
            return GetFileStream(info.ToUrl(null));
        }

        public Task<Stream> GetFileStream(string path)
        {
            return _fileRepository.GetFileStream(path);
        }

        public Task SaveOfflineUser(UserDto user)
        {
            return _userRepository.AddOrUpdate(user.Id, user);
        }

        public Task DeleteOfflineUser(string id)
        {
            return _userRepository.Delete(id);
        }

        public async Task SaveUserImage(UserDto user, Stream stream)
        {
            await DeleteUserImage(user).ConfigureAwait(false);

            await _imageRepository.SaveImage(user.Id, user.PrimaryImageTag, stream).ConfigureAwait(false);
        }

        public Task<Stream> GetUserImage(UserDto user)
        {
            return _imageRepository.GetImage(user.Id, user.PrimaryImageTag);
        }

        public Task DeleteUserImage(UserDto user)
        {
            return _imageRepository.DeleteImages(user.Id);
        }

        public Task<bool> HasImage(UserDto user)
        {
            return _imageRepository.HasImage(user.Id, user.PrimaryImageTag);
        }

        public async Task<List<BaseItemDto>> GetViews(UserDto user)
        {
            var list = new List<BaseItemDto>();

            var types = await _itemRepository.GetItemTypes(user.ServerId).ConfigureAwait(false);

            if (types.Contains("Audio", StringComparer.OrdinalIgnoreCase))
            {
                list.Add(new BaseItemDto
                {
                    Name = "Music",
                    ServerId = user.ServerId,
                    Id = "MusicView",
                    Type = "MusicView",
                    CollectionType = CollectionType.Music
                });
            }

            if (types.Contains("Photo", StringComparer.OrdinalIgnoreCase))
            {
                list.Add(new BaseItemDto
                {
                    Name = "Photos",
                    ServerId = user.ServerId,
                    Id = "PhotosView",
                    Type = "PhotosView",
                    CollectionType = CollectionType.Photos
                });
            }

            if (types.Contains("Episode", StringComparer.OrdinalIgnoreCase))
            {
                list.Add(new BaseItemDto
                {
                    Name = "TV",
                    ServerId = user.ServerId,
                    Id = "TVView",
                    Type = "TVView",
                    CollectionType = CollectionType.TvShows
                });
            }

            list.Add(new BaseItemDto
            {
                Name = "Videos",
                ServerId = user.ServerId,
                Id = "VideosView",
                Type = "VideosView",
                CollectionType = CollectionType.HomeVideos
            });

            return list;
        }

        public Task<List<BaseItemDto>> GetItems(UserDto user, BaseItemDto parentItem)
        {
            if (string.Equals(parentItem.Type, "MusicView"))
            {
                return GetMusicArtists(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "MusicArtist"))
            {
                return GetMusicAlbums(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "MusicAlbum"))
            {
                return GetAlbumSongs(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "PhotosView"))
            {
                return GetPhotoAlbums(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "PhotoAlbum"))
            {
                return GetPhotos(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "VideosView"))
            {
                return GetVideos(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "TVView"))
            {
                return GetTvShows(user, parentItem);
            }
            if (string.Equals(parentItem.Type, "TVShow"))
            {
                return GetTvEpisodes(user, parentItem);
            }

            return Task.FromResult(new List<BaseItemDto>());
        }

        private async Task<List<BaseItemDto>> GetMusicArtists(UserDto user, BaseItemDto parentItem)
        {
            var artists = await _itemRepository.GetAlbumArtists(user.ServerId).ConfigureAwait(false);

            return artists
                .OrderBy(i => i)
                .Select(i => new BaseItemDto
                {
                    Name = i,
                    Id = i,
                    Type = "MusicArtist",
                    ServerId = user.ServerId
                })
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetMusicAlbums(UserDto user, BaseItemDto parentItem)
        {
            var items = await _itemRepository.GetItems(new LocalItemQuery
            {
                AlbumArtist = parentItem.Name,
                ServerId = user.ServerId,
                Type = "Audio"
            });

            var dict = new Dictionary<string, List<BaseItemDto>>();

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Item.AlbumId))
                {
                    List<BaseItemDto> subItems;
                    if (!dict.TryGetValue(item.Item.AlbumId, out subItems))
                    {
                        subItems = new List<BaseItemDto>();
                        dict[item.Item.AlbumId] = subItems;
                    }
                    subItems.Add(item.Item);
                }
            }

            return dict
                .OrderBy(i => i.Value[0].Album)
                .Select(i => new BaseItemDto
                {
                    Name = i.Value[0].Album,
                    Id = i.Key,
                    Type = "MusicAlbum",
                    ServerId = user.ServerId,
                    SongCount = i.Value.Count,
                    ChildCount = i.Value.Count,
                    Genres = i.Value.SelectMany(m => m.Genres).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m).ToList(),
                    Artists = i.Value.SelectMany(m => m.Artists).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m).ToList()
                })
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetAlbumSongs(UserDto user, BaseItemDto parentItem)
        {
            var items = await _itemRepository.GetItems(new LocalItemQuery
            {
                AlbumId = parentItem.Id,
                ServerId = user.ServerId,
                MediaType = "Audio"
            });

            return items
                .Select(i => i.Item)
                .OrderBy(i => i.SortName)
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetPhotoAlbums(UserDto user, BaseItemDto parentItem)
        {
            var albums = await _itemRepository.GetPhotoAlbums(user.ServerId).ConfigureAwait(false);

            return albums
                .OrderBy(i => i.Name)
                .Select(i => new BaseItemDto
                {
                    Name = i.Name,
                    Id = i.Value,
                    Type = "PhotoAlbum",
                    ServerId = user.ServerId
                })
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetPhotos(UserDto user, BaseItemDto parentItem)
        {
            var items = await _itemRepository.GetItems(new LocalItemQuery
            {
                AlbumId = parentItem.Id,
                ServerId = user.ServerId,
                MediaType = "Video",
                ExcludeTypes = new[] { "Episode" }
            });

            return items
                .Select(i => i.Item)
                .OrderBy(i => i.SortName)
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetTvShows(UserDto user, BaseItemDto parentItem)
        {
            var shows = await _itemRepository.GetTvShows(user.ServerId).ConfigureAwait(false);

            return shows
                .OrderBy(i => i.Name)
                .Select(i => new BaseItemDto
                {
                    Name = i.Name,
                    Id = i.Value,
                    Type = "TVShow",
                    ServerId = user.ServerId
                })
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetTvEpisodes(UserDto user, BaseItemDto parentItem)
        {
            var items = await _itemRepository.GetItems(new LocalItemQuery
            {
                SeriesId = parentItem.Id,
                ServerId = user.ServerId,
                MediaType = "Episode"
            });

            return items
                .Select(i => i.Item)
                .OrderBy(i => i.SortName)
                .ToList();
        }

        private async Task<List<BaseItemDto>> GetVideos(UserDto user, BaseItemDto parentItem)
        {
            var items = await _itemRepository.GetItems(new LocalItemQuery
            {
                AlbumId = parentItem.Id,
                ServerId = user.ServerId,
                MediaType = "Video",
                ExcludeTypes = new[] { "Episode" }
            });

            return items
                .Select(i => i.Item)
                .OrderBy(i => i.SortName)
                .ToList();
        }
    }
}
