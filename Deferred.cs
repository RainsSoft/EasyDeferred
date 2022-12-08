using RSG;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace EasyPromise {

    /// <summary>
    /// A factory class to get <code>DeferredWhen</code> instances.
    /// 实例DeferredWhen对象构建入口
    /// </summary>
    public sealed class Deferred2
    {
        /// <remarks />
        private Deferred2() {
        }

        /// <summary>
        /// 创建新的DeferredWhen实例
        /// Returns an instance of an object implementing the <code>IDeferredWhen</code> interface.
        /// </summary>
        /// <returns>An object implementing the <code>IDeferredWhen</code> interface</returns>        
        public static IAsyncWhen DeferredWhen() {
            return new AsyncWhen();
        }
        /// <summary>
        /// [ThreadPool] 执行sailor.When(action),然后返回当前线程执行promise.then/finally..等
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IPromise RunAsyn(ISynchronizeInvoke owerControl, Action action) {
            ISailor sailor = A.Sailor();
            sailor.When(owerControl, action);
            return sailor.Promise;
        }
        /// <summary>
        /// 开启一个新线程 执行action
        /// </summary>
        /// <param name="action">线程中执行</param>
        /// <returns></returns>
        public static ISailor RunAsyn<T>(ISynchronizeInvoke owerControl, Action<T> action, T arg, bool waitTreadFinally) {
            ISailor sailor = A.Sailor();
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var sailor2 = z as ISailor;
                try {
                    action(arg);
                    //sailor2.Resolve(null);
                }
                catch(Exception exception) {
                    //sailor2.Reject(exception);
                }
                finally {
                    //sailor2.Finally();
                    mre?.Set();
                }
            });
            Thread newT = new Thread(newPTS);
            newT.Start(sailor);
            //
            mre?.WaitOne();
            return sailor;
        }
        /// <summary>
        /// 开启一个新线程 执行func
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="Result"></typeparam>
        /// <param name="func"></param>
        /// <param name="arg"></param>
        /// <param name="waitTreadFinally"></param>
        /// <returns></returns>
        public static ISailor GetResultAsyn<T, Result>(ISynchronizeInvoke owerControl, Func<T, Result> func, T arg, bool waitTreadFinally) {
            ISailor sailor = A.Sailor();
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var sailor2 = z as ISailor;
                Result ret;
                try {
                    ret = func(arg);
                    //sailor2.Resolve(ret);
                }
                catch(Exception exception) {
                    //sailor2.Reject(exception);
                }
                finally {
                    //sailor2.Finally();
                    mre?.Set();
                }
            });
            Thread newT = new Thread(newPTS);
            newT.Start(sailor);
            //
            mre?.WaitOne();
            return sailor;
        }

    }

    public interface IAsyncWhen {
        object ResolveValue {
            get; set;
        }
        Exception RejectReason {
            get; set;
        }
        void SetCache(string cacheKey, object cacheValue);
        object GetCache(string cacheKey);
        /// <summary>
        /// 获取用于管理异步操作的<code>IPromise</code>对象。
        /// Gets the <code>IPromise</code> object to manage the asynchronous operation.
        /// </summary>
        /// <value>The <code>ISailor</code> promise</value>
        IPromise Promise {
            get;
        }

        ///// <summary>
        ///// 给定解决值后 导致调用Then()执行
        ///// Resolves the given promise causing the Then promise action to be called.
        ///// </summary>
        ///// <param name="value">The result of the deferred operation if any, null otherwise.</param>
        //void Resolve(object value);

        ///// <summary>
        ///// 拒绝give承诺，导致调用OnError()操作。
        ///// Rejects the give promise causing the OnError action to be called.
        ///// </summary>
        ///// <param name="exception">The exception causing the promise to be rejected.</param>
        //void Reject(Exception exception);

        ///// <summary>
        ///// 在解决承诺和拒绝承诺时调用最终承诺操作。
        ///// Calls the Finally promise action both when the promise is resolved and when it is rejected.
        ///// </summary>
        ///// <remarks>It works exactly like the <code>finally</code> C# keyword.</remarks>
        //void Finally();

        /// <summary>
        /// 调用Notify promise操作以更新当前异步操作的状态。
        /// Calls the Notify promise action to update the state of the current asynchronous operation.
        /// </summary>
        /// <param name="value">A value indicating the progress if any, otherwise null.</param>
        void Notify(ISynchronizeInvoke owerControl, float value);

        /// <summary>
        /// [ThreadPool]在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        /// Executes the action asynchronously on another thread and the executes the standard promise pattern (then action if all is good, the OnError action if there are exceptions and so on).
        /// </summary>
        /// <param name="action">The action to be executed asynchronously on another thread.</param>
        /// <returns>The promise to interact with.</returns>
        IPromise When(ISynchronizeInvoke owerControl, Action action);
        /// <summary>
        /// [ThreadPool]在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        /// </summary>
        /// <param name="action"></param>
        /// <param name="waitTreadFinally">指定是否等待线程完成</param>
        /// <returns></returns>
        IPromise When(ISynchronizeInvoke owerControl, Action action, bool waitTreadFinally);
        ///// <summary>
        ///// 在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        ///// Executes the action asynchronously on another thread and the executes the standard promise pattern (then action if all is good, the OnError action if there are exceptions and so on).
        ///// </summary>
        ///// <remarks>A cancellationToken is passed to check if the action should be cancelled</remarks>
        ///// <param name="action">The action to be executed asynchronously on another thread.</param>
        ///// <returns>The promise to interact with.</returns>
        //IAbortablePromise When(Action<CancellationToken> action);
    }

    internal class AsyncWhen : IAsyncWhen
    {
        Promise promise = null;
        //AbortablePromise abortablePromise;
        //CancellationToken cancellationToken = new CancellationToken();
        object resolveValue = null;
        /// <remarks />
        Exception reason;
        public virtual object ResolveValue {
            get {
                //if (this.promise.CurState != PromiseState.Resolved) {
                //    throw new InvalidOperationException("Cannot get Value from a not fulfilled promise");
                //}

                return this.resolveValue;
            }
            set {
                this.resolveValue = value;
            }
        }

        public virtual Exception RejectReason {
            get {
                //if (this.promise.CurState != PromiseState.Rejected) {
                //    throw new InvalidOperationException("Cannot get Reason from a not rejected promise");
                //}

                return this.reason;
            }
            set {
                this.reason = value;
            }
        }
        private RSG.ConcurrentDictionary<string, object> cache = new ConcurrentDictionary<string, object>();
        public void SetCache(string cacheKey, object cacheValue) {
            if(cache.ContainsKey(cacheKey)) {
                cache[cacheKey] = cacheValue;
            }
            else {
                cache.TryAdd(cacheKey, cacheValue);
            }
        }
        public object GetCache(string cacheKey) {
            object obj = null;
            cache.TryGetValue(cacheKey, out obj);
            return obj;
        }
        /// <summary>
        /// 创建Sailor的新实例
        /// Create a new instance of a Sailor
        /// </summary>
        public AsyncWhen() : this(new Promise()) {//, new AbortablePromise()) {
        }

        //internal Sailor(Promise promise, AbortablePromise abortablePromise) {
        //    this.promise = promise;
        //    this.abortablePromise = abortablePromise;
        //    this.abortablePromise.AbortRequested += PromiseRequestedAbort;
        //    SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        //    if (synchronizationContext != null) {
        //        promise.SynchronizationContext = synchronizationContext;
        //        this.abortablePromise.SynchronizationContext = synchronizationContext;
        //    }
        //}
        internal AsyncWhen(Promise promise) {
            this.promise = promise;
            //this.abortablePromise = abortablePromise;
            //this.abortablePromise.AbortRequested += PromiseRequestedAbort;
            //SynchronizationContext synchronizationContext = SynchronizationContext.Current;

            //if (synchronizationContext != null) {
            //    promise.SynchronizationContext = synchronizationContext;
            //    this.abortablePromise.SynchronizationContext = synchronizationContext;
            //}
            this.m_OwnerSyncInvoke = new SimpleGuiInvokeHelper();
        }
        //private void PromiseRequestedAbort(object sender, EventArgs e) {
        //    this.cancellationToken.IsCancellationRequested = true;
        //}

        /// <summary>
        /// 获取用于管理异步操作的<code>IPromise</code>对象。
        /// Gets the <code>IPromise</code> object to manage the asynchronous operation.
        /// </summary>
        public IPromise Promise {
            get {
                return this.promise;
            }
        }

        /// <summary>
        /// 给promise解决值，从而调用then()
        /// Resolves the given promise causing the Then promise action to be called.
        /// </summary>
        /// <param name="value">The result of the deferred operation if any, null otherwise.</param>
        void Resolve(object value) {
            //this.promise.Fulfill(value);
            //this.abortablePromise.Fulfill(value);
            //this.resolveValue = value;
            this.promise.Resolve();
        }
        ///// <summary>
        ///// 不引发 promise.Resolve();
        ///// </summary>
        ///// <param name="value"></param>
        //public void ModifyResolveValue(object value) {
        //    this.resolveValue = value;
        //}
        /// <summary>
        /// 拒绝give承诺，导致调用OnError操作。
        /// Rejects the give promise causing the OnError action to be called.
        /// </summary>
        /// <param name="exception">The exception causing the promise to be rejected.</param>
        void Reject(Exception exception) {
            this.promise.Reject(exception);
            //this.abortablePromise.Reject(exception);

        }

        ///// <summary>
        ///// 在解决承诺和拒绝承诺时调用最终承诺操作。
        ///// Calls the Finally promise action both when the promise is resolved and when it is rejected.
        ///// </summary>
        ///// <remarks>It works exactly like the <code>finally</code> C# keyword.</remarks>
        //public void Finally() {

        //    //this.promise.Finally();
        //    //this.abortablePromise.Finally();
        //}

        /// <summary>
        /// 调用Notify promise操作以更新当前异步操作的状态。
        /// Calls the Notify promise action to update the state of the current asynchronous operation.
        /// </summary>
        /// <param name="value">A value indicating the progress if any, otherwise null.</param>
        public void Notify(ISynchronizeInvoke owerForm, float progress) {
            //this.promise.Notify(value);
            //this.abortablePromise.Notify(value);
            ISynchronizeInvoke owerControl = owerForm == null ? this.m_OwnerSyncInvoke : owerForm;
            InvokeIfRequired(owerControl, () => {
                this.promise.ReportProgress(progress);
            });
        }


        static public Thread GetControlOwnerThread(ISynchronizeInvoke ctrl) {
            if(ctrl.InvokeRequired)
                return (Thread)ctrl.Invoke(new Func<Thread>(() => GetControlOwnerThread(ctrl)), null);
            else
                return System.Threading.Thread.CurrentThread;
        }
        /// <summary>
        /// IfRequired
        /// 使用control.beginInvoke
        /// </summary>
        /// <param name="control"></param>
        /// <param name="code"></param>
        static public void BeginInvokeIfRequired(ISynchronizeInvoke control, Action code) {
            //if (control == null || control.IsDisposed)
            //    return;
            if(control.InvokeRequired) {
                control.BeginInvoke(code, null);
                return;
            }
            code.Invoke();
        }
        public static void BeginInvoke(ISynchronizeInvoke control, Action action) {
            control.BeginInvoke(action, null);
        }

        static public void InvokeIfRequired(ISynchronizeInvoke control, Action code) {
            //if (control == null || control.IsDisposed)
            //    return;
            if(control.InvokeRequired) {
                control.Invoke(code, null);
                return;
            }
            code.Invoke();
        }
        /// <summary>
        /// [ThreadPool]在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        /// Executes the action asynchronously on another thread and the executes the standard promise pattern (then action if all is good, the OnError action if there are exceptions and so on).
        /// </summary>
        /// <param name="action">The action to be executed asynchronously on another thread.</param>
        /// <returns>The promise to interact with.</returns>
        public IPromise When(ISynchronizeInvoke owerControl, Action action) {
            //ThreadPool.QueueUserWorkItem(new WaitCallback(Worker), action);
            //ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(Worker), action);
            //return this.Promise;
            //if (owerControl != null) {
            //不能使用  BeginInvoke,否则会导致不能正常执行zhen
            //    BeginInvokeInUIThread(owerControl, action);
            //    return this.promise;
            //}
            //
            return When(owerControl, action, false);
        }
        public IPromise When(ISynchronizeInvoke owerForm, Action action, bool waitTreadFinally) {
            //ThreadPool.QueueUserWorkItem(new WaitCallback(Worker), action);
            //ISailor sailor = A.Sailor();           
            //            
            ISynchronizeInvoke owerControl = owerForm == null ? this.m_OwnerSyncInvoke : owerForm;
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var sailor = z as Sailor;
                try {
                    action();
                    if(owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            sailor.Resolve(null);
                        });
                    }
                    else {
                        sailor.Resolve(null);
                    }
                }
                catch(Exception exception) {
                    sailor.reason = exception;
                    if(owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            sailor.Reject(exception);
                        });
                    }
                    else {
                        sailor.Reject(exception);
                    }
                }
                finally {
                    //sailor.Finally();
                    mre?.Set();
                }
            });
            Thread newT = new Thread(newPTS);
            newT.Start(this);
            //
            mre?.WaitOne();
            return this.promise;
        }
        private ISynchronizeInvoke m_OwnerSyncInvoke;


        //[SuppressMessage("Microsoft.Design", "CA1031", Justification = "I need the exception to be generic to catch all types of exceptions")]
        void Worker(object state) {
            Action action = state as Action;
            try {
                action();
                Resolve(null);
            }
            catch(Exception exc) {
                Reject(exc);
            }
            finally {
                //Finally();
            }
        }


        //      /// <summary>
        //      /// 在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        //      /// 该操作需要一个＜see cref＝“CancellationToken”/＞来提供停止工作线程的机会。
        ///// Executes the action asynchronously on another thread and the executes the standard promise pattern (then action if all is good, the OnError action if there are exceptions and so on).
        ///// Tha action takes a <see cref="CancellationToken"/> to give the chance to stop the working thread.
        ///// </summary>
        ///// <param name="action">The action to be executed asynchronously on another thread.</param>
        ///// <returns>The promise to interact with.</returns>
        //public IAbortablePromise When(Action<CancellationToken> action) {
        //          cancellationToken.IsCancellationRequested = false;
        //          //ThreadPool.QueueUserWorkItem(new WaitCallback(AbortableWorker), action);
        //          ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(AbortableWorker), action);
        //          return this.abortablePromise;
        //      }

        //[SuppressMessage("Microsoft.Design", "CA1031", Justification = "I need the exception to be generic to catch all types of exceptions")]
        //void AbortableWorker(object state) {
        //    Action<CancellationToken> action = state as Action<CancellationToken>;

        //    try {
        //        action(this.cancellationToken);
        //        if (!this.cancellationToken.IsCancellationRequested) {
        //            Resolve(null);
        //        }
        //    }
        //    catch (Exception exc) {
        //        Reject(exc);
        //    }
        //    finally {
        //        Finally();
        //    }
        //}
    }
}
