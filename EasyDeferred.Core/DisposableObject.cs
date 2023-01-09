using System;

namespace EasyDeferred.Core
{
    public abstract class DisposableObject : IDisposable
    {
        //
        protected DisposableObject() {
            //IsDisposed = false;
#if DEBUG
            var stackTrace = string.Empty;
#if !(SILVERLIGHT || XBOX || XBOX360 || WINDOWS_PHONE || ANDROID) && AXIOM_ENABLE_LOG_STACKTRACE
			stackTrace = Environment.StackTrace;
#endif
            //ObjectManager.Instance.Add(this, stackTrace);
#endif
        }

        //
        ~DisposableObject() {
            if (!IsDisposed) {
                dispose(false);
            }
        }
       
        #region IDisposable Implementation

        /// <summary>
        /// Determines if this instance has been disposed of already.
        /// </summary>       
        public bool IsDisposed { get;private set; }

        /// <summary>
        /// Class level dispose method
        /// </summary>
        /// <remarks>
        /// When implementing this method in an inherited class the following template should be used;
        /// protected override void dispose( bool disposeManagedResources )
        /// {
        /// 	if ( !IsDisposed )
        /// 	{
        /// 		if ( disposeManagedResources )
        /// 		{
        /// 			// Dispose managed resources.
        /// 		}
        ///
        /// 		// There are no unmanaged resources to release, but
        /// 		// if we add them, they need to be released here.
        /// 	}
        ///
        /// 	// If it is available, make the call to the
        /// 	// base class's Dispose(Boolean) method
        /// 	base.dispose( disposeManagedResources );
        /// }
        /// </remarks>
        /// <param name="disposeManagedResources">True if Unmanaged resources should be released.</param>
        protected virtual void dispose(bool disposeManagedResources) {
            //释放非托管资源           
            if (!IsDisposed) {
                if (disposeManagedResources) {
                    // Dispose managed resources.
#if DEBUG
                    // ObjectManager.Instance.Remove(this);
#endif
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }
            IsDisposed = true;
            //释放非托管资源 
            if (disposeManagedResources) {
                System.Threading.Interlocked.Exchange(ref _isDisposed, 1);
                //GC.SuppressFinalize(this);
            }
        }
        private int _isDisposed;
        /// <summary>
        /// 检查对象是否已被显示释放了
        /// </summary>
        protected void CheckDisposed() {
            if (_isDisposed == 1) {
                throw new Exception(string.Format("The {0} object has be disposed.", this.GetType().Name));
            }
        }

        public void Dispose() {
            dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Implementation
    };

  
}
