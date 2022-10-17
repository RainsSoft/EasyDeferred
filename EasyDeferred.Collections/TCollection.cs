using System;
using System.Collections.Generic;
using System.Collections;

namespace EasyDeferred.Collections
{
	/// <summary>
	///	Serves as a basis for strongly typed collections .
	///	用作强类型集合的基础。
	/// </summary>
	public class TCollection<T> : Dictionary<string, T>
	{
		#region Constants

		private const int InitialCapacity = 60;

		#endregion Constants

		#region Readonly & Static Fields

		protected static int nextUniqueKeyCounter;

		protected string typeName;

		#endregion Readonly & Static Fields

		#region Fields

		protected Object parent;

		#endregion Fields

		#region Constructors

		/// <summary>
		///
		/// </summary>
		public TCollection() {
			this.parent = null;
			this.typeName = typeof(T).Name;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="parent"></param>
		protected TCollection(Object parent)
			: base(InitialCapacity) {
			this.parent = parent;
			this.typeName = typeof(T).Name;
		}

		public TCollection(TCollection<int> copy)
			: base((IDictionary<string, T>)copy) { }

		#endregion Constructors

		#region Instance Methods

		/// <summary>
		///	Adds an unnamed object to the <see cref="TCollection{T}"/> and names it manually.
		/// </summary>
		/// <param name="item">The object to add.</param>
		virtual public void Add(T item) {
			Add(typeName + (nextUniqueKeyCounter++), item);
		}

		/// <summary>
		/// Adds multiple items from a specified source collection
		/// </summary>
		/// <param name="from"></param>
		virtual public void AddRange(IDictionary<string, T> source) {
			foreach (KeyValuePair<string, T> entry in source) {
				this.Add(entry.Key, entry.Value);
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through the <see cref="TCollection{T}"/>.
		/// </summary>
		/// <returns>An <see cref="IEnumerator{T}"/> for the <see cref="TCollection{T}"/> values.</returns>
		virtual new public IEnumerator GetEnumerator() {
			return Values.GetEnumerator();
		}

		public T this[int index] {
			get {
				foreach (T item in Values) {
					if (index == 0) {
						return item;
					}
					index--;
				}
				return default(T);
			}
		}

		new public T this[string key] {
			get { return base[key]; }
			set {
				if (this.ContainsKey(key)) {
					this.Remove(key);
				}
				this.Add(key, value);
			}
		}

		#endregion Instance Methods
	}

}
