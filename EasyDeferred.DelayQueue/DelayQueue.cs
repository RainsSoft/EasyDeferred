﻿using System;
using System.Threading;
using EasyDeferred.DelayQueue.Extensions;
namespace EasyDeferred.DelayQueue
{
    /// <summary>
    /// 延时队列任务项
    /// </summary>
    public interface IDelayItem : IComparable
    {
        /// <summary>
        /// 获取剩余延时
        /// </summary>
        /// <returns></returns>
        TimeSpan GetDelaySpan();
    }
    /// <summary>
    /// 延时队列，线程安全，参考java DelayQueue实现
    /// https://github.com/linys2333/DelayQueue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DelayQueue<T> where T : class, IDelayItem
    {
        [Obsolete()]
        public class CancellationToken
        {
            public static CancellationToken None = new CancellationToken();
            //internal ManualResetEvent WaitHandle = new ManualResetEvent(false);

            public bool IsCancellationRequested { get; private set; }

            //internal void ThrowIfCancellationRequested() {
            //    //throw new NotImplementedException();
            //}
        }
        readonly TimeSpan Timeout_InfiniteTimeSpan = TimeSpan.FromMilliseconds(-1);
        private readonly object _lock = new object();

        /// <summary>
        /// 有序列表
        /// </summary>
        private readonly SortedQueue<T> _sortedList = new SortedQueue<T>();

        /// <summary>
        /// 当前排队等待取元素的线程
        /// </summary>
        private Thread _waitThread = null;

        /// <summary>
        /// 队列当前元素数量
        /// </summary>
        public int Count {
            get {
                lock (_lock) {
                    return _sortedList.Count;
                }
            }
        }

        /// <summary>
        /// 队列是否为空
        /// </summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// 添加项
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryAdd(T item) {
            
            return TryAdd(item, Timeout_InfiniteTimeSpan, CancellationToken.None);
        }

        /// <summary>
        /// 添加项
        /// </summary>
        /// <param name="item"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryAdd(T item, CancellationToken cancelToken) {
            return TryAdd(item, Timeout_InfiniteTimeSpan, cancelToken);
        }

        /// <summary>
        /// 添加项
        /// </summary>
        /// <param name="item"></param>
        /// <param name="timeout">该方法执行超时时间</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryAdd(T item, TimeSpan timeout) {
            return TryAdd(item, timeout, CancellationToken.None);
        }

        /// <summary>
        /// 添加项
        /// </summary>
        /// <param name="item"></param>
        /// <param name="timeout">该方法执行超时时间</param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryAdd(T item, TimeSpan timeout, CancellationToken cancelToken) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            if (IsTimeout(timeout, cancelToken)) {
                throw new ArgumentException("Method execute timeout or cancelled");
            }

            if (!Monitor.TryEnter(_lock, timeout)) {
                return false;
            }

            if (cancelToken.IsCancellationRequested) {
                Monitor.Exit(_lock);
                return false;
            }

            try {
                if (_sortedList.TryAdd(item)) {
                    // 如果是首项，则唤醒就绪队列的首个线程准备获取锁
                    if (Peek() == item) {
                        _waitThread = null;
                        Monitor.Pulse(_lock);
                    }

                    return true;
                }

                return false;
            }
            finally {
                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// 取出首项，但不移除
        /// </summary>
        /// <returns></returns>
        public T Peek() {
            lock (_lock) {
                return _sortedList.FirstOrDefault();
            }
        }

        /// <summary>
        /// 取出首项，但不移除
        /// </summary>
        /// <returns></returns>
        public bool TryPeek(out T item) {
            item = Peek();
            return item != null;
        }

        /// <summary>
        /// 非阻塞获取项
        /// </summary>
        /// <returns></returns>
        public bool TryTakeNoBlocking(out T item) {
            lock (_lock) {
                item = Peek();
                if (item == null || item.GetDelaySpan() > TimeSpan.Zero) {
                    item = null;
                    return false;
                }
                return _sortedList.Remove(item);
            }
        }

        /// <summary>
        /// 取出项，如果未到期，则阻塞
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryTake(out T item) {
            return TryTake(out item, CancellationToken.None);
        }

        /// <summary>
        /// 取出项，如果未到期，则阻塞
        /// </summary>
        /// <param name="item"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public bool TryTake(out T item, CancellationToken cancelToken) {
            item = null;

            if (!Monitor.TryEnter(_lock)) {
                return false;
            }

            if (cancelToken.IsCancellationRequested) {
                Monitor.Exit(_lock);
                return false;
            }

            try {
                while (!cancelToken.IsCancellationRequested) {
                    // 当前没有项，阻塞等待
                    if (!TryPeek(out item)) {
                        Monitor.Wait(_lock);
                        continue;
                    }

                    // 如果已经到期，则出队
                    var delaySpan = item.GetDelaySpan();
                    if (delaySpan <= TimeSpan.Zero) {
                        return _sortedList.Remove(item);
                    }

                    // 移除引用，便于GC清理
                    item = null;

                    // 如果有其它线程也在等待，则阻塞等待
                    if (_waitThread != null) {
                        Monitor.Wait(_lock);
                        continue;
                    }

                    // 否则当前线程设为等待线程
                    var thisThread = Thread.CurrentThread;
                    _waitThread = thisThread;

                    try {
                        // 阻塞等待，如果有更早的项加入，会提前释放
                        // 否则等待delayMs时间，即当前项到期
                        // 注意，这里不能直接返回当前项，因为当前项可能被其它线程取出，所以要进入下一个循环获取
                        Monitor.Wait(_lock, delaySpan);
                        continue;
                    }
                    finally {
                        // 释放出来，让其它线程也可以获取
                        if (_waitThread == thisThread) {
                            _waitThread = null;
                        }
                    }
                }

                return false;
            }
            finally {
                // 当前线程已取到项，且还有剩余项，则唤醒其它就绪的线程
                if (_waitThread == null && _sortedList.Count > 0) {
                    Monitor.Pulse(_lock);
                }

                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// 取出项，如果未到期，则阻塞
        /// </summary>
        /// <param name="item"></param>
        /// <param name="timeout">该方法执行超时时间，注意，实际超时时间可能大于指定值</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryTake(out T item, TimeSpan timeout) {
            return TryTake(out item, timeout, CancellationToken.None);
        }

        /// <summary>
        /// 取出项，如果未到期，则阻塞
        /// </summary>
        /// <param name="item"></param>
        /// <param name="timeout">该方法执行超时时间，注意，实际超时时间可能大于指定值</param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public bool TryTake(out T item, TimeSpan timeout, CancellationToken cancelToken) {
            item = null;

            if (IsTimeout(timeout, cancelToken)) {
                throw new ArgumentException("Method execute timeout or cancelled");
            }

            if (!Monitor.TryEnter(_lock, timeout)) {
                return false;
            }

            if (IsTimeout(timeout, cancelToken)) {
                Monitor.Exit(_lock);
                return false;
            }

            try {
                while (!IsTimeout(timeout, cancelToken)) {
                    // 当前没有项，阻塞等待
                    if (!TryPeek(out item)) {
                        timeout = MonitorExt.Wait(_lock, timeout);
                        continue;
                    }

                    // 如果已经到期，则出队
                    var delaySpan = item.GetDelaySpan();
                    if (delaySpan <= TimeSpan.Zero) {
                        return _sortedList.Remove(item);
                    }

                    // 移除引用，便于GC清理
                    item = null;

                    // 如果有其它线程也在等待，则阻塞等待
                    if (timeout < delaySpan || _waitThread != null) {
                        timeout = MonitorExt.Wait(_lock, timeout);
                        continue;
                    }

                    // 否则当前线程设为等待线程
                    var thisThread = Thread.CurrentThread;
                    _waitThread = thisThread;

                    try {
                        // 阻塞等待，如果有更早的项加入，会提前释放
                        // 否则等待delayMs时间，即当前项到期
                        // 注意，这里不能直接返回当前项，因为当前项可能被其它线程取出，所以要进入下一个循环获取
                        var timeLeft = MonitorExt.Wait(_lock, delaySpan);
                        timeout -= delaySpan - timeLeft;
                        continue;
                    }
                    finally {
                        // 释放出来，让其它线程也可以获取
                        if (_waitThread == thisThread) {
                            _waitThread = null;
                        }
                    }
                }

                return false;
            }
            finally {
                // 当前线程已取到项，且还有剩余项，则唤醒其它就绪的线程
                if (_waitThread == null && _sortedList.Count > 0) {
                    Monitor.Pulse(_lock);
                }

                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear() {
            lock (_lock) {
                _sortedList.Clear();
            }
        }

        /// <summary>
        /// 是否超时
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        private bool IsTimeout(TimeSpan timeout, CancellationToken cancelToken) {
            return (timeout <= TimeSpan.Zero && timeout != Timeout_InfiniteTimeSpan) ||
                   cancelToken.IsCancellationRequested;
        }
    }


    /// <summary>
    /// 默认延时对象
    /// </summary>
    public class DelayItem<T> : IDelayItem
    {
        /// <summary>
        /// 过期时间戳，绝对时间
        /// </summary>
        public readonly long TimeoutMs;

        /// <summary>
        /// 延时对象
        /// </summary>
        public readonly T Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeoutSpan">过期时间，相对时间</param>
        /// <param name="item">延时对象</param>
        public DelayItem(TimeSpan timeoutSpan, T item) {
            TimeoutMs = (long)timeoutSpan.TotalMilliseconds + GetTimestamp();
            Item = item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeoutMs">过期时间戳，绝对时间</param>
        /// <param name="item">延时对象</param>
        public DelayItem(long timeoutMs, T item) {
            TimeoutMs = timeoutMs;
            Item = item;
        }

        public int CompareTo(object obj) {
            if (obj == null) {
                return 1;
            }

            if (obj is DelayItem<T> ) {
                DelayItem<T> value = obj as DelayItem<T>;
                return TimeoutMs.CompareTo(value.TimeoutMs);
            }

            throw new ArgumentException($"Object is not a {nameof(DelayItem<T>)}");
        }

        public TimeSpan GetDelaySpan() {
            var delayMs = Math.Max(TimeoutMs - GetTimestamp(), 0);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        /// <summary>
        /// 获取当前时间戳
        /// </summary>
        /// <returns></returns>
        private long GetTimestamp() {
            return ToUnixTimeMilliseconds(new DateTimeOffset(DateTime.Now));
        }
        //public long ToUnixTimeSeconds() {
        //    // Truncate sub-second precision before offsetting by the Unix Epoch to avoid
        //    // the last digit being off by one for dates that result in negative Unix times.
        //    //
        //    // For example, consider the DateTimeOffset 12/31/1969 12:59:59.001 +0
        //    //   ticks            = 621355967990010000
        //    //   ticksFromEpoch   = ticks - UnixEpochTicks                   = -9990000
        //    //   secondsFromEpoch = ticksFromEpoch / TimeSpan.TicksPerSecond = 0
        //    //
        //    // Notice that secondsFromEpoch is rounded *up* by the truncation induced by integer division,
        //    // whereas we actually always want to round *down* when converting to Unix time. This happens
        //    // automatically for positive Unix time values. Now the example becomes:
        //    //   seconds          = ticks / TimeSpan.TicksPerSecond = 62135596799
        //    //   secondsFromEpoch = seconds - UnixEpochSeconds      = -1
        //    //
        //    // In other words, we want to consistently round toward the time 1/1/0001 00:00:00,
        //    // rather than toward the Unix Epoch (1/1/1970 00:00:00).
        //    long seconds = UtcDateTime.Ticks / TimeSpan.TicksPerSecond;
        //    return seconds - UnixEpochSeconds;
        //}
        /// <summary>
        /// Convert Ticks to Unix Timestamp
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        //public static long ToUnixTimestamp(long time) {
        //    return (time - 621355968000000000) / TimeSpan.TicksPerMillisecond;
        //}
        public long ToUnixTimeMilliseconds(DateTimeOffset UtcDateTime) {
            return UtcDateTime.Ticks / 10000L - 62135596800000L;
        }
    }

}