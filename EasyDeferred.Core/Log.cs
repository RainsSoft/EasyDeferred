using System;
using System.IO;

namespace EasyDeferred.Core
{
    #region LogListenerEventArgs Class

    /// <summary>
    /// 
    /// </summary>
    public class LogListenerEventArgs : EventArgs
    {
        /// <summary>
        /// The message to be logged
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// The message level the log is using
        /// </summary>
        public LogMessageLevel Level { get; private set; }

        /// <summary>
        /// If we are printing to the console or not
        /// </summary>
        public bool MaskDebug { get; private set; }

        /// <summary>
        /// the name of this log (so you can have several listeners for different logs, and identify them)
        /// </summary>
        public string LogName { get; private set; }

        /// <summary>
        /// This is called whenever the log recieves a message and is about to write it out
        /// </summary>
        /// <param name="message">The message to be logged</param>
        /// <param name="lml">The message level the log is using</param>
        /// <param name="maskDebug">If we are printing to the console or not</param>
        /// <param name="logName">the name of this log (so you can have several listeners for different logs, and identify them)</param>
        public LogListenerEventArgs(string message, LogMessageLevel lml, bool maskDebug, string logName)
            : base() {
            this.Message = message;
            this.Level = lml;
            this.MaskDebug = maskDebug;
            this.LogName = logName;
        }
    }

    #endregion LogListenerEventArgs Class

    #region Log Class

    /// <summary>
    ///     Log class for writing debug/log data to files.
    /// </summary>
    public sealed class Log : DisposableObject// IDisposable
    {
        #region Fields
        public bool HasCreateFileStream {
            get;
            private set;
        }
        /// <summary>
        ///     File stream used for kepping the log file open.
        /// </summary>
        private FileStream log;

        /// <summary>
        ///     Writer used for writing to the log file.
        /// </summary>
        private StreamWriter writer;

        /// <summary>
        ///     Level of detail for this log.
        /// </summary>
        private LoggingLevel logLevel;

        /// <summary>
        ///     Debug output enabled?
        /// </summary>
        private bool debugOutput;

        /// <summary>
        ///		flag to indicate object already disposed.
        /// </summary>
        //private bool _isDisposed;

        /// <summary>
        ///     LogMessageLevel + LoggingLevel > LOG_THRESHOLD = message logged.
        /// </summary>
        private const int LogThreshold = 4;

        private string mLogName;

        #endregion Fields

        public event EventHandler<LogListenerEventArgs> MessageLogged;

        #region Constructors

        /// <summary>
        ///     Constructor.  Creates a log file that also logs debug output.
        /// </summary>
        /// <param name="fileName">Name of the log file to open.</param>
        public Log(string fileName)
            : this(fileName, true) { }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="fileName">Name of the log file to open.</param>
        /// <param name="debugOutput">Write log messages to the debug output?</param>
        public Log(string fileName, bool debugOutput) : base() {
            this.mLogName = fileName;
            this.MessageLogged = null;

            this.debugOutput = debugOutput;
            logLevel = LoggingLevel.Normal;

            if (fileName != null) {
                try {
                    // create the log file, or open
                    log = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);

                    // get a stream writer using the file stream
                    writer = new StreamWriter(log);
                    writer.AutoFlush = true; //always flush after write
                    HasCreateFileStream = true;
                }
                catch { }
            }
        }

        //~Log() {
        //    Dispose();
        //}

        #endregion Constructors

        #region Properties

        /// <summary>
        ///     Gets/Sets the level of the detail for this log.
        /// </summary>
        /// <value></value>
        public LoggingLevel LogDetail { get { return logLevel; } set { logLevel = value; } }

        #endregion Properties

        #region Methods

        /// <summary>
        ///     Write a message to the log.
        /// </summary>
        /// <remarks>
        ///     Message is written with a LogMessageLevel of Normal, and debug output is not written.
        /// </remarks>
        /// <param name="message">Message to write, which can include string formatting tokens.</param>
        /// <param name="substitutions">
        ///     When message includes string formatting tokens, these are the values to
        ///     inject into the formatted string.
        /// </param>
        public void Write(string message, params object[] substitutions) {
            Write(LogMessageLevel.Normal, false, message, substitutions);
        }

        /// <summary>
        ///     Write a message to the log.
        /// </summary>
        /// <remarks>
        ///     Message is written with a LogMessageLevel of Normal, and debug output is not written.
        /// </remarks>
        /// <param name="maskDebug">If true, debug output will not be written.</param>
        /// <param name="message">Message to write, which can include string formatting tokens.</param>
        /// <param name="substitutions">
        ///     When message includes string formatting tokens, these are the values to
        ///     inject into the formatted string.
        /// </param>
        public void Write(bool maskDebug, string message, params object[] substitutions) {
            Write(LogMessageLevel.Normal, maskDebug, message, substitutions);
        }

        /// <summary>
        ///     Write a message to the log.
        /// </summary>
        /// <param name="level">Importance of this logged message.</param>
        /// <param name="maskDebug">If true, debug output will not be written.</param>
        /// <param name="message">Message to write, which can include string formatting tokens.</param>
        /// <param name="substitutions">
        ///     When message includes string formatting tokens, these are the values to
        ///     inject into the formatted string.
        /// </param>
        public void Write(LogMessageLevel level, bool maskDebug, string message, params object[] substitutions) {
            if (IsDisposed) {
                return;
            }

            if (message == null) {
                throw new ArgumentNullException("The log message cannot be null");
            }
            if (((int)logLevel + (int)level) > LogThreshold) {
                return; //too verbose a message to write
            }

            // construct the log message
            if (substitutions != null && substitutions.Length > 0) {
                message = string.Format(message, substitutions);
            }

            // write the the debug output if requested
            if (debugOutput && !maskDebug) {
                System.Diagnostics.Debug.WriteLine(message);
            }

            if (writer != null && writer.BaseStream != null) {
                // prepend the current time to the message
                message = string.Format("[{0}] {1}", DateTime.Now.ToString("hh:mm:ss"), message);

                // write the message and flush the buffer
                writer.WriteLine(message);
                //writer auto-flushes
            }

            FireMessageLogged(level, maskDebug, message);
        }

        private void FireMessageLogged(LogMessageLevel level, bool maskDebug, string message) {
            // Now fire the MessageLogged event
            if (this.MessageLogged != null) {
                LogListenerEventArgs args = new LogListenerEventArgs(message, level, maskDebug, this.mLogName);
                this.MessageLogged(this, args);
            }
        }

        #endregion Methods

        #region IDisposable Members

        /// <summary>
        ///     Called to dispose of this objects resources.
        /// </summary>
        /// <remarks>
        ///     For the log, closes any open file streams and file writers.
        /// </remarks>
        //public void Dispose() {
        private void Close() {
            try {
                if (writer != null) {
                    writer.Close();
                }

                if (log != null) {
                    log.Close();
                }
            }
            catch { }
            //IsDisposed = true;
        }
        protected override void dispose(bool disposeManagedResources) {
            Close();
            base.dispose(disposeManagedResources);
        }
        #endregion IDisposable Members
    }

    #endregion Log Class
}
