using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace EasyDeferred.Synchronize
{

    /// <summary>
    /// only support Invoke
    /// 不支持BeginInvoke(...)/EndInvoke(...)
    /// </summary>
    public class SyncSimpleInvoke : ISynchronizeInvoke
    {
        // For more details about this solution see:
        // http://stackoverflow.com/questions/6708765/how-to-invoke-when-i-have-no-control-available

        private readonly SynchronizationContext context;
        private readonly Thread thread;
        private readonly Object locker;

        public SyncSimpleInvoke()
            : base() {
            this.context = SynchronizationContext.Current;
            //System.Diagnostics.Debug.Assert(this.context!=null);
            if (this.context == null) {
                this.context = new SynchronizationContext();               
            }
            this.thread = Thread.CurrentThread;
            this.locker = new Object();
           
        }
        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        //static void Install() {
        //    UnitySynchronizationContext = SynchronizationContext.Current;
        //    UnityThreadId = Thread.CurrentThread.ManagedThreadId;
        //}

        //public static int UnityThreadId {
        //    get; private set;
        //}

        //public static SynchronizationContext UnitySynchronizationContext {
        //    get; private set;
        //}
        #region ISynchronizeInvoke member implementation section.

        public Boolean InvokeRequired {
            get {
                return Thread.CurrentThread.ManagedThreadId != this.thread.ManagedThreadId;
            }
        }

        [Obsolete("This method is not supported!", true)]
        public IAsyncResult BeginInvoke(Delegate method, Object[] args) {
            throw new NotSupportedException();
        }

        [Obsolete("This method is not supported!", true)]
        public Object EndInvoke(IAsyncResult result) {
            throw new NotSupportedException();
        }

        public Object Invoke(Delegate method, Object[] args) {
            if (method == null) {
                throw new ArgumentNullException("method");
            }

            lock (this.locker) {
                var old = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(this.context);
                Object result = null;

                SendOrPostCallback invoker = new SendOrPostCallback(
                    delegate (Object data) {
                        result = method.DynamicInvoke(args);
                    });

                this.context.Send(new SendOrPostCallback(invoker), method.Target);
                //
                SynchronizationContext.SetSynchronizationContext(null);
                return result;
            }
        }

        public Object Invoke(Delegate method) {
            return this.Invoke(method, null);
        }

        #endregion // ISynchronizeInvoke member implementation section.
    }
}
