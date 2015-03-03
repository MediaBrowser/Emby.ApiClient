using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Sync;
using MediaBrowser.Model.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction.Data
{
    public class NullAssetManager : ILocalAssetManager
    {
        public Task RecordUserAction(UserAction action)
        {
            return Task.FromResult(true);
        }

        public Task Delete(UserAction action)
        {
            return Task.FromResult(true);
        }

        public Task Delete(LocalItem item)
        {
            return Task.FromResult(true);
        }

        public Task<IEnumerable<UserAction>> GetUserActions(string serverId)
        {
            return Task.FromResult<IEnumerable<UserAction>>(new List<UserAction>());
        }

        public Task AddOrUpdate(LocalItem item)
        {
            return Task.FromResult(true);
        }

        public Task<List<ItemFileInfo>> GetFiles(LocalItem item)
        {
            return Task.FromResult(new List<ItemFileInfo>());
        }

        public Task DeleteFile(string path)
        {
            return Task.FromResult(true);
        }

        public Task<string> SaveSubtitles(Stream stream, string format, LocalItem item, string language, bool isForced)
        {
            throw new NotImplementedException();
        }

        public Task SaveMedia(Stream stream, LocalItem localItem, ServerInfo server)
        {
            return Task.FromResult(true);
        }

        public LocalItem CreateLocalItem(BaseItemDto libraryItem, ServerInfo server, string originalFileName)
        {
            throw new NotImplementedException();
        }

        public Task<LocalItem> GetLocalItem(string localId)
        {
            return Task.FromResult<LocalItem>(null);
        }

        public Task<LocalItem> GetLocalItem(string serverId, string itemId)
        {
            return Task.FromResult<LocalItem>(null);
        }

        public Task<bool> FileExists(string path)
        {
            return Task.FromResult(false);
        }

        public Task<List<string>> GetServerItemIds(string serverId)
        {
            return Task.FromResult(new List<string>());
        }

        public Task<Stream> GetFileStream(StreamInfo info)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task<Stream> GetFileStream(string path)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task SaveOfflineUser(UserDto user)
        {
            return Task.FromResult(true);
        }

        public Task DeleteOfflineUser(string id)
        {
            return Task.FromResult(true);
        }

        public Task SaveImage(UserDto user, Stream stream)
        {
            return Task.FromResult(true);
        }

        public Task<Stream> GetImage(UserDto user)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task DeleteImage(UserDto user)
        {
            return Task.FromResult(true);
        }

        public Task<bool> HasImage(UserDto user)
        {
            return Task.FromResult(false);
        }

        public Task SaveImage(string serverId, string itemId, string imageId, Stream stream)
        {
            return Task.FromResult(true);
        }

        public Task<bool> HasImage(string serverId, string itemId, string imageId)
        {
            return Task.FromResult(false);
        }

        public Task<Stream> GetImage(string serverId, string itemId, string imageId)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task<bool> HasImage(BaseItemDto item, string imageId)
        {
            return Task.FromResult(false);
        }

        public Task<Stream> GetImage(BaseItemDto item, string imageId)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task<List<BaseItemDto>> GetViews(string serverId, string userId)
        {
            return Task.FromResult(new List<BaseItemDto>());
        }

        public Task<List<BaseItemDto>> GetItems(UserDto user, BaseItemDto parentItem)
        {
            return Task.FromResult(new List<BaseItemDto>());
        }

        public Task<UserDto> GetUser(string id)
        {
            return Task.FromResult<UserDto>(null);
        }
    }
}
