/*

    Copyright (c) 2011 Serge Danzanvilliers

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

*/

using System;
using System.Collections.Generic;
using System.Threading;
using EasyDeferred.RSG;
using EasyDeferred.Threading;

namespace EasyDeferred.FairThreadPool
{
    /// <summary>
    /// The "Fair Thread Pool", an instanciable thread pool that allows
    /// "fair scheduling" of enqueued workers. Worker jobs are associated to
    /// tags, which may be viewed as a family marker for jobs. Job scheduling
    /// alternate between the tags in round robin each time a thread has to pick
    /// a worker to run. Inside a given tag jobs are scheduled in fifo order.
    /// 
    /// Workers are simply 'Action' or 'Func<>'.
    /// Action can be waited for if you want or simply forgotten.
    /// Func<> may return any type of value. The returned value is accessed
    /// through a Future pattern.
    /// </summary>
    public sealed class FairThreadPool : IDisposable
    {
        // FairThreadPool

        //A simple thread pool allowing scheduling of jobs in a "fair" way.Jobs are
        //enqueued in the pool associated to a tag.To pick a job to run, the threads
        //in the pool cycle through the tags in round robin. Thus you're guaranted
        //that jobs associated to a tag are never blocked by a bunch of jobs associated
        //to another tag: the pool will alternate between tags when picking jobs from
        //its queue.

        //The interface is simple and allows:
        //	- pushing jobs in the pool and forget them.
        //	- pushing jobs and wait for completion.
        //	- pushing jobs and get a result through use of the Future pattern.
        //	- changing the number of threads in the pool at runtime.
        //	- multiple thread pools: the FairThreadPool is not a singleton.
        /// <summary>
        /// Build a new FairThreadPool and start the threads.
        /// </summary>
        /// <param name="name">A name for the thread pool.</param>
        /// <param name="maxThreads">Maximum number of threads in the pool.</param>
        public FairThreadPool(string name, int maxThreads) {
            //long id = Interlocked.Increment(ref _id);
            _name = name;//string.Format("{0}{1}", id, name ?? "");
            if (maxThreads <= 0) {
                throw new ArgumentOutOfRangeException("maxThreads", maxThreads, _name + ": maximum number of threads cannot be zero or less.");
            }
            _name = name;
            NThreads = maxThreads;
            StartThreads();
        }
        public FairThreadPool(string name) :
             this(name, ((2 % Environment.ProcessorCount) + 1) * Environment.ProcessorCount) {
            //int wmin = 0;
            //int iomin = 0;
            //int wmax = 0;
            //int iomax = 0;
            //System.Threading.ThreadPool.GetMinThreads(out wmin, out iomin);
            //System.Threading.ThreadPool.GetMaxThreads(out wmax, out iomax);
            //var min = wmin;
            //var max = Math.Min(50, iomax);
            //max = Math.Max(max, min);            
        }
        
        /// <summary>
        /// The name of the FairThreadPool instance.
        /// </summary>
        public string Name {
            get { return _name; }
        }

        /// <summary>
        /// Enqueue a job associated to a given queue tag.
        /// </summary>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        public void QueueWorker(int tag, Action worker) {
            lock (_condition) {
                _actions.Enqueue(tag, worker);
                Monitor.Pulse(_condition);
            }
        }

        /// <summary>
        /// Enqueue a job associated to queue tag 0.
        /// </summary>
        /// <param name="worker">Job to run.</param>
        public void QueueWorker(Action worker) {
            QueueWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job and provide a waitable object to wait for
        /// completion. The job is associated to queue tag 0.
        /// </summary>
        /// <param name="worker">Job to run.</param>
        /// <returns>A waitable object allowing waiting for the job completion.</returns>
        public IWaitable QueueWaitableWorker(Action worker) {
            return QueueWaitableWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job and provide a waitable object to wait for
        /// completion.
        /// </summary>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        /// <returns>A waitable object allowing waiting for the job completion.</returns>
        public IWaitable QueueWaitableWorker(int tag, Action worker) {
            return QueueWorker(tag, () => { worker(); return true; });
        }

        /// <summary>
        /// Enqueue a job returning a value and provide a Future&lt;&gt; to
        /// retrieve the result. The job is associated to queue tag 0.
        /// </summary>
        /// <typeparam name="TData">The type of the result.</typeparam>
        /// <param name="worker">Job to run.</param>
        /// <returns>A Future&lt;&gt; that will hold the result.</returns>
        public Future<TData> QueueWorker<TData>(Func<TData> worker) {
            return QueueWorker(0, worker);
        }

        /// <summary>
        /// Enqueue a job returning a value and provide a Future&lt;&gt; to
        /// retrieve the result.
        /// </summary>
        /// <typeparam name="TData">The type of the result.</typeparam>
        /// <param name="tag">Queue tag associated to the job.</param>
        /// <param name="worker">Job to run.</param>
        /// <returns>A Future&lt;&gt; that will hold the result.</returns>
        public Future<TData> QueueWorker<TData>(int tag, Func<TData> worker) {
            var future = new Future<TData>();
            QueueWorker(() => {
                try {
                    future.Value = worker();
                }
                catch (Exception ex) {
                    future.Throw(ex);
                }
            });
            return future;
        }

        /// <summary>
        /// Required nupmber of threads in the pool.
        /// </summary>
        public int NThreads {
            get {
                return _wanted_n_of_threads;
            }

            set {
                if (value <= 0) {
                    throw new ArgumentOutOfRangeException(_name + ": maximum number of threads cannot be zero or less.");
                }
                lock (_condition) {
                    _wanted_n_of_threads = value;
                }
            }
        }

        /// <summary>
        /// Number of pending jobs in the thread pool.
        /// </summary>
        public int Pending {
            get {
                lock (_condition) {
                    return _actions.Count;
                }
            }
        }

        /// <summary>
        /// Number of running jobs in the pool.
        /// </summary>
        public int Running {
            get {
                return _running_workers;
            }
        }
        /// <summary>
        ///     Waits for the queue to empty.
        /// </summary>
        public void WaitForEveryWorkerIdle() {
            // A spinWait ensures a yield from time to time, forcing the CPU to do a context switch, thus allowing other processes to finish.
            var spinWait = new SpinWait();
            while (_running_workers > 0) {
                Thread.MemoryBarrier();
                spinWait.SpinOnce();
            }
        }
        /// <summary>
        /// Shutdown the thread pool. It cannot be restarted afterward.
        /// Current running jobs end normaly, pending jobs are not processed.
        /// </summary>
        public void Dispose() {
            HashSet<Thread> t;
            lock (_condition) {
                _disposing = true;
                t = new HashSet<Thread>(_threads);
                Monitor.PulseAll(_condition);
            }
            foreach (var thread in t)
                thread.Join(50);
        }

        /// <summary>
        /// The loop performed by threads in the pool. Pick elements
        /// in the FairQueue and run them until the pool is disposed.
        /// </summary>
        void RunWorkers() {
            try {
                while (!_disposing) {
                    Action running = null;
                    lock (_condition) {
                        if (!_disposing && _actions.Empty) {
                            Monitor.Wait(_condition);
                        }
                        if (!_disposing && !_actions.Empty) {
                            running = _actions.Dequeue();
                        }
                    }
                    if (running != null) {
                        try {
                            Interlocked.Increment(ref _running_workers);
                            running();
                            Interlocked.Decrement(ref _running_workers);
                        }
                        finally {
                        }
                    }

                    // Check new thread start / thread stop
                    if (!CheckThreads()) break;
                }
            }
            finally {
                lock (_condition) {
                    //--_current_n_of_threads;
                    _current_n_of_threads=Interlocked.Decrement(ref _current_n_of_threads);
                    _threads.Remove(Thread.CurrentThread);
                }
            }
        }

        /// <summary>
        /// Starts threads if we're below the maximum number of threads, returns false otherwise.
        /// </summary>
        /// <returns>False if we must stop some threads, true otherwise.</returns>
        bool CheckThreads() {
            if (!_disposing && _current_n_of_threads != _wanted_n_of_threads) {
                lock (_condition) {
                    if (!_disposing && _current_n_of_threads != _wanted_n_of_threads) {
                        if (_current_n_of_threads > _wanted_n_of_threads) return false;
                        else StartThreads();
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Start threads up to the maximum number for this pool.
        /// </summary>
        void StartThreads() {
            lock (_condition) {
                int nToLaunch = _wanted_n_of_threads - _current_n_of_threads;
                for (int i = 0; i < nToLaunch; ++i) {
                    var thread = new Thread(RunWorkers);
                    thread.IsBackground = true;
                    _id = Interlocked.Increment(ref _id);
                    thread.Name = _name + "-FairTd#" + _id;
                    _threads.Add(thread);
                    ++_current_n_of_threads;
                    thread.Start();
                }
            }
        }

        static long _id;

        readonly string _name;
        readonly object _condition = new object();
        readonly HashSet<Thread> _threads = new HashSet<Thread>();
        readonly FairQueue<Action> _actions = new FairQueue<Action>();
        int _running_workers;
        volatile int _current_n_of_threads;
        volatile int _wanted_n_of_threads;
        volatile bool _disposing;
    }


    /// <summary>
    /// A "fair" queue.
    /// Data are enqueued associated to a "tag" and each tag gets its own queue. A tag is essentially a queue id
    /// provided by the client.
    /// Data is dequeued by round robin cycling between all the tagged queues each time an element is dequeued.
    /// That means no tag can "block" the dequeueing of elements bound to another tag. Data in the same tagged
    /// queue is dequeued in fifo order. 
    /// Enqueuing and Dequeuing are both O(1).
    /// This class is not thread safe, if needed synchronization burden belongs to the caller.
    /// </summary>
    public class FairQueue<TData>
    {
        /// <summary>
        /// Enqueue some data. O(1) operation.
        /// </summary>
        /// <param name="tag">Tag associated to the data.</param>
        /// <param name="data">Data to enqueue.</param>
        public void Enqueue(int tag, TData data) {
            LinkedQueue tagged;
            if (!_queues.TryGetValue(tag, out tagged)) {
                tagged = new LinkedQueue(tag, new Queue<TData>());
                _queues[tag] = tagged;
            }
            if (tagged.Content.Count == 0) {
                if (_tail != null) {
                    _tail.Next = tagged;
                    _tail = tagged;
                }
                else {
                    _head = _tail = tagged;
                }
            }
            tagged.Content.Enqueue(data);
            ++_count;
        }

        /// <summary>
        /// Enqueue some data associated to tag 0. O(1) operation.
        /// </summary>
        /// <param name="data">Data to enqueue.</param>
        public void Enqueue(TData data) {
            Enqueue(0, data);
        }

        /// <summary>
        /// Dequeue some data. You cannot predict the tag the next dequeued data belongs to, but data belonging
        /// to the same tag is guaranteed to come out in fifo order.
        /// O(1) operation.
        /// </summary>
        /// <returns>Dequeued data.</returns>
        public TData Dequeue() {
            int tag;
            return Dequeue(out tag);
        }

        /// <summary>
        /// Dequeue some data. You cannot predict the tag the next dequeued data belongs to, but data belonging
        /// to the same tag is guaranteed to come out in fifo order.
        /// O(1) operation.
        /// </summary>
        /// <param name="tag">The tag to which the data belonged to.</param>
        /// <returns>Dequeued data.</returns>
        public TData Dequeue(out int tag) {
            if (_head == null) throw new InvalidOperationException("Trying to dequeue from an empty FairQueue.");

            var data = _head.Content.Dequeue();
            tag = _head.Tag;

            var oldHead = _head;
            if (oldHead != _tail) {
                _head = oldHead.Next;
                oldHead.Next = null;
                if (oldHead.Content.Count != 0) {
                    _tail.Next = oldHead;
                    _tail = oldHead;
                }
            }
            else {
                if (oldHead.Content.Count == 0) {
                    _head = _tail = null;
                }
            }

            --_count;
            return data;
        }

        /// <summary>
        /// Number of elements in the FairQueue.
        /// O(1) operation.
        /// </summary>
        public int Count {
            get {
                return _count;
            }
        }

        /// <summary>
        /// Number of elements associated to a given tag in the FairQueue.
        /// O(1) operation.
        /// </summary>
        /// <param name="tag">The tag to count elements from.</param>
        /// <returns>Number of elements associated to the given tag.</returns>
        public int CountTagged(int tag) {
            return _queues[tag].Content.Count;
        }

        /// <summary>
        /// Returns true if the FairQueue is empty, false otherwise.
        /// O(1) operation.
        /// </summary>
        public bool Empty {
            get {
                return Count == 0;
            }
        }

        /// <summary>
        /// Basic linked list of queues to round robin order the dequeing operations.
        /// </summary>
        private class LinkedQueue
        {
            public LinkedQueue(int tag, Queue<TData> self) {
                Tag = tag;
                Content = self;
            }

            public int Tag;
            public Queue<TData> Content;
            public LinkedQueue Next;
        }

        Dictionary<int, LinkedQueue> _queues = new Dictionary<int, LinkedQueue>();
        LinkedQueue _head;
        LinkedQueue _tail;
        int _count;
    }

    /// <summary>
    /// A holder for "future" values.
    /// </summary>
    /// <typeparam name="TData">The type of the underlying value.</typeparam>
    public sealed class Future<TData> : IWaitable
    {
        /// <summary>
        /// Access to the undelrying value.
        /// Read access will block until a value is set or an exception is transmitted.
        /// Write access will set the underlying value and signal all waiting threads.
        /// Once set the value cannot be changed and trying to do so will throw.
        /// </summary>
        public TData Value {
            get {
                lock (this) {
                    if (!_set) {
                        Monitor.Wait(this);
                    }
                }

                if (_exception != null) throw new FutureValueException(_exception);
                return _data;
            }

            set {
                lock (this) {
                    if (_set) throw new FutureAlreadySetException();
                    _data = value;
                    _set = true;
                    Monitor.PulseAll(this);
                }
            }
        }

        /// <summary>
        /// Pass an exception to the Future and signal all waiting
        /// threads. The passed exception will be rethrown in all waiting threads.
        /// </summary>
        /// <param name="exception">The exception to signal to waiting threads.</param>
        public void Throw(Exception exception) {
            lock (this) {
                if (_set) throw new FutureAlreadySetException();
                _exception = exception;
                _set = true;
                Monitor.PulseAll(this);
            }
        }

        /// <summary>
        /// True if the Future is ready for access, false otherwise.
        /// Ready means either a value has been set or an exception transmitted.
        /// </summary>
        public bool IsSet {
            get {
                return _set;
            }
        }

        /// <summary>
        /// Wait for the Furture to be ready.
        /// </summary>
        public void Wait() {
            lock (this) {
                if (!_set) {
                    Monitor.Wait(this);
                }
            }
        }

        /// <summary>
        /// Wait for the Future to be ready, up to to a given delay.
        /// </summary>
        /// <param name="msec">Waiting delay in milliseconds.</param>
        /// <returns>True if the Future was ready before the delay, false otherwise.</returns>
        public bool Wait(int msec) {
            lock (this) {
                if (!_set) {
                    return Monitor.Wait(this, msec);
                }
            }
            return true;
        }

        /// <summary>
        /// Wait for the Future to be ready, up to to a given delay.
        /// </summary>
        /// <param name="msec">Waiting delay expressed as as TimeSpan.</param>
        /// <returns>True if the Future was ready before the delay, false otherwise.</returns>
        public bool Wait(TimeSpan span) {
            return Wait(span.Milliseconds);
        }

        TData _data;
        volatile Exception _exception;
        volatile bool _set;
    }

    /// <summary>
    /// An exception to signal exception occuring while setting a Future value.
    /// </summary>
    public class FutureValueException : Exception
    {
        public FutureValueException(Exception ex)
            : base("Exception while setting the value of a Future. Check inner exception to get the original exception.", ex) {
        }
    }

    /// <summary>
    /// An exception to signal multiple value set.
    /// </summary>
    public class FutureAlreadySetException : Exception
    {
        public FutureAlreadySetException()
            : base("Trying to set a value to an already set Future.") {
        }
    }

    /// <summary>
    /// A simple interface "waitable" objects.
    /// </summary>
    public interface IWaitable
    {
        /// <summary>
        /// Wait until signaled.
        /// </summary>
        void Wait();

        /// <summary>
        /// Wait until signaled or the specified delay is spent.
        /// </summary>
        /// <param name="msec">A maximum delay in milliseconds to wait for the signal.</param>
        /// <returns>true if return occurs before the delay, false otherwise</returns>
        bool Wait(int msec);

        /// <summary>
        /// Wait until signaled or the specified delay is spent.
        /// </summary>
        /// <param name="span">A maximum delay to wait for the signal.</param>
        /// <returns>true if return occurs before the delay, false otherwise</returns>
        bool Wait(TimeSpan span);
    }

    class Test_FairThreadPool
    {
        static Random rng = new Random();
        public void TestQueueWorker() {
            using (var ftp = new FairThreadPool("Glube", 8)) {

                using (var finished = new ManualResetEvent(false)) {
                    int countdown = 42;
                    for (int i = 0; i < 42; ++i) {
                        ftp.QueueWorker(
                            rng.Next(12),
                            () => {
                                //Assert.Greater(countdown, 0, "Incorrect countdown value, some jobs may have run more than once");
                                if (Interlocked.Decrement(ref countdown) == 0) finished.Set();
                            });
                    }
                    finished.WaitOne();
                    //Assert.AreEqual(0, countdown, "Incorrect countdown value, some jobs may not have run or run more than once");
                    //Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
                }
                
                
            }
        }

        public void TestQueueWorkerWaitable() {
            using (var ftp = new FairThreadPool("Glube", 8)) {
                int countdown = 42;
                var toWait = new List<IWaitable>();
                for (int i = 0; i < 42; ++i) {
                    toWait.Add(ftp.QueueWaitableWorker(
                        rng.Next(7),
                        () => {
                            //Assert.Greater(countdown, 0, "Incorrect countdown value, some jobs may have run more than once");
                            Interlocked.Decrement(ref countdown);
                        }));
                }
                foreach (var w in toWait) {
                    w.Wait();
                }
                //Assert.AreEqual(0, countdown, "Incorrect countdown value, some jobs may not have run or run more than once");
                //Assert.AreEqual(0, ftp.Pending, "There sould not be any pending job");
            }
        }

    }
}

