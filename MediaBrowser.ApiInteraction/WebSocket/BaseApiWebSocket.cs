using MediaBrowser.Model.ApiClient;
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
    public abstract class BaseApiWebSocket : IServerEvents
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

        /// <summary>
        /// Occurs when [browse command].
        /// </summary>
        public event EventHandler<BrowseRequestEventArgs> BrowseCommand;
        /// <summary>
        /// Occurs when [play command].
        /// </summary>
        public event EventHandler<PlayRequestEventArgs> PlayCommand;
        /// <summary>
        /// Occurs when [playstate command].
        /// </summary>
        public event EventHandler<PlaystateRequestEventArgs> PlaystateCommand;
        /// <summary>
        /// Occurs when [message command].
        /// </summary>
        public event EventHandler<MessageCommandEventArgs> MessageCommand;
        /// <summary>
        /// Occurs when [system command].
        /// </summary>
        public event EventHandler<SystemCommandEventArgs> SystemCommand;

        /// <summary>
        /// Occurs when [notification added].
        /// </summary>
        public event EventHandler<EventArgs> NotificationAdded;
        /// <summary>
        /// Occurs when [notification updated].
        /// </summary>
        public event EventHandler<EventArgs> NotificationUpdated;
        /// <summary>
        /// Occurs when [notifications marked read].
        /// </summary>
        public event EventHandler<EventArgs> NotificationsMarkedRead;

        /// <summary>
        /// Occurs when [server restarting].
        /// </summary>
        public event EventHandler<EventArgs> ServerRestarting;
        /// <summary>
        /// Occurs when [server shutting down].
        /// </summary>
        public event EventHandler<EventArgs> ServerShuttingDown;

        /// <summary>
        /// Occurs when [connected].
        /// </summary>
        public event EventHandler<EventArgs> Connected;
        
        /// <summary>
        /// Occurs when [sessions updated].
        /// </summary>
        public event EventHandler<SessionUpdatesEventArgs> SessionsUpdated;

        /// <summary>
        /// Occurs when [restart required].
        /// </summary>
        public event EventHandler<EventArgs> RestartRequired;

        /// <summary>
        /// Occurs when [user data changed].
        /// </summary>
        public event EventHandler<UserDataChangedEventArgs> UserDataChanged;
        
        /// <summary>
        /// Gets or sets the server host name (myserver or 192.168.x.x)
        /// </summary>
        /// <value>The name of the server host.</value>
        public string ServerHostName { get; protected set; }

        /// <summary>
        /// Gets the server web socket port.
        /// </summary>
        /// <value>The server web socket port.</value>
        public int ServerWebSocketPort { get; protected set; }

        /// <summary>
        /// Gets or sets the type of the client.
        /// </summary>
        /// <value>The type of the client.</value>
        public string ApplicationName { get; private set; }

        /// <summary>
        /// Gets the name of the device.
        /// </summary>
        /// <value>The name of the device.</value>
        public string DeviceName { get; private set; }
        
        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        /// <value>The application version.</value>
        public string ApplicationVersion { get; private set; }

        /// <summary>
        /// Gets or sets the device id.
        /// </summary>
        /// <value>The device id.</value>
        public string DeviceId { get; private set; }

        /// <summary>
        /// The identification message name
        /// </summary>
        protected const string IdentificationMessageName = "Identity";

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiWebSocket" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="deviceId">The device id.</param>
        /// <param name="applicationVersion">The application version.</param>
        /// <param name="applicationName">Name of the application.</param>
        /// <param name="deviceName">Name of the device.</param>
        protected BaseApiWebSocket(ILogger logger, IJsonSerializer jsonSerializer, string serverHostName, int serverWebSocketPort, string deviceId, string applicationVersion, string applicationName, string deviceName)
        {
            Logger = logger;
            _jsonSerializer = jsonSerializer;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
            DeviceId = deviceId;
            ServerWebSocketPort = serverWebSocketPort;
            ServerHostName = serverHostName;
            DeviceName = deviceName;
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

            OnMessageReceived(json);
        }

        /// <summary>
        /// Called when [message received].
        /// </summary>
        /// <param name="json">The json.</param>
        protected void OnMessageReceived(string json)
        {
            // deserialize the WebSocketMessage with an object payload
            string messageType;

            try
            {
                messageType = GetMessageType(json);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error deserializing web socket message", ex);
                return;
            }

            Logger.Info("Received web socket message: {0}", messageType);

            if (string.Equals(messageType, "LibraryChanged"))
            {
                FireEvent(LibraryChanged, this, new LibraryChangedEventArgs
                {
                    UpdateInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<LibraryUpdateInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "RestartRequired"))
            {
                FireEvent(RestartRequired, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "ServerRestarting"))
            {
                FireEvent(ServerRestarting, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "ServerShuttingDown"))
            {
                FireEvent(ServerShuttingDown, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "UserDeleted"))
            {
                var userId = _jsonSerializer.DeserializeFromString<WebSocketMessage<string>>(json).Data;

                FireEvent(UserDeleted, this, new UserDeletedEventArgs
                {
                    Id = userId
                });
            }
            else if (string.Equals(messageType, "ScheduledTaskStarted"))
            {
                var taskName = _jsonSerializer.DeserializeFromString<WebSocketMessage<string>>(json).Data;

                FireEvent(ScheduledTaskStarted, this, new ScheduledTaskStartedEventArgs
                {
                    Name = taskName
                });
            }
            else if (string.Equals(messageType, "ScheduledTaskEnded"))
            {
                FireEvent(ScheduledTaskEnded, this, new ScheduledTaskEndedEventArgs
                {
                    Result = _jsonSerializer.DeserializeFromString<WebSocketMessage<TaskResult>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstalling"))
            {
                FireEvent(PackageInstalling, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationFailed"))
            {
                FireEvent(PackageInstallationFailed, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationCompleted"))
            {
                FireEvent(PackageInstallationCompleted, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationCancelled"))
            {
                FireEvent(PackageInstallationCancelled, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "UserUpdated"))
            {
                FireEvent(UserUpdated, this, new UserUpdatedEventArgs
                {
                    User = _jsonSerializer.DeserializeFromString<WebSocketMessage<UserDto>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PluginUninstalled"))
            {
                FireEvent(PluginUninstalled, this, new PluginUninstallEventArgs
                {
                    PluginInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<PluginInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Browse"))
            {
                FireEvent(BrowseCommand, this, new BrowseRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<BrowseRequest>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Play"))
            {
                FireEvent(PlayCommand, this, new PlayRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlayRequest>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Playstate"))
            {
                FireEvent(PlaystateCommand, this, new PlaystateRequestEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlaystateRequest>>(json).Data
                });
            }
            else if (string.Equals(messageType, "NotificationAdded"))
            {
                FireEvent(NotificationAdded, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "NotificationUpdated"))
            {
                FireEvent(NotificationUpdated, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "NotificationsMarkedRead"))
            {
                FireEvent(NotificationsMarkedRead, this, EventArgs.Empty);
            }
            else if (string.Equals(messageType, "SystemCommand"))
            {
                FireEvent(SystemCommand, this, new SystemCommandEventArgs
                {
                    Command = _jsonSerializer.DeserializeFromString<WebSocketMessage<SystemCommand>>(json).Data
                });
            }
            else if (string.Equals(messageType, "MessageCommand"))
            {
                FireEvent(MessageCommand, this, new MessageCommandEventArgs
                {
                    Request = _jsonSerializer.DeserializeFromString<WebSocketMessage<MessageCommand>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Sessions"))
            {
                FireEvent(SessionsUpdated, this, new SessionUpdatesEventArgs
                {
                    Sessions = _jsonSerializer.DeserializeFromString<WebSocketMessage<SessionInfoDto[]>>(json).Data
                });
            }
            else if (string.Equals(messageType, "UserDataChanged"))
            {
                FireEvent(UserDataChanged, this, new UserDataChangedEventArgs
                {
                    ChangeInfo = _jsonSerializer.DeserializeFromString<WebSocketMessage<UserDataChangeInfo>>(json).Data
                });
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
        public abstract bool IsConnected
        {
            get;
        }

        /// <summary>
        /// Gets the type of the message.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <returns>System.String.</returns>
        private string GetMessageType(string json)
        {
            var message = _jsonSerializer.DeserializeFromString<WebSocketMessage>(json);
            return message.MessageType;
        }

        /// <summary>
        /// Called when [connected].
        /// </summary>
        protected void OnConnected()
        {
            FireEvent(Connected, this, EventArgs.Empty);
        }

        /// <summary>
        /// Queues the event if not null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The handler.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The args.</param>
        protected void FireEvent<T>(EventHandler<T> handler, object sender, T args)
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
        /// <returns>System.String.</returns>
        protected string GetIdentificationMessage()
        {
            return ApplicationName + "|" + DeviceId + "|" + ApplicationVersion + "|" + DeviceName;
        }
    }

    /// <summary>
    /// Class WebSocketMessage
    /// </summary>
    class WebSocketMessage
    {
        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        /// <value>The type of the message.</value>
        public string MessageType { get; set; }
    }
}
