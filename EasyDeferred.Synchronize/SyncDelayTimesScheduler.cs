using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;
using EasyDeferred.Collections;

namespace EasyDeferred.Synchronize
{
   
    /// <summary>
    ///     Provides functionality for timestamped delegate invocation.
    /// </summary>
    public class SyncDelayTimesScheduler : IDisposable, IComponent
    {
        #region IDisposable Members

        public void Dispose() {
            #region Guard

            if (disposed) return;

            #endregion

            Dispose(true);
        }

        #endregion

        #region DelegateScheduler Members

        #region Fields

        /// <summary>
        ///     A constant value representing an unlimited number of delegate invocations.
        /// </summary>
        public const int Infinite = -1;

        // Default polling interval.
        private const int DefaultPollingInterval = 10;

        // For queuing the delegates in priority order.
        private readonly PriorityQueue queue = new PriorityQueue();

        // Used for timing events for polling the delegate queue.
        private readonly Timer timer = new Timer(DefaultPollingInterval);

        // For storing tasks when the scheduler isn't running.
        private readonly List<SyncDelayTimesJob> tasks = new List<SyncDelayTimesJob>();

        // A value indicating whether the DelegateScheduler is running.

        // A value indicating whether the DelegateScheduler has been disposed.
        private bool disposed;

        #endregion
       
        #region Events

        /// <summary>
        ///     Raised when a delegate is invoked.
        /// </summary>
        public event EventHandler<InvokeCompletedEventArgs> InvokeCompleted;

        #endregion

        #region Construction

        /// <summary>
        ///     Initializes a new instance of the DelegateScheduler class.
        /// </summary>
        public SyncDelayTimesScheduler() {
            Initialize();
        }

        /// <summary>
        ///     Initializes a new instance of the DelegateScheduler class with the
        ///     specified IContainer.
        /// </summary>
        public SyncDelayTimesScheduler(IContainer container) {
            ///
            /// Required for Windows.Forms Class Composition Designer support
            ///
            container.Add(this);

            Initialize();
        }

        // Initializes the DelegateScheduler.
        private void Initialize() {

            timer.Elapsed += HandleElapsed;
        }

        ~SyncDelayTimesScheduler() {
            Dispose(false);
        }

        #endregion

        #region Methods

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                Stop();

                timer.Dispose();

                Clear();

                disposed = true;

                OnDisposed(EventArgs.Empty);

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        ///     Adds a delegate to the DelegateScheduler.
        /// </summary>
        /// <param name="count">
        ///     The number of times the delegate should be invoked.
        /// </param>
        /// <param name="millisecondsTimeout">
        ///     The time in milliseconds between delegate invocation.
        /// </param>
        /// <param name="method">
        /// </param>
        /// The delegate to invoke.
        /// <param name="args">
        ///     The arguments to pass to the delegate when it is invoked.
        /// </param>
        /// <returns>
        ///     A Task object representing the scheduled task.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        ///     If the DelegateScheduler has already been disposed.
        /// </exception>
        /// <remarks>
        ///     If an unlimited count is desired, pass the DelegateScheduler.Infinity
        ///     constant as the count argument.
        /// </remarks>
        public SyncDelayTimesJob Add(
            int count,
            int millisecondsTimeout,
            Delegate method,
            params object[] args) {
            #region Require

            if (disposed) throw new ObjectDisposedException("DelegateScheduler");

            #endregion

            var t = new SyncDelayTimesJob(count, millisecondsTimeout, method, args);

            lock (queue.SyncRoot) {
                // Only add the task to the DelegateScheduler if the count 
                // is greater than zero or set to Infinite.
                if (count > 0 || count == Infinite) {
                    if (IsRunning)
                        queue.Enqueue(t);
                    else
                        tasks.Add(t);
                }
            }

            return t;
        }

        /// <summary>
        ///     Removes the specified Task.
        /// </summary>
        /// <param name="task">
        ///     The Task to be removed.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        ///     If the DelegateScheduler has already been disposed.
        /// </exception>
        public void Remove(SyncDelayTimesJob task) {
            #region Require

            if (disposed) throw new ObjectDisposedException("DelegateScheduler");

            #endregion

            #region Guard

            if (task == null) return;

            #endregion

            lock (queue.SyncRoot) {
                if (IsRunning)
                    queue.Remove(task);
                else
                    tasks.Remove(task);
            }
        }

        /// <summary>
        ///     Starts the DelegateScheduler.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     If the DelegateScheduler has already been disposed.
        /// </exception>
        public void Start() {
            #region Require

            if (disposed) throw new ObjectDisposedException(GetType().Name);

            #endregion

            #region Guard

            if (IsRunning) return;

            #endregion

            lock (queue.SyncRoot) {
                SyncDelayTimesJob t;

                while (tasks.Count > 0) {
                    t = tasks[tasks.Count - 1];

                    tasks.RemoveAt(tasks.Count - 1);

                    t.ResetNextTimeout();

                    queue.Enqueue(t);
                }

                IsRunning = true;

                timer.Start();
            }
        }

        /// <summary>
        ///     Stops the DelegateScheduler.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     If the DelegateScheduler has already been disposed.
        /// </exception>
        public void Stop() {
            #region Require

            if (disposed) throw new ObjectDisposedException(GetType().Name);

            #endregion

            #region Guard

            if (!IsRunning) return;

            #endregion

            lock (queue.SyncRoot) {
                // While there are still tasks left in the queue.
                while (queue.Count > 0)
                    // Remove task from queue and add it to the Task list
                    // to be used again next time the DelegateScheduler is run.
                    tasks.Add((SyncDelayTimesJob)queue.Dequeue());

                timer.Stop();

                IsRunning = false;
            }
        }

        /// <summary>
        ///     Clears the DelegateScheduler of all tasks.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     If the DelegateScheduler has already been disposed.
        /// </exception>
        public void Clear() {
            #region Require

            if (disposed) throw new ObjectDisposedException(GetType().Name);

            #endregion

            lock (queue.SyncRoot) {
                queue.Clear();
                tasks.Clear();
            }
        }

        // Responds to the timer's Elapsed event by running any tasks that are due.
        private void HandleElapsed(object sender, ElapsedEventArgs e) {
            Debug.WriteLine("Signal time: " + e.SignalTime);

            lock (queue.SyncRoot) {
                #region Guard

                if (queue.Count == 0) return;

                #endregion

                // Take a look at the first task in the queue to see if it's
                // time to run it.
                var tk = (SyncDelayTimesJob)queue.Peek();

                // The return value from the delegate that will be invoked.
                object returnValue;

                // While there are still tasks in the queue and it is time 
                // to run one or more of them.
                while (queue.Count > 0 && tk.NextTimeout <= e.SignalTime) {
                    // Remove task from queue.
                    queue.Dequeue();

                    // While it's time for the task to run.
                    while ((tk.Count == Infinite || tk.Count > 0) && tk.NextTimeout <= e.SignalTime)
                        try {
                            Debug.WriteLine("Invoking delegate.");
                            Debug.WriteLine("Next timeout: " + tk.NextTimeout);

                            // Invoke delegate.
                            returnValue = tk.Invoke(e.SignalTime);

                            OnInvokeCompleted(
                                new InvokeCompletedEventArgs(
                                    tk.Method,
                                    tk.GetArgs(),
                                    returnValue,
                                    null));
                        }
                        catch (Exception ex) {
                            OnInvokeCompleted(
                                new InvokeCompletedEventArgs(
                                    tk.Method,
                                    tk.GetArgs(),
                                    null,
                                    ex));
                        }

                    // If this task should run again.
                    if (tk.Count == Infinite || tk.Count > 0) queue.Enqueue(tk);

                    // If there are still tasks in the queue.
                    if (queue.Count > 0) tk = (SyncDelayTimesJob)queue.Peek();
                }
            }
        }

        // Raises the Disposed event.
        protected virtual void OnDisposed(EventArgs e) {
            var handler = Disposed;

            if (handler != null) handler(this, e);
        }

        // Raises the InvokeCompleted event.
        protected virtual void OnInvokeCompleted(InvokeCompletedEventArgs e) {
            var handler = InvokeCompleted;

            if (handler != null) handler(this, e);
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets the interval in milliseconds in which the
        ///     DelegateScheduler polls its queue of delegates in order to
        ///     determine when they should run.
        /// </summary>
        public double PollingInterval {
            get {
                #region Require

                if (disposed) throw new ObjectDisposedException("PriorityQueue");

                #endregion

                return timer.Interval;
            }
            set {
                #region Require

                if (disposed) throw new ObjectDisposedException("PriorityQueue");

                #endregion

                timer.Interval = value;
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the DelegateScheduler is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        ///     Gets or sets the object used to marshal event-handler calls and delegate invocations.
        /// </summary>
        public ISynchronizeInvoke SynchronizingObject {
            get { return timer.SynchronizingObject; }
            set { timer.SynchronizingObject = value; }
        }

        #endregion

        #endregion

        #region IComponent Members

        public event EventHandler Disposed;

        public ISite Site { get; set; } = null;

        #endregion
    }

    public class SyncDelayTimesJob : IComparable
    {
        #region IComparable Members

        public int CompareTo(object obj) {
            var t = obj as SyncDelayTimesJob;

            if (t == null) throw new ArgumentException("obj is not the same type as this instance.");

            return -NextTimeout.CompareTo(t.NextTimeout);
        }

        #endregion

        #region Task Members

        #region Fields

        // The number of times left to invoke the delegate associated with this Task.

        // The interval between delegate invocation.

        // The delegate to invoke.

        // The arguments to pass to the delegate when it is invoked.
        private readonly object[] args;

        // The time for the next timeout;

        // For locking.
        private readonly object lockObject = new object();

        #endregion

        #region Construction

        internal SyncDelayTimesJob(
            int count,
            int millisecondsTimeout,
            Delegate method,
            object[] args) {
            Count = count;
            MillisecondsTimeout = millisecondsTimeout;
            Method = method;
            this.args = args;

            ResetNextTimeout();
        }

        #endregion

        #region Methods

        internal void ResetNextTimeout() {
            NextTimeout = DateTime.Now.AddMilliseconds(MillisecondsTimeout);
        }

        internal object Invoke(DateTime signalTime) {
            Debug.Assert(Count == SyncDelayTimesScheduler.Infinite || Count > 0);

            var returnValue = Method.DynamicInvoke(args);

            if (Count == SyncDelayTimesScheduler.Infinite) {
                NextTimeout = NextTimeout.AddMilliseconds(MillisecondsTimeout);
            }
            else {
                Count--;

                if (Count > 0) NextTimeout = NextTimeout.AddMilliseconds(MillisecondsTimeout);
            }

            return returnValue;
        }

        public object[] GetArgs() {
            return args;
        }

        #endregion

        #region Properties

        public DateTime NextTimeout { get; private set; }

        public int Count { get; private set; }

        public Delegate Method { get; }

        public int MillisecondsTimeout { get; }

        #endregion

        #endregion
    }
}
