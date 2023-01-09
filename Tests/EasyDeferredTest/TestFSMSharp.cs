using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using EasyDeferred.FSMSharp;
namespace EasyDeferredTest
{


    class TestFSMSharp1
    {
        // Define an enum to define the states. Anything could work, a string, int.. but enums are likely the easiest to manage
        enum Season
        {
            Winter,
            Spring,
            Summer,
            Fall
        }

        // Create the FSM
        FSM<Season> fsm = new FSM<Season>("seasons-fsm");

        void Init() {
            // Initialize the states, adding them to the FSM and configuring their behaviour
            FSM<int> fsm2 = new FSM<int>("testInt");
            fsm2.Add(1).Expires(3f).GoesTo(2);
            fsm2.Add(2).Expires(3f).GoesTo(3);;
            fsm2.Add(3).Expires(3f).GoesTo(1); ;

            fsm.Add(Season.Winter)
                .Expires(3f)
                .GoesTo(Season.Spring)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.White)
                .OnLeave((Z) => Console.WriteLine("Winter is ending..."))
                .Calls(d => Console.WriteLine("Winter is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Spring)
                .Expires(3f)
                .GoesTo(Season.Summer)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.Green)
                .OnLeave((Z) => Console.WriteLine("Spring is ending..."))
                .Calls(d => Console.WriteLine("Spring is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Summer)
                .Expires(3f)
                .GoesTo(Season.Fall)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.Red)
                .OnLeave((Z) => Console.WriteLine("Summer is ending..."))
                .Calls(d => Console.WriteLine("Summer is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Fall)
                .Expires(3f)
                .GoesTo(Season.Winter)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.DarkYellow)
                .OnLeave((Z) => Console.WriteLine("Fall is ending..."))
                .Calls(d => Console.WriteLine("Fall is going on.. {0}%", d.StateProgress * 100f));

            // Very important! set the starting state
            fsm.CurrentState = Season.Winter;
        }

        public void Run() {
            // Define a base time. This seems pedantic in a pure .NET world, but allows to use custom time providers,
            // Unity3D Time class (scaled or unscaled), MonoGame timing, etc.
            DateTime baseTime = DateTime.Now;

            // Initialize the FSM
            Init();

            // Call the FSM periodically... in a real world scenario this will likely be in a timer callback, or frame handling (e.g.
            // Unity's Update() method).
            while (true) {
                // 
                fsm.Process((float)(DateTime.Now - baseTime).TotalSeconds);
                System.Threading.Thread.Sleep(100);
            }
        }

        internal static void Test() {
            TestFSMSharp1 test1 = new TestFSMSharp1();           
            test1.Run();
        }
    }
    class TestFSMSharp2
    {
        public enum StepType
        {
            One,
            Two,
            Three,
            Four
        }
        public abstract class StateBase
        {
            public abstract void DoEnter();
            public abstract void DoLeave();
            public abstract void DoUpdate();
            public virtual StepType Step {
                get;
            }
        }
        class State1 : StateBase
        {
            public override void DoEnter() {
                Console.WriteLine("1---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("1---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("1---update");
            }
            public override StepType Step {
                get {
                    return StepType.One;
                }
            }
        }
        class State2 : StateBase
        {
            public override void DoEnter() {
                Console.WriteLine("2---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("2---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("2---update");
            }
            public override StepType Step {
                get {
                    return StepType.Two;
                }
            }
        }
        class State3 : StateBase
        {
            public override void DoEnter() {
                Console.WriteLine("3---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("3---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("3---update");
            }
            public override StepType Step {
                get {
                    return StepType.Three;
                }
            }
        }
        class State4 : StateBase
        {
            public override void DoEnter() {
                Console.WriteLine("4---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("4---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("4---update");
            }
            public override StepType Step {
                get {
                    return StepType.Four;
                }
            }
        }
        FSM<StateBase> fsm = new FSM<StateBase>("seasons-fsm",false);
        void Init() {
            // Initialize the states, adding them to the FSM and configuring their behaviour
            State1 s1 = new State1();
            State2 s2 = new State2();
            State3 s3 = new State3();
            State4 s4 = new State4();
            fsm.Add(s1)
                .Expires(3f,true)
                .GoesTo(s2)
                .OnEnter((Z) => {
                    Console.ForegroundColor = ConsoleColor.White;
                    Z.DoEnter();
                    System.Diagnostics.Debug.Assert(Z == s1, "传入对象不一致");
                })
                .OnLeave((Z) => {
                    Console.WriteLine("Winter is ending...");
                    Z.DoLeave();
                    System.Diagnostics.Debug.Assert(Z == s1, "传入对象不一致");
                })
                .Calls(d => {
                    Console.WriteLine("Winter is going on.. {0}%", d.StateProgress * 100f);
                    d.State.DoUpdate();
                    var v = d.Behaviour.CustomizeProgress;
                    d.Behaviour.CustomizeProgress = Math.Min(0.9f, v + 0.1f);
                    System.Diagnostics.Debug.Assert(d.State == s1, "传入对象不一致");
                    if (d.StateTime > 5f) {
                        d.Behaviour.CustomizeProgress = 1f;
                       
                    }
                });

            fsm.Add(s2)
                .Expires(3f)
                .GoesTo(s3)
                .OnEnter((Z) => {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Z.DoEnter();
                    System.Diagnostics.Debug.Assert(Z == s2, "传入对象不一致");
                })
                .OnLeave((Z) => {
                    Console.WriteLine("Spring is ending...");
                    Z.DoLeave();
                    System.Diagnostics.Debug.Assert(Z == s2, "传入对象不一致");
                })
                .Calls(d => {
                    Console.WriteLine("Spring is going on.. {0}%", d.StateProgress * 100f);
                    System.Diagnostics.Debug.Assert(d.State == s2, "传入对象不一致");
                });

            fsm.Add(s3)
                .Expires(3f)
                .GoesTo(s4)
                .OnEnter((Z) => {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Z.DoEnter();
                    System.Diagnostics.Debug.Assert(Z == s3, "传入对象不一致");
                })
                .OnLeave((Z) => {
                    Console.WriteLine("Summer is ending...");
                    Z.DoLeave();
                    System.Diagnostics.Debug.Assert(Z == s3, "传入对象不一致");
                })
                .Calls(d => {
                    Console.WriteLine("Summer is going on.. {0}%", d.StateProgress * 100f);
                    System.Diagnostics.Debug.Assert(d.State == s3, "传入对象不一致");
                });

            fsm.Add(s4)
                .Expires(3f)
                .GoesTo(s1)
                .OnEnter((Z) => {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Z.DoEnter();
                    System.Diagnostics.Debug.Assert(Z == s4, "传入对象不一致");
                })
                .OnLeave((Z) => {
                    Console.WriteLine("Fall is ending...");
                    Z.DoLeave();
                    System.Diagnostics.Debug.Assert(Z == s4, "传入对象不一致");
                })
                .Calls(d => {
                    Console.WriteLine("Fall is going on.. {0}%", d.StateProgress * 100f);
                    System.Diagnostics.Debug.Assert(d.State == s4, "传入对象不一致");
                })
                .SetEvent("触发事件s4",(arg)=> {
                    Console.WriteLine("...触发事件s4");
                });

            // Very important! set the starting state
            fsm.CurrentState = s1;
            //
            Thread t = new Thread(new ThreadStart(() => {
                while (true) {
                    var cr = Console.ReadKey(true);
                    if (cr != null && cr.Key == ConsoleKey.T) {
                        fsm.TriggerEvent("触发事件s4");
                    }
                }
            }));           
            t.Priority = ThreadPriority.Normal;
            t.IsBackground = true;
            t.Start();
        }

        public void Run() {
            // Define a base time. This seems pedantic in a pure .NET world, but allows to use custom time providers,
            // Unity3D Time class (scaled or unscaled), MonoGame timing, etc.
            DateTime baseTime = DateTime.Now;

            // Initialize the FSM
            Init();

            // Call the FSM periodically... in a real world scenario this will likely be in a timer callback, or frame handling (e.g.
            // Unity's Update() method).
            while (true) {
                // 
                fsm.Process((float)(DateTime.Now - baseTime).TotalSeconds);
                //or
                //fsm.ProcessIncremental(1f);
                System.Threading.Thread.Sleep(1000);
            }
        }

        internal static void Test() {
            TestFSMSharp2 test2 = new TestFSMSharp2();
           
            test2.Run();
        }
    }
}
