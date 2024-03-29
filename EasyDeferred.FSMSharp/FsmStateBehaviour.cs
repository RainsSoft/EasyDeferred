﻿using System;
using System.Collections.Generic;
using EasyDeferred.RSG;
namespace EasyDeferred.FSMSharp
{
    /// <summary>
    /// Defines the behaviour of a state of a finit state machine
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class FsmStateBehaviour<T>
    {
        private List<Action<FsmStateData<T>>> m_ProcessCallbacks = new List<Action<FsmStateData<T>>>();
        //private List<Action> m_EnterCallbacks = new List<Action>();
        private List<Action<T>> m_EnterCallbacks = new List<Action<T>>();
        //private List<Action> m_LeaveCallbacks = new List<Action>();
        private List<Action<T>> m_LeaveCallbacks = new List<Action<T>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FsmStateBehaviour{T}"/> class.
        /// </summary>
        /// <param name="state">The state.</param>
        internal FsmStateBehaviour(T state,string name,FSM<T> parentBuilder)
        {
            State = state;
            this.Name = name;
            this.parentBuilder = parentBuilder;
        }
        /// <summary>
        ///友好名 
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the state associated with this behaviour
        /// </summary>
        public T State { get; private set; }

        /// <summary>
        /// Gets the time duration of the state (if any) to Expires and can go to next state (if has)
        /// 控制时间进度,当进入状态时间>=Duration 时可切换State
        /// </summary>
        public float? Duration { get; private set; }
        /// <summary>
        /// 忽略时间进度，使用自己的进度控制,当<see cref="CustomizeProgress"/> >=1f时可切换State
        /// </summary>
        public bool IgnoreDurationAndEnableCustomProgress { get;private set; }
        /// <summary>
        /// 自定义进度，值范围(0~1),IgnoreDuration==true时起效，当 CustomizeProgress>=1f时可切换State
        /// </summary>
        public float CustomizeProgress { get;  set; }
        /// <summary>
        /// Gets the function which will be used to select the next state when this expires or Next() gets called.
        /// </summary>
        public Func<T> NextStateSelector { get; private set; }

        /// <summary>
        /// Sets a callback which will be called when the FSM enters in this state
        /// </summary>
        public FsmStateBehaviour<T> OnEnter(Action<T> callback)
        {
            CustomizeProgress = 0f;
            NeedGoToStateImmediately = false;
            m_EnterCallbacks.Add(callback);
            return this;
        }

        /// <summary>
        /// Sets a callback which will be called when the FSM leaves this state
        /// </summary>
        public FsmStateBehaviour<T> OnLeave(Action<T> callback)
        {
            m_LeaveCallbacks.Add(callback);
            return this;
        }

        ///// <summary>
        ///// Sets a callback which will be called everytime Process is called on the FSM, when this state is active
        ///// </summary>
        ////[Obsolete("use Update")]
        //public FsmStateBehaviour<T> Calls(Action<FsmStateData<T>> callback) {
        //    //return Update(callback);
        //    m_ProcessCallbacks.Add(callback);
        //    return this;
        //}
        /// <summary>
        /// Sets a callback which will be called everytime Process is called on the FSM, when this state is active
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public FsmStateBehaviour<T> Update(Action<FsmStateData<T>> callback) {
            m_ProcessCallbacks.Add(callback);
            return this;
        }
        /// <summary>
        /// Sets the state to automatically expire after the given time (in seconds)
        /// <paramref name="duration">时间进度总值</paramref>
        /// <paramref name="ignoreDuration">如果值为true,则使用CustomizeProgress值控制跳转State</paramref>
        /// </summary>
        public FsmStateBehaviour<T> Expires(float duration,bool ignoreDurationAndEnableCustomProgress = false)
        {
            this.Duration = duration;
            this.IgnoreDurationAndEnableCustomProgress = ignoreDurationAndEnableCustomProgress;
            return this;
        }

        /// <summary>
        /// Sets the state to which the FSM goes when the duration of this expires, or when Next() gets called on the FSM
        /// 指定下一个state(非立即进入),当时间达到过期时间或自定义进度状态下进度>=1时，则自动进入到指定的State
        /// </summary>
        /// <param name="state">The state.</param>
        public FsmStateBehaviour<T> GoesTo(T state)
        {            
            NextStateSelector = () => state;
            return this;
        }

        /// <summary>
        /// Sets a function which selects the state to which the FSM goes when the duration of this expires, or when Next() gets called on the FSM
        /// 指定下一个state(非立即进入),当时间达到过期时间或自定义进度状态下进度>=1时，则自动进入到指定的State
        /// </summary>
        /// <param name="stateSelector">The state selector function.</param>
        public FsmStateBehaviour<T> GoesTo(Func<T> stateSelector)
        {            
            NextStateSelector = stateSelector;
            return this;
        }
        #region GoToStateImmediately
        /// <summary>
        /// 标记立即完成进度进入下个状态
        /// </summary>
        internal bool NeedGoToStateImmediately {
            get;
            private set;
        }
        /// <summary>
        /// 立即跳转到指定State
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public FsmStateBehaviour<T> GoToStateImmediately(T state) {           
            this.NeedGoToStateImmediately = true;
            return GoesTo(state);
        }
        /// <summary>
        /// 立即跳转到指定State
        /// </summary>
        /// <param name="stateSelector"></param>
        /// <returns></returns>
        public FsmStateBehaviour<T> GoToStateImmediately(Func<T> stateSelector) {            
            this.NeedGoToStateImmediately = true;
            return GoesTo(stateSelector);
        }
        #endregion
        /// <summary>
        /// Calls the process callback
        /// </summary>
        internal void TriggerUpdate(FsmStateData<T> data)
        {
            for(int i = 0, len = m_ProcessCallbacks.Count; i < len; i++) {
                m_ProcessCallbacks[i](data);
            }
            // Update conditions
            for(var i = 0; i < conditions.Count; i++) {
                if(conditions[i].Predicate()) {
                    conditions[i].Action();
                }
            }
        }

        /// <summary>
        /// Calls the onenter callback
        /// </summary>
        internal void TriggerEnter()
        {
            //var data = new FsmStateData<T>() {
            //    Machine = this,
            //    Behaviour = m_CurrentStateBehaviour,
            //    State = m_CurrentState,
            //    StateTime = stateTime,
            //    AbsoluteTime = totalTime,
            //    StateProgress = stateProgress
            //};
            for (int i = 0, len = m_EnterCallbacks.Count; i < len; i++)
                m_EnterCallbacks[i](this.State);
        }

        /// <summary>
        /// Calls the onleave callback
        /// </summary>
        internal void TriggerLeave()
        {
            //var data = new FsmStateData<T>() {
            //    Machine = this,
            //    Behaviour = m_CurrentStateBehaviour,
            //    State = m_CurrentState,
            //    StateTime = stateTime,
            //    AbsoluteTime = totalTime,
            //    StateProgress = stateProgress
            //};
            for (int i = 0, len = m_LeaveCallbacks.Count; i < len; i++)
                m_LeaveCallbacks[i](this.State);
        }

        #region Trigger Events

        /// <summary>
        /// Dictionary of all actions associated with this state.
        /// </summary>
        private readonly IDictionary<string, Action<EventArgs>> events = new Dictionary<string, Action<EventArgs>>();
        /// <summary>
        /// Sets an action to be associated with an identifier that can later be used
        /// to trigger it.
        /// Convenience method that uses default event args intended for events that 
        /// don't need any arguments.
        /// </summary>
        public FsmStateBehaviour<T> SetEvent(string identifier, Action<EventArgs> eventTriggeredAction) {
           return SetEvent<EventArgs>(identifier, eventTriggeredAction);
        }

        /// <summary>
        /// Sets an action to be associated with an identifier that can later be used
        /// to trigger it.
        /// </summary>
        public FsmStateBehaviour<T> SetEvent<TEvent>(string identifier, Action<TEvent> eventTriggeredAction)
            where TEvent : EventArgs {
            events.Add(identifier, args => eventTriggeredAction(CheckEventArgs<TEvent>(identifier, args)));
            return this;
        }

        /// <summary>
        /// Cast the specified EventArgs to a specified type, throwing a descriptive exception if this fails.
        /// </summary>
        private static TEvent CheckEventArgs<TEvent>(string identifier, EventArgs args)
            where TEvent : EventArgs {
            try {
                return (TEvent)args;
            }
            catch(InvalidCastException ex) {
                throw new ApplicationException("Could not invoke event \"" + identifier + "\" with argument of type " +
                    args.GetType().Name + ". Expected " + typeof(TEvent).Name, ex);
            }
        }

        /// <summary>
        /// 【非线程安全】触发当前状态中的指定名称的事件
        /// [no thread safe] Triggered when and event occurs. Executes the event's action if the 
        /// current state is at the top of the stack, otherwise triggers it on 
        /// the next state down.
        /// </summary>
        /// <param name="name">Name of the event to trigger</param>
        public void TriggerEvent(string name) {
            TriggerEvent(name, EventArgs.Empty);
        }

        /// <summary>
        /// 【非线程安全】触发当前状态中的指定名称的事件
        /// [no thread safe] Triggered when and event occurs. Executes the event's action if the 
        /// current state is at the top of the stack, otherwise triggers it on 
        /// the next state down.
        /// </summary>
        /// <param name="name">Name of the event to trigger</param>
        /// <param name="eventArgs">Arguments to send to the event</param>
        public void TriggerEvent(string name, EventArgs eventArgs) {
            
            Action<EventArgs> myEvent;
            if(events.TryGetValue(name, out myEvent)) {
                myEvent(eventArgs);
            }
        }
        #endregion

        #region   Condition(条件执行)
        /// <summary>
        /// Data structure for associating a condition with an action.
        /// </summary>
        private struct Condition {
            public Func<bool> Predicate;
            public Action Action;
        }
        private readonly IList<Condition> conditions = new List<Condition>();
        /// <summary>
        /// Set an action to be called when the state is updated an a specified 
        /// predicate is true.
        /// </summary>
        public FsmStateBehaviour<T> SetCondition(Func<bool> predicate, Action action) {
            conditions.Add(new Condition() {
                Predicate = predicate,
                Action = action
            });
            return this;
        }
        #endregion

        #region FSM
        /// <summary>
        /// Class to return when we call .End()
        /// </summary>
        private readonly FSM<T> parentBuilder;
        /// <summary>
        /// 返回创建当前对象FSM
        /// </summary>
        /// <returns></returns>
        public FSM<T> End() {
            return parentBuilder;
        }
        #endregion
    }
}
