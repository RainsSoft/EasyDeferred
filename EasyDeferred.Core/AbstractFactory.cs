using System;
using System.Collections.Generic;


namespace EasyDeferred.Core
{
	/// <summary>
	/// Abstract factory class implementation. Provides a basic Factory
	/// implementation that can be overriden by derivitives
	/// </summary>
	/// <typeparam name="T">The Type to instantiate</typeparam>
	public class AbstractFactory<T>  where T : class
	{
		private static readonly List<T> _instances = new List<T>();

		#region Implementation of IAbstractFactory<T>

		/// <summary>
		/// The factory type.
		/// </summary>
		virtual public string Type { get { return typeof(T).Name; } protected set { throw new NotImplementedException(); } }

		/// <summary>
		/// Creates a new object.
		/// </summary>
		/// <param name="name">Name of the object to create</param>
		/// <returns>
		/// An object created by the factory. The type of the object depends on
		/// the factory.
		/// </returns>
		virtual public T CreateInstance(string name) {
			T instance = null;
			try {
				instance= (T)Activator.CreateInstance(typeof(T));
				_instances.Add(instance);				
			}
			catch (Exception e) {
				//必须包含无参数构造函数
				LogManager.Instance.Write("Failed to create instance of {0} of type {0} from assembly", typeof(T).Name);
				LogManager.Instance.Write(e.Message);
			}			
			return instance;
		}	

		/// <summary>
		/// Destroys an object which was created by this factory.
		/// </summary>
		/// <param name="obj">the object to destroy</param>
		virtual public void DestroyInstance(ref T obj) {
			_instances.Remove(obj);
			obj = null;
		}

		#endregion Implementation of IAbstractFactory<T>
	}
}
