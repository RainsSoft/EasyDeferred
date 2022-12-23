using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EasyDeferred.Threading;


namespace EasyDeferred.SimpleThreadPool
{

    public class SimpleThreadPool : IDisposable
    {
        readonly object lockerObj = new object();
        private int max = 5; //默认最大线程数

        private int min = 1;  //默认最小线程数

        private int increment = 1; //当活动线程不足的时候新增线程的默认增量

        private Dictionary<string, SimpleTask> publicPool; //所有的线程

        public Dictionary<string, SimpleTask> PublicPool {
            get { return publicPool; }
            //set { publicPool = value; }
        }

        private Queue<SimpleTask> freeQueue;  //空闲线程队列

        private Dictionary<string, SimpleTask> working;   //正在工作的线程

        private List<string> workingKeys;

        private Queue<SimpleWaitItem> waitQueue;  //等待执行工作队列

        private static SimpleThreadPool threadPoolManager = null;

        //设置最大线程数

        public void Setmaxthread(int Value) {

            lock (lockerObj) {

                max = Math.Max(this.min, Value);

            }

        }

        //设置最小线程数

        public void Setminthread(int Value) {

            lock (lockerObj) {

                min = Math.Max(0, Value);

            }

        }

        //设置增量

        public void Setincrement(int Value) {

            lock (lockerObj) {

                increment = Value;

            }

        }
        /// <summary>
        /// 获取当前线程池内的空闲线程数量
        /// </summary>
        /// <returns></returns>
        public int GetAvailableThreads() {
            return this.freeQueue.Count;
        }
        /// <summary>
        /// 获取当前线程池内工作项总数
        /// </summary>
        /// <returns></returns>
        public int GetWorkCount() {
            return this.workingKeys.Count;
        }
        public SimpleThreadPool(string threadsNamePrefix, int min, int max, int increment) {
            this._threadsNamePrefix = threadsNamePrefix;
            this.min = min;
            this.max = max;
            this.increment = increment;
            System.Diagnostics.Debug.Assert(max>min&&min>0,"参数异常");
            initThreadTool();
        }

        public SimpleThreadPool(string threadsNamePrefix) {
            this._threadsNamePrefix = threadsNamePrefix+ "-SimpleTd#";
            //int wmin = 0;
            //int iomin = 0;
            //int wmax = 0;
            //int iomax = 0;
            //System.Threading.ThreadPool.GetMinThreads(out wmin, out iomin);
            //System.Threading.ThreadPool.GetMaxThreads(out wmax, out iomax);
            //this.min = wmin;
            //this.max = Math.Min(50, iomax);
            //this.max = Math.Max(this.max, this.min);      
           
            var _processorCnt = Environment.ProcessorCount;
            var newMinThreads = ((2 % _processorCnt) + 1) * _processorCnt;
            this.min = 1;
            this.max = newMinThreads;
            this.increment = 1;
            initThreadTool();
        }
        string _threadsNamePrefix;
        ///// <summary>
        ///// 获取实例
        ///// </summary>
        ///// <param name="min">初始化最小线程数</param>
        ///// <param name="max">初始化最大线程数</param>
        ///// <param name="increment">线程增量数</param>
        ///// <returns></returns>
        //public static SimpleThreadPool getInstance(int min, int max, int increment) {
        //    if (threadPoolManager == null) {
        //        threadPoolManager = new SimpleThreadPool(min, max, increment);
        //    }

        //    return threadPoolManager;
        //}
        //public static SimpleThreadPool getInstance() {
        //    if (threadPoolManager == null) {
        //        threadPoolManager = new SimpleThreadPool(1, 5, 1);
        //    }
        //    return threadPoolManager;
        //}


        /// <summary>
        /// 初始化线程池
        /// </summary>
        void initThreadTool() {

            publicPool = new Dictionary<string, SimpleTask>();

            working = new Dictionary<string, SimpleTask>();

            workingKeys = new List<string>();

            freeQueue = new Queue<SimpleTask>();

            waitQueue = new Queue<SimpleWaitItem>();

            SimpleTask t = null;

            //先创建最小线程数的线程

            for (int i = 0; i < min; i++) {
                t = new SimpleTask(_threadsNamePrefix);
                //注册线程完成时触发的事件
                //t.WorkComplete -= new Action<NTask>(t_WorkComplete);
                t.WorkComplete += new Action<SimpleTask>(t_WorkComplete);
                //加入到所有线程的字典中
                publicPool.Add(t.Key, t);
                //因为还没加入具体的工作委托就先放入空闲队列
                freeQueue.Enqueue(t);
            }

        }

        //线程执行完毕后的触发事件
        void t_WorkComplete(SimpleTask obj) {
            obj.WorkComplete -= new Action<SimpleTask>(t_WorkComplete);
            lock (lockerObj) {

                //首先因为工作执行完了，所以从正在工作字典里删除
                working.Remove(obj.Key);
                //检查是否有等待执行的操作，如果有等待的优先执行等待的任务
                if (waitQueue.Count > 0) {
                    //先要注销当前的线程，将其从线程字典删
                    publicPool.Remove(obj.Key);
                    obj.Close();
                    //从等待任务队列提取一个任务
                    SimpleWaitItem item = waitQueue.Dequeue();
                    SimpleTask nt = null;
                    //如果有空闲的线程，就是用空闲的线程来处理
                    if (freeQueue.Count > 0) {
                        nt = freeQueue.Dequeue();
                    }
                    else {
                        //如果没有空闲的线程就再新创建一个线程来执行
                        nt = new SimpleTask(_threadsNamePrefix);
                        publicPool.Add(nt.Key, nt);
                        nt.WorkComplete += new Action<SimpleTask>(t_WorkComplete);
                    }

                    //设置线程的执行委托对象和上下文对象
                    nt.taskWorkItem = item.Works;
                    nt.contextdata = item.Context;
                    //添加到工作字典中
                    working.Add(nt.Key, nt);
                    workingKeys.Add(nt.Key);
                    //唤醒线程开始执行
                    nt.Active();
                }
                else {

                    //如果没有等待执行的操作就回收多余的工作线程
                    if (freeQueue.Count > min) {
                        //当空闲线程超过最小线程数就回收多余的这一个
                        publicPool.Remove(obj.Key);
                        obj.Dispose();
                    }
                    else {

                        //如果没超过就把线程从工作字典放入空闲队列
                        obj.contextdata = null;
                        freeQueue.Enqueue(obj);
                    }

                }

            }

        }

        /// <summary>
        /// 添加工作委托
        /// </summary>
        /// <param name="TaskItem"></param>
        /// <param name="Context"></param>
        /// <returns>ManagedThreadId工作线程ID</returns>
        public int AddTaskItem(WaitCallback TaskItem, object Context) {

            lock (lockerObj) {

                SimpleTask t = null;
                int len = publicPool.Values.Count;


                //如果空闲列表非空并且线程没有到达最大值
                if (freeQueue.Count == 0 && len < max) {
                    //如果没有空闲队列了，就根据增量创建线程
                    for (int i = 0; i < increment; i++) {

                        //判断线程的总量不能超过max

                        if ((len + i + 1) <= max) {

                            t = new SimpleTask(_threadsNamePrefix);

                            //设置完成响应事件
                            t.WorkComplete -= new Action<SimpleTask>(t_WorkComplete);
                            t.WorkComplete += new Action<SimpleTask>(t_WorkComplete);

                            //加入线程字典

                            publicPool.Add(t.Key, t);

                            //加入空闲队列

                            freeQueue.Enqueue(t);

                        }

                        else {

                            break;

                        }

                    }
                }
                else if (freeQueue.Count == 0 && len == max) {
                    //如果线程达到max就把任务加入等待队列
                    waitQueue.Enqueue(new SimpleWaitItem() { Context = Context, Works = TaskItem });

                    return -1;
                }

                //从空闲队列pop一个线程

                t = freeQueue.Dequeue();

                //加入工作字典

                working.Add(t.Key, t);

                workingKeys.Add(t.Key);

                //设置执行委托

                t.SetWorkItem(TaskItem, Context);

                //设置状态对象

                //t.contextdata = Context;
                Console.WriteLine(Context + TaskItem.Method.Name);

                //唤醒线程开始执行
                t.Active();
                return t.thread.ManagedThreadId;
            }
        }
        /// <summary>
        /// 通知在当前任务完成后关闭线程
        /// </summary>
        /// <param name="threadId"></param>
        public void CloseAfterCurrTask(int threadId) {
            //获取线程NTask对象
            SimpleTask task = getNTask(threadId);
            if (task != null) {
                task.Close();
            }

        }

        /// <summary>
        /// 根据线程ID销毁一个线程
        /// </summary>
        /// <param name="threadId"></param>
        public void DisposeThread(int threadId) {

            //获取线程NTask对象
            SimpleTask task = getNTask(threadId);
            //从工作列表删除，并添加至空闲
            if (task != null) {
                working.Remove(task.Key);
                workingKeys.Remove(task.Key);
                ////这里有bug,会导致某些情况下workcomplate又添加一次
                //freeQueue.Enqueue(task);
                ////task.m_Thread.Suspend();
                //task.locks.WaitOne();
                publicPool.Remove(task.Key);
                task.Dispose();
            }
        }

        /// <summary>
        /// 根据线程ID获取一个线程实例
        /// </summary>
        /// <param name="threadId"></param>
        /// <returns></returns>
        public SimpleTask getNTask(int threadId) {

            //List<NTask> list = working.Select(x => x.Value).Where(x => x.thread.ManagedThreadId == threadId).ToList();

            //if(list.Count>0){
            //    return list[0];
            //}
            for (int i = 0; i < workingKeys.Count; i++) {
                string key = workingKeys[i];
                SimpleTask n = working.ContainsKey(key) ? working[key] : null;
                if (n != null && n.thread.ManagedThreadId == threadId) {
                    return n;
                }

            }
            return null;

        }
        /// <summary>
        ///     Waits for the queue to empty.
        /// </summary>
        public void WaitForEveryWorkerIdle() {
            // A spinWait ensures a yield from time to time, forcing the CPU to do a context switch, thus allowing other processes to finish.
            var spinWait = new SpinWait();
            while (workingKeys.Count > 0) {
                Thread.MemoryBarrier();
                spinWait.SpinOnce();
            }
        }
        //回收资源
        public void Dispose() {

            //throw new NotImplementedException();

            foreach (SimpleTask t in publicPool.Values) {

                //关闭所有的线程

                using (t) { t.Close(); }

            }

            publicPool.Clear();

            working.Clear();

            workingKeys.Clear();

            waitQueue.Clear();

            freeQueue.Clear();
            //GC
            GC.Collect(0, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }
    }

    public class SimpleTask : IDisposable
    {
        public AutoResetEvent locks { get; set; } //线程锁
        public Thread thread { get; set; }  //线程对象
        public WaitCallback taskWorkItem { get; set; }//线程体委托
        public bool working { get; set; }  //线程是否工作
        public object contextdata { get; set; }
        public event Action<SimpleTask> WorkComplete;  //线程完成一次操作的事件
        public string Key { get; set; }        //用于字典的Key
        //初始化包装器
        public SimpleTask(string name) {
            threadsNamePrefix = name;
            //设置线程一进入就阻塞
            locks = new AutoResetEvent(false);
            Key = Guid.NewGuid().ToString();
            //初始化线程对象
            thread = new Thread(Work);
            thread.IsBackground = true;
            working = true;
            contextdata = new object();
            //开启线程
            m_threadId=Interlocked.Increment(ref m_threadId);
            thread.Name = threadsNamePrefix +  m_threadId;
            thread.Start();
        }
        string threadsNamePrefix="SimpleThreadPool#";
        static int m_threadId = 0;
        //唤醒线程
        public void Active() {
            working = true;
            locks.Set();
        }

        //设置执行委托和状态对象
        public void SetWorkItem(WaitCallback action, object context) {
            taskWorkItem = action;
            contextdata = context;
        }

        //线程体包装方法
        private void Work() {
            while (working) {
                //阻塞线程
                locks.WaitOne();
                try {

                    taskWorkItem(contextdata);
                }
                finally {
                    //完成一次执行，触发事件
                    WorkComplete(this);
                }
            }
            //线程要退出了,释放资源
            this.Dispose();
        }

        //关闭线程
        public void Close() {
            working = false;
        }
        public bool IsDisposed {
            get;
            private set;
        }
        //回收资源
        protected void Dispose(bool disposing) {
            if (this.IsDisposed)
                return;
            IsDisposed = true;
            //防止GC自动回收
            if (disposing) {
                //释放信号
                try {
                    this.locks.Close();
                }
                catch {
                }
                try {
                    //注意：只有调用 Dispose() 的不是当前对象维护的线程才调用 Abort
                    System.Diagnostics.Debug.Assert(Thread.CurrentThread.ManagedThreadId != this.thread.ManagedThreadId);

                    thread.Abort();
                }
                catch {
                }

            }


        }

        ~SimpleTask() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }

    public class SimpleWaitItem
    {
        public WaitCallback Works { get; set; }

        public object Context { get; set; }
    }
}