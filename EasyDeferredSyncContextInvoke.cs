using EasyDeferred.Core;
using EasyDeferred.RSG;
using EasyDeferred.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace EasyDeferred
{
    /// <summary>
    /// 本项目同步上下文SynchronizationContext基类 ,不能直接New()
    /// </summary>
    public class EasyDeferredSyncContextInvoke : SynchronizationContext, ISynchronizeInvoke
    {
       
        protected EasyDeferredSyncContextInvoke() : base() {

        }
        /// <summary>
        /// 内部用同步上下文
        /// </summary>
        protected virtual SynchronizationContext context => throw new NotImplementedException();
        public virtual bool InvokeRequired => throw new NotImplementedException();

        public virtual IAsyncResult BeginInvoke(Delegate method, params object[] args) {
            throw new NotImplementedException();
        }

        public virtual object EndInvoke(IAsyncResult result) {
            throw new NotImplementedException();
        }

        public virtual object Invoke(Delegate method, params object[] args) {
            throw new NotImplementedException();
        }
        public virtual bool NeedActionLoopInCreatedThread {
            get { return false; }
        }
        /// <summary>
        /// 处理当前创建线程上下文同步队列中的所有回调
        /// 比如需要在console主线程中同步上下文执行
        /// </summary>
        protected virtual void ProcessActionsQueue() {
        }


        #region 主线程执行上下文方法委托

        static RSG.ConcurrentDictionary<string, RSG.Func<EasyDeferredSyncContextInvoke>> m_EasyDeferredSyncContextInvoke_CreateCD = new ConcurrentDictionary<string, Func<EasyDeferredSyncContextInvoke>>();

        static RSG.ConcurrentQueue<EasyDeferredSyncContextInvoke> m_Queue = new RSG.ConcurrentQueue<EasyDeferredSyncContextInvoke>();
        /// <summary>
        /// 构建同步上下文对象,并把对象纳入队列管理
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T CreateEasySynchronizaInvoke<T>()
            where T : EasyDeferredSyncContextInvoke {
            //T t = new T();
            string name = typeof(T).Name;
            RSG.Func<EasyDeferredSyncContextInvoke> func = null;
            m_EasyDeferredSyncContextInvoke_CreateCD.TryGetValue(name,out func);
            T t = func() as T;
            m_Queue.Enqueue(t);
            return t;
        }
        public static T GetEasySynchronizaInvoke<T>() 
            where T:EasyDeferredSyncContextInvoke{
            var ie = m_Queue.GetEnumerator();
            while (ie.MoveNext()) {
                if (ie.Current != null) {
                    if (ie.Current is T) {
                        return ie.Current as T;    
                        
                    }
                }
            }
            return default(T);
        }
        /// <summary>
        /// 注册 同步上下文EasyDeferredSyncContextInvoke 对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="createSyncContextInvoke"></param>
        public static void RegisterEasySynchronizaInvokeType<T>(RSG.Func<T> createSyncContextInvoke) 
            where T: EasyDeferredSyncContextInvoke{
            string name = typeof(T).Name;
            RSG.Func<EasyDeferredSyncContextInvoke> func = () => {
                return createSyncContextInvoke();
            };
            m_EasyDeferredSyncContextInvoke_CreateCD.TryAdd(name, func);
        }
        /// <summary>
        /// 处理当前创建线程上下文同步队列中的所有回调
        /// </summary>
        internal static void DoEvents(){//bool doLoopMSGEvents = false) {
            //do {
                var ie = m_Queue.GetEnumerator();
                while (ie.MoveNext()) {
                    if (ie.Current != null) {
                        ie.Current.ProcessActionsQueue();
                    }
                }
                threadSpinOnce();
                //Thread.Sleep(10);
            //} while (doLoopMSGEvents);
        }
        public static void threadSpinOnce() {
            //System.Threading.Thread.Sleep(0);
            SpinWait spinWait = new SpinWait();
            Thread.MemoryBarrier();
            spinWait.SpinOnce();
        }
        static bool m_HasSetup = false;
        /// <summary>
        /// 内部初始化基本同步上下文构建器
        /// </summary>
        internal static void Setup() {
            if (m_HasSetup) return;
            RegisterEasySynchronizaInvokeType<Synchronize.SyncContextInvoke>(() => new Synchronize.SyncContextInvoke());
            RegisterEasySynchronizaInvokeType<Synchronize.SyncEasyContextInvoke>(() => new Synchronize.SyncEasyContextInvoke());
            EasySyncContextInvoke = CreateEasySynchronizaInvoke<Synchronize.SyncEasyContextInvoke>();
            SyncContextInvoke=CreateEasySynchronizaInvoke<Synchronize.SyncContextInvoke>();
            m_HasSetup = true;
        }
        /// <summary>
        ///  Setup()后的简易 上下文同步，外部执行其上下文处理队列DoEvents()
        /// </summary>
        public static Synchronize.SyncEasyContextInvoke EasySyncContextInvoke {
            get;private set;
        }
        /// <summary>
        ///  Setup()后 上下文同步，外部不可执行其上下文处理队列
        /// </summary>
        public static Synchronize.SyncContextInvoke SyncContextInvoke {
            get;private set;
        }
        #endregion

    }

   
}
