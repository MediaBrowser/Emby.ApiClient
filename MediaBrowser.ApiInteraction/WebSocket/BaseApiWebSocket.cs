using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Updates;
using System;
using System.Text;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public abstract class BaseApiWebSocket
    {
        /// <summary>
        /// The _logger
        /// </summary>
        protected readonly ILogger Logger;
        /// <summary>
        /// The _json serializer
        /// </summary>
        private readonly IJsonSerializer _jsonSerializer;
        /// <summary>
        /// Occurs when [user deleted].
        /// </summary>
        public event EventHandler<UserDeletedEventArgs> UserDeleted;
        /// <summary>
        /// Occurs when [scheduled task started].
        /// </summary>
        public event EventHandler<ScheduledTaskStartedEventArgs> ScheduledTaskStarted;
        /// <summary>
        /// Occurs when [scheduled task ended].
        /// </summary>
        public event EventHandler<ScheduledTaskEndedEventArgs> ScheduledTaskEnded;
        /// <summary>
        /// Occurs when [package installing].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstalling;
        /// <summary>
        /// Occurs when [package installation failed].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationFailed;
        /// <summary>
        /// Occurs when [package installation completed].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationCompleted;
        /// <summary>
        /// Occurs when [package installation cancelled].
        /// </summary>
        public event EventHandler<PackageInstallationEventArgs> PackageInstallationCancelled;
        /// <summary>
        /// Occurs when [user updated].
        /// </summary>
        public event EventHandler<UserUpdatedEventArgs> UserUpdated;
        /// <summary>
        /// Occurs when [plugin uninstalled].
        /// </summary>
        public event EventHandler<PluginUninstallEventArgs> PluginUninstalled;
        /// <summary>
        /// Occurs when [library changed].
        /// </summary>
        public event EventHandler<LibraryChangedEventArgs> LibraryChanged;

        public event EventHandler<BrowseRequestEventArgs> BrowseCommand;
        public event EventHandler<PlayRequestEventArgs> PlayCommand;
        public event EventHandler<PlaystateRequestEventArgs> PlaystateCommand;

        public event EventHandler<EventArgs> NotificationAdded;
        public event EventHandler<EventArgs> NotificationUpdated;
        public event EventHandler<EventArgs> NotificationsMarkedRead;

        /// <summary>
        /// Occurs when [restart required].
        /// </summary>
        public event EventHandler<EventArgs> RestartRequired;

        /// <summary>
        /// The identification message name
        /// </summary>
        protected const string IdentificationMessageName = "Identity";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        protected BaseApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer)
        {
            Logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Gets the web socket URL.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <returns>System.String.</returns>
        protected string GetWebSocketUrl(string serverHostName, int serverWebSocketPort)
        {
            return string.Format("ws://{0}:{1}/mediabrowser", serverHostName, serverWebSocketPort);
        }

        /// <summary>
        /// Called when [message received].
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        protected void OnMessageReceived(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

            var message = _jsonSerializer.DeserializeFromString<WebSocketMessage<string>>(json);

            Logger.Info("Received web socket message: {0}", message.MessageType);

            if (string.Equals(message.MessageType, "LibraryChanged"))
            {
                FireEvent(LibraryChanged, this, new LibraryChangedEventArgs
                {
                    UpdateInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<LibraryUpdateInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "RestartRequired"))
            {
                FireEvent(RestartRequired, this, EventArgs.Empty);
            }
            else if (string.Equals(message.MessageType, "UserDeleted"))
            {
                FireEvent(UserDeleted, this, new UserDeletedEventArgs
                {
                    Id = message.Data
                });
            }
            else if (string.Equals(message.MessageType, "ScheduledTaskStarted"))
            {
                FireEvent(ScheduledTaskStarted, this, new ScheduledTaskStartedEventArgs
                {
                    Name = message.Data
                });
            }
            else if (string.Equals(message.MessageType, "ScheduledTaskEnded"))
            {
                FireEvent(ScheduledTaskEnded, this, new ScheduledTaskEndedEventArgs
                {
                    Result = _jsonSerializer.DeserializeFromString<WebSocketMessage<TaskResult>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstalling"))
            {
                FireEvent(PackageInstalling, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationFailed"))
            {
                FireEvent(PackageInstallationFailed, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationCompleted"))
            {
                FireEvent(PackageInstallationCompleted, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationCancelled"))
            {
                FireEvent(PackageInstallationCancelled, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "UserUpdated"))
            {
                FireEvent(UserUpdated, this, new UserUpdatedEventArgs
                {
                    User = _jsonSerializer.DeserializeFromString<WebSocketMessage<UserDto>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "PluginUninstalled"))
            {
                FireEvent(PluginUninstalled, this, new PluginUninstallEventArgs
                {
                    PluginInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<PluginInfo>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "Browse"))
            {
                FireEvent(BrowseCommand, this, new BrowseRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<BrowseRequest>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "Play"))
            {
                FireEvent(PlayCommand, this, new PlayRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlayRequest>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "UpdatePlaystate"))
            {
                FireEvent(PlaystateCommand, this, new PlaystateRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlaystateRequest>>(json).Data
                });
            }
            else if (string.Equals(message.MessageType, "NotificationAdded"))
            {
                FireEvent(NotificationAdded, this, EventArgs.Empty);
            }
            else if (string.Equals(message.MessageType, "NotificationUpdated"))
            {
                FireEvent(NotificationUpdated, this, EventArgs.Empty);
            }
            else if (string.Equals(message.MessageType, "NotificationsMarkedRead"))
            {
                FireEvent(NotificationsMarkedRead, this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Queues the event if not null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The handler.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The args.</param>
        private void FireEvent<T>(EventHandler<T> handler, object sender, T args)
            where T : EventArgs
        {
            if (handler != null)
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Error in event handler", ex);
                }
            }
        }

        /// <summary>
        /// Gets the message bytes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <returns>System.Byte[][].</returns>
        protected byte[] GetMessageBytes<T>(string messageName, T data)
        {
            var msg = new WebSocketMessage<T> { MessageType = messageName, Data = data };

            return _jsonSerializer.SerializeToBytes(msg);
        }

        /// <summary>
        /// Gets the identification message.
        /// </summary>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <returns>System.String.</returns>
        protected string GetIdentificationMessage(string clientName, string deviceId, string applicationVersion)
        {
            return clientName + "|" + deviceId + "|" + applicationVersion;
        }
    }
}
