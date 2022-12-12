using System;
using System.Collections.Generic;
using System.Collections;
using EasyDeferred.Core;
using EasyDeferred.RSG;

namespace EasyDeferred
{
    /// <summary>
    /// Delegate of Deferred IEnumerator.
    /// </summary>
    public delegate IEnumerator DeferredCoroutine(PromiseCoroutine deferred);
    /// <summary>
    /// wrapper IEnumerator auto execute promise
    /// </summary>
    public class PromiseCoroutine
    {
#if UNITY3D

        //private static readonly ILog log = LogManager.GetLogger(typeof(InterceptableEnumerator));
#endif
        private object current;
        private Stack<IEnumerator> stack = new Stack<IEnumerator>();

        private Action<Exception> onException;
        private Action onFinally;
        private Func<bool> hasNext;
        public Promise promise { get; private set; }
        public PromiseCoroutine(IEnumerator routine) {
            this.promise = new Promise();
            this.stack.Push(routine);

        }

        public object Current { get { return this.current; } }

        public bool MoveNext() {
            try {
               
                if (!this.HasNext()) {
                    this.OnFinally(true);
                    return false;
                }

                if (stack.Count <= 0) {
                    this.OnFinally(true);
                    return false;
                }

                IEnumerator ie = stack.Peek();
                bool hasNext = ie.MoveNext();
                if (!hasNext) {
                    this.stack.Pop();
                    return MoveNext();
                }

                this.current = ie.Current;
                if (this.current is IEnumerator) {
                    stack.Push(this.current as IEnumerator);
                    return MoveNext();
                }
#if UNITY3D
                //if (this.current is Coroutine && log.IsWarnEnabled)
                    //log.Warn("The Enumerator's results contains the 'UnityEngine.Coroutine' type,If occurs an exception,it can't be catched.It is recommended to use 'yield return routine',rather than 'yield return StartCoroutine(routine)'.");
                if(this.current is Coroutine ){
                    LogManager.Instance.Write(LogMessageLevel.Normal, false, "The Enumerator's results contains the 'UnityEngine.Coroutine' type,If occurs an exception,it can't be catched.It is recommended to use 'yield return routine',rather than 'yield return StartCoroutine(routine)'.");
                }
#endif
                return hasNext;
            }
            catch (Exception e) {
                this.OnException(e);
                this.OnFinally(false);
                return false;
            }
        }

        public void Reset() {
            throw new NotSupportedException();
        }

        private void OnException(Exception e) {
            try {
                if (this.onException == null)
                    return;

                foreach (Action<Exception> action in this.onException.GetInvocationList()) {
                    try {
                        action(e);
                    }
                    catch (Exception ex) {
                        //if (log.IsWarnEnabled)
                        //    log.WarnFormat("{0}", ex);
                        LogManager.Instance.Write(LogMessageLevel.Critical, false, "OnFinally1(bool resolve)=>  " + ex.ToString());
                    }
                }
            }
            catch (Exception ee) {
                LogManager.Instance.Write(LogMessageLevel.Critical, false, "OnFinally2(bool resolve)=>{...}  " + ee.ToString());
            }
            finally {
                this.promise.Reject(e);
            }
        }

        private void OnFinally(bool resolve) {
            try {
                if (this.onFinally == null) {                    
                    return;
                }
                foreach (Action action in this.onFinally.GetInvocationList()) {
                    try {
                        action();
                    }
                    catch (Exception ex) {
                        //if (log.IsWarnEnabled)
                        //    log.WarnFormat("{0}", ex);
                        LogManager.Instance.Write(LogMessageLevel.Critical, false, "OnFinally1(bool resolve)=>  " + ex.ToString());
                    }
                }
            }
            catch (Exception ee) {
                LogManager.Instance.Write(LogMessageLevel.Critical, false, "OnFinally2(bool resolve)=>{...}  " + ee.ToString());
            }
            finally {
                if (resolve) {
                    //处于解决状态
                    this.promise.Resolve();
                }
            }
        }

        private bool HasNext() {
            if (hasNext == null)
                return true;
            return hasNext();
        }

        /// <summary>
        /// Register a condition code block.
        /// </summary>
        /// <param name="hasNext"></param>
        public virtual void RegisterConditionBlock(Func<bool> hasNext) {
            this.hasNext = hasNext;
        }

        /// <summary>
        /// Register a code block, when an exception occurs it will be executed.
        /// </summary>
        /// <param name="onException"></param>
        public virtual void RegisterCatchBlock(Action<Exception> onException) {
            this.onException += onException;
        }

        /// <summary>
        /// Register a code block, when the end of the operation is executed.
        /// </summary>
        /// <param name="onFinally"></param>
        public virtual void RegisterFinallyBlock(Action onFinally) {
            this.onFinally += onFinally;
        }
    }
}
