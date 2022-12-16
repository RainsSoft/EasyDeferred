using System.Collections.Generic;
using EasyDeferred.RSG;

namespace EasyDeferred.DelayQueue.Extensions
{
    /// <summary>
    /// 有序队列，非线程安全
    /// </summary>
    public class SortedQueue<T>
    {
        private readonly SortedSet<T> _list = new SortedSet<T>(new SortComparer());

        public int Count => _list.Count;

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryAdd(T item)
        {
            return _list.Add(item);
        }

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            return _list.Remove(item);
        }

        public T FirstOrDefault()
        {
            return FirstOrDefault(_list);
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            _list.Clear();
        }
        T FirstOrDefault(SortedSet<T> list) {
            if (list != null && list.Count > 0) {
                var ve = list.GetEnumerator();
                ve.MoveNext();
                return ve.Current;
            }
            return default(T);
        }
        private sealed class SortComparer : Comparer<T>
        {
            public override int Compare(T x, T y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                // 先用默认比较器比较，如果相等，再用HashCode比较
                var result = Default.Compare(x, y);
                if (result == 0)
                {
                    // 如果HashCode相等，表示同一个对象，不允许重复添加
                    result = x.GetHashCode().CompareTo(y.GetHashCode());
                }

                return result;
            }
        }
    }
}