using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.ApiInteraction
{
    public interface IBaseApiClient
    {
        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        IJsonSerializer JsonSerializer { get; set; }

        /// <summary>
        ///  If specified this will be used as a default when an explicit value is not specified.
        /// </summary>
        int? ImageQuality { get; set; }

        /// <summary>
        /// Gets or sets the server host name (myserver or 192.168.x.x)
        /// </summary>
        /// <value>The name of the server host.</value>
        string ServerHostName { get; set; }

        /// <summary>
        /// Gets or sets the port number used by the API
        /// </summary>
        /// <value>The server API port.</value>
        int ServerApiPort { get; set; }

        /// <summary>
        /// Gets or sets the type of the client.
        /// </summary>
        /// <value>The type of the client.</value>
        string ClientName { get; set; }

        /// <summary>
        /// Gets or sets the name of the device.
        /// </summary>
        /// <value>The name of the device.</value>
        string DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        /// <value>The application version.</value>
        string ApplicationVersion { get; set; }

        /// <summary>
        /// Gets or sets the device id.
        /// </summary>
        /// <value>The device id.</value>
        string DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the current user id.
        /// </summary>
        /// <value>The current user id.</value>
        string CurrentUserId { get; set; }

        /// <summary>
        /// Gets the image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string GetImageUrl(BaseItemDto item, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="itemId">The Id of the item</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">itemId</exception>
        string GetImageUrl(string itemId, ImageOptions options);

        /// <summary>
        /// Gets the user image URL.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">user</exception>
        string GetUserImageUrl(UserDto user, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="userId">The Id of the user</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">userId</exception>
        string GetUserImageUrl(string userId, ImageOptions options);

        /// <summary>
        /// Gets the person image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string GetPersonImageUrl(BaseItemPerson item, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name of the person</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetPersonImageUrl(string name, ImageOptions options);

        /// <summary>
        /// Gets the year image URL.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string GetYearImageUrl(BaseItemDto item, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        string GetYearImageUrl(int year, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetGenreImageUrl(string name, ImageOptions options);

        /// <summary>
        /// Gets the music genre image URL.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetMusicGenreImageUrl(string name, ImageOptions options);

        /// <summary>
        /// Gets the game genre image URL.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetGameGenreImageUrl(string name, ImageOptions options);

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetStudioImageUrl(string name, ImageOptions options);

        /// <summary>
        /// Gets the artist image URL.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        string GetArtistImageUrl(string name, ImageOptions options);

        /// <summary>
        /// This is a helper to get a list of backdrop url's from a given ApiBaseItemWrapper. If the actual item does not have any backdrops it will return backdrops from the first parent that does.
        /// </summary>
        /// <param name="item">A given item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String[][].</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string[] GetBackdropImageUrls(BaseItemDto item, ImageOptions options);

        /// <summary>
        /// This is a helper to get the logo image url from a given ApiBaseItemWrapper. If the actual item does not have a logo, it will return the logo from the first parent that does, or null.
        /// </summary>
        /// <param name="item">A given item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string GetLogoImageUrl(BaseItemDto item, ImageOptions options);

        /// <summary>
        /// This is a helper to get the art image url from a given BaseItemDto. If the actual item does not have a logo, it will return the logo from the first parent that does, or null.
        /// </summary>
        /// <param name="item">A given item.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">item</exception>
        string GetArtImageUrl(BaseItemDto item, ImageOptions options);

        /// <summary>
        /// Gets the url needed to stream an audio file
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        string GetAudioStreamUrl(StreamOptions options);

        /// <summary>
        /// Gets the url needed to stream a video file
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        string GetVideoStreamUrl(VideoStreamOptions options);

        /// <summary>
        /// Formulates a url for streaming audio using the HLS protocol
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        string GetHlsAudioStreamUrl(StreamOptions options);

        /// <summary>
        /// Formulates a url for streaming video using the HLS protocol
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        string GetHlsVideoStreamUrl(VideoStreamOptions options);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        void Dispose();
    }
}