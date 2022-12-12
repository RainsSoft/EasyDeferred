using System;
using System.Collections.Generic;
using System.Text;
using EasyDeferred;
namespace EasyDeferredTest
{
    class TestSync
    {
        public static void doTest() {
            EasyDeferred.EDefer.Setup();
            EasyDeferred.EDefer.SetSyncContext(EasyDeferredSyncContextInvoke.EasySyncContextInvoke);
            testGetFuncResult_1(true);
            System.Threading.Thread.Sleep(1000);
            testGetFuncResult_1(false);
            System.Threading.Thread.Sleep(1000);
            //Console.ReadLine();
            testRunAction_1(true);
            System.Threading.Thread.Sleep(1000);
            testRunAction_1(false);
            EasyDeferred.EDefer.DoEventsSync(() => { System.Threading.Thread.Sleep(1); }, true);
        }

        static void testGetFuncResult_1(bool waitThreadFinally) {
            EasyDeferred.RSG.Func<int, int> func1 = (Z) => {
                System.Threading.Thread.Sleep(100);
                Console.WriteLine("thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                return Z * Z;
            };
            if (waitThreadFinally) {
                var ret = EasyDeferred.EDefer.GetResultAsyn<int, int>(func1, 3, true);
                //print 4
                Console.WriteLine(ret.ResolveValue);
                ret.Promise.Then(() => {
                    //print 4
                    Console.WriteLine("finally thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine(ret.ResolveValue == null ? "null" : ret.ResolveValue);
                });
            }
            else {
                var ret2 = EasyDeferred.EDefer.GetResultAsyn<int, int>(func1, 4, false);
                //print null
                Console.WriteLine(ret2.ResolveValue == null ? "null" : ret2.ResolveValue);
                ret2.Promise.Then(() => {
                    //print 16
                    Console.WriteLine("finally thread id2:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    Console.WriteLine(ret2.ResolveValue == null ? "null" : ret2.ResolveValue);
                });
            }
        }
        static void testRunAction_1(bool waitThreadFinally) {
            if (waitThreadFinally) {
                EasyDeferred.EDefer.RunAsyn<string>((Z) => {
                    Console.WriteLine("thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    System.Threading.Thread.Sleep(100);
                    Console.WriteLine(Z);
                }, "test", true).Promise.Then(() => {
                    Console.WriteLine("then1 thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                });
            }
            else {
                EasyDeferred.EDefer.RunAsyn(() => {
                    Console.WriteLine("thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    System.Threading.Thread.Sleep(100);
                    for (int i = 0; i < 10; i++) {
                        Console.WriteLine("i=" + i);
                    }
                }).Then(() => {
                    Console.WriteLine("then2 thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                });
            }
        }
    }
}
