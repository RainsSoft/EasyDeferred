using System;
using System.Collections.Generic;

namespace EasyDeferred.FSMSharp
{
    /// <summary>
    /// A Finite State Machine. 
    /// T is a type which will be used as descriptors of the state. Usually this is an enum, string or an integral type,
    /// but any type can be used.
    /// </summary>
    /// <typeparam name="T">A type which will be used as descriptors of the state. Usually this is an enum, string or an integral type,
    /// but any type can be used.</typeparam>
    public class FSM<T>
    {
        Dictionary<T, FsmStateBehaviour<T>> m_StateBehaviours = new Dictionary<T, FsmStateBehaviour<T>>();
        T m_CurrentState = default(T);
        FsmStateBehaviour<T> m_CurrentStateBehaviour;
        float m_StateAge = -1f;
        string m_FsmName = null;
        float m_TimeBaseForIncremental;
        /// <summary>
        ///is allow duplicate state name,if not then add state will check has exist state name
        /// </summary>
        public bool AllowDuplicateName {
            get;private set;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="FSM{T}"/> class.
        /// </summary>
        /// <param name="name">The name of the FSM, used in throw exception and for debug purposes.</param>
        public FSM(string name,bool allowDuplicateName=true)
        {
            m_FsmName = name;
            AllowDuplicateName = allowDuplicateName;
        }
        

        /// <summary>
        /// Gets or sets a callback which will be called when the FSM logs state transitions. Used to track state transition for debug purposes.
        /// </summary>
        /// <value>
        /// The debug log handler.
        /// </value>
        public Action<string> DebugLogHandler { get; set; }


        /// <summary>
        /// Adds the specified state.所有状态为兄弟结构
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>The newly created behaviour, so that it could be configured with a fluent-like syntax.</returns>
        public FsmStateBehaviour<T> Add(T state) {
            string name = getStateTypeName(state);
            return Add(state, name);
            //var behaviour = new FsmStateBehaviour<T>(state, getTName(state));
            //m_StateBehaviours.Add(state, behaviour);
            //return behaviour;
        }
        /// <summary>
        /// Adds the specified state.所有状态为兄弟结构
        /// </summary>
        /// <param name="state"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public FsmStateBehaviour<T> Add(T state,string name) {
            var behaviour = new FsmStateBehaviour<T>(state, name);
            if(this.AllowDuplicateName == false) {
                if(checkExistName(name)) {
                    System.Diagnostics.Debug.Assert(false,"已经存在该名称State:"+name);
                    throw new NotSupportedException("已经存在该名称State:"+name);
                }
            }
            m_StateBehaviours.Add(state, behaviour);
            return behaviour;
        }
        #region 名称相关辅助
        string getStateTypeName<T>(T state) {
            var t1 = state.GetType();
            if(t1.IsEnum||t1.IsValueType){
                return state.ToString();
            }
            return t1.Name;
        }
        private bool checkExistName(string name) {
            foreach(var v in m_StateBehaviours.Values) {
                if(string.Compare(name, v.Name, true) == 0) {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 获取已加入state列表实例的友好名
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public string GetStateName(T state) {
            //FsmStateBehaviour<T> value = null;
            if (m_StateBehaviours.ContainsKey(state)) {
                return m_StateBehaviours[state].Name;
            }
            return getStateTypeName(state);
        }
        public FsmStateBehaviour<T> GetState(string stateName) {
            //FsmStateBehaviour<T> value;
            foreach (var v in m_StateBehaviours.Values) {
                if (string.Compare(stateName, v.Name, true) == 0) {
                    return v;
                }
            }
            return null;
        }
        #endregion
        /// <summary>
        /// Gets the number of states currently in the FSM.
        /// </summary>
        /// <value>
        /// The number of states currently in the FSM.
        /// </value>
        public int Count
        {
            get { return m_StateBehaviours.Count; }
        }

        /// <summary>
        /// Processes the logic for the FSM. 
        /// </summary>
        /// <param name="time">步进时间The time, expressed in seconds.</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void ProcessIncremental(float dtime)
        {
            m_TimeBaseForIncremental += dtime;
            Process(m_TimeBaseForIncremental);
        }

        /// <summary>
        /// Processes the logic for the FSM. 
        /// </summary>
        /// <param name="time">总时间The time, expressed in seconds.</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void Process(float time)
        {
            if (m_StateAge < 0f)
                m_StateAge = time;

            float totalTime = time;
            float stateTime = (totalTime - m_StateAge);
            float stateProgress = 0f;

            if (m_CurrentStateBehaviour == null)
            {
                throw new InvalidOperationException(string.Format("[FSM {0}] : Can't call 'Process' before setting the starting state.", m_FsmName));
            }

            if (m_CurrentStateBehaviour.Duration.HasValue)
            {
                stateProgress = Math.Max(0f, Math.Min(1f, stateTime / m_CurrentStateBehaviour.Duration.Value));
            }
            if (m_CurrentStateBehaviour.IgnoreDurationAndEnableCustomProgress) {
                stateProgress = m_CurrentStateBehaviour.CustomizeProgress;
            }
            var data = new FsmStateData<T>()
            {
                Machine = this,
                Behaviour = m_CurrentStateBehaviour,
                State = m_CurrentState,
                StateTime = stateTime,
                AbsoluteTime = totalTime,
                StateProgress = stateProgress
            };

            m_CurrentStateBehaviour.TriggerUpdate(data);

            if (stateProgress >= 1f && m_CurrentStateBehaviour.NextStateSelector != null)
            {
                CurrentState = m_CurrentStateBehaviour.NextStateSelector();
                m_StateAge = time;
            }
        }

        /// <summary>
        /// Gets or sets the current state of the FSM.
        /// </summary>
        public T CurrentState
        {
            get { return m_CurrentState; }
            set
            {
                InternalSetCurrentState(value, true);
            }
        }

        private void InternalSetCurrentState(T value, bool executeSideEffects)
        {
            if (DebugLogHandler != null)
                DebugLogHandler(string.Format("[FSM {0}] : Changing state from {1} to {2}", m_FsmName, m_CurrentState, value));

            if (m_CurrentStateBehaviour != null && executeSideEffects)
                m_CurrentStateBehaviour.TriggerLeave();

            m_StateAge = -1f;

            m_CurrentStateBehaviour = m_StateBehaviours[value];
            m_CurrentState = value;

            if (m_CurrentStateBehaviour != null && executeSideEffects)
                m_CurrentStateBehaviour.TriggerEnter();

        }



        /// <summary>
        /// Moves the FSM to the next state as configured using FsmStateBehaviour.GoesTo(...).
        /// Note: to change the state freely, use the CurrentState property.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the behaviour has not a next state / state selector configured.</exception>
        public void Next()
        {
            if (m_CurrentStateBehaviour.NextStateSelector != null)
                this.CurrentState = m_CurrentStateBehaviour.NextStateSelector();
            else
                throw new InvalidOperationException(string.Format("[FSM {0}] : Can't call 'Next' on current behaviour.", m_FsmName));
        }

        /// <summary>
        /// Saves a snapshot of the FSM,对值类型状态有效
        /// </summary>
        /// <returns>The snapshot.</returns>
        public FsmSnapshot<T> SaveSnapshot()
        {
            return new FsmSnapshot<T>(m_StateAge, m_CurrentState);
        }

        /// <summary>
        /// Restores a snapshot of the FSM taken with SaveSnapshot,对值类型状态有效
        /// </summary>
        /// <param name="snap">The snapshot.</param>
        public void RestoreSnapshot(FsmSnapshot<T> snap, bool executeSideEffects)
        {
            InternalSetCurrentState(snap.CurrentState, executeSideEffects);
            m_StateAge = snap.StateAge;
        }

        public FsmStateBehaviour<T> GetBehaviour(T state)
        {
            FsmStateBehaviour<T> v = null;
            this.m_StateBehaviours.TryGetValue(state, out v);
            return v;
            //return this.m_StateBehaviours[state];
        }

        #region Trigger Event
        /// <summary>
        /// 【非线程安全】 触发当前状态的中的指定事件名
        ///[No thread safe] Triggered when and event occurs. Executes the event's action if the 
        /// current state is at the top of the stack, otherwise triggers it on 
        /// the next state down.
        /// </summary>
        /// <param name="name">Name of the event to trigger</param>
        public void TriggerEvent(string name) {
            TriggerEvent(name, EventArgs.Empty);
        }

        /// <summary>
        /// 【非线程安全】 触发当前状态的中的指定事件名
        /// [No thread safe] Triggered when and event occurs. Executes the event's action if the 
        /// current state is at the top of the stack, otherwise triggers it on 
        /// the next state down.
        /// </summary>
        /// <param name="name">Name of the event to trigger</param>
        /// <param name="eventArgs">Arguments to send to the event</param>
        public void TriggerEvent(string name, EventArgs eventArgs) {
            this.m_CurrentStateBehaviour?.TriggerEvent(name, eventArgs);           
        }
        #endregion
    }


    class FSM_Test1
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

        public void Init() {
            // Initialize the states, adding them to the FSM and configuring their behaviour

            fsm.Add(Season.Winter)
                .Expires(3f)
                .GoesTo(Season.Spring)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.White)
                .OnLeave((Z) => Console.WriteLine("Winter is ending..."))
                .Update(d => Console.WriteLine("Winter is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Spring)
                .Expires(3f)
                .GoesTo(Season.Summer)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.Green)
                .OnLeave((Z) => Console.WriteLine("Spring is ending..."))
                .Update(d => Console.WriteLine("Spring is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Summer)
                .Expires(3f)
                .GoesTo(Season.Fall)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.Red)
                .OnLeave((Z) => Console.WriteLine("Summer is ending..."))
                .Update(d => Console.WriteLine("Summer is going on.. {0}%", d.StateProgress * 100f));

            fsm.Add(Season.Fall)
                .Expires(3f)
                .GoesTo(Season.Winter)
                .OnEnter((Z) => Console.ForegroundColor = ConsoleColor.DarkYellow)
                .OnLeave((Z) => Console.WriteLine("Fall is ending..."))
                .Update(d => Console.WriteLine("Fall is going on.. {0}%", d.StateProgress * 100f));

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
    }
    class FSM_Test2 {
        public enum StateType {
            One,
            Two,
            Three,
            Four
        }
        public abstract class StateBase {
            public abstract void DoEnter();
            public abstract void DoLeave();
            public abstract void DoUpdate();
            public virtual StateType State {
                get;
            }
        }
        class State1 : StateBase {
            public override void DoEnter() {
                Console.WriteLine("1---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("1---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("1---update");
            }
            public override StateType State {
                get {
                    return  StateType.One;
                }
            }
        }
        class State2 : StateBase {
            public override void DoEnter() {
                Console.WriteLine("2---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("2---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("2---update");
            }
            public override StateType State {
                get {
                    return StateType.Two;
                }
            }
        }
        class State3 : StateBase {
            public override void DoEnter() {
                Console.WriteLine("3---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("3---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("3---update");
            }
            public override StateType State {
                get {
                    return StateType.Three;
                }
            }
        }
        class State4 : StateBase {
            public override void DoEnter() {
                Console.WriteLine("4---enter");
            }

            public override void DoLeave() {
                Console.WriteLine("4---leave");
            }
            public override void DoUpdate() {
                Console.WriteLine("4---update");
            }
            public override StateType State {
                get {
                    return StateType.Four;
                }
            }
        }
        FSM<StateBase> fsm = new FSM<StateBase>("seasons-fsm",false);
        public void Init() {
            // Initialize the states, adding them to the FSM and configuring their behaviour
            State1 s1 = new State1();
            State2 s2 = new State2();
            State3 s3 = new State3();
            State4 s4 = new State4();
            fsm.Add(s1)
                .Expires(3f)
                .GoesTo(s2)
                .OnEnter((Z) => {
                    Console.ForegroundColor = ConsoleColor.White;
                    Z.DoEnter();
                    System.Diagnostics.Debug.Assert(Z==s1,"传入对象不一致");
                })
                .OnLeave((Z) => {
                    Console.WriteLine("Winter is ending...");
                    Z.DoLeave();
                    System.Diagnostics.Debug.Assert(Z == s1, "传入对象不一致");
                })
                .Update(d => {
                    Console.WriteLine("Winter is going on.. {0}%", d.StateProgress * 100f);
                    d.State.DoUpdate();
                    System.Diagnostics.Debug.Assert(d.State==s1, "传入对象不一致");
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
                .Update(d => {
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
                .Update(d => {
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
                .Update(d => {
                    Console.WriteLine("Fall is going on.. {0}%", d.StateProgress * 100f);                  
                    System.Diagnostics.Debug.Assert(d.State == s4, "传入对象不一致");
                });

            // Very important! set the starting state
            fsm.CurrentState = s1;
        }

        public void Run() {
            // Define a base time. This seems pedantic in a pure .NET world, but allows to use custom time providers,
            // Unity3D Time class (scaled or unscaled), MonoGame timing, etc.
            DateTime baseTime = DateTime.Now;

            // Initialize the FSM
            Init();

            // Call the FSM periodically... in a real world scenario this will likely be in a timer callback, or frame handling (e.g.
            // Unity's Update() method).
            while(true) {
                // 
                fsm.Process((float)(DateTime.Now - baseTime).TotalSeconds);
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
