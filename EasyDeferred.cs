using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using EasyDeferred.RSG;
using EasyDeferred.Synchronize;
using EasyDeferred.ThreadPooling;

namespace EasyDeferred
{

    /// <summary>
    /// EasyDeferred 
    /// </summary>
    public static class EDefer
    {

        /// <summary>
        /// 处理当前创建线程上下文同步队列中的所有回调
        /// </summary>
        public static void DoEventsSync(Action callBack, bool doLoopMSGEvents) {
            do {
                EasyDeferredSyncContextInvoke.DoEvents();
                if (callBack != null) {
                    callBack();
                }
            } while (doLoopMSGEvents);
        }
        static EasyDeferredSyncContextInvoke m_ContextInvoke;
        public static void Setup() {
            EasyDeferredSyncContextInvoke.Setup();
            PromiseTimer = new PromiseTimer();
            SyncTimesScheduler = new SyncDelayTimesScheduler();
            EasyThreadPoolSingleton.Instance.Initialize();
            FairThreadPoolSingleton.Instance.Initialize();
            SimpleThreadPoolSingleton.Instance.Initialize();
        }
        /// <summary>
        /// 指定上线文同步线程,主要用于console/asp.net项目
        /// </summary>
        /// <param name="syncContext"></param>
        public static void SetSyncContext(EasyDeferredSyncContextInvoke syncContext) {
            m_ContextInvoke = syncContext;
        }
        /// <summary>
        /// 外部执行更新PromiseTimer.Update(float deltaTime)
        /// </summary>
        public static IPromiseTimer PromiseTimer {
            get;
            private set;
        }
        public static SyncDelayTimesScheduler SyncTimesScheduler {
            get;
            private set;
        }
        /// <summary>
        /// 创建新的IDeferred实例
        /// Returns an instance of an object implementing the <see cref="IDeferred"   /> interface.
        /// </summary>
        /// <returns>An object implementing the <code>IDeferred</code> interface</returns>        
        public static IDeferred NewDeferred() {
            return new Deferred();
        }
        /// <summary>
        /// [EasyThreadPool] 执行IDeferred.When(action),然后返回当前线程执行promise.then/finally..等
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IPromise RunAsyn(Action action) {
            IDeferred deferred = NewDeferred();           
            deferred.When(m_ContextInvoke, action);
            return deferred.Promise;
        }
        /// <summary>
        /// [EasyThreadPool] 执行IDeferred.When(action),然后返回当前线程执行promise.then/finally..等
        /// </summary>
        /// <param name="delay">延时</param>
        /// <param name="action">执行方法</param>
        /// <returns></returns>
        public static IPromise RunAsyn(TimeSpan delay,Action action) {
            IDeferred deferred = NewDeferred();
            Action ac = () => {
                System.Threading.Thread.Sleep((int)delay.TotalMilliseconds);
                action();
            };
            deferred.When(m_ContextInvoke, ac);
            return deferred.Promise;
        }
        /// <summary>
        /// [EasyThreadPool] 执行IDeferred.When(action)
        /// </summary>
        /// <param name="action">线程中执行</param>
        /// <param name="arg">参数</param>
        /// <param name="waitTreadFinally">等when待线程完成</param>
        /// <returns></returns>
        public static IDeferred RunAsyn<T>(Action<T> action, T arg, bool waitTreadFinally) {
            IDeferred deferred = NewDeferred();
            deferred.When(m_ContextInvoke, action, arg, waitTreadFinally);
            return deferred;
        }
        /// <summary>
        ///  [EasyThreadPool] 执行IDeferred.When(action)，返回IDeferred，当IDeferred.Promise执行then时取得IDeferred.ResolveValue值为计算结果
        ///  如果参数<see  langword="waitTreadFinally=true" /> 则直接取IDeferred.ResolveValue值为计算结果
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <typeparam name="Result">函数返回值类型</typeparam>
        /// <param name="func">带返回值函数</param>
        /// <param name="arg">函数参数</param>
        /// <param name="waitTreadFinally">等待when线程执行完成</param>
        /// <returns></returns>
        public static IDeferred GetResultAsyn<T, Result>(Func<T, Result> func, T arg, bool waitTreadFinally) {
            IDeferred deferred = NewDeferred();
            //string id=Guid.NewGuid().ToString();
            //string funcID = "funcResult_" + id;
            //string exceptionID = "exception_" + id;  
            Action<T> ac = (argT) => {
                Result ret = func(argT);
                //deferred.SetCache(funcID, ret);
                deferred.ResolveValue = ret;
            };

            deferred.When<T>(m_ContextInvoke, ac, arg, waitTreadFinally);

            //            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            //            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
            //                var deferred2 = z as IDeferred;
            //                Result ret;
            //                try {
            //                    ret = func(arg);
            //                    deferred2.ResolveValue = ret;
            //                    (deferred2.Promise as Promise).Resolve();
            //                }
            //                catch (Exception exception) {
            //                    (deferred2 as Promise).Reject(exception);
            //                }
            //                finally {
            //#if DEBUG
            //                    deferred2.Promise.Finally(() => {
            //                        Console.WriteLine("finally thread id=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
            //                    });
            //#endif
            //                    mre?.Set();
            //                }
            //            });
            //            Thread newT = new Thread(newPTS);
            //            newT.Start(deferred);
            //            //
            //            mre?.WaitOne();
            return deferred;
        }

    }
    public interface IDeferred
    {
        object ResolveValue {
            get; set;
        }
        Exception RejectReason {
            get; set;
        }
        void SetCache(string cacheKey, object cacheValue);
        object GetCache(string cacheKey);
        /// <summary>
        /// 获取用于管理异步操作的<code >IPromise</code>对象。
        /// Gets the <code>IPromise</code> object to manage the asynchronous operation.
        /// </summary>
        /// <value>The <code>IDeferred</code> promise</value>
        IPromise Promise {
            get;
        }



        /// <summary>
        /// 调用Notify promise操作以更新当前异步操作的状态。
        /// Calls the Notify promise action to update the state of the current asynchronous operation.
        /// </summary>
        /// <param name="value">A value indicating the progress if any, otherwise null.</param>
        void Notify(EasyDeferredSyncContextInvoke owerControl, float value);

        /// <summary>
        /// [ThreadPool]在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        /// Executes the action asynchronously on another thread and the executes the standard promise pattern (then action if all is good, the OnError action if there are exceptions and so on).
        /// </summary>
        /// <param name="action">The action to be executed asynchronously on another thread.</param>
        /// <returns>The promise to interact with.</returns>
        IPromise When(EasyDeferredSyncContextInvoke owerControl, Action action);
        /// <summary>
        /// [ThreadPool]在另一个线程上异步执行该操作，并执行标准承诺模式（如果一切正常，则执行操作，如果存在异常，则执行OnError操作，以此类推）。
        /// </summary>
        /// <param name="action"></param>
        /// <param name="waitTreadFinally">指定是否等待线程完成</param>
        /// <returns></returns>
        IPromise When(EasyDeferredSyncContextInvoke owerControl, Action action, bool waitTreadFinally);
        IPromise When<T>(EasyDeferredSyncContextInvoke owerControl, Action<T> action, T arg, bool waitTreadFinally);
    }

    internal class Deferred : IDeferred
    {

        Promise promise = null;
        //AbortablePromise abortablePromise;
        //CancellationToken cancellationToken = new CancellationToken();
        object m_resolveValue = null;
        /// <remarks />
        Exception m_reason;
        public virtual object ResolveValue {
            get {
                //if (this.promise.CurState != PromiseState.Resolved) {
                //    throw new InvalidOperationException("Cannot get Value from a not fulfilled promise");
                //}

                return this.m_resolveValue;
            }
            set {
                this.m_resolveValue = value;
            }
        }

        public virtual Exception RejectReason {
            get {
                //if (this.promise.CurState != PromiseState.Rejected) {
                //    throw new InvalidOperationException("Cannot get Reason from a not rejected promise");
                //}

                return this.m_reason;
            }
            set {
                this.m_reason = value;
            }
        }
        private RSG.ConcurrentDictionary<string, object> cache = new ConcurrentDictionary<string, object>();
        public void SetCache(string cacheKey, object cacheValue) {
            if (cache.ContainsKey(cacheKey)) {
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
        /// 创建Deferred的新实例
        /// Create a new instance of a Deferred
        /// </summary>
        public Deferred() : this(new Promise()) {//, new AbortablePromise()) {
        }

        //internal Deferred(Promise promise, AbortablePromise abortablePromise) {
        //    this.promise = promise;
        //    this.abortablePromise = abortablePromise;
        //    this.abortablePromise.AbortRequested += PromiseRequestedAbort;
        //}
        internal Deferred(Promise promise) {
            this.promise = promise;
            //this.abortablePromise = abortablePromise;
            //this.abortablePromise.AbortRequested += PromiseRequestedAbort;                      
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
        public void Notify(EasyDeferredSyncContextInvoke owerForm, float progress) {
            //this.promise.Notify(value);
            //this.abortablePromise.Notify(value);
            EasyDeferredSyncContextInvoke owerControl = owerForm;
            InvokeIfRequired(owerControl, () => {
                this.promise.ReportProgress(progress);
            });
        }


        static public Thread GetControlOwnerThread(EasyDeferredSyncContextInvoke ctrl) {
            if (ctrl.InvokeRequired)
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
        static public void BeginInvokeIfRequired(EasyDeferredSyncContextInvoke control, Action code) {
            //if (control == null || control.IsDisposed)
            //    return;
            if (control.InvokeRequired) {
                control.BeginInvoke(code, null);
                return;
            }
            code.Invoke();
        }
        public static void BeginInvoke(EasyDeferredSyncContextInvoke control, Action action) {
            control.BeginInvoke(action, null);
        }

        static public void InvokeIfRequired(EasyDeferredSyncContextInvoke control, Action code) {
            //if (control == null || control.IsDisposed)
            //    return;
            if (control.InvokeRequired) {
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
        public IPromise When(EasyDeferredSyncContextInvoke owerControl, Action action) {
            return When(owerControl, action, false);
        }
        public IPromise When(EasyDeferredSyncContextInvoke owerForm, Action action, bool waitTreadFinally) {
            EasyThreadPoolSingleton.Instance.Initialize();
            //            
            //ParameterizedThreadStart newPTS = null;
            EasyDeferredSyncContextInvoke owerControl = owerForm;
            //ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            if (waitTreadFinally) {
                ManualResetEvent mre = new ManualResetEvent(false);
                string exceptionID = "exception_" + Guid.NewGuid().ToString();
                Action func = () => {
                    try {
                        action();
                    }
                    catch (Exception ex) {
                        this.SetCache(exceptionID, ex);
                    }
                    finally {
                        mre.Set();
                    }
                };
              
                var state = EasyThreadPoolSingleton.Instance.ThreadPool.EnqueueWorkItem(func);
                mre.WaitOne();
                //
#if DEBUG
                Console.WriteLine("state:" + state.ToString());
#endif
                //
                object exObj = null;// this.GetCache(exceptionID) as Exception;
                this.cache.TryRemove(exceptionID, out exObj);
                if (exObj != null) {
                    this.RejectReason = exObj as Exception;
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            this.Reject(this.RejectReason);
                        });
                    }
                    else {
                        this.Reject(this.RejectReason);
                    }
                }
                else {
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            Resolve(null);
                        });
                    }
                    else {
                        Resolve(null);
                    }
                }
            }
            else {
                Action func = () => {
                    try {
                        action();
                        if (owerControl != null) {
                            InvokeIfRequired(owerControl, () => {
                                this.Resolve(null);
                            });
                        }
                        else {
                            this.Resolve(null);
                        }
                    }
                    catch (Exception ex) {
                        this.RejectReason = ex;
                        if (owerControl != null) {
                            InvokeIfRequired(owerControl, () => {
                                this.Reject(ex);
                            });
                        }
                        else {
                            this.Reject(ex);
                        }
                    }
                    finally {

                    }
                };
                
                var state = EasyThreadPoolSingleton.Instance.ThreadPool.EnqueueWorkItem(func);
                //
#if DEBUG
                Console.WriteLine("state:" + state.ToString());
#endif
                //
            }

            return this.promise;
        }
        //private EasyDeferredSyncContextInvoke m_OwnerSyncInvoke;

        public IPromise When<T>(EasyDeferredSyncContextInvoke owerForm, Action<T> action, T arg, bool waitTreadFinally) {
            EasyThreadPoolSingleton.Instance.Initialize();
            //ParameterizedThreadStart newPTS = null;
            EasyDeferredSyncContextInvoke owerControl = owerForm;
            //ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            if (waitTreadFinally) {
                ManualResetEvent mre = new ManualResetEvent(false);
                string exceptionID = "exception_" + Guid.NewGuid().ToString();
                Action<T> func = (argT) => {
                    try {
                        action(argT);
                    }
                    catch (Exception ex) {
                        this.SetCache(exceptionID, ex);
                    }
                    finally {
                        mre.Set();
                    }
                };

                var state = EasyThreadPoolSingleton.Instance.ThreadPool.EnqueueWorkItem(func, arg);
                mre.WaitOne();
                //
#if DEBUG
                Console.WriteLine(state.ToString());
#endif
                //
                object exObj = null;// this.GetCache(exceptionID) as Exception;
                this.cache.TryRemove(exceptionID, out exObj);
                if (exObj != null) {
                    this.RejectReason = exObj as Exception;
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            this.Reject(this.RejectReason);
                        });
                    }
                    else {
                        this.Reject(this.RejectReason);
                    }
                }
                else {
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            Resolve(null);
                        });
                    }
                    else {
                        Resolve(null);
                    }
                }
            }
            else {
                Action<T> func = (argT) => {
                    try {
                        action(argT);
                        if (owerControl != null) {
                            InvokeIfRequired(owerControl, () => {
                                this.Resolve(null);
                            });
                        }
                        else {
                            this.Resolve(null);
                        }
                    }
                    catch (Exception ex) {
                        this.RejectReason = ex;
                        if (owerControl != null) {
                            InvokeIfRequired(owerControl, () => {
                                this.Reject(ex);
                            });
                        }
                        else {
                            this.Reject(ex);
                        }
                    }
                    finally {

                    }
                };
                var state = EasyThreadPoolSingleton.Instance.ThreadPool.EnqueueWorkItem(func, arg);
                //
#if DEBUG
                Console.WriteLine("state:" + state.ToString());
#endif
                //
            }

            return this.promise;
        }
    }

    /*

    /// <summary>
    /// EasyDeferred 
    /// </summary>
    public static class EDeferred
    {
        ///// <summary>
        ///// Get the factory instance.
        ///// </summary>
        //public IFactory Factory {
        //    get;
        //    private set;
        //}

        ///// <summary>
        ///// Global logger.r5
        ///// </summary>
        //public RSG.Utils.ILogger Logger {
        //    get;
        //    private set;
        //}

        ///// <summary>
        ///// Used to schedule code onto the main thread.
        ///// </summary>
        //public IDispatcher Dispatcher {
        //    get;
        //    private set;
        //}

        ///// <summary>
        ///// Get the global promise timer.
        ///// </summary>
        //public IPromiseTimer PromiseTimer {
        //    get;
        //    private set;
        //}

        ///// <summary>
        ///// Global singleton manager.
        ///// </summary>
        //public ISingletonManager SingletonManager {
        //    get;
        //    private set;
        //}

        /// <summary>
        /// 创建新的IDeferred实例
        /// Returns an instance of an object implementing the <code>IDeferred</code> interface.
        /// </summary>
        /// <returns>An object implementing the <code>IDeferred</code> interface</returns>        
        public static IDeferred NewDeferred() {
            return new Deferred();
        }
        /// <summary>
        /// [ThreadPool] 执行IDeferred.When(action),然后返回当前线程执行promise.then/finally..等
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IPromise RunAsyn(ISynchronizeInvoke owerControl, Action action) {
            IDeferred deferred = NewDeferred();
            deferred.When(owerControl, action);
            return deferred.Promise;
        }
        /// <summary>
        /// 开启一个新线程 执行action
        /// </summary>
        /// <param name="action">线程中执行</param>
        /// <returns></returns>
        public static IDeferred RunAsyn<T>(ISynchronizeInvoke owerControl, Action<T> action, T arg, bool waitTreadFinally) {
            IDeferred deferred = NewDeferred();
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var deferred2 = z as IDeferred;
                try {
                    action(arg);
                    (deferred2.Promise as Promise).Resolve();
                }
                catch (Exception exception) {
                    (deferred2.Promise as Promise).Reject(exception);
                }
                finally {
#if DEBUG
                    deferred2.Promise.Finally(() => {
                        Console.WriteLine("finally thread id=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    });
#endif
                    mre?.Set();
                }
            });
            Thread newT = new Thread(newPTS);
            newT.Start(deferred);
            //
            mre?.WaitOne();
            return deferred;
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
        public static IDeferred GetResultAsyn<T, Result>(ISynchronizeInvoke owerControl, Func<T, Result> func, T arg, bool waitTreadFinally) {
            IDeferred deferred = NewDeferred();
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var deferred2 = z as IDeferred;
                Result ret;
                try {
                    ret = func(arg);
                    deferred2.ResolveValue = ret;
                    (deferred2.Promise as Promise).Resolve();
                }
                catch (Exception exception) {
                    (deferred2 as Promise).Reject(exception);
                }
                finally {
#if DEBUG
                    deferred2.Promise.Finally(() => {
                        Console.WriteLine("finally thread id="+System.Threading.Thread.CurrentThread.ManagedThreadId);
                    });
#endif
                    mre?.Set();
                }
            });
            Thread newT = new Thread(newPTS);
            newT.Start(deferred);
            //
            mre?.WaitOne();
            return deferred;
        }

    }
    public interface IDeferred
    {
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
        /// <value>The <code>IDeferred</code> promise</value>
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

    internal class Deferred : IDeferred
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
            if (cache.ContainsKey(cacheKey)) {
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
        /// 创建Deferred的新实例
        /// Create a new instance of a Deferred
        /// </summary>
        public Deferred() : this(new Promise()) {//, new AbortablePromise()) {
        }

        //internal Deferred(Promise promise, AbortablePromise abortablePromise) {
        //    this.promise = promise;
        //    this.abortablePromise = abortablePromise;
        //    this.abortablePromise.AbortRequested += PromiseRequestedAbort;
        //    SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        //    if (synchronizationContext != null) {
        //        promise.SynchronizationContext = synchronizationContext;
        //        this.abortablePromise.SynchronizationContext = synchronizationContext;
        //    }
        //}
        internal Deferred(Promise promise) {
            this.promise = promise;
            //this.abortablePromise = abortablePromise;
            //this.abortablePromise.AbortRequested += PromiseRequestedAbort;
            //SynchronizationContext synchronizationContext = SynchronizationContext.Current;

            //if (synchronizationContext != null) {
            //    promise.SynchronizationContext = synchronizationContext;
            //    this.abortablePromise.SynchronizationContext = synchronizationContext;
            //}
            this.m_OwnerSyncInvoke = new SyncEasyInvoke();
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
            if (ctrl.InvokeRequired)
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
            if (control.InvokeRequired) {
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
            if (control.InvokeRequired) {
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
            //IDeferred Deferred = Deferred();           
            //            
            ISynchronizeInvoke owerControl = owerForm == null ? this.m_OwnerSyncInvoke : owerForm;
            ManualResetEvent mre = waitTreadFinally ? new ManualResetEvent(false) : null;
            ParameterizedThreadStart newPTS = new ParameterizedThreadStart((z) => {
                var deferred = z as Deferred;
                try {
                    action(); 
                    mre?.Set();
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            deferred.Resolve(null);
                        });
                    }
                    else {
                        deferred.Resolve(null);
                    }
                }
                catch (Exception exception) {
                    mre?.Set();
                    deferred.reason = exception;
                    if (owerControl != null) {
                        InvokeIfRequired(owerControl, () => {
                            deferred.Reject(exception);
                        });
                    }
                    else {
                        deferred.Reject(exception);
                    }
                }
                finally {
#if DEBUG
                    deferred.promise.Finally(()=> {
                        Console.WriteLine("finally thread id=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    });
#endif
                    
                 
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
            catch (Exception exc) {
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

    */
}
