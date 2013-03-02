using MediaBrowser.Model.Logging;
using System;
using System.Text;

namespace MediaBrowser.ApiInteraction
{
    /// <summary>
    /// Class NullLogger
    /// </summary>
    public class NullLogger : ILogger
    {
        /// <summary>
        /// Debugs the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Debug(string message, params object[] paramList)
        {
        }

        /// <summary>
        /// Errors the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Error(string message, params object[] paramList)
        {
        }

        /// <summary>
        /// Errors the exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="paramList">The param list.</param>
        public void ErrorException(string message, Exception exception, params object[] paramList)
        {
        }

        /// <summary>
        /// Fatals the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Fatal(string message, params object[] paramList)
        {
        }

        /// <summary>
        /// Fatals the exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="paramList">The param list.</param>
        public void FatalException(string message, Exception exception, params object[] paramList)
        {
        }

        /// <summary>
        /// Infoes the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Info(string message, params object[] paramList)
        {
        }

        /// <summary>
        /// Logs the specified severity.
        /// </summary>
        /// <param name="severity">The severity.</param>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Log(LogSeverity severity, string message, params object[] paramList)
        {
        }

        /// <summary>
        /// Logs the multiline.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="additionalContent">Content of the additional.</param>
        public void LogMultiline(string message, LogSeverity severity, StringBuilder additionalContent)
        {
        }

        /// <summary>
        /// Warns the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="paramList">The param list.</param>
        public void Warn(string message, params object[] paramList)
        {
        }
    }
}
