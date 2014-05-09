using System.Globalization;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
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
        /// Occurs when [system command].
        /// </summary>
        public event EventHandler<GeneralCommandEventArgs> GeneralCommand;
        public event EventHandler<GenericEventArgs<BrowseRequest>> BrowseCommand;
        public event EventHandler<GenericEventArgs<LibraryUpdateInfo>> LibraryChanged;
        public event EventHandler<GenericEventArgs<MessageCommand>> MessageCommand;
        public event EventHandler<GenericEventArgs<InstallationInfo>> PackageInstallationCancelled;
        public event EventHandler<GenericEventArgs<InstallationInfo>> PackageInstallationCompleted;
        public event EventHandler<GenericEventArgs<InstallationInfo>> PackageInstallationFailed;
        public event EventHandler<GenericEventArgs<InstallationInfo>> PackageInstalling;
        public event EventHandler<GenericEventArgs<PlayRequest>> PlayCommand;
        public event EventHandler<GenericEventArgs<PlaystateRequest>> PlaystateCommand;
        public event EventHandler<GenericEventArgs<PluginInfo>> PluginUninstalled;
        public event EventHandler<GenericEventArgs<TaskResult>> ScheduledTaskEnded;
        public event EventHandler<GenericEventArgs<string>> SendStringCommand;
        public event EventHandler<GenericEventArgs<int>> SetAudioStreamIndexCommand;
        public event EventHandler<GenericEventArgs<int>> SetSubtitleStreamIndexCommand;
        public event EventHandler<GenericEventArgs<int>> SetVolumeCommand;
        public event EventHandler<GenericEventArgs<UserDataChangeInfo>> UserDataChanged;
        public event EventHandler<GenericEventArgs<string>> UserDeleted;
        public event EventHandler<GenericEventArgs<UserDto>> UserUpdated;
        public event EventHandler<EventArgs> NotificationAdded;
        public event EventHandler<EventArgs> NotificationUpdated;
        public event EventHandler<EventArgs> NotificationsMarkedRead;
        public event EventHandler<EventArgs> ServerRestarting;
        public event EventHandler<EventArgs> ServerShuttingDown;
        public event EventHandler<EventArgs> Connected;
        public event EventHandler<SessionUpdatesEventArgs> SessionsUpdated;
        public event EventHandler<EventArgs> RestartRequired;

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
            try
            {
                OnMessageReceivedInternal(json);
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error in OnMessageReceivedInternal", ex);
            }
        }

        private void OnMessageReceivedInternal(string json)
        {
            // deserialize the WebSocketMessage with an object payload
            var messageType = GetMessageType(json);

            Logger.Info("Received web socket message: {0}", messageType);

            if (string.Equals(messageType, "LibraryChanged"))
            {
                FireEvent(LibraryChanged, this, new GenericEventArgs<LibraryUpdateInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<LibraryUpdateInfo>>(json).Data
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

                FireEvent(UserDeleted, this, new GenericEventArgs<string>
                {
                    Argument = userId
                });
            }
            else if (string.Equals(messageType, "ScheduledTaskEnded"))
            {
                FireEvent(ScheduledTaskEnded, this, new GenericEventArgs<TaskResult>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<TaskResult>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstalling"))
            {
                FireEvent(PackageInstalling, this, new GenericEventArgs<InstallationInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationFailed"))
            {
                FireEvent(PackageInstallationFailed, this, new GenericEventArgs<InstallationInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationCompleted"))
            {
                FireEvent(PackageInstallationCompleted, this, new GenericEventArgs<InstallationInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PackageInstallationCancelled"))
            {
                FireEvent(PackageInstallationCancelled, this, new GenericEventArgs<InstallationInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<InstallationInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "UserUpdated"))
            {
                FireEvent(UserUpdated, this, new GenericEventArgs<UserDto>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<UserDto>>(json).Data
                });
            }
            else if (string.Equals(messageType, "PluginUninstalled"))
            {
                FireEvent(PluginUninstalled, this, new GenericEventArgs<PluginInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<PluginInfo>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Play"))
            {
                FireEvent(PlayCommand, this, new GenericEventArgs<PlayRequest>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlayRequest>>(json).Data
                });
            }
            else if (string.Equals(messageType, "Playstate"))
            {
                FireEvent(PlaystateCommand, this, new GenericEventArgs<PlaystateRequest>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<PlaystateRequest>>(json).Data
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
            else if (string.Equals(messageType, "GeneralCommand"))
            {
                OnGeneralCommand(json);
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
                FireEvent(UserDataChanged, this, new GenericEventArgs<UserDataChangeInfo>
                {
                    Argument = _jsonSerializer.DeserializeFromString<WebSocketMessage<UserDataChangeInfo>>(json).Data
                });
            }
        }

        private void OnGeneralCommand(string json)
        {
            var args = new GeneralCommandEventArgs
            {
                Command = _jsonSerializer.DeserializeFromString<WebSocketMessage<GeneralCommand>>(json).Data
            };

            try
            {
                args.KnownCommandType = (GeneralCommandType)Enum.Parse(typeof(GeneralCommandType), args.Command.Name, true);
            }
            catch
            {
                // Could be a custom name.
            }

            if (args.KnownCommandType.HasValue)
            {
                if (args.KnownCommandType.Value == GeneralCommandType.DisplayContent)
                {
                    string itemId;
                    string itemName;
                    string itemType;

                    args.Command.Arguments.TryGetValue("ItemId", out itemId);
                    args.Command.Arguments.TryGetValue("ItemName", out itemName);
                    args.Command.Arguments.TryGetValue("ItemType", out itemType);

                    FireEvent(BrowseCommand, this, new GenericEventArgs<BrowseRequest>
                    {
                        Argument = new BrowseRequest
                        {
                            ItemId = itemId,
                            ItemName = itemName,
                            ItemType = itemType
                        }
                    });
                    return;
                }
                if (args.KnownCommandType.Value == GeneralCommandType.DisplayMessage)
                {
                    string header;
                    string text;
                    string timeoutMs;

                    args.Command.Arguments.TryGetValue("Header", out header);
                    args.Command.Arguments.TryGetValue("Text", out text);
                    args.Command.Arguments.TryGetValue("TimeoutMs", out timeoutMs);

                    long? timeoutVal = string.IsNullOrEmpty(timeoutMs) ? (long?)null : long.Parse(timeoutMs, CultureInfo.InvariantCulture);

                    FireEvent(MessageCommand, this, new GenericEventArgs<MessageCommand>
                    {
                        Argument = new MessageCommand
                        {
                            Header = header,
                            Text = text,
                            TimeoutMs = timeoutVal
                        }
                    });
                    return;
                }
                if (args.KnownCommandType.Value == GeneralCommandType.SetVolume)
                {
                    string volume;

                    args.Command.Arguments.TryGetValue("Volume", out volume);

                    FireEvent(SetVolumeCommand, this, new GenericEventArgs<int>
                    {
                        Argument = int.Parse(volume, CultureInfo.InvariantCulture)
                    });
                    return;
                }
                if (args.KnownCommandType.Value == GeneralCommandType.SetAudioStreamIndex)
                {
                    string index;

                    args.Command.Arguments.TryGetValue("Index", out index);

                    FireEvent(SetAudioStreamIndexCommand, this, new GenericEventArgs<int>
                    {
                        Argument = int.Parse(index, CultureInfo.InvariantCulture)
                    });
                    return;
                }
                if (args.KnownCommandType.Value == GeneralCommandType.SetSubtitleStreamIndex)
                {
                    string index;

                    args.Command.Arguments.TryGetValue("Index", out index);

                    FireEvent(SetSubtitleStreamIndexCommand, this, new GenericEventArgs<int>
                    {
                        Argument = int.Parse(index, CultureInfo.InvariantCulture)
                    });
                    return;
                }
                if (args.KnownCommandType.Value == GeneralCommandType.SendString)
                {
                    string val;

                    args.Command.Arguments.TryGetValue("String", out val);

                    FireEvent(SendStringCommand, this, new GenericEventArgs<string>
                    {
                        Argument = val
                    });
                    return;
                }
            }

            FireEvent(GeneralCommand, this, args);
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
