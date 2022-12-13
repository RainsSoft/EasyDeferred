﻿using System;
using System.ComponentModel;
using System.Threading;
using EasyDeferred;
namespace Promise_Net20
{
    class ThreadingSystem
    {
        #region Delegates

        /// <summary>
        /// Delegate for the ThreadCompleted event.
        /// </summary>
        public delegate void ThreadCompletedDelegate(object sender, string updateText, bool aborted);

        /// <summary>
        /// Delegate for the ProgressCompleted event.
        /// </summary>
        public delegate void ThreadProgressDelegate(object sender, string updateText, int percentage);

        /// <summary>
        /// Delegate for the ThreadStarted event.
        /// </summary>
        public delegate void ThreadStartedDelegate(object sender, string updateText);

        #endregion Delegates

        /// <summary>
        /// Class used to control IThreadObjects and update IThreadIndicators.
        /// </summary>
        public class ThreadController
        {
            #region Fields

            private ISynchronizeInvoke invokeObject;
            private Thread thread;
            private volatile bool threadAborted;
            private ThreadCompletedDelegate threadCompletedFunction;
            private volatile bool threadPaused;
            private ThreadProgressDelegate threadProgressFunction;
            private volatile bool threadRunning;
            private ThreadStartedDelegate threadStartFunction;

            #endregion Fields

            #region Constructors

            /// <summary>
            /// ThreadController constructor.
            /// </summary>
            public ThreadController(ISynchronizeInvoke invokeObject) {
                this.threadRunning = false;
                this.threadAborted = false;
                this.threadPaused = false;

                this.invokeObject = invokeObject;
            }

            #endregion Constructors

            #region Properties

            public ISynchronizeInvoke InvokeObject {
                get {
                    return this.invokeObject;
                }
            }

            /// <summary>
            /// Property indication the aborted status of a IThreadObject.
            /// </summary>
            public bool ThreadAborted {
                get {
                    return this.threadAborted;
                }
            }

            public bool ThreadPaused {
                get {
                    return this.threadPaused;
                }
            }

            /// <summary>
            /// Property indication the running status of a IThreadObject.
            /// </summary>
            public bool ThreadRunning {
                get {
                    return this.threadRunning;
                }
            }

            #endregion Properties

            #region Methods

            /// <summary>
            /// Method to abort a IThreadObject from executing.
            /// </summary>
            public void AbortThread() {
                this.threadAborted = true;

                if (this.thread != null)
                    this.thread.Join();
            }

            public void PauseThread() {
                this.threadPaused = true;
                this.threadRunning = false;
            }

            /// <summary>
            /// Method meant to be called by a IThreadObject to indicated 
            /// whether it has completed its work.
            /// </summary>
            public void ReportThreadCompleted(object sender, string updateText, bool aborted) {
                this.threadRunning = false;

                if (this.invokeObject == null)
                    return;

                Object[] objects = { sender, updateText, aborted };

                this.invokeObject.BeginInvoke(this.threadCompletedFunction, objects);
            }

            /// <summary>
            /// Method meant to be called by a IThreadObject to indicated 
            /// how much work the it has completed.
            /// </summary>
            public void ReportThreadPercentage(object sender, string updateText, int percentage) {
                if (this.invokeObject == null)
                    return;

                if (this.threadAborted)
                    return;

                Object[] objects = { sender, updateText, percentage };
                this.invokeObject.BeginInvoke(this.threadProgressFunction, objects);
            }

            public void ReportThreadPercentage(object sender, string updateText, int position, int total) {
                if (this.threadAborted)
                    return;

                if (position > total)
                    position = total;

                float percentage = (float)position / total;

                this.ReportThreadPercentage(this, updateText, (int)(percentage * 100.0));
            }

            /// <summary>
            /// Method meant to be called by a IThreadObject to indicated 
            /// whether it has started its work.
            /// </summary>
            public void ReportThreadStarted(object sender, string updateText) {
                if (this.invokeObject == null)
                    return;

                if (this.threadAborted)
                    return;

                Object[] objects = { sender, updateText };

                this.invokeObject.BeginInvoke(this.threadStartFunction, objects);
            }

            public void ResumeThread() {
                this.threadPaused = false;
                this.threadRunning = true;
            }

            public void SetThreadCompletedCallback(ThreadCompletedDelegate threadCompletedFunction) {
                this.threadCompletedFunction = threadCompletedFunction;
            }

            public void SetThreadProgressCallback(ThreadProgressDelegate threadProgressFunction) {
                this.threadProgressFunction = threadProgressFunction;
            }

            public void SetThreadStartedCallback(ThreadStartedDelegate threadStartFunction) {
                this.threadStartFunction = threadStartFunction;
            }

            /// <summary>
            /// Method to join an IThreadObject.
            /// </summary>
            public void ThreadJoin() {
                if (this.ThreadRunning == false)
                    return;

                thread.Join();
            }

            /// <summary>
            /// Method to start an IThreadObject.
            /// </summary>
            public void ThreadStart(string threadName, ThreadStart threadStart) {
                if (this.ThreadRunning == true)
                    return;

                this.thread = new Thread(threadStart);
                this.thread.Name = threadName;

                this.threadAborted = false;
                this.threadPaused = false;
                this.threadRunning = true;

                thread.Start();
            }

            #endregion Methods
        }
      
        public static class Scheduler
        {
            private static Object addEventLock = new Object();

            public static void AddTask(Delegate ev, object[] paramArray, int time) {
                lock (addEventLock) {
                    EasyDeferred.RSG.Action<Delegate, object[], int> myDelegate = new EasyDeferred.RSG.Action<Delegate, object[], int>(AddTaskDelay);
                    myDelegate.BeginInvoke(ev, paramArray, time, null, null);
                }
                
            }

            private static void AddTaskDelay(Delegate ev, object[] paramArray, int time) {
                System.Threading.Thread.Sleep(time);
                bool bFired;

                if (ev != null) {
                    foreach (Delegate singleCast in ev.GetInvocationList()) {
                        bFired = false;
                        try {
                            ISynchronizeInvoke syncInvoke = (ISynchronizeInvoke)singleCast.Target;
                            if (syncInvoke != null && syncInvoke.InvokeRequired) {
                                bFired = true;
                                syncInvoke.BeginInvoke(singleCast, paramArray);
                            }
                            else {
                                bFired = true;
                                singleCast.DynamicInvoke(paramArray);
                            }
                        }
                        catch (Exception) {
                            if (!bFired)
                                singleCast.DynamicInvoke(paramArray);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Exception thrown by AsyncUtils.AsyncOperation.Start when an
        /// operation is already in progress.
        /// </summary>
        public class AlreadyRunningException : System.ApplicationException
        {
            /// <summary>
            /// 
            /// </summary>
            public AlreadyRunningException()
                : base("Asynchronous operation already running") { }
        }

        /// <summary>
        /// This base class is designed to be used by lengthy operations that wish to
        /// support cancellation.  It also allows those operations to invoke delegates
        /// on the UI Thread of a hosting control.
        /// </summary>
        /// <remarks>
        /// This class is from the MSDN article:
        /// http://msdn.microsoft.com/msdnmag/issues/03/02/Multithreading/default.aspx
        /// (C) 2001-2002 I D Griffiths
        /// Please see the article for a complete description of the intentions and
        /// operation of this class.
        /// </remarks>
        public abstract class AsyncOperation
        {
            /// <summary>
            /// Initialises an AsyncOperation with an association to the
            /// supplied ISynchronizeInvoke.  All events raised from this
            /// object will be delivered via this target.  (This might be a
            /// Control object, so events would be delivered to that Control's
            /// UI thread.)
            /// </summary>
            /// <param name="target">An object implementing the
            /// ISynchronizeInvoke interface.  All events will be delivered
            /// through this target, ensuring that they are delivered to the
            /// correct thread.</param>
            public AsyncOperation(ISynchronizeInvoke target) {
                isiTarget = target;
                isRunning = false;
            }

            /// <summary>
            /// Launch the operation on a worker thread.  This method will
            /// return immediately, and the operation will start asynchronously
            /// on a worker thread.
            /// </summary>
            public void Start() {
                lock (this) {
                    if (isRunning) {
                        throw new AlreadyRunningException();
                    }
                    // Set this flag here, not inside InternalStart, to avoid
                    // race condition when Start called twice in quick
                    // succession.
                    isRunning = true;
                }
                //new MethodInvoker(InternalStart).BeginInvoke(null, null);
                new EasyDeferred.RSG.Action(InternalStart).BeginInvoke(null,null);
            }


            /// <summary>
            /// Attempt to cancel the current operation.  This returns
            /// immediately to the caller.  No guarantee is made as to
            /// whether the operation will be successfully cancelled.  All
            /// that can be known is that at some point, one of the
            /// three events Completed, Cancelled, or Failed will be raised
            /// at some point.
            /// </summary>
            public virtual void Cancel() {
                lock (this) {
                    cancelledFlag = true;
                }
            }

            /// <summary>
            /// Attempt to cancel the current operation and block until either
            /// the cancellation succeeds or the operation completes.
            /// </summary>
            /// <returns>true if the operation was successfully cancelled
            /// or it failed, false if it ran to completion.</returns>
            public bool CancelAndWait() {
                lock (this) {
                    // Set the cancelled flag

                    cancelledFlag = true;


                    // Now sit and wait either for the operation to
                    // complete or the cancellation to be acknowledged.
                    // (Wake up and check every second - shouldn't be
                    // necessary, but it guarantees we won't deadlock
                    // if for some reason the Pulse gets lost - means
                    // we don't have to worry so much about bizarre
                    // race conditions.)
                    while (!IsDone) {
                        Monitor.Wait(this, 1000);
                    }
                }
                return !HasCompleted;
            }

            /// <summary>
            /// Blocks until the operation has either run to completion, or has
            /// been successfully cancelled, or has failed with an internal
            /// exception.
            /// </summary>
            /// <returns>true if the operation completed, false if it was
            /// cancelled before completion or failed with an internal
            /// exception.</returns>
            public bool WaitUntilDone() {
                lock (this) {
                    // Wait for either completion or cancellation.  As with
                    // CancelAndWait, we don't sleep forever - to reduce the
                    // chances of deadlock in obscure race conditions, we wake
                    // up every second to check we didn't miss a Pulse.
                    while (!IsDone) {
                        Monitor.Wait(this, 1000);
                    }
                }
                return HasCompleted;
            }


            /// <summary>
            /// Returns false if the operation is still in progress, or true if
            /// it has either completed successfully, been cancelled
            ///  successfully, or failed with an internal exception.
            /// </summary>
            public bool IsDone {
                get {
                    lock (this) {
                        return completeFlag || cancelAcknowledgedFlag || failedFlag;
                    }
                }
            }

            /// <summary>
            /// This event will be fired if the operation runs to completion
            /// without being cancelled.  This event will be raised through the
            /// ISynchronizeTarget supplied at construction time.  Note that
            /// this event may still be received after a cancellation request
            /// has been issued.  (This would happen if the operation completed
            /// at about the same time that cancellation was requested.)  But
            /// the event is not raised if the operation is cancelled
            /// successfully.
            /// </summary>
            public event EventHandler Completed;


            /// <summary>
            /// This event will be fired when the operation is successfully
            /// stoped through cancellation.  This event will be raised through
            /// the ISynchronizeTarget supplied at construction time.
            /// </summary>
            public event EventHandler Cancelled;


            /// <summary>
            /// This event will be fired if the operation throws an exception.
            /// This event will be raised through the ISynchronizeTarget
            /// supplied at construction time.
            /// </summary>
            public event System.Threading.ThreadExceptionEventHandler Failed;


            /// <summary>
            /// The ISynchronizeTarget supplied during construction - this can
            /// be used by deriving classes which wish to add their own events.
            /// </summary>
            protected ISynchronizeInvoke Target {
                get { return isiTarget; }
            }
            private ISynchronizeInvoke isiTarget;


            /// <summary>
            /// To be overridden by the deriving class - this is where the work
            /// will be done.  The base class calls this method on a worker
            /// thread when the Start method is called.
            /// </summary>
            protected abstract void DoWork();


            /// <summary>
            /// Flag indicating whether the request has been cancelled.  Long-
            /// running operations should check this flag regularly if they can
            /// and cancel their operations as soon as they notice that it has
            /// been set.
            /// </summary>
            protected bool CancelRequested {
                get {
                    lock (this) { return cancelledFlag; }
                }
            }
            private bool cancelledFlag;


            /// <summary>
            /// Flag indicating whether the request has run through to
            /// completion.  This will be false if the request has been
            /// successfully cancelled, or if it failed.
            /// </summary>
            protected bool HasCompleted {
                get {
                    lock (this) { return completeFlag; }
                }
            }
            private bool completeFlag;


            /// <summary>
            /// This is called by the operation when it wants to indicate that
            /// it saw the cancellation request and honoured it.
            /// </summary>
            protected void AcknowledgeCancel() {
                lock (this) {
                    cancelAcknowledgedFlag = true;
                    isRunning = false;

                    // Pulse the event in case the main thread is blocked
                    // waiting for us to finish (e.g. in CancelAndWait or
                    // WaitUntilDone).
                    Monitor.Pulse(this);

                    // Using async invocation to avoid a potential deadlock
                    // - using Invoke would involve a cross-thread call
                    // whilst we still held the object lock.  If the event
                    // handler on the UI thread tries to access this object
                    // it will block because we have the lock, but using
                    // async invocation here means that once we've fired
                    // the event, we'll run on and release the object lock,
                    // unblocking the UI thread.
                    FireAsync(Cancelled, this, EventArgs.Empty);
                }
            }
            private bool cancelAcknowledgedFlag;


            // Set to true if the operation fails with an exception.
            private bool failedFlag;
            // Set to true if the operation is running
            private bool isRunning;

            public bool IsRunning() {
                return this.isRunning;
            }


            // This method is called on a worker thread (via asynchronous
            // delegate invocation).  This is where we call the operation (as
            // defined in the deriving class's DoWork method).
            private void InternalStart() {
                // Reset our state - we might be run more than once.
                cancelledFlag = false;
                completeFlag = false;
                cancelAcknowledgedFlag = false;
                failedFlag = false;
                // isRunning is set during Start to avoid a race condition
                try {
                    DoWork();
                }
                catch (Exception e) {
                    // Raise the Failed event.  We're in a catch handler, so we
                    // had better try not to throw another exception.
                    try {
                        FailOperation(e);
                    }
                    catch { }

                    // The documentation recommends not catching
                    // SystemExceptions, so having notified the caller we
                    // rethrow if it was one of them.
                    if (e is SystemException) {
                        //throw;
                    }
                }

                lock (this) {
                    // If the operation wasn't cancelled (or if the UI thread
                    // tried to cancel it, but the method ran to completion
                    // anyway before noticing the cancellation) and it
                    // didn't fail with an exception, then we complete the
                    // operation - if the UI thread was blocked waiting for
                    // cancellation to complete it will be unblocked, and
                    // the Completion event will be raised.
                    if (!cancelAcknowledgedFlag && !failedFlag) {
                        CompleteOperation();
                    }
                }
            }


            // This is called when the operation runs to completion.
            // (This is private because it is called automatically
            // by this base class when the deriving class's DoWork
            // method exits without having cancelled

            private void CompleteOperation() {
                lock (this) {
                    completeFlag = true;
                    isRunning = false;
                    Monitor.Pulse(this);
                    // See comments in AcknowledgeCancel re use of
                    // Async.
                    FireAsync(Completed, this, EventArgs.Empty);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="e"></param>
            private void FailOperation(Exception e) {
                lock (this) {
                    failedFlag = true;
                    isRunning = false;
                    Monitor.Pulse(this);
                    FireAsync(Failed, this, new ThreadExceptionEventArgs(e));
                }
            }

            /// <summary>
            /// Utility function for firing an event through the target.
            /// It uses C#'s variable length parameter list support
            /// to build the parameter list.
            /// This functions presumes that the caller holds the object lock.
            /// (This is because the event list is typically modified on the UI
            /// thread, but events are usually raised on the worker thread.)
            /// </summary>
            /// <param name="dlg"></param>
            /// <param name="pList"></param>
            protected void FireAsync(Delegate dlg, params object[] pList) {
                if (dlg != null) {
                    Target.BeginInvoke(dlg, pList);
                }
            }
        }

        //class AsyncInvokeResult : IAsyncResult
        //{
        //    ManualResetEventSlim asyncResetEvent = new ManualResetEventSlim(false);

        //    public AsyncInvokeResult() {
        //        this.asyncResetEvent = new ManualResetEventSlim();
        //    }

        //    internal void Invoke(Delegate method, object[] args) {
        //        Application.Invoke(delegate {
        //            try {
        //                AsyncState = method.DynamicInvoke(args);
        //            }
        //            catch (Exception ex) {
        //                Exception = ex;
        //            }
        //            finally {
        //                IsCompleted = true;
        //                asyncResetEvent.Set();
        //            }
        //        });
        //    }

        //    #region IAsyncResult implementation

        //    public object AsyncState {
        //        get;
        //        private set;
        //    }

        //    public Exception Exception {
        //        get;
        //        private set;
        //    }

        //    internal ManualResetEventSlim AsyncResetEvent {
        //        get {
        //            return asyncResetEvent;
        //        }
        //    }

        //    public WaitHandle AsyncWaitHandle {
        //        get {
        //            if (asyncResetEvent == null) {
        //                asyncResetEvent = new ManualResetEventSlim(false);

        //                if (IsCompleted)
        //                    asyncResetEvent.Set();
        //            }
        //            return asyncResetEvent.WaitHandle;
        //        }
        //    }

        //    public bool CompletedSynchronously { get { return false; } }


        //    public bool IsCompleted {
        //        get;
        //        private set;
        //    }

        //    #endregion
        //}
    }

    //sealed class DispatcherMessageLoop : IMessageLoop, ISynchronizeInvoke
    //{
    //    readonly System.Windows.Threading.Dispatcher dispatcher;
    //    readonly SynchronizationContext synchronizationContext;

    //    public DispatcherMessageLoop(Dispatcher dispatcher, SynchronizationContext synchronizationContext) {
    //        this.dispatcher = dispatcher;
    //        this.synchronizationContext = synchronizationContext;
    //    }

    //    public Thread Thread {
    //        get { return dispatcher.Thread; }
    //    }

    //    public Dispatcher Dispatcher {
    //        get { return dispatcher; }
    //    }

    //    public SynchronizationContext SynchronizationContext {
    //        get { return synchronizationContext; }
    //    }

    //    public ISynchronizeInvoke SynchronizingObject {
    //        get { return this; }
    //    }

    //    public bool InvokeRequired {
    //        get { return !dispatcher.CheckAccess(); }
    //    }

    //    public bool CheckAccess() {
    //        return dispatcher.CheckAccess();
    //    }

    //    public void VerifyAccess() {
    //        dispatcher.VerifyAccess();
    //    }

    //    public void InvokeIfRequired(Action callback) {
    //        if (dispatcher.CheckAccess())
    //            callback();
    //        else
    //            dispatcher.Invoke(callback);
    //    }

    //    public void InvokeIfRequired(Action callback, DispatcherPriority priority) {
    //        if (dispatcher.CheckAccess())
    //            callback();
    //        else
    //            dispatcher.Invoke(callback, priority);
    //    }

    //    public void InvokeIfRequired(Action callback, DispatcherPriority priority, CancellationToken cancellationToken) {
    //        if (dispatcher.CheckAccess())
    //            callback();
    //        else
    //            dispatcher.Invoke(callback, priority, cancellationToken);
    //    }

    //    public T InvokeIfRequired<T>(Func<T> callback) {
    //        if (dispatcher.CheckAccess())
    //            return callback();
    //        else
    //            return dispatcher.Invoke(callback);
    //    }

    //    public T InvokeIfRequired<T>(Func<T> callback, DispatcherPriority priority) {
    //        if (dispatcher.CheckAccess())
    //            return callback();
    //        else
    //            return dispatcher.Invoke(callback, priority);
    //    }

    //    public T InvokeIfRequired<T>(Func<T> callback, DispatcherPriority priority, CancellationToken cancellationToken) {
    //        if (dispatcher.CheckAccess())
    //            return callback();
    //        else
    //            return dispatcher.Invoke(callback, priority, cancellationToken);
    //    }

    //    public Task InvokeAsync(Action callback) {
    //        return dispatcher.InvokeAsync(callback).Task;
    //    }

    //    public Task InvokeAsync(Action callback, DispatcherPriority priority) {
    //        return dispatcher.InvokeAsync(callback, priority).Task;
    //    }

    //    public Task InvokeAsync(Action callback, DispatcherPriority priority, CancellationToken cancellationToken) {
    //        return dispatcher.InvokeAsync(callback, priority, cancellationToken).Task;
    //    }

    //    public Task<T> InvokeAsync<T>(Func<T> callback) {
    //        return dispatcher.InvokeAsync(callback).Task;
    //    }

    //    public Task<T> InvokeAsync<T>(Func<T> callback, DispatcherPriority priority) {
    //        return dispatcher.InvokeAsync(callback, priority).Task;
    //    }

    //    public Task<T> InvokeAsync<T>(Func<T> callback, DispatcherPriority priority, CancellationToken cancellationToken) {
    //        return dispatcher.InvokeAsync(callback, priority, cancellationToken).Task;
    //    }

    //    public void InvokeAsyncAndForget(Action callback) {
    //        dispatcher.BeginInvoke(callback);
    //    }

    //    public void InvokeAsyncAndForget(Action callback, DispatcherPriority priority) {
    //        dispatcher.BeginInvoke(callback, priority);
    //    }

    //    public async void CallLater(TimeSpan delay, Action method) {
    //        await Task.Delay(delay).ConfigureAwait(false);
    //        InvokeAsyncAndForget(method);
    //    }

    //    IAsyncResult ISynchronizeInvoke.BeginInvoke(Delegate method, object[] args) {
    //        return dispatcher.BeginInvoke(method, args).Task;
    //    }

    //    object ISynchronizeInvoke.EndInvoke(IAsyncResult result) {
    //        return ((Task<object>)result).Result;
    //    }

    //    object ISynchronizeInvoke.Invoke(Delegate method, object[] args) {
    //        return dispatcher.Invoke(method, args);
    //    }
    //}

    ///// <summary>
    ///// Class with extension methods for <see cref="Dispatcher"/> and <see cref="DispatcherObject"/>.
    ///// </summary>
    //internal static class DispatcherExtensions
    //{
    //    public static void RunInDispatcherAsync(this DispatcherObject dispatcher, Action action,
    //        DispatcherPriority priority = DispatcherPriority.Normal) {
    //        if (dispatcher == null) {
    //            action();
    //            return;
    //        }

    //        dispatcher.Dispatcher.RunInDispatcherAsync(action, priority);
    //    }

    //    public static void RunInDispatcherAsync(this Dispatcher dispatcher, Action action,
    //        DispatcherPriority priority = DispatcherPriority.Normal) {
    //        if (dispatcher == null) {
    //            action();
    //        }
    //        else {
    //            dispatcher.BeginInvoke(priority, action);
    //        }
    //    }

    //    public static void RunInDispatcher(this DispatcherObject dispatcher, Action action,
    //        DispatcherPriority priority = DispatcherPriority.Normal) {
    //        if (dispatcher == null) {
    //            action();
    //            return;
    //        }

    //        dispatcher.Dispatcher.RunInDispatcher(action, priority);
    //    }

    //    public static void RunInDispatcher(this Dispatcher dispatcher, Action action,
    //        DispatcherPriority priority = DispatcherPriority.Normal) {
    //        if (dispatcher == null
    //            || dispatcher.CheckAccess()) {
    //            action();
    //        }
    //        else {
    //            dispatcher.Invoke(priority, action);
    //        }
    //    }
    //}
}