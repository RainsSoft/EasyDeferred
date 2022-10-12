using System;


namespace EasyDeferred.Core
{
	// <summary>
	/// A generic singleton
	/// </summary>
	/// <remarks>
	/// Although this class will allow it, don't try to do this: Singleton&lt; interface &gt;
	/// </remarks>
	/// <typeparam name="T">a class</typeparam>
	public class Singleton<T> : IDisposable where T : class, new()
	{
		public Singleton() {
			if (SingletonFactory.instance != null && !IntPtr.ReferenceEquals(this, SingletonFactory.instance)) {
				throw new Exception(String.Format("Cannot create instances of the {0} class. Use the static Instance property instead.", this.GetType().Name));
			}
		}

		~Singleton() {
			dispose(false);
		}

		virtual public bool Initialize(params object[] args) {
			return true;
		}

		public static T Instance {
			get {
				try {
					if (SingletonFactory.instance != null) {
						return SingletonFactory.instance;
					}
					lock (SingletonFactory.singletonLock) {
						SingletonFactory.instance = new T();
						return SingletonFactory.instance;
					}
				}
				catch ( /*TypeInitialization*/Exception) {
					throw new Exception(string.Format("Type {0} must implement a private parameterless constructor.", typeof(T)));
				}
			}
		}

		private class SingletonFactory
		{
			internal static object singletonLock = new object();

			static SingletonFactory() { }

			internal static T instance = new T();
		}

		public static void Destroy() {
			SingletonFactory.instance = null;
		}

		public static void Reinitialize() { }

		#region IDisposable Implementation

		#region isDisposed Property

		private bool _disposed = false;

		/// <summary>
		/// Determines if this instance has been disposed of already.
		/// </summary>
		protected bool IsDisposed { get { return _disposed; } set { _disposed = value; } }

		#endregion isDisposed Property

		/// <summary>
		/// Class level dispose method
		/// </summary>
		/// <remarks>
		/// When implementing this method in an inherited class the following template should be used;
		/// protected override void dispose( bool disposeManagedResources )
		/// {
		/// 	if ( !isDisposed )
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
		virtual protected void dispose(bool disposeManagedResources) {
			if (!_disposed) {
				if (disposeManagedResources) {
					Singleton<T>.Destroy();
				}

				// There are no unmanaged resources to release, but
				// if we add them, they need to be released here.
			}
			_disposed = true;
		}

		public void Dispose() {
			dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion IDisposable Implementation
	}
}
