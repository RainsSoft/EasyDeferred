using System;
using System.Collections.Generic;
using System.Threading;
using EasyDeferred;
using EasyDeferred.RSG;

namespace EasyDeferred.Synchronize
{
    /// <summary>
    /// 用于控制台线程同步上下文，类似winfor UI的WindowsFormsSynchronizationContext以及wpf的DispatcherSynchronizationContext
    /// </summary>
    public class ExeArg
    {
        //要放到主线程(UI线程)中执行的方法
        public Action<object> Action { get; set; }
        //方法执行时额外的参数
        public object State { get; set; }
        //是否同步执行
        public bool Sync { get; set; }
        //静态字典，key: 线程Id，value: 队列
        public static ConcurrentDictionary<int, BlockingCollection<ExeArg>> QueueDics = new ConcurrentDictionary<int, BlockingCollection<ExeArg>>();
        //当前线程对应的字典
        public static BlockingCollection<ExeArg> CurrentQueue => QueueDics.ContainsKey(Thread.CurrentThread.ManagedThreadId) ? QueueDics[Thread.CurrentThread.ManagedThreadId] : null;
    }

    public class MySynchronizationContext : SynchronizationContext
    {
        private readonly MyControl ctrl;

        public MySynchronizationContext(MyControl ctrl) {
            this.ctrl = ctrl;
        }
        public override void Send(SendOrPostCallback d, object state) {
            ctrl.Invoke(state => d(state), state);
        }

        public override void Post(SendOrPostCallback d, object state) {
            ctrl.BeginInvoke(state => d(state), state);
        }

        
    }

    public class MyControl
    {
        //记录创建这个控件的线程(UI线程)
        private Thread _createThread = null;
        public MyControl() {
            //第一个控件创建时初始化一个SynchronizationContext实例，并将它和当前线程绑定一起
            if (SynchronizationContext.Current == null) SynchronizationContext.SetSynchronizationContext(new MySynchronizationContext(this));
            _createThread = Thread.CurrentThread;
            //初始化一个字典队列，key: 线程Id，value：参数队列
            ExeArg.QueueDics.TryAdd(Thread.CurrentThread.ManagedThreadId, new BlockingCollection<ExeArg>());
        }
        //同步调用
        public void Invoke(Action<object> action, object state) {
            var queues = ExeArg.QueueDics[_createThread.ManagedThreadId];
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            queues.Add(new ExeArg() {
                Action = obj => {
                    action(state);
                    manualResetEvent.Set();
                },
                State = state,
                Sync = true
            });
            manualResetEvent.WaitOne();
            manualResetEvent.Dispose();
        }
        //异步调用
        public void BeginInvoke(Action<object> action, object state) {
            var queues = ExeArg.QueueDics[_createThread.ManagedThreadId];
            queues.Add(new ExeArg() {
                Action = action,
                State = state
            });
        }
    }

    class MySynchronizationContextTest {
        public static void Main(string[] arg) {
           
            Console.WriteLine($"主线程: {Thread.CurrentThread.ManagedThreadId}");
            //主线程创建控件
            var ctrl = new MyControl();
            var syncContext = SynchronizationContext.Current;
            //模拟一个用户操作
            Task.Run(() => {
                Thread.Sleep(2000);
                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Post前");
                syncContext.Post((state) => {
                    Console.WriteLine($"Post内的方法执行线程: {Thread.CurrentThread.ManagedThreadId},参数:{state}");
                }, new { name = "小明" });
                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Post后,Send前");
                syncContext.Send((state) => {
                    Thread.Sleep(3000);
                    Console.WriteLine($"Send内的方法执行线程: {Thread.CurrentThread.ManagedThreadId},参数:{state}");
                }, new { name = "小红" });
                Console.WriteLine($"用户线程: {Thread.CurrentThread.ManagedThreadId},Send后");
            });
            //主线程开启消息垒
            while (true) {
                var exeArg = ExeArg.CurrentQueue.Take();
                exeArg.Action?.Invoke(exeArg.State);
            }
        }
    }
}
