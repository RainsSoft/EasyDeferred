using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace EasyDeferred.Threading
{
    #region ITimer
    public interface ITimer : IComponent
    {
        /// <summary>
        ///     Gets a value indicating whether the Timer is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        ///     Gets the timer mode.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     If the timer has already been disposed.
        /// </exception>
        TimerMode Mode { get; set; }

        /// <summary>
        ///     Period between timer events in milliseconds.
        ///     计时器事件之间的时间间隔（毫秒）。
        /// </summary>
        int Period { get; set; }

        /// <summary>
        ///     Resolution of the timer in milliseconds.
        ///     计时器的分辨率（毫秒）。
        /// </summary>
        int Resolution { get; set; }

        /// <summary>
        ///     Gets or sets the object used to marshal event-handler calls.
        /// </summary>
        ISynchronizeInvoke SynchronizingObject { get; set; }

        /// <summary>
        ///     Occurs when the Timer has started;
        /// </summary>
        event EventHandler Started;

        /// <summary>
        ///     Occurs when the Timer has stopped;
        /// </summary>
        event EventHandler Stopped;

        /// <summary>
        ///     Occurs when the time period has elapsed.
        ///     在经过时间段时发生。
        /// </summary>
        event EventHandler Tick;

        /// <summary>
        ///     Starts the timer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     The timer has already been disposed.
        /// </exception>
        /// <exception cref="TimerStartException">
        ///     The timer failed to start.
        /// </exception>
        void Start();

        /// <summary>
        ///     Stops timer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        ///     If the timer has already been disposed.
        /// </exception>
        void Stop();
    }
    #endregion 

    #region enum/struct
    /// <summary>
    ///     Defines constants for the multimedia Timer's event types.
    /// </summary>
    public enum TimerMode
    {
        /// <summary>
        ///     Timer event occurs once.
        /// </summary>
        OneShot,

        /// <summary>
        ///     Timer event occurs periodically.
        /// </summary>
        Periodic
    }
    #endregion
    /// <summary>
    ///     Replacement for the Windows multimedia timer that also runs on Mono
    ///     单线程执行时间Tick
    /// </summary>
    public sealed class ThreadTimer : ITimer
    {
        private static readonly object[] emptyArgs = { EventArgs.Empty };

        private bool disposed;

        private TimerMode mode;
        private readonly ThreadTimerQueue queue;
        private TimeSpan resolution;

        // For implementing IComponent.

        // The ISynchronizeInvoke object to use for marshaling events.
        private ISynchronizeInvoke synchronizingObject;

        // Represents the method that raises the Tick event.
        private readonly EventRaiser tickRaiser;

        public ThreadTimer()
            : this(ThreadTimerQueue.Instance) {
            if (!Stopwatch.IsHighResolution) throw new NotImplementedException("Stopwatch is not IsHighResolution");

            IsRunning = false;
            mode = TimerMode.Periodic;
            resolution = TimeSpan.FromMilliseconds(1);
            PeriodTimeSpan = resolution;

            tickRaiser = OnTick;
        }

        private ThreadTimer(ThreadTimerQueue queue) {
            this.queue = queue;
        }

        public TimeSpan PeriodTimeSpan { get; private set; }

        public bool IsRunning { get; private set; }

        public TimerMode Mode {
            get {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                return mode;
            }

            set {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                mode = value;

                if (IsRunning) {
                    Stop();
                    Start();
                }
            }
        }

        public int Period {
            get {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                return (int)PeriodTimeSpan.TotalMilliseconds;
            }
            set {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                var wasRunning = IsRunning;

                if (wasRunning) Stop();

                PeriodTimeSpan = TimeSpan.FromMilliseconds(value);

                if (wasRunning) Start();
            }
        }

        public int Resolution {
            get { return (int)resolution.TotalMilliseconds; }

            set { resolution = TimeSpan.FromMilliseconds(value); }
        }

        public ISite Site { get; set; } = null;

        /// <summary>
        ///     Gets or sets the object used to marshal event-handler calls.
        /// </summary>
        public ISynchronizeInvoke SynchronizingObject {
            get {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                return synchronizingObject;
            }
            set {
                #region Require

                if (disposed) throw new ObjectDisposedException("Timer");

                #endregion

                synchronizingObject = value;
            }
        }

        public event EventHandler Disposed;
        public event EventHandler Started;
        public event EventHandler Stopped;
        public event EventHandler Tick;

        public void Dispose() {
            Stop();
            disposed = true;
            OnDisposed(EventArgs.Empty);
        }

        public void Start() {
            #region Require

            if (disposed) throw new ObjectDisposedException("Timer");

            #endregion

            #region Guard

            if (IsRunning) return;

            #endregion

            // If the periodic event callback should be used.
            if (Mode == TimerMode.Periodic) {
                queue.Add(this);
                IsRunning = true;
            }
            // Else the one shot event callback should be used.
            else {
                throw new NotImplementedException();
            }

            if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                SynchronizingObject.BeginInvoke(
                    new EventRaiser(OnStarted),
                    new object[] { EventArgs.Empty });
            else
                OnStarted(EventArgs.Empty);
        }

        public void Stop() {
            #region Require

            if (disposed) throw new ObjectDisposedException("Timer");

            #endregion

            #region Guard

            if (!IsRunning) return;

            #endregion

            queue.Remove(this);
            IsRunning = false;

            if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                SynchronizingObject.BeginInvoke(
                    new EventRaiser(OnStopped),
                    new object[] { EventArgs.Empty });
            else
                OnStopped(EventArgs.Empty);
        }

        internal void DoTick() {
            if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                SynchronizingObject.BeginInvoke(tickRaiser, emptyArgs);
            else
                OnTick(EventArgs.Empty);
        }

        // Represents methods that raise events.
        private delegate void EventRaiser(EventArgs e);

        #region Event Raiser Methods

        // Raises the Disposed event.
        private void OnDisposed(EventArgs e) {
            var handler = Disposed;

            if (handler != null) handler(this, e);
        }

        // Raises the Started event.
        private void OnStarted(EventArgs e) {
            var handler = Started;

            if (handler != null) handler(this, e);
        }

        // Raises the Stopped event.
        private void OnStopped(EventArgs e) {
            var handler = Stopped;

            if (handler != null) handler(this, e);
        }

        // Raises the Tick event.
        private void OnTick(EventArgs e) {
            var handler = Tick;

            if (handler != null) handler(this, e);
        }

        #endregion
    }

    /// <summary>
    ///     Queues and executes timer events in an internal worker thread.
    /// </summary>
    class ThreadTimerQueue
    {
        private static ThreadTimerQueue instance;
        private Thread loop;
        private readonly List<Tick> tickQueue = new List<Tick>();
        private readonly Stopwatch watch = Stopwatch.StartNew();

        private ThreadTimerQueue() {
        }

        public static ThreadTimerQueue Instance {
            get {
                if (instance == null) instance = new ThreadTimerQueue();
                return instance;
            }
        }

        public void Add(ThreadTimer timer) {
            lock (this) {
                var tick = new Tick {
                    Timer = timer,
                    Time = watch.Elapsed
                };
                tickQueue.Add(tick);
                tickQueue.Sort();

                if (loop == null) {
                    loop = new Thread(TimerLoop);
                    loop.Start();
                }

                Monitor.PulseAll(this);
            }
        }

        public void Remove(ThreadTimer timer) {
            lock (this) {
                var i = 0;
                for (; i < tickQueue.Count; ++i)
                    if (tickQueue[i].Timer == timer)
                        break;
                if (i < tickQueue.Count) tickQueue.RemoveAt(i);
                Monitor.PulseAll(this);
            }
        }

        private static TimeSpan Min(TimeSpan x0, TimeSpan x1) {
            if (x0 > x1)
                return x1;
            return x0;
        }

        /// <summary>
        ///     The thread to execute the timer events
        /// </summary>
        private void TimerLoop() {
            lock (this) {
                var maxTimeout = TimeSpan.FromMilliseconds(500);

                for (var queueEmptyCount = 0; queueEmptyCount < 3; ++queueEmptyCount) {
                    var waitTime = maxTimeout;
                    if (tickQueue.Count > 0) {
                        waitTime = Min(tickQueue[0].Time - watch.Elapsed, waitTime);
                        queueEmptyCount = 0;
                    }

                    if (waitTime > TimeSpan.Zero) Monitor.Wait(this, waitTime);

                    if (tickQueue.Count > 0) {
                        var tick = tickQueue[0];
                        var mode = tick.Timer.Mode;
                        Monitor.Exit(this);
                        tick.Timer.DoTick();
                        Monitor.Enter(this);
                        if (mode == TimerMode.Periodic) {
                            tick.Time += tick.Timer.PeriodTimeSpan;
                            tickQueue.Sort();
                        }
                        else {
                            tickQueue.RemoveAt(0);
                        }
                    }
                }

                loop = null;
            }
        }

        private class Tick : IComparable
        {
            public TimeSpan Time;
            public ThreadTimer Timer;

            public int CompareTo(object obj) {
                var r = obj as Tick;
                if (r == null) return -1;
                return Time.CompareTo(r.Time);
            }
        }
    }
}
