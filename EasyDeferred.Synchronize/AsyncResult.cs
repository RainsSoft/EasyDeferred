﻿using System;
using System.Threading;

namespace EasyDeferred.Synchronize
{
    /// <summary>
    ///     Provides basic implementation of the IAsyncResult interface.
    /// </summary>
    internal class AsyncResult : IAsyncResult
    {
        #region AsyncResult Members

        #region Fields

        // The owner of this AsyncResult object.

        // The callback to be invoked when the operation completes.
        private readonly AsyncCallback callback;

        // User state information.

        // For signaling when the operation has completed.
        private readonly ManualResetEvent waitHandle = new ManualResetEvent(false);

        // A value indicating whether the operation completed synchronously.

        // A value indicating whether the operation has completed.

        // The ID of the thread this AsyncResult object originated on.
        private readonly int threadId;

        #endregion

        #region Construction

        /// <summary>
        ///     Initializes a new instance of the AsyncResult object with the
        ///     specified owner of the AsyncResult object, the optional callback
        ///     delegate, and optional state object.
        /// </summary>
        /// <param name="owner">
        ///     The owner of the AsyncResult object.
        /// </param>
        /// <param name="callback">
        ///     An optional asynchronous callback, to be called when the
        ///     operation is complete.
        /// </param>
        /// <param name="state">
        ///     A user-provided object that distinguishes this particular
        ///     asynchronous request from other requests.
        /// </param>
        public AsyncResult(object owner, AsyncCallback callback, object state) {
            Owner = owner;
            this.callback = callback;
            AsyncState = state;

            // Get the current thread ID. This will be used later to determine
            // if the operation completed synchronously.
            threadId = Thread.CurrentThread.ManagedThreadId;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Signals that the operation has completed.
        /// </summary>
        public void Signal() {
            IsCompleted = true;

            CompletedSynchronously = threadId == Thread.CurrentThread.ManagedThreadId;

            waitHandle.Set();

            if (callback != null) callback(this);
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the owner of this AsyncResult object.
        /// </summary>
        public object Owner { get; }

        #endregion

        #endregion

        #region IAsyncResult Members

        public object AsyncState { get; }

        public WaitHandle AsyncWaitHandle => waitHandle;

        public bool CompletedSynchronously { get; private set; }

        public bool IsCompleted { get; private set; }

        #endregion
    }
}
