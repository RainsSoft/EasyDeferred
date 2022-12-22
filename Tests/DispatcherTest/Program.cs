using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EasyDeferred.Synchronize;
using EasyDeferred.Threading;
namespace DispatcherTest
{
    class Program
    {
        static void Main(string[] args) {
            //Main2(args);
            Console.WriteLine("thread id:" + Thread.CurrentThread.ManagedThreadId);         
            var dp = Dispatcher.CurrentDispatcher;
            if (dp != null) {
                Console.WriteLine(dp.ToString());
                dp.Invoke(TimeSpan.FromMilliseconds(10000), new DispatcherOperationCallback((Z) => {
                    Console.WriteLine( "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    return 0;
                }), null);
            }
            System.Timers.Timer tm = new  System.Timers.Timer(1000);
            tm.Elapsed += (s, e) => {
                Console.WriteLine("000 thread id:" + Thread.CurrentThread.ManagedThreadId);
                dp.Invoke(TimeSpan.FromMilliseconds(10000), new DispatcherOperationCallback((Z) => {
                    Console.WriteLine("111" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    return 0;
                }), null);
                dp.BeginInvoke(new DispatcherOperationCallback((Z) => {
                    Console.WriteLine("222" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    return 0;
                }), null);
            };
            tm.Start();
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(1000);
            dt.Tick += (s, e) => {
                Console.WriteLine("222 thread id: " + Thread.CurrentThread.ManagedThreadId);
                dp.Invoke(TimeSpan.FromMilliseconds(10000), new DispatcherOperationCallback((Z) => {
                    Console.WriteLine("333" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    return 0;
                }), null);
                dp.BeginInvoke(new DispatcherOperationCallback((Z) => {
                    Console.WriteLine("444" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    return 0;
                }), null);

            };
            dt.Start();
            Dispatcher.Run();
            Console.ReadLine();
        }

        static void Main2(string[] args) {
            Console.WriteLine("thread id:" + Thread.CurrentThread.ManagedThreadId);
            StaSynchronizationContext sc = new StaSynchronizationContext();
            //SynchronizationContext.SetSynchronizationContext(sc);
            System.Timers.Timer tm = new System.Timers.Timer(1000);
            tm.Elapsed += (s, e) => {
                Console.WriteLine("thread id:" + Thread.CurrentThread.ManagedThreadId);
                sc.Post(new SendOrPostCallback((Z) => {
                    Console.WriteLine("000" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    //return 0;
                }), null);
                sc.Send(new SendOrPostCallback((Z) => {
                    Console.WriteLine("111" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    //return 0;
                }), null);
            };
            tm.Start();
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(1000);
            dt.Tick += (s, e) => {
                Console.WriteLine("222 thread id: " + Thread.CurrentThread.ManagedThreadId);
                sc.Post(new  SendOrPostCallback((Z) => {
                    Console.WriteLine("333" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    //return 0;
                }), null);
                sc.Send(new SendOrPostCallback((Z) => {
                    Console.WriteLine("444" + "=>thread id: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    //return 0;
                }), null);

            };
            dt.Start();
            Dispatcher.Run();
            Console.ReadLine();
        }
    }
}
