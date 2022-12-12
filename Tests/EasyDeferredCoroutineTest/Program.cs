using System;
using System.Collections;
using System.Threading;
using EasyDeferred.Coroutine;
using EasyDeferred.RSG;
using EasyDeferred.Synchronize;
using EasyDeferred.Threading;

namespace EasyDeferredCoroutineTest
{
    class Program
    {
        static void Main(string[] args) {
            TestEllpeckCoroutineRunner.Test();
            Console.ReadLine();
            int a, b;
            a = int.Parse(Console.ReadLine());
            b = int.Parse(Console.ReadLine());
            Console.WriteLine((a + b).ToString());
            //test1
            //TestCoroutineRunner tr = new TestCoroutineRunner();
            //tr.test();
            //test2

            //ConsoleSynchronizationContext sc = new ConsoleSynchronizationContext();
            //SynchronizationContext.SetSynchronizationContext(sc);
            Console.WriteLine("in main -" + SynchronizationContext.Current?.GetType() + " id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
            CoroutineMgr.Instance.IntervalMilliseconds = 100;
            CoroutineMgr.Instance.Initialize();
            CoroutineMgr.Instance.StartCoroutine("testAll", TestCoroutineRunner.Movement());
         
            int i = 0;
            Action ac = () => {
                Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + " -beginInvoke- " + i++);
            };
            var ir = CoroutineMgr.Instance.syncInvoke.BeginInvoke(ac, null);
            CoroutineMgr.Instance.syncInvoke.EndInvoke(ir);
            Console.ReadLine();
            //Dispatcher.Run();
            while (true) {
                //sc.DoActions();
                CoroutineMgr.Instance.DoEvents(() => {
                    if (CoroutineMgr.Instance.GetCoroutineHandle("testAll").IsRunning) {
                        //Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId + " -- " + i++);
                        TestCoroutineRunner.DrawMap();
                    }
                });

                System.Threading.Thread.Sleep(1000);


            }

        }

        private static void Dt_Tick(object sender, EventArgs e) {
            throw new NotImplementedException();
        }


        //public class MySynchronizationContext : SynchronizationContext
        //{
        //    public override void Post(SendOrPostCallback d, object state) {
        //        SendOrPostCallback callbackAndRestoreContext = (obj) => {
        //            SynchronizationContext.SetSynchronizationContext(this);

        //            d?.Invoke(obj);
        //        };

        //        base.Post(callbackAndRestoreContext, state);
        //    }
        //}
        //public sealed class NaiveSingleThreadSynchronizationContext : SynchronizationContext
        //{
        //    private readonly ConcurrentQueue<Action> _queue
        //        = new ConcurrentQueue<Action>();
        //    //private readonly Task _eventLoop;

        //    //public NaiveSingleThreadSynchronizationContext() {
        //    //    _eventLoop = Task.Run(() =>
        //    //    {
        //    //        while (true) {
        //    //            if (_queue.TryDequeue(out var action))
        //    //                action();

        //    //            Thread.Sleep(10);
        //    //        }
        //    //    });
        //    //}

        //    public override void Post(SendOrPostCallback d, object state) {
        //        Action action = () => {
        //            SynchronizationContext.SetSynchronizationContext(this);

        //            d?.Invoke(state);
        //        };

        //        _queue.Enqueue(action);
        //    }

        //    public void ExecuteAction() {
        //        //while (true) {
        //        if (_queue.TryDequeue(out var action)) {
        //            action();
        //        }
        //        //Thread.Sleep(10);
        //        //}
        //    }

        //}
    }
    
}
