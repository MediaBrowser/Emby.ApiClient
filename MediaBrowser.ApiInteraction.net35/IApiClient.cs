using MediaBrowser.ApiInteraction.WebSocket;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;

namespace MediaBrowser.ApiInteraction.net35
{
    public interface IApiClient : IDisposable
    {
        string ApplicationVersion { get; set; }
        string ClientName { get; set; }
        string CurrentUserId { get; set; }
        string DeviceId { get; set; }
        string DeviceName { get; set; }
        string GetArtImageUrl(BaseItemDto item, ImageOptions options);
        string GetArtistImageUrl(string name, ImageOptions options);
        string GetAudioStreamUrl(StreamOptions options);
        string[] GetBackdropImageUrls(BaseItemDto item, ImageOptions options);
        string GetGenreImageUrl(string name, ImageOptions options);
        string GetHlsAudioStreamUrl(StreamOptions options);
        string GetHlsVideoStreamUrl(VideoStreamOptions options);
        string GetImageUrl(BaseItemDto item, ImageOptions options);
        string GetImageUrl(string itemId, ImageOptions options);
        string GetLogoImageUrl(BaseItemDto item, ImageOptions options);
        string GetMusicGenreImageUrl(string name, ImageOptions options);
        string GetPersonImageUrl(BaseItemPerson item, ImageOptions options);
        string GetPersonImageUrl(string name, ImageOptions options);
        string GetStudioImageUrl(string name, ImageOptions options);
        string GetUserImageUrl(UserDto user, ImageOptions options);
        string GetUserImageUrl(string userId, ImageOptions options);
        string GetVideoStreamUrl(VideoStreamOptions options);
        string GetYearImageUrl(BaseItemDto item, ImageOptions options);
        string GetYearImageUrl(int year, ImageOptions options);
        int? ImageQuality { get; set; }
        IJsonSerializer JsonSerializer { get; set; }
        int ServerApiPort { get; set; }
        string ServerHostName { get; set; }
        void AuthenticateUser(string userId, byte[] sha1Hash, Action<MediaBrowser.Model.Entities.EmptyRequestResult> onSuccess, Action<Exception> onError);
        void AuthenticateUser(string userId, string password, Action<bool> onResponse);
        void GetGenres(ItemsByNameQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError);
        void GetStudios(ItemsByNameQuery query, Action<ItemsResult> onSuccess, Action<Exception> onError);
        void GetItem(string id, string userId, Action<BaseItemDto> onSuccess, Action<Exception> onError);
        void GetItems(MediaBrowser.Model.Querying.ItemQuery query, Action<MediaBrowser.Model.Querying.ItemsResult> onSuccess, Action<Exception> onError);
        void GetRootFolder(string userId, Action<BaseItemDto> onSuccess, Action<Exception> onError);
        void GetServerConfiguration(Action<ServerConfiguration> onSuccess, Action<Exception> onError);
        void GetSystemInfo(Action<SystemInfo> onSuccess, Action<Exception> onError);
        void GetUser(string id, Action<UserDto> onSuccess, Action<Exception> onError);
        void GetUsers(Action<UserDto[]> onSuccess, Action<Exception> onError);
        void Post<T>(string url, Dictionary<string, string> args, Action<T> onSuccess, Action<Exception> onError) where T : class;
        ApiWebSocket WebSocketConnection { get; set; }
    }
}
