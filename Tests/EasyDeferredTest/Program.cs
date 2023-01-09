using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using EasyDeferred;

namespace EasyDeferredTest
{
    class Program
    {
        static void Main(string[] args) {
            Console.WriteLine("console thread id=" + Thread.CurrentThread.ManagedThreadId);
            //TestAsyn.doTest();

            //TestSync.doTest();

            //TestFlow.testFlow1();

            Console.ReadLine();
            //TestStateMachineBuilder.Test2();
            //TestFSMSharp1.Test();
            TestFSMSharp2.Test();
        }


      

      

    }
}
