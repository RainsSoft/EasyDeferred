using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;
using EasyDeferred.RSG;
using EasyDeferred.Threading;

namespace EasyDeferred.Synchronize
{

    /// <summary>
    /// only support Invoke,console need before set SynchronizationContext:
    ///      ConsoleSynchronizationContext sc = new ConsoleSynchronizationContext();
    ///        SynchronizationContext.SetSynchronizationContext(sc);
    /// 不支持BeginInvoke(...)/EndInvoke(...)
    /// </summary>
    public class SyncEasyInvoke : ISynchronizeInvoke, IDisposable
    {
        // For more details about this solution see:
        // http://stackoverflow.com/questions/6708765/how-to-invoke-when-i-have-no-control-available

        private readonly SynchronizationContext context;
        private readonly Thread thread;
        private readonly int threadId;
        private readonly Object locker;        
        private readonly bool needDoActionsMethodInThisThread;

        public bool NeedDoActionLoop {
            get {
                return needDoActionsMethodInThisThread;
            }
        }
        public SyncEasyInvoke()
            : base() {
            this.context = SynchronizationContext.Current;
            System.Diagnostics.Debug.Assert(this.context != null);
            needDoActionsMethodInThisThread = (this.context == null);
            if (this.context == null) {
                this.context = new ConsoleSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(this.context);
            }
            this.thread = Thread.CurrentThread;
            this.threadId = Thread.CurrentThread.ManagedThreadId;
            this.locker = new Object();
            //
            this.initAsynStaThread();

        }
        /// <summary>
        /// need update this method in created instance thread
        /// 在创建线程中执行消息循环
        /// </summary>
        public void DoActions() {
            if (needDoActionsMethodInThisThread) {
                if (Thread.CurrentThread != this.thread) {
                    throw new TargetException(
                        this.GetType() + "." + MethodBase.GetCurrentMethod().Name + "() " +
                        "must be called from the same thread it was created on " +
                        "(created on thread id: " + this.thread.ManagedThreadId + ", called from thread id: " + Thread.CurrentThread.ManagedThreadId
                    );
                }
                (this.context as ConsoleSynchronizationContext).DoActions();
            }
        }
        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        //static void Install() {
        //    UnitySynchronizationContext = SynchronizationContext.Current;
        //    UnityThreadId = Thread.CurrentThread.ManagedThreadId;
        //}

        //public static int UnityThreadId {
        //    get; private set;
        //}

        //public static SynchronizationContext UnitySynchronizationContext {
        //    get; private set;
        //}
        #region ISynchronizeInvoke member implementation section.

        public bool InvokeRequired {
            get {
                return Thread.CurrentThread.ManagedThreadId != this.threadId;
            }
        }



        public object Invoke(Delegate method, Object[] args) {
            if (method == null) {
                throw new ArgumentNullException("method");
            }

            lock (this.locker) {
                //var old = SynchronizationContext.Current;
                //SynchronizationContext.SetSynchronizationContext(this.context);
                Object result = null;

                SendOrPostCallback invoker = new SendOrPostCallback(
                    delegate (Object data) {
                        result = method.DynamicInvoke(args);
                    });

                this.context.Send(new SendOrPostCallback(invoker), method.Target);
                //
                //SynchronizationContext.SetSynchronizationContext(old);
                return result;
            }
        }

        public object Invoke(Delegate method) {
            return this.Invoke(method, null);
        }


        [Obsolete("This method is not supported!", false)]
        public IAsyncResult BeginInvoke(Delegate method, Object[] args) {
            //throw new NotSupportedException(); 
            if (method == null) {
                throw new ArgumentNullException("method");
            }
            var asyncResult = new AsyncResult() {
                method = method,
                args = args,
                IsCompleted = false,
                AsyncWaitHandle = new ManualResetEvent(false),
            };
            //lock (queueToExecute) {
            queueToExecute.Enqueue(asyncResult);
            //}
            while (queueToExecute.Count > 0) {
                //等待线程执行
                System.Threading.Thread.Sleep(1);
            }
            return asyncResult;

            //
            //lock (this.locker) {   
            //        SendOrPostCallback invoker = new SendOrPostCallback(
            //            delegate (Object data) {
            //                method.DynamicInvoke(args);
            //                asyncResult.IsCompleted = true;
            //                (asyncResult.AsyncWaitHandle as ManualResetEvent).Set();
            //            });
            //    this.context.Post(new SendOrPostCallback(invoker), method.Target);		
            //}

            //            {
            //                SendOrPostCallback invoker = new SendOrPostCallback(
            //                     delegate (Object data) {
            //                         asyncResult.IsCompleted = true;
            //                         (asyncResult.AsyncWaitHandle as ManualResetEvent).Set();
            //                     });
            //                Action<object> pool = (Z) => {
            //                    method.DynamicInvoke(args);
            //                    this.context.Post(new SendOrPostCallback(invoker), method.Target);
            //                };
            //                bool ok = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(pool), null);
            //#if DEBUG
            //                Console.WriteLine("BeginInvoke:" + ok);
            //#endif
            //                return asyncResult;
            //            }


            //            //
            //            return asyncResult;
            //var operation = AsyncOperationManager.CreateOperation(null);
            // operation.Post(new SendOrPostCallback(delegate (object state) {
            //     EventHandler handler = SomethingHappened;
            //     if (handler != null) {
            //         handler(this, EventArgs.Empty);
            //     }
            // }), null);
            // operation.OperationCompleted();

            //MyTaskWorkerDelegate worker = new MyTaskWorkerDelegate(MyTaskWorker);
            //AsyncCallback completedCallback = new AsyncCallback(MyTaskCompletedCallback);

            //lock (_sync) {
            //    if (_myTaskIsRunning)
            //        throw new InvalidOperationException("The control is currently busy.");

            //    AsyncOperation async = AsyncOperationManager.CreateOperation(null);
            //    worker.BeginInvoke(files, completedCallback, async);
            //    _myTaskIsRunning = true;
            //}


        }

        [Obsolete("This method is not supported!", false)]
        public object EndInvoke(IAsyncResult result) {
            //throw new NotSupportedException();
            if (!result.IsCompleted) {
                if (this.InvokeRequired) {
                    result.AsyncWaitHandle.WaitOne();
                }
                else {
                    //防止死锁
                    if (needDoActionsMethodInThisThread) {
                        (this.context as ConsoleSynchronizationContext).DoActions();
                    }

                }
            }
            return result.AsyncState;
        }
        #endregion // ISynchronizeInvoke member implementation section.
        ConcurrentQueue<AsyncResult> queueToExecute = new ConcurrentQueue<AsyncResult>();
        private Thread mStaThread;
        private void initAsynStaThread() {
            //return;
            //todo:
            mStaThread = new Thread(ProcessQueue);
            mStaThread.Name = "STA_Thread_"+this.GetType().Name;
            mStaThread.SetApartmentState(ApartmentState.STA);
            mStaThread.Start();
        }
        void ProcessQueue() {
            AsyncResult data = null;
            while (true) {
                //bool loop = queueToExecute.Count > 0;// true;
                while (queueToExecute.Count > 0) {
                    if (queueToExecute.TryDequeue(out data)) {
                        //lock (queueToExecute) {
                        //	loop = queueToExecute.Count > 0;
                        //	if (!loop) break;
                        //	data = queueToExecute.Dequeue();
                        //}

                        data.AsyncState = Invoke(data.method, data.args);
                        data.IsCompleted = true;
                        (data.AsyncWaitHandle as ManualResetEvent).Set();
                    }
                    //loop = queueToExecute.Count > 0;
                }
                System.Threading.Thread.Sleep(1);
            }
        }

        public void Dispose() {            
            if (this.mStaThread != null) {                
                this.mStaThread.Abort();
            }
            this.mStaThread = null;
            queueToExecute.Clear();
        }

        class AsyncResult : IAsyncResult
        {
            public Delegate method;
            public object[] args;
            public bool IsCompleted { get; set; }
            public WaitHandle AsyncWaitHandle { get; internal set; }
            public object AsyncState { get; set; }
            public bool CompletedSynchronously { get { return IsCompleted; } }
        }
    }


    public class DeferredSynchronizeInvoke : ISynchronizeInvoke
    {
        public class Owner
        {
            public Thread MainThread { get; set; }
            List<DeferredSynchronizeInvoke> ownedDeferredSynchronizeInvoke = new List<DeferredSynchronizeInvoke>();

            public Owner() {
                MainThread = Thread.CurrentThread;
            }

            public void ProcessQueue() {
                if (Thread.CurrentThread != MainThread) {
                    throw new TargetException(
                        this.GetType() + "." + MethodBase.GetCurrentMethod().Name + "() " +
                        "must be called from the same thread it was created on " +
                        "(created on thread id: " + MainThread.ManagedThreadId + ", called from thread id: " + Thread.CurrentThread.ManagedThreadId
                    );
                }
                foreach (var d in ownedDeferredSynchronizeInvoke) {
                    d.ProcessQueue();
                }
            }
        }

        Queue<AsyncResult> queueToExecute = new Queue<AsyncResult>();
        Owner owner;
        public bool InvokeRequired { get { return owner.MainThread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId; } }

        public DeferredSynchronizeInvoke(Owner owner) {
            this.owner = owner;
        }
        public DeferredSynchronizeInvoke() : this(new Owner()) {

        }

        public IAsyncResult BeginInvoke(Delegate method, object[] args) {
            var asyncResult = new AsyncResult() {
                method = method,
                args = args,
                IsCompleted = false,
                AsyncWaitHandle = new ManualResetEvent(false),
            };
            lock (queueToExecute) {
                queueToExecute.Enqueue(asyncResult);
            }
            return asyncResult;
        }

        public object EndInvoke(IAsyncResult result) {
            if (!result.IsCompleted) {
                result.AsyncWaitHandle.WaitOne();
            }
            return result.AsyncState;
        }

        public object Invoke(Delegate method, object[] args) {
            if (InvokeRequired) {
                var asyncResult = BeginInvoke(method, args);
                return EndInvoke(asyncResult);
            }
            else {
                return method.DynamicInvoke(args);
            }
        }

        void ProcessQueue() {
            bool loop = true;
            AsyncResult data = null;
            while (loop) {
                lock (queueToExecute) {
                    loop = queueToExecute.Count > 0;
                    if (!loop) break;
                    data = queueToExecute.Dequeue();
                }

                data.AsyncState = Invoke(data.method, data.args);
                data.IsCompleted = true;
                (data.AsyncWaitHandle as ManualResetEvent).Set();
            }
        }

        class AsyncResult : IAsyncResult
        {
            public Delegate method;
            public object[] args;
            public bool IsCompleted { get; set; }
            public WaitHandle AsyncWaitHandle { get; internal set; }
            public object AsyncState { get; set; }
            public bool CompletedSynchronously { get { return IsCompleted; } }
        }
    }
}
