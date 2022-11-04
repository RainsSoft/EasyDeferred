using System;
using System.Collections.Generic;
using System.Threading;
using EasyDeferred.Threading;
using EasyDeferred.RSG;

namespace EasyDeferred.Synchronize
{
    /// <summary>
    /// 当前线程（包括主线程）同步上下文,如果当前线程使用该同步上下文，必须在该线程的循环中执行DoActions();
    /// </summary>
    public class ConsoleSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _queue
           = new ConcurrentQueue<Action>();
        public override void Post(SendOrPostCallback d, object state) {
            Action action = () => {
                SynchronizationContext.SetSynchronizationContext(this);

                d?.Invoke(state);
            };

            _queue.Enqueue(action);
        }
        public override void Send(SendOrPostCallback d, object state) {
            Exception ex = null;
            using (var waiter = new ManualResetEventSlim(false)) {
                Post(wrapper => {
                    try {
                        d.Invoke(state);
                    }
                    catch (Exception e) {
                        ex = e;
                    }
                    finally {
                        waiter.Set();
                    }
                }, null);
                waiter.Wait();
            }

            if (ex != null)
                throw ex;
        }
        /// <summary>
        /// 外部调用,在当前线程执行
        /// </summary>
        public void DoActions() {
            //while (true) {
            Action action = null;
            while (_queue.Count > 0) {
                if (_queue.TryDequeue(out action)) {
                    action();
                }
            }
            //Thread.Sleep(10);
            //}
        }
    }

    //public class ConsoleSyncContext : SynchronizationContext
    //{
    //    BlockingCollection<Action> queue = new BlockingCollection<Action>();

    //    public override void Post(SendOrPostCallback d, object state) {
    //        queue.Add(() => d(state));
    //    }

    //    public override void Send(SendOrPostCallback d, object state) {
    //        Exception ex = null;
    //        using (var waiter = new ManualResetEventSlim(false)) {
    //            Post(wrapper => {
    //                try {
    //                    d.Invoke(state);
    //                }
    //                catch (Exception e) {
    //                    ex = e;
    //                }
    //                finally {
    //                    waiter.Set();
    //                }
    //            }, null);
    //            waiter.Wait();
    //        }

    //        if (ex != null)
    //            throw ex;
    //    }

    //    public void Run() {
    //        SetSynchronizationContext(this);

    //        Action item;
    //        while (true) {
    //            try {
    //                item = queue.Take();
    //            }
    //            catch (InvalidOperationException) {
    //                // The collection has been closed, so let's exit!
    //                return;
    //            }

    //            item();
    //        }
    //    }

    //    public void Exit() {
    //        queue.CompleteAdding();
    //    }
    //}

    ///// <summary>
    ///// 控制台 同步上下文
    ///// </summary>
    //public class MySynchronizationContext : SynchronizationContext
    //{
    //    private readonly MyControl ctrl;

    //    public MySynchronizationContext(MyControl ctrl) {
    //        this.ctrl = ctrl;
    //    }
    //    public override void Send(SendOrPostCallback d, object state) {
    //        ctrl.Invoke((Z) => d(Z), state);
    //    }

    //    public override void Post(SendOrPostCallback d, object state) {
    //        ctrl.BeginInvoke((Z) => d(Z), state);
    //    }


    //    /// <summary>
    //    /// 用于控制台线程同步上下文，类似winfor UI的WindowsFormsSynchronizationContext以及wpf的DispatcherSynchronizationContext
    //    /// </summary>
    //    public class ExeArg
    //    {
    //        //要放到主线程(UI线程)中执行的方法
    //        public Action<object> Action { get; set; }
    //        //方法执行时额外的参数
    //        public object State { get; set; }
    //        //是否同步执行
    //        public bool Sync { get; set; }
    //        //静态字典，key: 线程Id，value: 队列
    //        public static ConcurrentDictionary<int, BlockingCollection<ExeArg>> QueueDics = new ConcurrentDictionary<int, BlockingCollection<ExeArg>>();
    //        //当前线程对应的字典
    //        public static BlockingCollection<ExeArg> CurrentQueue => QueueDics.ContainsKey(Thread.CurrentThread.ManagedThreadId) ? QueueDics[Thread.CurrentThread.ManagedThreadId] : null;
    //    }
    //    public class MyControl
    //    {
    //        //记录创建这个控件的线程(UI线程)
    //        private Thread _createThread = null;
    //        public MyControl() {
    //            //第一个控件创建时初始化一个SynchronizationContext实例，并将它和当前线程绑定一起
    //            if (SynchronizationContext.Current == null) SynchronizationContext.SetSynchronizationContext(new MySynchronizationContext(this));
    //            _createThread = Thread.CurrentThread;
    //            //初始化一个字典队列，key: 线程Id，value：参数队列
    //            ExeArg.QueueDics.TryAdd(Thread.CurrentThread.ManagedThreadId, new BlockingCollection<ExeArg>());
    //        }
    //        //同步调用
    //        public void Invoke(Action<object> action, object state) {
    //            var queues = ExeArg.QueueDics[_createThread.ManagedThreadId];
    //            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
    //            queues.Add(new ExeArg() {
    //                Action = obj => {
    //                    action(state);
    //                    manualResetEvent.Set();
    //                },
    //                State = state,
    //                Sync = true
    //            });
    //            manualResetEvent.WaitOne();
    //            (manualResetEvent as IDisposable).Dispose();
    //        }
    //        //异步调用
    //        public void BeginInvoke(Action<object> action, object state) {
    //            var queues = ExeArg.QueueDics[_createThread.ManagedThreadId];
    //            queues.Add(new ExeArg() {
    //                Action = action,
    //                State = state
    //            });
    //        }
    //    }
    //    class MySynchronizationContextTest
    //    {
    //        public static void Main(string[] arg) {

    //            Console.WriteLine($"主线程: {Thread.CurrentThread.ManagedThreadId}");
    //            //主线程创建控件
    //            var ctrl = new MyControl();
    //            var syncContext = SynchronizationContext.Current;
    //            //模拟一个用户操作
    //            Action ac =()=> {
    //                Thread.Sleep(2000);
    //                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Post前");
    //                syncContext.Post((state) => {
    //                    Console.WriteLine($"Post内的方法执行线程: {Thread.CurrentThread.ManagedThreadId},参数:{state}");
    //                }, new { name = "小明" });
    //                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Post后,Send前");
    //                syncContext.Send((state) => {
    //                    Thread.Sleep(3000);
    //                    Console.WriteLine($"Send内的方法执行线程: {Thread.CurrentThread.ManagedThreadId},参数:{state}");
    //                }, new { name = "小红" });
    //                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Send后");
    //            };
    //            Thread mStaThread = new Thread(new ThreadStart( ac));
    //            mStaThread.Name = "STA Worker Thread";
    //            mStaThread.SetApartmentState(ApartmentState.STA);
    //            mStaThread.Start();
    //            //主线程开启消息垒
    //            while (true) {
    //                var exeArg = ExeArg.CurrentQueue.Take();
    //                exeArg.Action?.Invoke(exeArg.State);
    //            }
    //        }
    //    }
    //}






    //public class STASynchronizationContext : SynchronizationContext, IDisposable
    //{
    //    private readonly Dispatcher dispatcher;
    //    private object dispObj;
    //    private readonly Thread mainThread;

    //    public STASynchronizationContext() {
    //        mainThread = new Thread(MainThread) { Name = "STASynchronizationContextMainThread", IsBackground = false };
    //        mainThread.SetApartmentState(ApartmentState.STA);
    //        mainThread.Start();

    //        //wait to get the main thread's dispatcher
    //        while (Thread.VolatileRead(ref dispObj) == null)
    //            Thread.Yield();

    //        dispatcher = dispObj as Dispatcher;
    //    }

    //    public override void Post(SendOrPostCallback d, object state) {
    //        dispatcher.BeginInvoke(d, new object[] { state });
    //    }

    //    public override void Send(SendOrPostCallback d, object state) {
    //        dispatcher.Invoke(d, new object[] { state });
    //    }

    //    private void MainThread(object param) {
    //        Thread.VolatileWrite(ref dispObj, Dispatcher.CurrentDispatcher);
    //        Console.WriteLine("Main Thread is setup ! Id = {0}", Thread.CurrentThread.ManagedThreadId);
    //        Dispatcher.Run();
    //    }

    //    public void Dispose() {
    //        if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
    //            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

    //        GC.SuppressFinalize(this);
    //    }

    //    ~STASynchronizationContext() {
    //        Dispose();
    //    }
    //}

    public class StaSynchronizationContext : SynchronizationContext, IDisposable
    {
        private BlockingQueue<SendOrPostCallbackItem> mQueue;
        private StaThread mStaThread;
        private SynchronizationContext oldSync;

        public StaSynchronizationContext()
            : base() {
            mQueue = new BlockingQueue<SendOrPostCallbackItem>();
            mStaThread = new StaThread(mQueue, this);
            mStaThread.Start();
            oldSync = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);
        }

        public override void Send(SendOrPostCallback d, object state) {
            // create an item for execution
            SendOrPostCallbackItem item = new SendOrPostCallbackItem(d, state,
                                                                     ExecutionType.Send);
            // queue the item
            mQueue.Enqueue(item);
            // wait for the item execution to end
            item.ExecutionCompleteWaitHandle.WaitOne();

            // if there was an exception, throw it on the caller thread, not the
            // sta thread.
            if (item.ExecutedWithException)
                throw item.Exception;
        }

        public override void Post(SendOrPostCallback d, object state) {
            // queue the item and don't wait for its execution. This is risky because
            // an unhandled exception will terminate the STA thread. Use with caution.
            SendOrPostCallbackItem item = new SendOrPostCallbackItem(d, state,
                                                                     ExecutionType.Post);
            mQueue.Enqueue(item);
        }

        public void Dispose() {
            mStaThread.Stop();
            SynchronizationContext.SetSynchronizationContext(oldSync);
        }

        public override SynchronizationContext CreateCopy() {
            return this;
        }

        internal class StaThread
        {
            private Thread mStaThread;
            private IQueueReader<SendOrPostCallbackItem> mQueueConsumer;
            private readonly SynchronizationContext syncContext;

            private ManualResetEvent mStopEvent = new ManualResetEvent(false);


            internal StaThread(IQueueReader<SendOrPostCallbackItem> reader, SynchronizationContext syncContext) {
                mQueueConsumer = reader;
                this.syncContext = syncContext;
                mStaThread = new Thread(Run);
                mStaThread.Name = "STA Worker Thread";
                mStaThread.SetApartmentState(ApartmentState.STA);
            }

            internal void Start() {
                mStaThread.Start();
            }


            internal void Join() {
                mStaThread.Join();
            }

            private void Run() {
                SynchronizationContext.SetSynchronizationContext(syncContext);
                while (true) {
                    bool stop = mStopEvent.WaitOne(0);
                    if (stop) {
                        mQueueConsumer.Dispose();
                        break;
                    }

                    SendOrPostCallbackItem workItem = mQueueConsumer.Dequeue();
                    if (workItem != null)
                        workItem.Execute();
                }
            }

            internal void Stop() {
                mStopEvent.Set();
                mQueueConsumer.ReleaseReader();
            }
        }
        internal enum ExecutionType
        {
            Post,
            Send
        }

        internal class SendOrPostCallbackItem
        {
            object mState;
            private ExecutionType mExeType;
            SendOrPostCallback mMethod;
            ManualResetEvent mAsyncWaitHandle = new ManualResetEvent(false);
            Exception mException = null;

            internal SendOrPostCallbackItem(SendOrPostCallback callback,
               object state, ExecutionType type) {
                mMethod = callback;
                mState = state;
                mExeType = type;
            }

            internal Exception Exception {
                get { return mException; }
            }

            internal bool ExecutedWithException {
                get { return mException != null; }
            }

            // this code must run ont the STA thread
            internal void Execute() {
                if (mExeType == ExecutionType.Send)
                    Send();
                else
                    Post();
            }

            // calling thread will block until mAsyncWaitHandle is set
            internal void Send() {
                try {
                    // call the thread
                    mMethod(mState);
                }
                catch (Exception e) {
                    mException = e;
                }
                finally {
                    mAsyncWaitHandle.Set();
                }
            }

            /// <summary />
            /// Unhandled exceptions will terminate the STA thread
            /// </summary />
            internal void Post() {
                mMethod(mState);
            }

            internal WaitHandle ExecutionCompleteWaitHandle {
                get { return mAsyncWaitHandle; }
            }
        }
        internal interface IQueueReader<T> : IDisposable
        {
            T Dequeue();
            void ReleaseReader();
        }

        internal interface IQueueWriter<T> : IDisposable
        {
            void Enqueue(T data);
        }


        internal class BlockingQueue<T> : IQueueReader<T>,
                                             IQueueWriter<T>, IDisposable
        {
            // use a .NET queue to store the data
            private Queue<T> mQueue = new Queue<T>();
            // create a semaphore that contains the items in the queue as resources.
            // initialize the semaphore to zero available resources (empty queue).
            private Semaphore mSemaphore = new Semaphore(0, int.MaxValue);
            // a event that gets triggered when the reader thread is exiting
            private ManualResetEvent mKillThread = new ManualResetEvent(false);
            // wait handles that are used to unblock a Dequeue operation.
            // Either when there is an item in the queue
            // or when the reader thread is exiting.
            private WaitHandle[] mWaitHandles;

            public BlockingQueue() {
                mWaitHandles = new WaitHandle[2] { mSemaphore, mKillThread };
            }
            public void Enqueue(T data) {
                lock (mQueue) mQueue.Enqueue(data);
                // add an available resource to the semaphore,
                // because we just put an item
                // into the queue.
                mSemaphore.Release();
            }

            public T Dequeue() {
                // wait until there is an item in the queue
                WaitHandle.WaitAny(mWaitHandles);
                lock (mQueue) {
                    if (mQueue.Count > 0)
                        return mQueue.Dequeue();
                }
                return default(T);
            }

            public void ReleaseReader() {
                mKillThread.Set();
            }


            void IDisposable.Dispose() {
                if (mSemaphore != null) {
                    mSemaphore.Close();
                    mQueue.Clear();
                    mSemaphore = null;
                }
            }
        }
    }
}
