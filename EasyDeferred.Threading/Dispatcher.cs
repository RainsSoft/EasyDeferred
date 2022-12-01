using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace EasyDeferred.Threading
{
    /// <summary>
    ///     An enumeration describing the status of a DispatcherOperation.
    /// </summary>
    ///
    public enum DispatcherOperationStatus
    {
        /// <summary>
        ///     The operation is still pending.
        /// </summary>
        Pending,

        /// <summary>
        ///     The operation has been aborted.
        /// </summary>
        Aborted,

        /// <summary>
        ///     The operation has been completed.
        /// </summary>
        Completed,

        /// <summary>
        ///     The operation has started executing, but has not completed yet.
        /// </summary>
        Executing
    }
    /// <summary>
    ///     Provides UI services for a thread.
    /// </summary>
    public sealed class Dispatcher
    {
        //In what case was EnsureStatics needed?
        //When a program has a static Window type that gets initialized in the static constructor?
        //Does this case still work or not? If not, keep code as it was but inlined.
        /// <summary>
        /// Returns the Dispatcher for the current thread.
        /// </summary>
        /// <value>Dispatcher</value>
        public static Dispatcher CurrentDispatcher {
            get {
                Dispatcher dispatcher = FromThread(Thread.CurrentThread);

                //While FromThread() and Dispatcher() both operate in the GlobalLock,
                //and this function does not, there is no race condition because threads cannot
                //create Dispatchers on behalf of other threads. Thus, while other threads may
                //create a Dispatcher for themselves, they cannot create a Dispatcher for this
                //thread, therefore only one Dispatcher for each thread can exist in the ArrayList,
                //and there is no race condition.
                if (dispatcher == null) {
                    lock (typeof(GlobalLock)) {
                        dispatcher = FromThread(Thread.CurrentThread);

                        if (dispatcher == null) {
                            dispatcher = new Dispatcher();
                        }
                    }
                }

                return dispatcher;
            }
        }

        /// <summary>
        ///     Returns the Dispatcher for the specified thread.
        /// </summary>
        /// <remarks>
        ///     If there is no dispatcher available for the specified thread,
        ///     this method will return null.
        /// </remarks>
        public static Dispatcher FromThread(Thread thread) {
            Dispatcher dispatcher = null;

            // _possibleDispatcher is initialized in the static constructor and is never changed.
            // According to section 12.6.6 of Partition I of ECMA 335, reads and writes of object
            // references shall be atomic.
            dispatcher = _possibleDispatcher;
            if (dispatcher == null || dispatcher._thread != thread) {
                // The "possible" dispatcher either was null or belongs to
                // the a different thread.
                dispatcher = null;

                WeakReference wref = (WeakReference)_dispatchers[thread.ManagedThreadId];

                if (wref != null) {
                    dispatcher = wref.Target as Dispatcher;
                    if (dispatcher != null) {
                        if (dispatcher._thread == thread) {
                            // Shortcut: we track one static reference to the last current
                            // dispatcher we gave out.  For single-threaded apps, this will
                            // be set all the time.  For multi-threaded apps, this will be
                            // set for periods of time during which accessing CurrentDispatcher
                            // is cheap.  When a thread switch happens, the next call to
                            // CurrentDispatcher is expensive, but then the rest are fast
                            // again.
                            _possibleDispatcher = dispatcher;
                        }
                        else {
                            _dispatchers.Remove(thread.ManagedThreadId);
                        }
                    }
                }
            }

            return dispatcher;
        }

        private Dispatcher() {
            _thread = Thread.CurrentThread;
            _queue = new Queue();
            _event = new AutoResetEvent(false);
            _instanceLock = new Object();

            // Add ourselves to the map of dispatchers to threads.
            _dispatchers[_thread.ManagedThreadId] = new WeakReference(this);

            if (_possibleDispatcher == null) {
                _possibleDispatcher = this;
            }
        }

        /// <summary>
        ///     Checks that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that any thread can probe to
        ///     see if it has access to the DispatcherObject.
        /// </remarks>
        /// <returns>
        ///     True if the calling thread has access to this object.
        /// </returns>
        public bool CheckAccess() {
            return (_thread == Thread.CurrentThread);
        }

        /// <summary>
        ///     Verifies that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that derived classes can probe to
        ///     see if the calling thread has access to itself.
        /// </remarks>
        public void VerifyAccess() {
            if (_thread != Thread.CurrentThread) {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Thread for the dispatcher.
        /// </summary>
        /// <value></value>
        public Thread Thread {
            get {
                return _thread;
            }
        }

        // Returns whether or not the operation was removed.
        internal bool Abort(DispatcherOperation operation) {
            bool notify = false;

            lock (_instanceLock) {
                if (operation.Status == DispatcherOperationStatus.Pending) {
                    operation.Status = DispatcherOperationStatus.Aborted;
                    notify = true;
                }
            }

            return notify;
        }

        /// <summary>
        ///     Begins the process of shutting down the dispatcher, synchronously.
        ///     The process may complete asynchronously, since we may be
        ///     nested in dispatcher frames.
        /// </summary>
        public void InvokeShutdown() {
            VerifyAccess();

            if (_hasShutdownFinished) {
                throw new InvalidOperationException();
            }

            try {
                if (!_hasShutdownStarted) {
                    // Call the ShutdownStarted event before we actually mark ourselves
                    // as shutting down.  This is so the handlers can actaully do work
                    // when they get this event without throwing exceptions.
                    EventHandler e = ShutdownStarted;
                    if (e != null) {
                        e(this, EventArgs.Empty);
                    }

                    _hasShutdownStarted = true;

                    if (_frameDepth > 0) {
                        // If there are any frames running, we have to wait for them
                        // to unwind before we can safely destroy the dispatcher.
                    }
                    else {
                        // The current thread is not spinning inside of the Dispatcher,
                        // so we can go ahead and destroy it.
                        ShutdownImpl();
                    }

                    _dispatchers.Remove(_thread.ManagedThreadId);
                }
            }
            catch (Exception e) {
                if (_finalExceptionHandler == null || !_finalExceptionHandler(this, e)) {
                    throw;
                }
            }
        }

        private void ShutdownImpl() {
            Debug.Assert(_hasShutdownStarted);
            Debug.Assert(!_hasShutdownFinished);

            // Call the ShutdownFinished event before we actually mark ourselves
            // as shut down.  This is so the handlers can actaully do work
            // when they get this event without throwing exceptions.
            EventHandler e = ShutdownFinished;
            if (e != null) {
                e(this, EventArgs.Empty);
            }

            // Mark this dispatcher as shut down.  Attempts to BeginInvoke
            // or Invoke will result in an exception.
            _hasShutdownFinished = true;

            lock (_instanceLock) {
                // Now that the queue is off-line, abort all pending operations,
                // including inactive ones.
                while (_queue.Count > 0) {
                    DispatcherOperation operation = (DispatcherOperation)_queue.Dequeue();

                    if (operation != null) {
                        operation.Abort();
                    }
                }
            }
        }

        //
        // wakes up the dispatcher to force it to check the
        // frame.Continue flag
        internal void QueryContinueFrame() {
            _event.Set();
        }

        /// <summary>
        ///     Whether or not the dispatcher is shutting down.
        /// </summary>
        public bool HasShutdownStarted {
            get {
                return _hasShutdownStarted;
            }
        }

        /// <summary>
        ///     Whether or not the dispatcher has been shut down.
        /// </summary>
        public bool HasShutdownFinished {
            get {
                return _hasShutdownFinished;
            }
        }

        /// <summary>
        ///     Raised when the dispatcher starts shutting down.
        /// </summary>
        public event EventHandler ShutdownStarted;

        /// <summary>
        ///     Raised when the dispatcher is shut down.
        /// </summary>
        public event EventHandler ShutdownFinished;

        /// <summary>
        ///     Push the main execution frame.
        /// </summary>
        /// <remarks>
        ///     This frame will continue until the dispatcher is shut down.
        /// </remarks>
        public static void Run() {
            PushFrame(new DispatcherFrame());
        }

        /// <summary>
        ///     Push an execution frame.
        /// </summary>
        /// <param name="frame">
        ///     The frame for the dispatcher to process.
        /// </param>
        public static void PushFrame(DispatcherFrame frame) {
            if (frame == null) {
                throw new ArgumentNullException();
            }

            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            if (dispatcher._hasShutdownFinished) {
                throw new InvalidOperationException();
            }

            dispatcher.PushFrameImpl(frame);
        }

        internal DispatcherFrame CurrentFrame {
            get { return _currentFrame; }
        }

        //
        // instance implementation of PushFrame
        private void PushFrameImpl(DispatcherFrame frame) {
            DispatcherFrame prevFrame = _currentFrame;
            _frameDepth++;
            try {
                _currentFrame = frame;

                while (frame.Continue) {
                    DispatcherOperation op = null;
                    bool aborted = false;

                    //
                    // Dequeue the next operation if appropriate
                    if (_queue.Count > 0) {
                        op = (DispatcherOperation)_queue.Dequeue();

                        //Must check aborted flag inside lock because
                        //user program could call op.Abort() between
                        //here and before the call to Invoke()
                        aborted = op.Status == DispatcherOperationStatus.Aborted;
                    }

                    if (op != null) {
                        if (!aborted) {
                            // Invoke the operation:
                            Debug.Assert(op._status == DispatcherOperationStatus.Pending);

                            // Mark this operation as executing.
                            op._status = DispatcherOperationStatus.Executing;

                            op._result = null;

                            try {
                                op._result = op._method(op._args);
                            }
                            catch (Exception e) {
                                if (_finalExceptionHandler == null ||
                                        !_finalExceptionHandler(op, e)) {
                                    throw;
                                }
                            }

                            // Mark this operation as completed.
                            op._status = DispatcherOperationStatus.Completed;

                            // Raise the Completed so anyone who is waiting will wake up.
                            op.OnCompleted();
                        }
                    }
                    else {
                        _event.WaitOne();
                    }
                }
            }
            finally {
                _frameDepth--;

                _currentFrame = prevFrame;

                // If this was the last frame to exit after a quit, we
                // can now dispose the dispatcher.
                if (_frameDepth == 0) {
                    if (_hasShutdownStarted) {
                        ShutdownImpl();
                    }
                }
            }
        }

        /// <summary>
        ///     Executes the specified delegate asynchronously with the specified
        ///     arguments, on the thread that the Dispatcher was created on.
        /// </summary>
        /// <param name="method">
        ///     A delegate to a method that takes parameters of the same number
        ///     and type that are contained in the args parameter.
        /// </param>
        /// <param name="args">
        ///     An object to pass as the argument to the given method.
        ///     This can be null if no arguments are needed.
        /// </param>
        /// <returns>
        ///     A DispatcherOperation object that represents the result of the
        ///     BeginInvoke operation.  null if the operation could not be queued.
        /// </returns>
        public DispatcherOperation BeginInvoke(DispatcherOperationCallback method, object args) {
            if (method == null) {
                throw new ArgumentNullException();
            }

            DispatcherOperation operation = null;

            if (!_hasShutdownFinished) {
                operation = new DispatcherOperation(this, method, args);

                // Add the operation to the work queue
                _queue.Enqueue(operation);

                // this will only cause at most 1 extra dispatcher loop, so
                // always set the event.
                _event.Set();
            }

            return operation;
        }

        /// <summary>
        ///     Executes the specified delegate synchronously with the specified
        ///     arguments, on the thread that the Dispatcher was created on.
        /// </summary>
        /// <param name="timeout">
        ///     The maximum amount of time to wait for the operation to complete.
        /// </param>
        /// <param name="method">
        ///     A delegate to a method that takes parameters of the same number
        ///     and type that are contained in the args parameter.
        /// </param>
        /// <param name="args">
        ///     An object to pass as the argument to the given method.
        ///     This can be null if no arguments are needed.
        /// </param>
        /// <returns>
        ///     The return value from the delegate being invoked, or null if
        ///     the delegate has no return value or if the operation was aborted.
        /// </returns>
        public object Invoke(TimeSpan timeout, DispatcherOperationCallback method, object args) {
            if (method == null) {
                throw new ArgumentNullException();
            }

            object result = null;

            DispatcherOperation op = BeginInvoke(method, args);

            if (op != null) {
                op.Wait(timeout);

                if (op.Status == DispatcherOperationStatus.Completed) {
                    result = op.Result;
                }
                else if (op.Status == DispatcherOperationStatus.Aborted) {
                    // Hm, someone aborted us.  Maybe the dispatcher got
                    // shut down on us?  Just return null.
                }
                else {
                    // We timed out, just abort the op so that it doesn't
                    // invoke later.
                    //
                    // Note the race condition: if this is a foreign thread,
                    // it is possible that the dispatcher thread could actually
                    // dispatch the operation between the time our Wait()
                    // call returns and we get here.  In the case the operation
                    // will actually be dispatched, but we will return failure.
                    //
                    // We recognize this but decide not to do anything about it,
                    // as this is a common problem is multi-threaded programming.
                    op.Abort();
                }
            }

            return result;
        }

        //
        // Invoke a delegate in a try/catch.
        //
        internal object WrappedInvoke(DispatcherOperationCallback callback, object arg) {
            object result = null;

            try {
                result = callback(arg);
            }
            catch (Exception e) {
#if TINYCLR_DEBUG_DISPATCHER
                // allow the debugger to break on the original exception.
                if (System.Diagnostics.Debugger.IsAttached)
                {
                }
                else
#endif
                if (_finalExceptionHandler == null || !_finalExceptionHandler(this, e)) {
                    throw;
                }
            }

            return result;
        }

        internal static void SetFinalDispatcherExceptionHandler(DispatcherExceptionEventHandler handler) {
            Dispatcher.CurrentDispatcher.SetFinalExceptionHandler(handler);
        }

        internal void SetFinalExceptionHandler(DispatcherExceptionEventHandler handler) {
            _finalExceptionHandler = handler;
        }

        private DispatcherFrame _currentFrame;
        private int _frameDepth;
        internal bool _hasShutdownStarted;  // used from DispatcherFrame
        private bool _hasShutdownFinished;

        private Queue _queue;
        private AutoResetEvent _event;
        private object _instanceLock;

        static Hashtable _dispatchers = new Hashtable();
        static Dispatcher _possibleDispatcher;

        // note: avalon uses a weakreference to track the thread.  the advantage i can see to that
        // is in case some other thread has a reference to the dispatcher object, but the dispatcher thread
        // has terminated.  In that case the Thread object would remain until the Dispatcher is GC'd.
        // we dont' have much unmanaged state associated with a dead thread, so it's probably okay to let it
        // hang around.   if we need to run a finalizer on the thread or something, then we should use a weakreference here.

        private Thread _thread;

        // Raised when a dispatcher exception was caught during an Invoke or BeginInvoke
        // Hooked in by the application.
        internal DispatcherExceptionEventHandler _finalExceptionHandler;

        //// these are per dispatcher, track them here.
        //internal Microsoft.SPOT.Presentation.LayoutManager _layoutManager;
        //internal Microsoft.SPOT.Input.InputManager _inputManager;
        //internal Microsoft.SPOT.Presentation.Media.MediaContext _mediaContext;

        //
        // we use this type of a global static lock.  we can't guarantee
        // static constructors are run int he right order, but we can guarantee the
        // lock for the type exists.
        class GlobalLock { }
    }

    /// <summary>
    ///   Delegate for processing exceptions that happen during Invoke or BeginInvoke.
    ///   Return true if the exception was processed.
    /// </summary>
    internal delegate bool DispatcherExceptionEventHandler(object sender, Exception e);

    /// <summary>
    ///   A convenient delegate to use for dispatcher operations.
    /// </summary>
    public delegate object DispatcherOperationCallback(object arg);


    /// <summary>
    ///     A timer that is integrated into the Dispatcher queues, and will
    ///     be processed after a given amount of time
    /// </summary>
    public class DispatcherTimer : IDisposable
    {
        /// <summary>
        ///     Creates a timer that uses the current thread's Dispatcher to
        ///     process the timer event
        /// </summary>
        public DispatcherTimer()
            : this(Dispatcher.CurrentDispatcher) {
        }

        /// <summary>
        ///     Creates a timer that uses the specified Dispatcher to
        ///     process the timer event.
        /// </summary>
        /// <param name="dispatcher">
        ///     The dispatcher to use to process the timer.
        /// </param>
        public DispatcherTimer(Dispatcher dispatcher) {
            if (dispatcher == null) {
                throw new ArgumentNullException("dispatcher");
            }

            _dispatcher = dispatcher;

            _timer = new Timer(new TimerCallback(this.Callback), null, Timeout.Infinite, Timeout.Infinite);

        }

        /// <summary>
        ///     Gets the dispatcher this timer is associated with.
        /// </summary>
        public Dispatcher Dispatcher {
            get {
                return _dispatcher;
            }
        }

        /// <summary>
        ///     Gets or sets whether the timer is running.
        /// </summary>
        public bool IsEnabled {
            get {
                return _isEnabled;
            }

            set {
                lock (_instanceLock) {
                    if (!value && _isEnabled) {
                        Stop();
                    }
                    else if (value && !_isEnabled) {
                        Start();
                    }
                }
            }
        }

        /// <summary>
        ///     Gets or sets the time between timer ticks.
        /// </summary>
        public TimeSpan Interval {
            get {
                return new TimeSpan(_interval * TimeSpan.TicksPerMillisecond);
            }

            set {
                bool updateTimer = false;

                long ticks = value.Ticks;

                if (ticks < 0)
                    throw new ArgumentOutOfRangeException("value", "too small");

                if (ticks > Int32.MaxValue * TimeSpan.TicksPerMillisecond)
                    throw new ArgumentOutOfRangeException("value", "too large");

                lock (_instanceLock) {
                    _interval = (int)(ticks / TimeSpan.TicksPerMillisecond);

                    if (_isEnabled) {
                        updateTimer = true;
                    }
                }

                if (updateTimer) {
                    _timer.Change(_interval, _interval);
                }
            }
        }

        /// <summary>
        ///     Starts the timer.
        /// </summary>
        public void Start() {
            lock (_instanceLock) {
                if (!_isEnabled) {
                    _isEnabled = true;

                    _timer.Change(_interval, _interval);
                }
            }
        }

        /// <summary>
        ///     Stops the timer.
        /// </summary>
        public void Stop() {
            lock (_instanceLock) {
                if (_isEnabled) {
                    _isEnabled = false;

                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        ///     Occurs when the specified timer interval has elapsed and the
        ///     timer is enabled.
        /// </summary>
        public event EventHandler Tick;

        /// <summary>
        ///     Any data that the caller wants to pass along with the timer.
        /// </summary>
        public object Tag {
            get {
                return _tag;
            }

            set {
                _tag = value;
            }
        }

        private void Callback(object state) {
            // BeginInvoke a new operation.
            _dispatcher.BeginInvoke(
                new DispatcherOperationCallback(FireTick),
                null);
        }

        private object FireTick(object unused) {
            EventHandler e = Tick;
            if (e != null) {
                e(this, EventArgs.Empty);
            }

            return null;
        }

        private object _instanceLock = new Object();
        private Dispatcher _dispatcher;
        private int _interval;
        private object _tag;
        private bool _isEnabled;
        private Timer _timer;

        public virtual void Close() {
            Dispose();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            _timer.Dispose();
        }
    }

    /// <summary>
    ///     DispatcherOperation represents a delegate that has been
    ///     posted to the Dispatcher queue.
    /// </summary>
    public sealed class DispatcherOperation
    {

        internal DispatcherOperation(
            Dispatcher dispatcher,
            DispatcherOperationCallback method,
            object args) {
            _dispatcher = dispatcher;
            _method = method;
            _args = args;
        }

        /// <summary>
        ///     Returns the Dispatcher that this operation was posted to.
        /// </summary>
        public Dispatcher Dispatcher {
            get {
                return _dispatcher;
            }
        }

        /// <summary>
        ///     The status of this operation.
        /// </summary>
        public DispatcherOperationStatus Status {
            get {
                return _status;
            }

            internal set {
                _status = value;
            }
        }

        /// <summary>
        ///     Waits for this operation to complete.
        /// </summary>
        /// <returns>
        ///     The status of the operation.  To obtain the return value
        ///     of the invoked delegate, use the the Result property.
        /// </returns>
        public DispatcherOperationStatus Wait() {
            return Wait(new TimeSpan(-TimeSpan.TicksPerMillisecond)); /// Negative one (-1) milliseconds to prevent the timer from starting. See documentation.
        }

        /// <summary>
        ///     Waits for this operation to complete.
        /// </summary>
        /// <param name="timeout">
        ///     The maximum amount of time to wait.
        /// </param>
        /// <returns>
        ///     The status of the operation.  To obtain the return value
        ///     of the invoked delegate, use the the Result property.
        /// </returns>
        public DispatcherOperationStatus Wait(TimeSpan timeout) {
            if ((_status == DispatcherOperationStatus.Pending || _status == DispatcherOperationStatus.Executing) &&
                timeout.Ticks != 0) {
                if (_dispatcher.Thread == Thread.CurrentThread) {
                    if (_status == DispatcherOperationStatus.Executing) {
                        // We are the dispatching thread, and the current operation state is
                        // executing, which means that the operation is in the middle of
                        // executing (on this thread) and is trying to wait for the execution
                        // to complete.  Unfortunately, the thread will now deadlock, so
                        // we throw an exception instead.
                        throw new InvalidOperationException();
                    }

                    // We are the dispatching thread for this operation, so
                    // we can't block.  We will push a frame instead.
                    DispatcherOperationFrame frame = new DispatcherOperationFrame(this, timeout);
                    Dispatcher.PushFrame(frame);
                }
                else {
                    // We are some external thread, so we can just block.  Of
                    // course this means that the Dispatcher (queue)for this
                    // thread (if any) is now blocked.
                    // Because we have a single dispatcher per app domain, this thread
                    // must be from another app domain.  We will enforce semantics on
                    // dispatching between app domains so we don't lock up the system.

                    DispatcherOperationEvent wait = new DispatcherOperationEvent(this, timeout);
                    wait.WaitOne();
                }
            }

            return _status;
        }

        /// <summary>
        ///     Aborts this operation.
        /// </summary>
        /// <returns>
        ///     False if the operation could not be aborted (because the
        ///     operation was already in  progress)
        /// </returns>
        public bool Abort() {
            bool removed = _dispatcher.Abort(this);

            if (removed) {
                _status = DispatcherOperationStatus.Aborted;

                // Raise the Aborted so anyone who is waiting will wake up.
                EventHandler e = Aborted;
                if (e != null) {
                    e(this, EventArgs.Empty);
                }
            }

            return removed;
        }

        /// <summary>
        ///     Returns the result of the operation if it has completed.
        /// </summary>
        public object Result {
            get {
                return _result;
            }
        }

        /// <summary>
        ///     An event that is raised when the operation is aborted.
        /// </summary>
        public event EventHandler Aborted;

        /// <summary>
        ///     An event that is raised when the operation completes.
        /// </summary>
        public event EventHandler Completed;

        internal void OnCompleted() {
            EventHandler e = Completed;
            if (e != null) {
                e(this, EventArgs.Empty);
            }
        }

        private class DispatcherOperationFrame : DispatcherFrame, IDisposable
        {
            // Note: we pass "exitWhenRequested=false" to the base
            // DispatcherFrame construsctor because we do not want to exit
            // this frame if the dispatcher is shutting down. This is
            // because we may need to invoke operations during the shutdown process.
            public DispatcherOperationFrame(DispatcherOperation op, TimeSpan timeout)
                : base(false) {
                _operation = op;

                // We will exit this frame once the operation is completed or aborted.
                _operation.Aborted += new EventHandler(OnCompletedOrAborted);
                _operation.Completed += new EventHandler(OnCompletedOrAborted);

                // We will exit the frame if the operation is not completed within
                // the requested timeout.
                if (timeout.Ticks > 0) {
                    _waitTimer = new Timer(new TimerCallback(OnTimeout),
                                           null,
                                           timeout,
                                           new TimeSpan(-TimeSpan.TicksPerMillisecond)); /// Negative one (-1) milliseconds to disable periodic signaling.
                }

                // Some other thread could have aborted the operation while we were
                // setting up the handlers.  We check the state again and mark the
                // frame as "should not continue" if this happened.
                if (_operation._status != DispatcherOperationStatus.Pending) {
                    Exit();
                }
            }

            private void OnCompletedOrAborted(object sender, EventArgs e) {
                Exit();
            }

            private void OnTimeout(object arg) {
                Exit();
            }

            private void Exit() {
                Continue = false;

                if (_waitTimer != null) {
                    _waitTimer.Dispose();
                }

                _operation.Aborted -= new EventHandler(OnCompletedOrAborted);
                _operation.Completed -= new EventHandler(OnCompletedOrAborted);
            }

            private DispatcherOperation _operation;
            private Timer _waitTimer;

            public virtual void Close() {
                Dispose();
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                _waitTimer.Dispose();
            }

        }

        private class DispatcherOperationEvent : IDisposable
        {
            public DispatcherOperationEvent(DispatcherOperation op, TimeSpan timeout) {
                _operation = op;
                _timeout = timeout;
                _event = new AutoResetEvent(false);

                // We will set our event once the operation is completed or aborted.
                _operation.Aborted += new EventHandler(OnCompletedOrAborted);
                _operation.Completed += new EventHandler(OnCompletedOrAborted);

                // Since some other thread is dispatching this operation, it could
                // have been dispatched while we were setting up the handlers.
                // We check the state again and set the event ourselves if this
                // happened.
                if (_operation._status != DispatcherOperationStatus.Pending && _operation._status != DispatcherOperationStatus.Executing) {
                    _event.Set();
                }
            }

            private void OnCompletedOrAborted(object sender, EventArgs e) {
                _event.Set();
            }

            public void WaitOne() {
                _waitTimer = new Timer(new TimerCallback(OnTimeout), null, _timeout, new TimeSpan(-TimeSpan.TicksPerMillisecond));
                _event.WaitOne();
                _waitTimer.Dispose();

                // Cleanup the events.
                _operation.Aborted -= new EventHandler(OnCompletedOrAborted);
                _operation.Completed -= new EventHandler(OnCompletedOrAborted);
            }

            private void OnTimeout(object arg) {
                _event.Set();
            }

            private DispatcherOperation _operation;
            private TimeSpan _timeout;
            private AutoResetEvent _event;
            private Timer _waitTimer;

            public virtual void Close() {
                Dispose();
            }

            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                _waitTimer.Dispose();
            }
        }

        private Dispatcher _dispatcher;
        internal DispatcherOperationCallback _method;
        internal object _args;
        internal object _result;
        internal DispatcherOperationStatus _status;
    }

    /// <summary>
    ///     A DispatcherObject is an object associated with a
    ///     <see cref="Dispatcher"/>.  A DispatcherObject instance should
    ///     only be access by the dispatcher's thread.
    /// </summary>
    /// <remarks>
    ///     Subclasses of <see cref="DispatcherObject"/> should enforce thread
    ///     safety by calling <see cref="VerifyAccess"/> on all their public
    ///     methods to ensure the calling thread is the appropriate thread.
    ///     <para/>
    ///     DispatcherObject cannot be independently instantiated; that is,
    ///     all constructors are protected.
    /// </remarks>
    public abstract class DispatcherObject
    {

        /// <summary>
        ///     Checks that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that any thread can probe to
        ///     see if it has access to the DispatcherObject.
        /// </remarks>
        /// <returns>
        ///     True if the calling thread has access to this object.
        /// </returns>
        public bool CheckAccess() {
            bool accessAllowed = true;

            // Note: a DispatcherObject that is not associated with a
            // dispatcher is considered to be free-threaded.
            if (Dispatcher != null) {
                accessAllowed = Dispatcher.CheckAccess();
            }

            return accessAllowed;
        }

        /// <summary>
        ///     Verifies that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that derived classes can probe to
        ///     see if the calling thread has access to itself.
        ///
        ///     This is only verified in debug builds.
        /// </remarks>
        public void VerifyAccess() {
            // Note: a DispatcherObject that is not associated with a
            // dispatcher is considered to be free-threaded.
            if (Dispatcher != null) {
                Dispatcher.VerifyAccess();
            }
        }

        /// <summary>
        ///     Instantiate this object associated with the current Dispatcher.
        /// </summary>
        protected DispatcherObject() {
            Dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        ///     Instantiate this object associated with the current Dispatcher.
        /// </summary>
        /// <param name="canBeUnbound">
        ///     Whether or not the object can be detached from any Dispatcher.
        /// </param>
        internal DispatcherObject(bool canBeUnbound) {
            if (canBeUnbound) {
                // DispatcherObjects that can be unbound do not force
                // the creation of a dispatcher.
                Dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
            }
            else {
                Dispatcher = Dispatcher.CurrentDispatcher;
            }
        }

        /// <summary>
        ///     The <see cref="Dispatcher"/> that this
        ///     <see cref="DispatcherObject"/> is associated with.
        /// </summary>
        public readonly Dispatcher Dispatcher;
    }
    /// <summary>
    ///     Representation of Dispatcher frame.
    /// </summary>
    public class DispatcherFrame
    {
        /// <summary>
        ///     Constructs a new instance of the DispatcherFrame class.
        /// </summary>
        public DispatcherFrame()
            : this(true) {
        }

        /// <summary>
        ///     Constructs a new instance of the DispatcherFrame class.
        /// </summary>
        /// <param name="exitWhenRequested">
        ///     Indicates whether or not this frame will exit when all frames
        ///     are requested to exit.
        ///     <p/>
        ///     Dispatcher frames typically break down into two categories:
        ///     1) Long running, general purpose frames, that exit only when
        ///        told to.  These frames should exit when requested.
        ///     2) Short running, very specific frames that exit themselves
        ///        when an important criteria is met.  These frames may
        ///        consider not exiting when requested in favor of waiting
        ///        for their important criteria to be met.  These frames
        ///        should have a timeout associated with them.
        /// </param>
        public DispatcherFrame(bool exitWhenRequested) {
            _exitWhenRequested = exitWhenRequested;
            _continue = true;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        ///     Indicates that this dispatcher frame should exit.
        /// </summary>
        public bool Continue {
            get {
                // First check if this frame wants to continue.
                bool shouldContinue = _continue;
                if (shouldContinue) {
                    // This frame wants to continue, so next check if it will
                    // respect the "exit requests" from the dispatcher.
                    // and if the dispatcher wants to exit.
                    if (_exitWhenRequested && _dispatcher._hasShutdownStarted) {
                        shouldContinue = false;
                    }
                }

                return shouldContinue;
            }

            set {
                _continue = value;

                _dispatcher.QueryContinueFrame();
            }
        }

        private bool _exitWhenRequested;
        private bool _continue;
        private Dispatcher _dispatcher;
    }

}
