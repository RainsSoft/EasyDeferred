using System;
using System.Collections;
using System.ComponentModel;
using EasyDeferred.Core;
using EasyDeferred.Threading;
using EasyDeferred.Synchronize;
using EasyDeferred.RSG;
using EasyDeferred.RSG.Linq;


namespace EasyDeferred.Coroutine
{
    /// <summary>
    /// 当前申请该对象线程执行协程,线程安全
    /// </summary>
    public class CoroutineMgr : Singleton<CoroutineMgr>
    {
        private int m_IntervalMilliseconds = 20;
        public int IntervalMilliseconds {
            get {
                return m_IntervalMilliseconds;
            }
            set {
                m_IntervalMilliseconds = Math.Max(1, value);
                if (this.syncTimer != null) {
                    this.syncTimer.Period = m_IntervalMilliseconds;
                }
            }
        }
        private bool m_Init = false;
        public override bool Initialize(params object[] args) {
#if DEBUG
            Console.WriteLine("CoroutineMgr Initialize  thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif 
            this.coroutineHandleMap = new ConcurrentDictionary<string, CoroutineHandle>();
            this.coroutineRunner = new CoroutineRunner();
            this.syncInvoke = new SyncEasyInvoke();//new SyncContextInvoke();
            this.syncTimer = new ThreadTimer();
            //this.syncTimer = new SyncDelayTimesScheduler();
            //this.syncTimer.SynchronizingObject = new SyncContextInvoke();

            int milliseconds = (int)(m_IntervalMilliseconds * 1000f);
            //this.syncTimer.PollingInterval = milliseconds;//每秒更新50次
            //this.syncTimer.InvokeCompleted += SynTimer_InvokeCompleted;
            this.syncTimer.Mode = TimerMode.Periodic;
            this.syncTimer.Period = milliseconds;
            this.syncTimer.Tick += (a, b) => fixedUpdate();
            //OnFixedUpdate fUpdate = fixedUpdate;
            this.syncTimer.Start();
            //this.syncTimer.Add(SyncDelayTimesScheduler.Infinite, milliseconds, (Action)fixedUpdate, null); ;

            base.Initialize(args);
            //
            m_Init = true;
            return m_Init;

        }
        //private delegate void OnFixedUpdate();
        //static int count = 10;
        private void fixedUpdate() {
#if DEBUG
            //Console.WriteLine( "CoroutineMgr fixedUpdate InvokeCompleted thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif
            //            int ret = System.Threading.Interlocked.Add(ref count, -2);
            //            if (ret < 0) {
            //                this.syncTimer.Stop();
            //                //return;
            //            }
            //            else {
            //                System.Threading.Thread.Sleep(count * 1000);
            //            }
            //            //执行完成回调
            //#if DEBUG
            //            Console.WriteLine(count + "CoroutineMgr fixedUpdate InvokeCompleted thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
            //#endif
            Action callCRUpdate = () => {
                if (this.coroutineRunner != null) {
                    this.coroutineRunner.Update(m_IntervalMilliseconds*0.001f);
                }
            };
            //触发定时器      
            if (this.syncInvoke.InvokeRequired) {
                //var ir = this.syncInvoke.BeginInvoke(callCRUpdate, null);
                //var obj = this.syncInvoke.EndInvoke(ir);
                //int i = 0;
                this.syncInvoke.Invoke(callCRUpdate,null);
            }
            else {
                //System.Threading.Thread.Sleep(1000);
                callCRUpdate();
            }
        }
        
        
        protected override void dispose(bool disposeManagedResources) {
            if (this.syncTimer != null) {
                this.syncTimer.Dispose();
            }
            if (this.coroutineRunner != null) {
                this.coroutineRunner.StopAll();
            }
            this.coroutineHandleMap.Clear();
            base.dispose(disposeManagedResources);
        }
        public SyncEasyInvoke syncInvoke;
        //private SyncDelayTimesScheduler syncTimer;
        private ThreadTimer syncTimer;
        private CoroutineRunner coroutineRunner;
        private ConcurrentDictionary<string, CoroutineHandle> coroutineHandleMap;
        public CoroutineHandle GetCoroutineHandle(string name) {
            CoroutineHandle ch;
            coroutineHandleMap.TryGetValue(name, out ch);
            return ch;
        }
        public bool StartCoroutine(string coName, IEnumerator routine, float delaySeconds2Run = 0f) {
            if (coroutineHandleMap.ContainsKey(coName)) {
                return false;
            }
            CoroutineHandle run = coroutineRunner.Run(delaySeconds2Run, routine);
            return coroutineHandleMap.TryAdd(coName, run);
        }
        public bool StopCoroutine(string coName) {
            if (coroutineHandleMap.ContainsKey(coName) == false) {
                return false;
            }
            CoroutineHandle run;
            if (coroutineHandleMap.TryGetValue(coName, out run)) {
                return coroutineRunner.Stop(run);
            }
            return false;
        }
        /// <summary>
        /// do action in main thread or your select thread
        /// </summary>
        public void DoEvents(Action callBack) {
            this.syncInvoke.DoActions();            
            if (callBack != null) {
                callBack();
            }

        }
    }
}
