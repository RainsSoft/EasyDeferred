using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Nito.Async;
// The System.Timers.Timer class raises its Elapsed event on a ThreadPool thread
//  (if SynchronizingObject is null, which is true for this example code).
//using Timer = System.Timers.Timer;
namespace NitoAsyncTest
{
    class Program
    {
        static void Main(string[] args) {
           // Console.ReadLine();
           //Main2();
      
            //
            Nito.Async.ActionThread at = new Nito.Async.ActionThread();
            at.Start();
            at.DoSynchronously(() => {
                System.Threading.Thread.Sleep(1000);
                Console.WriteLine("thread id2:"+System.Threading.Thread.CurrentThread.ManagedThreadId);
            });
            using (ActionDispatcher actionDispatcher = new ActionDispatcher()) {
                actionDispatcher.QueueExit();
                actionDispatcher.Run();

                // Once Run returns, it is safe to Dispose the ActionDispatcher
            }
            using (ActionDispatcher actionDispatcher = new ActionDispatcher()) {
                // At this point in the code, ActionDispatcher.Current is null
                // However, inside an action queued to actionDispatcher, ActionDispatcher.Current
                //  refers to actionDispatcher.
                actionDispatcher.QueueAction(() =>
                ActionDispatcher.Current.QueueExit()
                );
                actionDispatcher.Run();

                // Once Run returns, it is safe to Dispose the ActionDispatcher
            }
            using (Nito.Async.ActionDispatcher ad = new Nito.Async.ActionDispatcher()) {
                ad.QueueAction(FirstAction);
                ad.Run();
            }
            Console.WriteLine("thread id1:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        #region
        // This is the first action done by the main thread when it runs the ActionDispatcher
        static void FirstAction() {
            Console.WriteLine("ActionDispatcher thread ID is " + Thread.CurrentThread.ManagedThreadId +
                " and is " + (Thread.CurrentThread.IsThreadPoolThread ? "" : "not ") + "a threadpool thread");

            // Start a BGW
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += BackgroundWorkerWork;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            backgroundWorker.RunWorkerAsync();
        }

        // This is the BackgroundWorker's work that it has to do
        static void BackgroundWorkerWork(object sender, DoWorkEventArgs e) {
            Console.WriteLine("1BackgroundWorker thread ID is " + Thread.CurrentThread.ManagedThreadId +
                " and is " + (Thread.CurrentThread.IsThreadPoolThread ? "" : "not ") + "a threadpool thread");

            // Sleep is very important work; don't let anyone tell you otherwise
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        // This is an event raised by the BGW. Since the BGW is owned by the main thread,
        //  this event is raised on the main thread.
        static void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            for (int i = 0; i < 10; i++) {
                System.Threading.Thread.Sleep(1);
                Console.WriteLine("2BGW event thread ID is " + Thread.CurrentThread.ManagedThreadId +
                    " and is " + (Thread.CurrentThread.IsThreadPoolThread ? "" : "not ") + "a threadpool thread");
            }
            // When the BGW is done, signal our ActionThread to exit
            ActionDispatcher.Current.QueueExit();
        }
        #endregion

        #region
        static void Main2() {
            Console.WriteLine("In main thread (thread ID " +
                Thread.CurrentThread.ManagedThreadId + ")");
            ActionDispatcher actionDispatcher;
            // By using an ActionDispatcher, we can give a Console application thread
            //  (or any other thread) an event-driven main loop.
            using (actionDispatcher = new ActionDispatcher()) {
                Action ta = () => {
                    Nito.Async.Timer timer = new Nito.Async.Timer();
                    //using (Nito.Async.Timer timer = new Nito.Async.Timer()) {
                        int elapsedCount = 0;
                        timer.AutoReset = false;
                        timer.Interval = TimeSpan.FromMilliseconds(100);
                        timer.Elapsed += () => {
                            // (This method executes in a ThreadPool thread)
                            Console.WriteLine("Elapsed running in thread pool thread (thread ID " +
                                Thread.CurrentThread.ManagedThreadId + ")");

                            //Timer timer = (Timer)sender;
                            if (elapsedCount < 100) {
                                // The first time the timer goes off, send a message to the main thread
                                elapsedCount++;//= 1;
                                actionDispatcher.QueueAction(
                                    () => Console.WriteLine("Hello from main thread (thread ID " +
                                        Thread.CurrentThread.ManagedThreadId + ")"));
                                //timer.Start();
                            }
                            else {
                                // The second time the timer goes off, tell the main thread to exit
                                actionDispatcher.QueueExit();
                            }
                        };
                        timer.Restart();
                    //}
                };
            actionDispatcher.QueueAction(ta);
            actionDispatcher.Run();
        }
            //
            Console.ReadLine();
        }
        #endregion

    }
}
