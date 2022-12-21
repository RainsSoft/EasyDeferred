using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using EasyDeferred.RSG;
using EasyDeferred.RSG.Linq;
namespace EasyDeferred.Coroutine.Ellpeck {
    /// <summary>
    /// An event is any kind of action that a <see cref="Wait"/> can listen for.
    /// Note that, by default, events don't have a custom <see cref="object.Equals(object)"/> implementation. 
    /// </summary>
    public class Event {

    }

    /// <summary>
    /// Represents either an amount of time, or an <see cref="Coroutine.Event"/> that is being waited for by an <see cref="ActiveCoroutine"/>.
    /// </summary>
    public struct Wait {

        internal readonly Event Event;
        private double seconds;

        /// <summary>
        /// Creates a new wait that waits for the given <see cref="Coroutine.Event"/>.
        /// </summary>
        /// <param name="evt">The event to wait for</param>
        public Wait(Event evt) {
            this.Event = evt;
            this.seconds = 0;
        }

        /// <summary>
        /// Creates a new wait that waits for the given amount of seconds.
        /// </summary>
        /// <param name="seconds">The amount of seconds to wait for</param>
        public Wait(double seconds) {
            this.seconds = seconds;
            this.Event = null;
        }

        /// <summary>
        /// Creates a new wait that waits for the given <see cref="TimeSpan"/>.
        /// Note that the exact value may be slightly different, since waits operate in <see cref="TimeSpan.TotalSeconds"/> rather than ticks.
        /// </summary>
        /// <param name="time">The time span to wait for</param>
        public Wait(TimeSpan time) : this(time.TotalSeconds) {
        }

        internal bool Tick(double deltaSeconds) {
            this.seconds -= deltaSeconds;
            return this.seconds <= 0;
        }

    }
    /// <summary>
    /// A reference to a currently running coroutine.
    /// This is returned by <see cref="CoroutineHandler.Start(System.Collections.Generic.IEnumerator{Coroutine.Wait},string,int)"/>.
    /// </summary>
    public class ActiveCoroutine : IComparable<ActiveCoroutine> {

        private readonly IEnumerator<Wait> enumerator;
        private readonly Stopwatch stopwatch;
        private Wait current;

        internal Event Event => this.current.Event;
        internal bool IsWaitingForEvent => this.Event != null;

        /// <summary>
        /// This property stores whether or not this active coroutine is finished.
        /// A coroutine is finished if all of its waits have passed, or if it <see cref="WasCanceled"/>.
        /// </summary>
        public bool IsFinished {
            get; private set;
        }
        /// <summary>
        /// This property stores whether or not this active coroutine was cancelled using <see cref="Cancel"/>.
        /// </summary>
        public bool WasCanceled {
            get; private set;
        }
        /// <summary>
        /// The total amount of time that <see cref="MoveNext"/> took.
        /// This is the amount of time that this active coroutine took for the entirety of its "steps", or yield statements.
        /// </summary>
        public TimeSpan TotalMoveNextTime {
            get; private set;
        }
        /// <summary>
        /// The total amount of times that <see cref="MoveNext"/> was invoked.
        /// This is the amount of "steps" in your coroutine, or the amount of yield statements.
        /// </summary>
        public int MoveNextCount {
            get; private set;
        }
        /// <summary>
        /// The amount of time that the last <see cref="MoveNext"/> took.
        /// This is the amount of time that this active coroutine took for the last "step", or yield statement.
        /// </summary>
        public TimeSpan LastMoveNextTime {
            get; private set;
        }

        /// <summary>
        /// An event that gets fired when this active coroutine finishes or gets cancelled.
        /// When this event is called, <see cref="IsFinished"/> is always true.
        /// </summary>
        public event FinishCallback OnFinished;
        /// <summary>
        /// The name of this coroutine.
        /// When not specified on startup of this coroutine, the name defaults to an empty string.
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The priority of this coroutine. The higher the priority, the earlier it is advanced compared to other coroutines that advance around the same time.
        /// When not specified at startup of this coroutine, the priority defaults to 0.
        /// </summary>
        public readonly int Priority;

        internal ActiveCoroutine(IEnumerator<Wait> enumerator, string name, int priority, Stopwatch stopwatch) {
            this.enumerator = enumerator;
            this.Name = name;
            this.Priority = priority;
            this.stopwatch = stopwatch;
        }

        /// <summary>
        /// Cancels this coroutine, causing all subsequent <see cref="Wait"/>s and any code in between to be skipped.
        /// </summary>
        /// <returns>Whether the cancellation was successful, or this coroutine was already cancelled or finished</returns>
        public bool Cancel() {
            if(this.IsFinished || this.WasCanceled)
                return false;
            this.WasCanceled = true;
            this.IsFinished = true;
            this.OnFinished?.Invoke(this);
            return true;
        }

        internal bool Tick(double deltaSeconds) {
            if(!this.WasCanceled && this.current.Tick(deltaSeconds))
                this.MoveNext();
            return this.IsFinished;
        }

        internal bool OnEvent(Event evt) {
            if(!this.WasCanceled && Equals(this.current.Event, evt))
                this.MoveNext();
            return this.IsFinished;
        }

        internal bool MoveNext() {
            //this.stopwatch.Restart();
            this.stopwatch.Reset();
            this.stopwatch.Start();
            var result = this.enumerator.MoveNext();
            this.stopwatch.Stop();
            this.LastMoveNextTime = this.stopwatch.Elapsed;
            this.TotalMoveNextTime += this.stopwatch.Elapsed;
            this.MoveNextCount++;

            if(!result) {
                this.IsFinished = true;
                this.OnFinished?.Invoke(this);
                return false;
            }
            this.current = this.enumerator.Current;
            return true;
        }

        /// <summary>
        /// A delegate method used by <see cref="ActiveCoroutine.OnFinished"/>.
        /// </summary>
        /// <param name="coroutine">The coroutine that finished</param>
        public delegate void FinishCallback(ActiveCoroutine coroutine);

        /// <inheritdoc />
        public int CompareTo(ActiveCoroutine other) {
            return other.Priority.CompareTo(this.Priority);
        }

    }

    /// <summary>
    /// This class can be used for static coroutine handling of any kind.
    /// Note that it uses an underlying <see cref="CoroutineHandlerInstance"/> object for management.
    /// </summary>
    public static class CoroutineHandler {

        private static readonly CoroutineHandlerInstance Instance = new CoroutineHandlerInstance();

        /// <inheritdoc cref="CoroutineHandlerInstance.TickingCount"/>
        public static int TickingCount => Instance.TickingCount;
        /// <inheritdoc cref="CoroutineHandlerInstance.EventCount"/>
        public static int EventCount => Instance.EventCount;

        /// <inheritdoc cref="CoroutineHandlerInstance.Start(IEnumerable{Wait},string,int)"/>
        public static ActiveCoroutine Start(IEnumerable<Wait> coroutine, string name = "", int priority = 0) {
            return Instance.Start(coroutine, name, priority);
        }

        /// <inheritdoc cref="CoroutineHandlerInstance.Start(IEnumerator{Wait},string,int)"/>
        public static ActiveCoroutine Start(IEnumerator<Wait> coroutine, string name = "", int priority = 0) {
            return Instance.Start(coroutine, name, priority);
        }

        /// <inheritdoc cref="CoroutineHandlerInstance.InvokeLater"/>
        public static ActiveCoroutine InvokeLater(Wait wait, Action action, string name = "", int priority = 0) {
            return Instance.InvokeLater(wait, action, name, priority);
        }

        /// <inheritdoc cref="CoroutineHandlerInstance.Tick(double)"/>
        public static void Tick(double deltaSeconds) {
            Instance.Tick(deltaSeconds);
        }
        /// <inheritdoc cref="CoroutineHandlerInstance.Tick(TimeSpan)"/>
        public static void Tick(TimeSpan delta) {
            Instance.Tick(delta);
        }

        /// <inheritdoc cref="CoroutineHandlerInstance.RaiseEvent"/>
        public static void RaiseEvent(Event evt) {
            Instance.RaiseEvent(evt);
        }

        /// <inheritdoc cref="CoroutineHandlerInstance.GetActiveCoroutines"/>
        public static IEnumerable<ActiveCoroutine> GetActiveCoroutines() {
            return Instance.GetActiveCoroutines();
        }

    }
    /// <summary>
    /// An object of this class can be used to start, tick and otherwise manage active <see cref="ActiveCoroutine"/>s as well as their <see cref="Event"/>s.
    /// Note that a static implementation of this can be found in <see cref="CoroutineHandler"/>.
    /// https://github.com/Ellpeck/Coroutine
    /// </summary>
    public class CoroutineHandlerInstance {
        struct KeyValuePair2<TKey, TValue> {
            public KeyValuePair2(TKey key, TValue value) {
                Item1 = key;
                Item2 = value;
            }
            public TKey Item1 {
                get;
            }
            public TValue Item2 {
                get;
            }

        }
        private readonly List<ActiveCoroutine> tickingCoroutines = new List<ActiveCoroutine>();
        private readonly Dictionary<Event, List<ActiveCoroutine>> eventCoroutines = new Dictionary<Event, List<ActiveCoroutine>>();
        private readonly HashSet<KeyValuePair2<Event, ActiveCoroutine>> eventCoroutinesToRemove = new HashSet<KeyValuePair2<Event, ActiveCoroutine>>(); //new HashSet<(Event, ActiveCoroutine)>();
        private readonly HashSet<ActiveCoroutine> outstandingEventCoroutines = new HashSet<ActiveCoroutine>();
        private readonly HashSet<ActiveCoroutine> outstandingTickingCoroutines = new HashSet<ActiveCoroutine>();
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly object lockObject = new object();

        /// <summary>
        /// The amount of <see cref="ActiveCoroutine"/> instances that are currently waiting for a tick (waiting for time to pass)
        /// </summary>
        public int TickingCount {
            get {
                lock(this.lockObject)
                    return this.tickingCoroutines.Count;
            }
        }
        /// <summary>
        /// The amount of <see cref="ActiveCoroutine"/> instances that are currently waiting for an <see cref="Event"/>
        /// </summary>
        public int EventCount {
            get {
                lock(this.lockObject) {
                    //return this.eventCoroutines.Sum(c => c.Value.Count);
                    return this.eventCoroutines.Select(c => c.Value.Count).Sum();
                }
            }
        }

        /// <summary>
        /// Starts the given coroutine, returning a <see cref="ActiveCoroutine"/> object for management.
        /// Note that this calls <see cref="IEnumerable{T}.GetEnumerator"/> to get the enumerator.
        /// </summary>
        /// <param name="coroutine">The coroutine to start</param>
        /// <param name="name">The <see cref="ActiveCoroutine.Name"/> that this coroutine should have. Defaults to an empty string.</param>
        /// <param name="priority">The <see cref="ActiveCoroutine.Priority"/> that this coroutine should have. The higher the priority, the earlier it is advanced. Defaults to 0.</param>
        /// <returns>An active coroutine object representing this coroutine</returns>
        public ActiveCoroutine Start(IEnumerable<Wait> coroutine, string name = "", int priority = 0) {
            return this.Start(coroutine.GetEnumerator(), name, priority);
        }

        /// <summary>
        /// Starts the given coroutine, returning a <see cref="ActiveCoroutine"/> object for management.
        /// </summary>
        /// <param name="coroutine">The coroutine to start</param>
        /// <param name="name">The <see cref="ActiveCoroutine.Name"/> that this coroutine should have. Defaults to an empty string.</param>
        /// <param name="priority">The <see cref="ActiveCoroutine.Priority"/> that this coroutine should have. The higher the priority, the earlier it is advanced compared to other coroutines that advance around the same time. Defaults to 0.</param>
        /// <returns>An active coroutine object representing this coroutine</returns>
        public ActiveCoroutine Start(IEnumerator<Wait> coroutine, string name = "", int priority = 0) {
            var inst = new ActiveCoroutine(coroutine, name, priority, this.stopwatch);
            if(inst.MoveNext()) {
                lock(this.lockObject)
                    this.GetOutstandingCoroutines(inst.IsWaitingForEvent).Add(inst);
            }
            return inst;
        }

        /// <summary>
        /// Causes the given action to be invoked after the given <see cref="Wait"/>.
        /// This is equivalent to a coroutine that waits for the given wait and then executes the given <see cref="Action"/>.
        /// </summary>
        /// <param name="wait">The wait to wait for</param>
        /// <param name="action">The action to execute after waiting</param>
        /// <param name="name">The <see cref="ActiveCoroutine.Name"/> that the underlying coroutine should have. Defaults to an empty string.</param>
        /// <param name="priority">The <see cref="ActiveCoroutine.Priority"/> that the underlying coroutine should have. The higher the priority, the earlier it is advanced compared to other coroutines that advance around the same time. Defaults to 0.</param>
        /// <returns>An active coroutine object representing this coroutine</returns>
        public ActiveCoroutine InvokeLater(Wait wait, Action action, string name = "", int priority = 0) {
            return this.Start(InvokeLaterImpl(wait, action), name, priority);
        }

        /// <summary>
        /// Ticks this coroutine handler, causing all time-based <see cref="Wait"/>s to be ticked.
        /// </summary>
        /// <param name="deltaSeconds">The amount of seconds that have passed since the last time this method was invoked</param>
        public void Tick(double deltaSeconds) {
            lock(this.lockObject) {
                this.MoveOutstandingCoroutines(false);
                this.tickingCoroutines.RemoveAll(c => {
                    if(c.Tick(deltaSeconds)) {
                        return true;
                    }
                    else if(c.IsWaitingForEvent) {
                        this.outstandingEventCoroutines.Add(c);
                        return true;
                    }
                    return false;
                });
            }
        }

        /// <summary>
        /// Ticks this coroutine handler, causing all time-based <see cref="Wait"/>s to be ticked.
        /// This is a convenience method that calls <see cref="Tick(double)"/>, but accepts a <see cref="TimeSpan"/> instead of an amount of seconds.
        /// </summary>
        /// <param name="delta">The time that has passed since the last time this method was invoked</param>
        public void Tick(TimeSpan delta) {
            this.Tick(delta.TotalSeconds);
        }

        /// <summary>
        /// Raises the given event, causing all event-based <see cref="Wait"/>s to be updated.
        /// </summary>
        /// <param name="evt">The event to raise</param>
        public void RaiseEvent(Event evt) {
            lock(this.lockObject) {
                this.MoveOutstandingCoroutines(true);
                var coroutines = this.GetEventCoroutines(evt, false);
                if(coroutines != null) {
                    for(var i = 0; i < coroutines.Count; i++) {
                        var c = coroutines[i];
                        KeyValuePair2<Event, ActiveCoroutine> tup = new KeyValuePair2<Event, ActiveCoroutine>(c.Event, c);
                        if(this.eventCoroutinesToRemove.Contains(tup))
                            continue;
                        if(c.OnEvent(evt)) {
                            this.eventCoroutinesToRemove.Add(tup);
                        }
                        else if(!c.IsWaitingForEvent) {
                            this.eventCoroutinesToRemove.Add(tup);
                            this.outstandingTickingCoroutines.Add(c);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a list of all currently active <see cref="ActiveCoroutine"/> objects under this handler.
        /// </summary>
        /// <returns>All active coroutines</returns>
        public IEnumerable<ActiveCoroutine> GetActiveCoroutines() {
            lock(this.lockObject)
                return this.tickingCoroutines.Concat(this.eventCoroutines.Values.SelectMany(c => c));
        }

        private void MoveOutstandingCoroutines(bool evt) {
            // RemoveWhere is twice as fast as iterating and then clearing
            if(this.eventCoroutinesToRemove.Count > 0) {
                this.eventCoroutinesToRemove.RemoveWhere(c => {
                    this.GetEventCoroutines(c.Item1, false).Remove(c.Item2);
                    return true;
                });
            }
            var coroutines = this.GetOutstandingCoroutines(evt);
            if(coroutines.Count > 0) {
                coroutines.RemoveWhere(c => {
                    var list = evt ? this.GetEventCoroutines(c.Event, true) : this.tickingCoroutines;
                    var position = list.BinarySearch(c);
                    list.Insert(position < 0 ? ~position : position, c);
                    return true;
                });
            }
        }

        private HashSet<ActiveCoroutine> GetOutstandingCoroutines(bool evt) {
            return evt ? this.outstandingEventCoroutines : this.outstandingTickingCoroutines;
        }

        private List<ActiveCoroutine> GetEventCoroutines(Event evt, bool create) {
            List<ActiveCoroutine> ret;
            if(!this.eventCoroutines.TryGetValue(evt, out ret) && create) {
                ret = new List<ActiveCoroutine>();
                this.eventCoroutines.Add(evt, ret);
            }
            return ret;
        }

        private static IEnumerator<Wait> InvokeLaterImpl(Wait wait, Action action) {
            yield return wait;
            action();
        }

    }


    #region
    //class TestCoroutineRunner2
    //{
    //    private static readonly Event TestEvent = new Event();

    //    public static void Test() {
    //        var seconds = CoroutineHandler.Start(WaitSeconds(), "Awesome Waiting Coroutine");
    //        CoroutineHandler.Start(PrintEvery10Seconds(seconds));

    //        CoroutineHandler.Start(EmptyCoroutine());

    //        CoroutineHandler.InvokeLater(new Wait(5), () => {
    //            Console.WriteLine("Raising test event");
    //            CoroutineHandler.RaiseEvent(TestEvent);
    //        });
    //        CoroutineHandler.InvokeLater(new Wait(TestEvent), () => Console.WriteLine("Example event received"));

    //        CoroutineHandler.InvokeLater(new Wait(TestEvent), () => Console.WriteLine("I am invoked after 'Example event received'"), priority: -5);
    //        CoroutineHandler.InvokeLater(new Wait(TestEvent), () => Console.WriteLine("I am invoked before 'Example event received'"), priority: 2);

    //        var lastTime = DateTime.Now;
    //        while (true) {
    //            Console.WriteLine(CoroutineHandler.EventCount);
    //            var currTime = DateTime.Now;
    //            CoroutineHandler.Tick(currTime - lastTime);
    //            lastTime = currTime;
    //            System.Threading.Thread.Sleep(1);
    //        }
    //    }

    //    private static IEnumerator<Wait> WaitSeconds() {
    //        Console.WriteLine("First thing " + DateTime.Now);
    //        yield return new Wait(1);
    //        Console.WriteLine("After 1 second " + DateTime.Now);
    //        yield return new Wait(9);
    //        Console.WriteLine("After 10 seconds " + DateTime.Now);
    //        CoroutineHandler.Start(NestedCoroutine());
    //        yield return new Wait(5);
    //        Console.WriteLine("After 5 more seconds " + DateTime.Now);
    //        yield return new Wait(10);
    //        Console.WriteLine("After 10 more seconds " + DateTime.Now);

    //        yield return new Wait(20);
    //        Console.WriteLine("First coroutine done");
    //    }

    //    private static IEnumerator<Wait> PrintEvery10Seconds(ActiveCoroutine first) {
    //        while (true) {
    //            yield return new Wait(10);
    //            Console.WriteLine("The time is " + DateTime.Now);
    //            if (first.IsFinished) {
    //                Console.WriteLine("By the way, the first coroutine has finished!");
    //                Console.WriteLine($"{first.Name} data: {first.MoveNextCount} moves, " +
    //                                  $"{first.TotalMoveNextTime.TotalMilliseconds} total time, " +
    //                                  $"{first.LastMoveNextTime.TotalMilliseconds} last time");
    //                Environment.Exit(0);
    //            }
    //        }
    //    }

    //    private static IEnumerator<Wait> EmptyCoroutine() {
    //        yield break;
    //    }

    //    private static IEnumerable<Wait> NestedCoroutine() {
    //        Console.WriteLine("I'm a coroutine that was started from another coroutine!");
    //        yield return new Wait(5);
    //        Console.WriteLine("It's been 5 seconds since a nested coroutine was started, yay!");
    //    }
    //    class Assert
    //    {
    //        internal static void AreEqual(int v1, int counter, string v2) {
    //            throw new NotImplementedException();
    //        }
    //        internal static void AreEqual(bool v1, bool counter, string v2) {
    //            throw new NotImplementedException();
    //        }
    //        internal static void AreEqual(string v1, string counter, string v2) {
    //            throw new NotImplementedException();
    //        }
    //        internal static void IsTrue(bool v) {
    //            throw new NotImplementedException();
    //        }

    //        internal static void AreEqual(int v, int counter) {
    //            throw new NotImplementedException();
    //        }
    //    }
    //    public class EventBasedCoroutineTests
    //    {

    //        //[Test]
    //        public void TestEventBasedCoroutine() {
    //            var counter = 0;
    //            var myEvent = new Event();

    //            IEnumerator<Wait> OnEventTriggered() {
    //                counter++;
    //                yield return new Wait(myEvent);
    //                counter++;
    //            }

    //            var cr = CoroutineHandler.Start(OnEventTriggered());
    //            Assert.AreEqual(1, counter, "instruction before yield is not executed.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(2, counter, "instruction after yield is not executed.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(2, counter, "instruction after yield is not executed.");

    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(cr.MoveNextCount, 2, "Incorrect MoveNextCount value.");
    //        }

    //        //[Test]
    //        public void TestInfiniteCoroutineNeverFinishesUnlessCanceled() {
    //            var myEvent = new Event();
    //            var myOtherEvent = new Event();
    //            var counter = 0;

    //            IEnumerator<Wait> OnEventTriggeredInfinite() {
    //                while (true) {
    //                    counter++;
    //                    yield return new Wait(myEvent);
    //                }
    //            }

    //            void SetCounterToUnreachableValue(ActiveCoroutine coroutine) {
    //                counter = -100;
    //            }

    //            var cr = CoroutineHandler.Start(OnEventTriggeredInfinite());
    //            CoroutineHandler.Tick(1);
    //            cr.OnFinished += SetCounterToUnreachableValue;
    //            for (var i = 0; i < 50; i++)
    //                CoroutineHandler.RaiseEvent(myOtherEvent);

    //            for (var i = 0; i < 50; i++)
    //                CoroutineHandler.RaiseEvent(myEvent);

    //            Assert.AreEqual(51, counter, "Incorrect counter value.");
    //            Assert.AreEqual(false, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(51, cr.MoveNextCount, "Incorrect MoveNextCount value.");

    //            cr.Cancel();
    //            Assert.AreEqual(true, cr.WasCanceled, "Incorrect IsCanceled value after canceling.");
    //            Assert.AreEqual(-100, counter, "OnFinished event not triggered when canceled.");
    //            Assert.AreEqual(51, cr.MoveNextCount, "Incorrect MoveNextCount value.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //        }

    //        //[Test]
    //        public void TestOnFinishedEventExecuted() {
    //            var myEvent = new Event();
    //            var counter = 0;

    //            IEnumerator<Wait> OnEvent() {
    //                counter++;
    //                yield return new Wait(myEvent);
    //            }

    //            void SetCounterToUnreachableValue(ActiveCoroutine coroutine) {
    //                counter = -100;
    //            }

    //            var cr = CoroutineHandler.Start(OnEvent());
    //            CoroutineHandler.Tick(1);
    //            cr.OnFinished += SetCounterToUnreachableValue;
    //            for (var i = 0; i < 10; i++)
    //                CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(-100, counter, "Incorrect counter value.");
    //        }

    //        //[Test]
    //        public void TestNestedCoroutine() {
    //            var onChildCreated = new Event();
    //            var onParentCreated = new Event();
    //            var myEvent = new Event();
    //            var counterAlwaysRunning = 0;

    //            IEnumerator<Wait> AlwaysRunning() {
    //                while (true) {
    //                    yield return new Wait(myEvent);
    //                    counterAlwaysRunning++;
    //                }
    //            }

    //            var counterChild = 0;

    //            IEnumerator<Wait> Child() {
    //                yield return new Wait(myEvent);
    //                counterChild++;
    //            }

    //            var counterParent = 0;

    //            IEnumerator<Wait> Parent() {
    //                yield return new Wait(myEvent);
    //                counterParent++;
    //                // OnFinish I will start child.
    //            }

    //            var counterGrandParent = 0;

    //            IEnumerator<Wait> GrandParent() {
    //                yield return new Wait(myEvent);
    //                counterGrandParent++;
    //                // Nested corotuine starting.
    //                var p = CoroutineHandler.Start(Parent());
    //                CoroutineHandler.RaiseEvent(onParentCreated);
    //                // Nested corotuine starting in OnFinished.
    //                p.OnFinished += ac => {
    //                    CoroutineHandler.Start(Child());
    //                    CoroutineHandler.RaiseEvent(onChildCreated);
    //                };
    //            }

    //            var always = CoroutineHandler.Start(AlwaysRunning());
    //            CoroutineHandler.Start(GrandParent());
    //            Assert.AreEqual(0, counterAlwaysRunning, "Always running counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterGrandParent, "Grand Parent counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 0.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(1, counterAlwaysRunning, "Always running counter is invalid at event 1.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 1.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at event 1.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 1.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(2, counterAlwaysRunning, "Always running counter is invalid at event 2.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 2.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 2.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 2.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(3, counterAlwaysRunning, "Always running counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at event 3.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(4, counterAlwaysRunning, "Always running counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at event 4.");
    //            always.Cancel();
    //        }

    //        //[Test]
    //        public void TestNestedRaiseEvent() {
    //            var event1 = new Event();
    //            var event2 = new Event();
    //            var event3 = new Event();
    //            var coroutineCreated = new Event();
    //            var counterCoroutineA = 0;
    //            var counter = 0;

    //            var infinite = CoroutineHandler.Start(OnCoroutineCreatedInfinite());
    //            CoroutineHandler.Start(OnEvent1());
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(event1);
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(event2);
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(event3);
    //            Assert.AreEqual(3, counter);
    //            Assert.AreEqual(2, counterCoroutineA);
    //            infinite.Cancel();

    //            IEnumerator<Wait> OnCoroutineCreatedInfinite() {
    //                while (true) {
    //                    yield return new Wait(coroutineCreated);
    //                    counterCoroutineA++;
    //                }
    //            }

    //            IEnumerator<Wait> OnEvent1() {
    //                yield return new Wait(event1);
    //                counter++;
    //                CoroutineHandler.Start(OnEvent2());
    //                CoroutineHandler.RaiseEvent(coroutineCreated);
    //            }

    //            IEnumerator<Wait> OnEvent2() {
    //                yield return new Wait(event2);
    //                counter++;
    //                CoroutineHandler.Start(OnEvent3());
    //                CoroutineHandler.RaiseEvent(coroutineCreated);
    //            }

    //            IEnumerator<Wait> OnEvent3() {
    //                yield return new Wait(event3);
    //                counter++;
    //            }
    //        }

    //        //[Test]
    //        public void TestPriority() {
    //            var myEvent = new Event();
    //            var counterShouldExecuteBefore0 = 0;

    //            IEnumerator<Wait> ShouldExecuteBefore0() {
    //                while (true) {
    //                    yield return new Wait(myEvent);
    //                    counterShouldExecuteBefore0++;
    //                }
    //            }

    //            var counterShouldExecuteBefore1 = 0;

    //            IEnumerator<Wait> ShouldExecuteBefore1() {
    //                while (true) {
    //                    yield return new Wait(myEvent);
    //                    counterShouldExecuteBefore1++;
    //                }
    //            }

    //            var counterShouldExecuteAfter = 0;

    //            IEnumerator<Wait> ShouldExecuteAfter() {
    //                while (true) {
    //                    yield return new Wait(myEvent);
    //                    if (counterShouldExecuteBefore0 == 1 &&
    //                        counterShouldExecuteBefore1 == 1) {
    //                        counterShouldExecuteAfter++;
    //                    }
    //                }
    //            }

    //            var counterShouldExecuteFinally = 0;

    //            IEnumerator<Wait> ShouldExecuteFinally() {
    //                while (true) {
    //                    yield return new Wait(myEvent);
    //                    if (counterShouldExecuteAfter > 0) {
    //                        counterShouldExecuteFinally++;
    //                    }
    //                }
    //            }

    //            const int highPriority = int.MaxValue;
    //            var before1 = CoroutineHandler.Start(ShouldExecuteBefore1(), priority: highPriority);
    //            var after = CoroutineHandler.Start(ShouldExecuteAfter());
    //            var before0 = CoroutineHandler.Start(ShouldExecuteBefore0(), priority: highPriority);
    //            var @finally = CoroutineHandler.Start(ShouldExecuteFinally(), priority: -1);
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(1, counterShouldExecuteAfter, $"ShouldExecuteAfter counter  {counterShouldExecuteAfter} is invalid.");
    //            Assert.AreEqual(1, counterShouldExecuteFinally, $"ShouldExecuteFinally counter  {counterShouldExecuteFinally} is invalid.");

    //            before1.Cancel();
    //            after.Cancel();
    //            before0.Cancel();
    //            @finally.Cancel();
    //        }

    //        //[Test]
    //        public void InvokeLaterAndNameTest() {
    //            var myEvent = new Event();
    //            var counter = 0;
    //            var cr = CoroutineHandler.InvokeLater(new Wait(myEvent), () => {
    //                counter++;
    //            }, "Bird");

    //            CoroutineHandler.InvokeLater(new Wait(myEvent), () => {
    //                counter++;
    //            });

    //            CoroutineHandler.InvokeLater(new Wait(myEvent), () => {
    //                counter++;
    //            });

    //            Assert.AreEqual(0, counter, "Incorrect counter value after 5 seconds.");
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.RaiseEvent(myEvent);
    //            Assert.AreEqual(3, counter, "Incorrect counter value after 10 seconds.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(cr.MoveNextCount, 2, "Incorrect MoveNextCount value.");
    //            Assert.AreEqual(cr.Name, "Bird", "Incorrect name of the coroutine.");
    //        }

    //        //[Test]
    //        public void MovingCoroutineTest() {
    //            var evt = new Event();

    //            IEnumerator<Wait> MovingCoroutine() {
    //                while (true) {
    //                    yield return new Wait(evt);
    //                    yield return new Wait(0d);
    //                }
    //            }

    //            var moving = CoroutineHandler.Start(MovingCoroutine(), "MovingCoroutine");
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.RaiseEvent(evt);

    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.Tick(1d);

    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.Tick(1d);

    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.RaiseEvent(evt);
    //            CoroutineHandler.Tick(1d);
    //            CoroutineHandler.RaiseEvent(evt);

    //            moving.Cancel();
    //        }

    //    }
    //    public class TimeBasedCoroutineTests
    //    {

    //        //[Test]
    //        public void TestTimerBasedCoroutine() {
    //            var counter = 0;

    //            IEnumerator<Wait> OnTimeTickCodeExecuted() {
    //                counter++;
    //                yield return new Wait(0.1d);
    //                counter++;
    //            }

    //            var cr = CoroutineHandler.Start(OnTimeTickCodeExecuted());
    //            Assert.AreEqual(1, counter, "instruction before yield is not executed.");
    //            Assert.AreEqual(string.Empty, cr.Name, "Incorrect default name found");
    //            Assert.AreEqual(0, cr.Priority, "Default priority is not minimum");
    //            for (var i = 0; i < 5; i++)
    //                CoroutineHandler.Tick(1);
    //            Assert.AreEqual(2, counter, "instruction after yield is not executed.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(cr.MoveNextCount, 2, "Incorrect MoveNextCount value.");
    //        }

    //        //[Test]
    //        public void TestCoroutineReturningWeirdYields() {
    //            var counter = 0;

    //            IEnumerator<Wait> OnTimeTickNeverReturnYield() {
    //                counter++; // 1
    //                           // condition that's expected to be false
    //                if (counter == 100)
    //                    yield return new Wait(0.1d);
    //                counter++; // 2
    //            }

    //            IEnumerator<Wait> OnTimeTickYieldBreak() {
    //                counter++; // 3
    //                yield break;
    //            }

    //            var cr = new ActiveCoroutine[2];
    //            cr[0] = CoroutineHandler.Start(OnTimeTickNeverReturnYield());
    //            cr[1] = CoroutineHandler.Start(OnTimeTickYieldBreak());
    //            for (var i = 0; i < 5; i++)
    //                CoroutineHandler.Tick(1);

    //            Assert.AreEqual(3, counter, "Incorrect counter value.");
    //            for (var i = 0; i < cr.Length; i++) {
    //                Assert.AreEqual(true, cr[i].IsFinished, $"Incorrect IsFinished value on index {i}.");
    //                Assert.AreEqual(false, cr[i].WasCanceled, $"Incorrect IsCanceled value on index {i}");
    //                Assert.AreEqual(1, cr[i].MoveNextCount, $"Incorrect MoveNextCount value on index {i}");
    //            }
    //        }

    //        //[Test]
    //        public void TestCoroutineReturningDefaultYield() {
    //            var counter = 0;

    //            IEnumerator<Wait> OnTimeTickYieldDefault() {
    //                counter++; // 1
    //                yield return default;
    //                counter++; // 2
    //            }

    //            var cr = CoroutineHandler.Start(OnTimeTickYieldDefault());
    //            for (var i = 0; i < 5; i++)
    //                CoroutineHandler.Tick(1);

    //            Assert.AreEqual(2, counter, "Incorrect counter value.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(2, cr.MoveNextCount, "Incorrect MoveNextCount value.");
    //        }

    //        //[Test]
    //        public void TestInfiniteCoroutineNeverFinishesUnlessCanceled() {
    //            var counter = 0;

    //            IEnumerator<Wait> OnTimerTickInfinite() {
    //                while (true) {
    //                    counter++;
    //                    yield return new Wait(1);
    //                }
    //            }

    //            void SetCounterToUnreachableValue(ActiveCoroutine coroutine) {
    //                counter = -100;
    //            }

    //            var cr = CoroutineHandler.Start(OnTimerTickInfinite());
    //            cr.OnFinished += SetCounterToUnreachableValue;
    //            for (var i = 0; i < 50; i++)
    //                CoroutineHandler.Tick(1);

    //            Assert.AreEqual(51, counter, "Incorrect counter value.");
    //            Assert.AreEqual(false, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(51, cr.MoveNextCount, "Incorrect MoveNextCount value.");

    //            cr.Cancel();
    //            Assert.AreEqual(true, cr.WasCanceled, "Incorrect IsCanceled value after canceling.");
    //            Assert.AreEqual(-100, counter, "OnFinished event not triggered when canceled.");
    //            Assert.AreEqual(51, cr.MoveNextCount, "Incorrect MoveNextCount value.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //        }

    //        //[Test]
    //        public void TestOnFinishedEventExecuted() {
    //            var counter = 0;

    //            IEnumerator<Wait> OnTimeTick() {
    //                counter++;
    //                yield return new Wait(0.1d);
    //            }

    //            void SetCounterToUnreachableValue(ActiveCoroutine coroutine) {
    //                counter = -100;
    //            }

    //            var cr = CoroutineHandler.Start(OnTimeTick());
    //            cr.OnFinished += SetCounterToUnreachableValue;
    //            CoroutineHandler.Tick(50);
    //            Assert.AreEqual(-100, counter, "Incorrect counter value.");
    //        }

    //        //[Test]
    //        public void TestNestedCoroutine() {
    //            var counterAlwaysRunning = 0;

    //            IEnumerator<Wait> AlwaysRunning() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    counterAlwaysRunning++;
    //                }
    //            }

    //            var counterChild = 0;

    //            IEnumerator<Wait> Child() {
    //                yield return new Wait(1);
    //                counterChild++;
    //            }

    //            var counterParent = 0;

    //            IEnumerator<Wait> Parent() {
    //                yield return new Wait(1);
    //                counterParent++;
    //                // OnFinish I will start child.
    //            }

    //            var counterGrandParent = 0;

    //            IEnumerator<Wait> GrandParent() {
    //                yield return new Wait(1);
    //                counterGrandParent++;
    //                // Nested corotuine starting.
    //                var p = CoroutineHandler.Start(Parent());
    //                // Nested corotuine starting in OnFinished.
    //                p.OnFinished += ac => CoroutineHandler.Start(Child());
    //            }

    //            var always = CoroutineHandler.Start(AlwaysRunning());
    //            CoroutineHandler.Start(GrandParent());
    //            Assert.AreEqual(0, counterAlwaysRunning, "Always running counter is invalid at time 0.");
    //            Assert.AreEqual(0, counterGrandParent, "Grand Parent counter is invalid at time 0.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at time 0.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at time 0.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(1, counterAlwaysRunning, "Always running counter is invalid at time 1.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at time 1.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at time 1.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at time 1.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(2, counterAlwaysRunning, "Always running counter is invalid at time 2.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at time 2.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at time 2.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at time 2.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(3, counterAlwaysRunning, "Always running counter is invalid at time 3.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at time 3.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at time 3.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at time 3.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(4, counterAlwaysRunning, "Always running counter is invalid at time 4.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at time 4.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at time 4.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at time 4.");
    //            always.Cancel();
    //        }

    //        //[Test]
    //        public void TestPriority() {
    //            var counterShouldExecuteBefore0 = 0;

    //            IEnumerator<Wait> ShouldExecuteBefore0() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    counterShouldExecuteBefore0++;
    //                }
    //            }

    //            var counterShouldExecuteBefore1 = 0;

    //            IEnumerator<Wait> ShouldExecuteBefore1() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    counterShouldExecuteBefore1++;
    //                }
    //            }

    //            var counterShouldExecuteAfter = 0;

    //            IEnumerator<Wait> ShouldExecuteAfter() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    if (counterShouldExecuteBefore0 == 1 &&
    //                        counterShouldExecuteBefore1 == 1) {
    //                        counterShouldExecuteAfter++;
    //                    }
    //                }
    //            }

    //            var counterShouldExecuteFinally = 0;

    //            IEnumerator<Wait> ShouldExecuteFinally() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    if (counterShouldExecuteAfter > 0) {
    //                        counterShouldExecuteFinally++;
    //                    }
    //                }
    //            }

    //            const int highPriority = int.MaxValue;
    //            var before1 = CoroutineHandler.Start(ShouldExecuteBefore1(), priority: highPriority);
    //            var after = CoroutineHandler.Start(ShouldExecuteAfter());
    //            var before0 = CoroutineHandler.Start(ShouldExecuteBefore0(), priority: highPriority);
    //            var @finally = CoroutineHandler.Start(ShouldExecuteFinally(), priority: -1);
    //            CoroutineHandler.Tick(10);
    //            Assert.AreEqual(1, counterShouldExecuteAfter, $"ShouldExecuteAfter counter  {counterShouldExecuteAfter} is invalid.");
    //            Assert.AreEqual(1, counterShouldExecuteFinally, $"ShouldExecuteFinally counter  {counterShouldExecuteFinally} is invalid.");

    //            before1.Cancel();
    //            after.Cancel();
    //            before0.Cancel();
    //            @finally.Cancel();
    //        }

    //        //[Test]
    //        public void TestTimeBasedCoroutineIsAccurate() {
    //            var counter0 = 0;

    //            IEnumerator<Wait> IncrementCounter0Ever10Seconds() {
    //                while (true) {
    //                    yield return new Wait(10);
    //                    counter0++;
    //                }
    //            }

    //            var counter1 = 0;

    //            IEnumerator<Wait> IncrementCounter1Every5Seconds() {
    //                while (true) {
    //                    yield return new Wait(5);
    //                    counter1++;
    //                }
    //            }

    //            var incCounter0 = CoroutineHandler.Start(IncrementCounter0Ever10Seconds());
    //            var incCounter1 = CoroutineHandler.Start(IncrementCounter1Every5Seconds());
    //            CoroutineHandler.Tick(3);
    //            Assert.AreEqual(0, counter0, "Incorrect counter0 value after 3 seconds.");
    //            Assert.AreEqual(0, counter1, "Incorrect counter1 value after 3 seconds.");
    //            CoroutineHandler.Tick(3);
    //            Assert.AreEqual(0, counter0, "Incorrect counter0 value after 6 seconds.");
    //            Assert.AreEqual(1, counter1, "Incorrect counter1 value after 6 seconds.");

    //            // it's 5 over here because IncrementCounter1Every5Seconds
    //            // increments 5 seconds after last yield. not 5 seconds since start.
    //            // So the when we send 3 seconds in the last SimulateTime,
    //            // the 3rd second was technically ignored.
    //            CoroutineHandler.Tick(5);
    //            Assert.AreEqual(1, counter0, "Incorrect counter0 value after 10 seconds.");
    //            Assert.AreEqual(2, counter1, "Incorrect counter1 value after next 5 seconds.");

    //            incCounter0.Cancel();
    //            incCounter1.Cancel();
    //        }

    //        //[Test]
    //        public void InvokeLaterAndNameTest() {
    //            var counter = 0;
    //            var cr = CoroutineHandler.InvokeLater(new Wait(10), () => {
    //                counter++;
    //            }, "Bird");

    //            CoroutineHandler.Tick(5);
    //            Assert.AreEqual(0, counter, "Incorrect counter value after 5 seconds.");
    //            CoroutineHandler.Tick(5);
    //            Assert.AreEqual(1, counter, "Incorrect counter value after 10 seconds.");
    //            Assert.AreEqual(true, cr.IsFinished, "Incorrect IsFinished value.");
    //            Assert.AreEqual(false, cr.WasCanceled, "Incorrect IsCanceled value.");
    //            Assert.AreEqual(cr.MoveNextCount, 2, "Incorrect MoveNextCount value.");
    //            Assert.AreEqual(cr.Name, "Bird", "Incorrect name of the coroutine.");
    //        }
    //        static IEnumerator<Wait> CoroutineTakesMax500Ms() {
    //            System.Threading.Thread.Sleep(200);
    //            yield return new Wait(10);
    //            System.Threading.Thread.Sleep(500);
    //        }
    //        //[Test]
    //        public void CoroutineStatsAreUpdated() {


    //            var cr = CoroutineHandler.Start(CoroutineTakesMax500Ms());
    //            for (var i = 0; i < 5; i++)
    //                CoroutineHandler.Tick(50);

    //            Assert.IsTrue(cr.TotalMoveNextTime.TotalMilliseconds >= 700);
    //            Assert.IsTrue(cr.LastMoveNextTime.TotalMilliseconds >= 500);
    //            Assert.IsTrue(cr.MoveNextCount == 2);
    //        }

    //        //[Test]
    //        public void TestTickWithNestedAddAndRaiseEvent() {
    //            var coroutineCreated = new Event();
    //            var counterCoroutineA = 0;
    //            var counter = 0;

    //            var infinite = CoroutineHandler.Start(OnCoroutineCreatedInfinite());
    //            CoroutineHandler.Start(OnEvent1());
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.Tick(1);
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(3, counter);
    //            Assert.AreEqual(2, counterCoroutineA);
    //            infinite.Cancel();

    //            IEnumerator<Wait> OnCoroutineCreatedInfinite() {
    //                while (true) {
    //                    yield return new Wait(coroutineCreated);
    //                    counterCoroutineA++;
    //                }
    //            }

    //            IEnumerator<Wait> OnEvent1() {
    //                yield return new Wait(1);
    //                counter++;
    //                CoroutineHandler.Start(OnEvent2());
    //                CoroutineHandler.RaiseEvent(coroutineCreated);
    //            }

    //            IEnumerator<Wait> OnEvent2() {
    //                yield return new Wait(1);
    //                counter++;
    //                CoroutineHandler.Start(OnEvent3());
    //                CoroutineHandler.RaiseEvent(coroutineCreated);
    //            }

    //            IEnumerator<Wait> OnEvent3() {
    //                yield return new Wait(1);
    //                counter++;
    //            }
    //        }

    //        //[Test]
    //        public void TestTickWithNestedAddAndRaiseEventOnFinish() {
    //            var onChildCreated = new Event();
    //            var onParentCreated = new Event();
    //            var counterAlwaysRunning = 0;

    //            IEnumerator<Wait> AlwaysRunning() {
    //                while (true) {
    //                    yield return new Wait(1);
    //                    counterAlwaysRunning++;
    //                }
    //            }

    //            var counterChild = 0;

    //            IEnumerator<Wait> Child() {
    //                yield return new Wait(1);
    //                counterChild++;
    //            }

    //            var counterParent = 0;

    //            IEnumerator<Wait> Parent() {
    //                yield return new Wait(1);
    //                counterParent++;
    //                // OnFinish I will start child.
    //            }

    //            var counterGrandParent = 0;

    //            IEnumerator<Wait> GrandParent() {
    //                yield return new Wait(1);
    //                counterGrandParent++;
    //                // Nested corotuine starting.
    //                var p = CoroutineHandler.Start(Parent());
    //                CoroutineHandler.RaiseEvent(onParentCreated);
    //                // Nested corotuine starting in OnFinished.
    //                p.OnFinished += ac => {
    //                    CoroutineHandler.Start(Child());
    //                    CoroutineHandler.RaiseEvent(onChildCreated);
    //                };
    //            }

    //            var always = CoroutineHandler.Start(AlwaysRunning());
    //            CoroutineHandler.Start(GrandParent());
    //            Assert.AreEqual(0, counterAlwaysRunning, "Always running counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterGrandParent, "Grand Parent counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at event 0.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 0.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(1, counterAlwaysRunning, "Always running counter is invalid at event 1.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 1.");
    //            Assert.AreEqual(0, counterParent, "Parent counter is invalid at event 1.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 1.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(2, counterAlwaysRunning, "Always running counter is invalid at event 2.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 2.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 2.");
    //            Assert.AreEqual(0, counterChild, "Child counter is invalid at event 2.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(3, counterAlwaysRunning, "Always running counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 3.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at event 3.");
    //            CoroutineHandler.Tick(1);
    //            Assert.AreEqual(4, counterAlwaysRunning, "Always running counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterGrandParent, "Grand Parent counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterParent, "Parent counter is invalid at event 4.");
    //            Assert.AreEqual(1, counterChild, "Child counter is invalid at event 4.");
    //            always.Cancel();
    //        }

    //    }
    //}
    #endregion
}
