using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace EasyDeferred
{
    public class EasySynchronizationContextInvokeBase : SynchronizationContext, ISynchronizeInvoke
    {
        public virtual bool InvokeRequired => throw new NotImplementedException();

        public virtual IAsyncResult BeginInvoke(Delegate method,params object[] args) {
            throw new NotImplementedException();
        }

        public virtual object EndInvoke(IAsyncResult result) {
            throw new NotImplementedException();
        }

        public virtual object Invoke(Delegate method, params object[] args) {
            throw new NotImplementedException();
        }
    }
}
