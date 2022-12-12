using System;
using System.Collections.Generic;
using System.Text;

namespace EasyDeferredTest
{

    class TestFlow
    {
        public static void testFlow1() {
            EasyDeferred.RSG.Func<int, int> func1 = (Z) => {
                System.Threading.Thread.Sleep(100);
                Console.WriteLine("thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                return Z * Z;
            };
            var ret2 = EasyDeferred.EDefer.GetResultAsyn<int, int>(func1, 2, false);
            ret2.Promise.Then(() => {
                var value = (int)ret2.ResolveValue;
                Console.WriteLine(value);
                ret2 = EasyDeferred.EDefer.GetResultAsyn<int, int>(func1, value, false);
                return ret2.Promise;
            }).Then(() => {
                var value = (int)ret2.ResolveValue;
                Console.WriteLine(value);
                ret2 = EasyDeferred.EDefer.GetResultAsyn<int, int>(func1, value, false);
                return ret2.Promise;
            }).Then(() => {
                var value = (int)ret2.ResolveValue;
                Console.WriteLine(value);
            });
            Console.ReadLine();
        }
        public static void testFlow2() {


        }
    }
}
