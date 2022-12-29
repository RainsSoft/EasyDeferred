using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EasyDeferred.Core;
using EasyDeferred.ThreadPooling;
namespace EasyDeferred
{
    public class EasyThreadPoolSingleton : Singleton<EasyThreadPoolSingleton>
    {
        private EasyThreadPool m_ThreadPool;
        public EasyThreadPool ThreadPool { get { return m_ThreadPool; } }
        public override bool Initialize(params object[] args) {
            if (m_ThreadPool == null) {
                int threadNum = Math.Max(Environment.ProcessorCount, (int)((2 % Environment.ProcessorCount) + 1) * Environment.ProcessorCount / 2);
                m_ThreadPool = new EasyThreadPool("easy",threadNum);                
            }
            return base.Initialize(args);
        }
        protected override void dispose(bool disposeManagedResources) {
            if (m_ThreadPool != null) {
                m_ThreadPool.Dispose();
            }
            m_ThreadPool = null;
            base.dispose(disposeManagedResources);
        }
        public bool QueueWorkItem(WaitCallback callBack) {

            return false;
        }
        public bool QueueWorkItem(WaitCallback callBack, object state) {

            return false;
        }
    }
    public class FairThreadPoolSingleton : Singleton<FairThreadPoolSingleton>
    {

        private FairThreadPool.FairThreadPool m_ThreadPool;
        public FairThreadPool.FairThreadPool ThreadPool { get { return m_ThreadPool; } }
        public override bool Initialize(params object[] args) {
            if (m_ThreadPool == null) {
                int threadNum= Math.Max(Environment.ProcessorCount,(int)((2 % Environment.ProcessorCount) + 1) * Environment.ProcessorCount/2);
                m_ThreadPool = new FairThreadPool.FairThreadPool("fair",threadNum);
            }
            return base.Initialize(args);
        }
        protected override void dispose(bool disposeManagedResources) {
            if (m_ThreadPool != null) {
                m_ThreadPool.Dispose(); 
            }
            m_ThreadPool = null;
            base.dispose(disposeManagedResources);
        }
    }
    public class SimpleThreadPoolSingleton : Singleton<SimpleThreadPoolSingleton>
    {

        private SimpleThreadPool.SimpleThreadPool m_ThreadPool;
        public SimpleThreadPool.SimpleThreadPool ThreadPool { get { return m_ThreadPool; } }
        public override bool Initialize(params object[] args) {
            if (m_ThreadPool == null) {
                int threadNum = Math.Max(Environment.ProcessorCount, (int)((2 % Environment.ProcessorCount) + 1) * Environment.ProcessorCount / 2);
                m_ThreadPool = new SimpleThreadPool.SimpleThreadPool("simple",1,threadNum,1);
            }
            return base.Initialize(args);
        }
        protected override void dispose(bool disposeManagedResources) {
            if (m_ThreadPool != null) {
                m_ThreadPool.Dispose();
            }
            m_ThreadPool = null;
            base.dispose(disposeManagedResources);
        }
    }

}
