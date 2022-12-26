using EasyDeferred.Threading;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace EasyDeferred.Synchronize
{

    /// <summary>
    /// 外部执行线程同步回调队列
    /// </summary>
    public class SyncEasyContextInvoke : EasyDeferredSyncContextInvoke
    {

        internal SyncEasyContextInvoke(/*Owner owner*/) {
            //this.owner = owner;
            threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            needDoActionsMethodInThisThread = (this.context == null);
            var oldSync = SynchronizationContext.Current;
            if (oldSync == null) {
                m_context = this;//new SynchronizationContext();
                //SynchronizationContext.SetSynchronizationContext(this);
            }
            else {
                m_context = oldSync;
            }
        }

        public override SynchronizationContext CreateCopy() {
            return this;
        }
        public override bool InvokeRequired { get { return Thread.CurrentThread.ManagedThreadId != this.threadId; } }
        private readonly int threadId;
        protected override SynchronizationContext context { get { return m_context; } }
        private SynchronizationContext m_context;
        public override bool NeedActionLoopInCreatedThread {
            get { return needDoActionsMethodInThisThread; }
        }
        private readonly bool needDoActionsMethodInThisThread;
        /// <summary>
        /// 处理当前创建线程上下文同步队列中的所有回调
        /// 比如需要在console主线程中同步上下文执行
        /// </summary>
        protected override void ProcessActionsQueue() {
            if (needDoActionsMethodInThisThread == false) {
                return;
            }
            if (this.InvokeRequired) { 
                //Todo:
            }
            imtlProcessQueue();
        }

        public override void Post(SendOrPostCallback d, object state) {
            //Action action = () => {
            //    SynchronizationContext.SetSynchronizationContext(this);

            //    d?.Invoke(state);
            //};

            //_queue.Enqueue(action);
            this.BeginInvoke(d, state);
        }
        public override void Send(SendOrPostCallback d, object state) {
            this.Invoke(d, state);
            //Exception ex = null;
            //using (var waiter = new ManualResetEventSlim(false)) {
            //    Post(wrapper => {
            //        try {
            //            d.Invoke(state);
            //        }
            //        catch (Exception e) {
            //            ex = e;
            //        }
            //        finally {
            //            waiter.Set();
            //        }
            //    }, null);
            //    waiter.Wait();
            //}

            //if (ex != null)
            //    throw ex;
        }


        #region invoke
        Queue<AsyncResult> queueToExecute = new Queue<AsyncResult>();
        //Owner owner;
        //public override bool InvokeRequired { get { return owner.MainThread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId; } }


        public override IAsyncResult BeginInvoke(Delegate method,params object[] args) {
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

        public override object EndInvoke(IAsyncResult result) {
            if (this.InvokeRequired == false) {
                //防止死锁
                //if (needDoActionsMethodInThisThread) {
                //(this.context as SyncEasyContextInvoke).ProcessActionsQueue();
                this.ProcessActionsQueue();
                //}
            }
            else {
                //while (result.IsCompleted == false) {
                //    threadSpinOnce();
                //}
            }
            if (!result.IsCompleted) {
                result.AsyncWaitHandle.WaitOne();
            }

            return result.AsyncState;
        }

        public override object Invoke(Delegate method,params object[] args) {
            if (this.InvokeRequired) {
                var asyncResult = BeginInvoke(method, args);
                return EndInvoke(asyncResult);
            }
            else {
                return method.DynamicInvoke(args);
            }
        }

        void imtlProcessQueue() {
            if (Thread.CurrentThread.ManagedThreadId != this.threadId) {
                //创建线程更新？
                //return;
            }
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

        #endregion
        /*
        public class Owner
        {
            public Thread MainThread { get; set; }
            List<SyncEasyContextInvoke> ownedDeferredSynchronizeInvoke = new List<SyncEasyContextInvoke>();

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
        */
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
