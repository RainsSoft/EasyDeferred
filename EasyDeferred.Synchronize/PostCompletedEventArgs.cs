using System;
using System.ComponentModel;
using System.Threading;

namespace EasyDeferred.Synchronize
{
    public class PostCompletedEventArgs : AsyncCompletedEventArgs
    {
        public PostCompletedEventArgs(SendOrPostCallback callback, Exception error, object state)
            : base(error, false, state) {
            Callback = callback;
        }

        public SendOrPostCallback Callback { get; }
    }
}
