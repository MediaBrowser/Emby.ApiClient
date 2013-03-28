using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Updates;
using System;
using System.Text;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public abstract  class BaseApiWebSocket
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
        /// Occurs when [restart required].
        /// </summary>
        public event EventHandler<EventArgs> RestartRequired;

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
                QueueEventIfNotNull(LibraryChanged, this, new LibraryChangedEventArgs
                {
                    UpdateInfo = _jsonSerializer.DeserializeFromString<LibraryUpdateInfo>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "RestartRequired"))
            {
                QueueEventIfNotNull(RestartRequired, this, EventArgs.Empty);
            }
            else if (string.Equals(message.MessageType, "UserDeleted"))
            {
                QueueEventIfNotNull(UserDeleted, this, new UserDeletedEventArgs
                {
                    Id = message.Data
                });
            }
            else if (string.Equals(message.MessageType, "ScheduledTaskStarted"))
            {
                QueueEventIfNotNull(ScheduledTaskStarted, this, new ScheduledTaskStartedEventArgs
                {
                    Name = message.Data
                });
            }
            else if (string.Equals(message.MessageType, "ScheduledTaskEnded"))
            {
                QueueEventIfNotNull(ScheduledTaskEnded, this, new ScheduledTaskEndedEventArgs
                {
                    Result = _jsonSerializer.DeserializeFromString<TaskResult>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstalling"))
            {
                QueueEventIfNotNull(PackageInstalling, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationFailed"))
            {
                QueueEventIfNotNull(PackageInstallationFailed, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationCompleted"))
            {
                QueueEventIfNotNull(PackageInstallationCompleted, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "PackageInstallationCancelled"))
            {
                QueueEventIfNotNull(PackageInstallationCancelled, this, new PackageInstallationEventArgs
                {
                    InstallationInfo = _jsonSerializer.DeserializeFromString<InstallationInfo>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "UserUpdated"))
            {
                QueueEventIfNotNull(UserUpdated, this, new UserUpdatedEventArgs
                {
                    User = _jsonSerializer.DeserializeFromString<UserDto>(message.Data)
                });
            }
            else if (string.Equals(message.MessageType, "PluginUninstalled"))
            {
                QueueEventIfNotNull(PluginUninstalled, this, new PluginUninstallEventArgs
                {
                    PluginInfo = _jsonSerializer.DeserializeFromString<PluginInfo>(message.Data)
                });
            }
        }

        /// <summary>
        /// Queues the event if not null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">The handler.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The args.</param>
        protected abstract void QueueEventIfNotNull<T>(EventHandler<T> handler, object sender, T args)
            where T : EventArgs;
    }
}
