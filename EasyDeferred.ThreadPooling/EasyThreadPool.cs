// *************************************************************************** 
// This is free and unencumbered software released into the public domain.
// 
// Anyone is free to copy, modify, publish, use, compile, sell, or
// distribute this software, either in source code form or as a compiled
// binary, for any purpose, commercial or non-commercial, and by any
// means.
// 
// In jurisdictions that recognize copyright laws, the author or authors
// of this software dedicate any and all copyright interest in the
// software to the public domain. We make this dedication for the benefit
// of the public at large and to the detriment of our heirs and
// successors. We intend this dedication to be an overt act of
// relinquishment in perpetuity of all present and future rights to this
// software under copyright law.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// 
// For more information, please refer to <http://unlicense.org>
// ***************************************************************************

//using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Text;
using EasyDeferred.Threading;
using EasyDeferred.RSG;

namespace EasyDeferred.ThreadPooling
{

    /// <summary>
    /// 此类实现线程池。它缓冲所有的工作项。
    ///     This class implements a thread-pool. It buffers all the work items, which regrettably have to be classes, and
    ///     reuses them. It only cleans them up at
    ///     shutdown.
    /// </summary>
    public partial class EasyThreadPool : IDisposable
    {
        /// <summary>
        ///     The form of the callback delegate that carries the payload of the workItem.
        /// </summary>
        internal delegate object WorkItemCallback(object state);

        /// <summary>
        ///     The callback function that should be called when the work item is finished.
        /// </summary>
        public delegate void CallbackFunction();

        private bool isDisposeDoneWorkItemsAutomatically;
        private readonly Queue<EasySingleThreadRunner> threads;
        private readonly ConcurrentQueue<EasySingleThreadRunner> threadsIdle;
        private int threadsWorking;
        private readonly ConcurrentQueue<EasyWorkItem> workItemQueue = new ConcurrentQueue<EasyWorkItem>();

        private readonly ConcurrentQueue<EasyWorkItem> returnedWorkItems = new ConcurrentQueue<EasyWorkItem>();
        private bool shutDownSignaled;
        private readonly object lockObjectShutDownSignaled = new object();

        public int NumberOfThreads { get; }

        /// <summary>
        /// 获取或设置一个值，该值指示此实例是否设置为自动销毁已完成的工作项。注意：如果启用此选项，每个工作项的dispose方法将在完成后立即调用，从而销毁引用。显然，这只是在将操作接口（无返回值）与<c>WaitForEveryWorkerIdle</c>-方法一起使用时的可行选项。
        ///     Gets or sets a value indicating whether this instance is set to dispose done work items automatically. Beware: If
        ///     you enable this option, the
        ///     dispose method of each work item is called immediately after its completion, thus destroying the reference. This
        ///     obviously is only an viable option
        ///     when using the action-interface (no return values) together with the <c>WaitForEveryWorkerIdle</c>-Method.
        /// </summary>
        /// <value>
        ///     <c>true</c>如果此实例是自动销毁已完成的工作项；否则 if this instance is dispose done work items automatically; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposeDoneWorkItemsAutomatically {
            get {
                Thread.MemoryBarrier();
                return isDisposeDoneWorkItemsAutomatically;
            }
            set {
                isDisposeDoneWorkItemsAutomatically = value;
                Thread.MemoryBarrier();
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyThreadPool" /> class.
        /// </summary>
        public EasyThreadPool(string threadsNamePrefix):
            this(threadsNamePrefix, ((2 % Environment.ProcessorCount) + 1) * Environment.ProcessorCount) { 
        }
        public EasyThreadPool(string threadsNamePrefix, int numberOfThreads) {           
            NumberOfThreads = numberOfThreads;
            threads = new Queue<EasySingleThreadRunner>();
            threadsIdle = new ConcurrentQueue<EasySingleThreadRunner>();

            // allocate threads...
            for (var i = 0; i < NumberOfThreads; i++) {
                var singleThreadRunner = new EasySingleThreadRunner(this);
                singleThreadRunner.Thread = new Thread(singleThreadRunner.DoWork);
                singleThreadRunner.Thread.Name = threadsNamePrefix + "-EasyTd#" +singleThreadRunner.Thread.ManagedThreadId ;//(i + 1);
                singleThreadRunner.Thread.IsBackground = true;

                threads.Enqueue(singleThreadRunner);
                threadsIdle.Enqueue(singleThreadRunner);

                singleThreadRunner.Thread.Start();
            }
        }

        /// <summary>
        ///     Clears the work item queue.
        /// </summary>
        public void ClearWorkItemQueue() {
            EasyWorkItem wi;
            while (workItemQueue.TryDequeue(out wi)) { }
        }

        /// <summary>
        ///     The number of items that are still to be processed.
        /// </summary>
        /// <returns></returns>
        public int NumberOfItemsLeft() {
            Thread.MemoryBarrier();
            return workItemQueue.Count;
        }

        /// <summary>
        ///     The number of items that are done processing and returned.
        /// </summary>
        /// <returns></returns>
        public int NumberOfItemsDone() {
            Thread.MemoryBarrier();
            return returnedWorkItems.Count;
        }

        /// <summary>
        ///     Enqueues the work item.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        internal void EnqueueWorkItemInternal(EasyWorkItem workItem) {
            // look for an idle worker...
            EasySingleThreadRunner singleThreadRunner;
            if (threadsIdle.TryDequeue(out singleThreadRunner)) {
                // hand over the work item...
                workItem.SingleThreadRunner = singleThreadRunner;
                Interlocked.Increment(ref threadsWorking);
                singleThreadRunner.SignalWork(workItem);
            }
            else {
                // just enqueue the item since all workers are busy...
                workItemQueue.Enqueue(workItem);
            }
        }

        /// <summary>
        ///     Dequeues the work item.
        /// </summary>
        /// <param name="singleThreadRunner">The single thread runner.</param>
        /// <param name="isGetNewOne">
        ///     if set to <c>true</c> [is get new one].
        /// </param>
        /// <param name="returnedWorkItem">The returned work item.</param>
        /// <returns>
        ///     <see langword="true" />, if a work item has been
        ///     successfully dequeued. <see langword="false" /> otherwise.
        /// </returns>
        internal EasyWorkItem DequeueWorkItemInternal(EasySingleThreadRunner singleThreadRunner, bool isGetNewOne,
            EasyWorkItem returnedWorkItem = null) {
            if (returnedWorkItem != null) {
                returnedWorkItems.Enqueue(returnedWorkItem);
            }

            if (!shutDownSignaled && isGetNewOne) {
                EasyWorkItem workItem;
                if (workItemQueue.TryDequeue(out workItem)) {
                    workItem.SingleThreadRunner = singleThreadRunner;
                    return workItem;
                }
            }

            // If we are here, there is no more work to do left...
            // The worker has to be set to idle...
            Interlocked.Decrement(ref threadsWorking);
            threadsIdle.Enqueue(singleThreadRunner);
            return null;
        }

        private EasyWorkItem GetWorkItem(CallbackFunction asyncCallback) {
            EasyWorkItem workItem;
            if (!returnedWorkItems.TryDequeue(out workItem)) {
                workItem = new EasyWorkItem();
                workItem.WorkItemStateTypeless = new EasyWorkItemStateTypeless(workItem);
            }

            workItem.SingleThreadRunner = null;
            workItem.IsCompleted = false;
            workItem.Result = null;
            workItem.AsyncCallback = asyncCallback;
            return workItem;
        }

        /// <summary>
        ///     Returns the work item.
        /// </summary>
        /// <param name="returnedWorkItem">The returned work item.</param>
        public void ReturnWorkItem(EasyWorkItem returnedWorkItem) {
            returnedWorkItems.Enqueue(returnedWorkItem);
        }

        /// <summary>
        ///     Waits for the queue to empty.
        /// </summary>
        public void WaitForEveryWorkerIdle() {
            // A spinWait ensures a yield from time to time, forcing the CPU to do a context switch, thus allowing other processes to finish.
            var spinWait = new SpinWait();
            while (threadsWorking > 0) {
                Thread.MemoryBarrier();
                spinWait.SpinOnce();
            }
        }

        /// <summary>
        ///     Clears the work item cache of all returned and "to be reused" work items returned via the dispose-method of a
        ///     work-item-state-struct.
        /// </summary>
        public void ClearWorkItemCache() {
            EasyWorkItem w;
            while (returnedWorkItems.TryDequeue(out w)) { }
        }

        /// <summary>
        ///     Aborts all active threads.
        /// </summary>
        private void ShutDown() {
            // First, we want to close. So stop dealing new work items...
            lock (lockObjectShutDownSignaled) {
                shutDownSignaled = true;
            }

            // signal the shutdown-command to all workers...
            if (threads.Count > 0) {
                foreach (var thread in threads) {
                    thread.SignalShutDown();
                }
            }
        }
        /// <summary>
        /// Shutdown the thread pool. It cannot be restarted afterward.
        /// Current running jobs end normaly, pending jobs are not processed.
        /// </summary>
        public void Dispose() {
            ShutDown();
        }
        /// <summary>
        ///     Pauses all active threads.
        /// </summary>
        public void Sleep() {
            // signal the pause-command to all workers...
            if (threads.Count > 0) {
                foreach (var thread in threads) {
                    thread.SignalPause();
                }
            }
        }

        /// <summary>
        ///     Resumes all active threads.
        /// </summary>
        public void Wakeup() {
            // signal the resume-command to all workers...
            if (threads.Count > 0) {
                foreach (var thread in threads) {
                    thread.SignalResume();
                }
            }
        }
    }
    public partial class EasyThreadPool
    {
        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<V>(Func<V> workerFunction, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { };
            workItem.Delegate = delegateInputParameters => { return workerFunction.Invoke(); };

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<T1, V>(Func<T1, V> workerFunction, T1 arg1,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);

            workItem.DelegateInputParameters = new object[] { arg1 };
            workItem.Delegate = delegateInputParameters => workerFunction.Invoke(arg1);

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<T1, T2, V>(Func<T1, T2, V> workerFunction, T1 arg1, T2 arg2,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2 };
            workItem.Delegate = delegateInputParameters => workerFunction.Invoke(arg1, arg2);

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<T1, T2, T3, V>(Func<T1, T2, T3, V> workerFunction, T1 arg1, T2 arg2,
            T3 arg3,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3 };
            workItem.Delegate = delegateInputParameters => workerFunction.Invoke(arg1, arg2, arg3);

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <typeparam name="T4">The type of the 4.</typeparam>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="arg4">The arg4.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<T1, T2, T3, T4, V>(Func<T1, T2, T3, T4, V> workerFunction, T1 arg1,
            T2 arg2,
            T3 arg3, T4 arg4, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3, arg4 };
            workItem.Delegate = delegateInputParameters => workerFunction.Invoke(arg1, arg2, arg3, arg4);

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         ManualResetEvent internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <typeparam name="T4">The type of the 4.</typeparam>
        /// <typeparam name="T5">The type of the 5.</typeparam>
        /// <typeparam name="V">The type of the result.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="arg4">The arg4.</param>
        /// <param name="arg5">The arg5.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState<V> EnqueueWorkItem<T1, T2, T3, T4, T5, V>(Func<T1, T2, T3, T4, T5, V> workerFunction,
            T1 arg1,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3, arg4, arg5 };
            workItem.Delegate = delegateInputParameters => workerFunction.Invoke(arg1, arg2, arg3, arg4, arg5);

            var workItemState = new EasyWorkItemState<V>(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }
    }
    public partial class EasyThreadPool
    {
        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem(Action workerFunction, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke();
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem<T1>(Action<T1> workerFunction, T1 arg1,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1 };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke(arg1);
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem<T1, T2>(Action<T1, T2> workerFunction, T1 arg1, T2 arg2,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2 };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke(arg1, arg2);
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem<T1, T2, T3>(Action<T1, T2, T3> workerFunction, T1 arg1, T2 arg2, T3 arg3,
            CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3 };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke(arg1, arg2, arg3);
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <typeparam name="T4">The type of the 4.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="arg4">The arg4.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem<T1, T2, T3, T4>(Action<T1, T2, T3, T4> workerFunction, T1 arg1, T2 arg2,
            T3 arg3,
            T4 arg4, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3, arg4 };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke(arg1, arg2, arg3, arg4);
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }

        /// <summary>
        ///     Enqueues the work item. Returns a work-item-state-struct as a handle to the operation that is just about, or
        ///     queued, to be executed.
        ///     <para>Information on the returned struct... </para>
        ///     <para>
        ///         Call the result property on this struct to trigger a lock, thus blocking your current thread until the function
        ///         has executed.
        ///     </para>
        ///     <para>
        ///         Call Dispose on that returned item to automatically reuse the data-structure behind each work-item in order to
        ///         avoid
        ///         garbage-collector-cycles.
        ///     </para>
        ///     <para>
        ///         Use its <c>IsCompleted</c>-Property to verify within a monitor if your method has finished executing. The
        ///         <c>IsCompleted</c>-Property actually triggers a WaitOne(1) on a
        ///         <see cref="ManualResetEvent" /> internally thus returning almost instantly.
        ///     </para>
        /// </summary>
        /// <typeparam name="T1">The type of the 1.</typeparam>
        /// <typeparam name="T2">The type of the 2.</typeparam>
        /// <typeparam name="T3">The type of the 3.</typeparam>
        /// <typeparam name="T4">The type of the 4.</typeparam>
        /// <typeparam name="T5">The type of the 5.</typeparam>
        /// <param name="workerFunction">The worker function.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="arg3">The arg3.</param>
        /// <param name="arg4">The arg4.</param>
        /// <param name="arg5">The arg5.</param>
        /// <param name="asyncCallback">The async callback.</param>
        /// <returns>
        ///     Returns a work-item-state-struct as a handle to the operation that is just about, or queued, to be executed.
        /// </returns>
        public IEasyWorkItemState EnqueueWorkItem<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> workerFunction, T1 arg1,
            T2 arg2,
            T3 arg3, T4 arg4, T5 arg5, CallbackFunction asyncCallback = null) {
            var workItem = GetWorkItem(asyncCallback);
            workItem.DelegateInputParameters = new object[] { arg1, arg2, arg3, arg4, arg5 };
            workItem.Delegate = delegateInputParameters => {
                workerFunction.Invoke(arg1, arg2, arg3, arg4, arg5);
                return null;
            };

            var workItemState = new EasyWorkItemState(workItem.WorkItemStateTypeless);
            EnqueueWorkItemInternal(workItem);
            return workItemState;
        }
    }
    /// <summary>
    ///     This is a frame for a single thread to run the defined payload.
    /// </summary>
    public class EasySingleThreadRunner
    {
        private bool signalClose;
        private bool signalWork;

        private EasyWorkItem currentWorkItem;

        public EasyThreadPool ThreadPool { get; set; }
        public Thread Thread { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasySingleThreadRunner" /> class.
        /// </summary>
        public EasySingleThreadRunner() {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasySingleThreadRunner" /> class.
        /// </summary>
        /// <param name="threadPool">The thread pool.</param>
        public EasySingleThreadRunner(EasyThreadPool threadPool) {
            ThreadPool = threadPool;
        }

        /// <summary>
        ///     Does the work reacting on and setting various signals.
        /// </summary>
        public void DoWork() {
            var spinWait = new SpinWait();
            while (!signalClose) {
                if (signalWork) {
                    while (currentWorkItem != null && !signalClose) {
                        // Start the payload.
                        currentWorkItem.Result = currentWorkItem.Delegate(currentWorkItem.DelegateInputParameters);

                        // Set the work item to completed.
                        currentWorkItem.IsCompleted = true;

                        // Call the async callback - method, if available.
                        if (currentWorkItem.AsyncCallback != null) {
                            currentWorkItem.AsyncCallback.Invoke();
                        }

                        // Dequeue the next work item.
                        if (ThreadPool.IsDisposeDoneWorkItemsAutomatically) {
                            // Return the work item automatically for reuse, if preferred.
                            currentWorkItem = ThreadPool.DequeueWorkItemInternal(this, signalWork, currentWorkItem);
                        }
                        else {
                            currentWorkItem = ThreadPool.DequeueWorkItemInternal(this, signalWork);
                        }
                    }
                    // The worker has no more work or is paused.
                    signalWork = false;
                }
                else {
                    spinWait.SpinOnce();
                }
            }
            // The thread is dead.
            signalClose = false;
        }

        /// <summary>
        ///     Signals this instance to immediately start doing some work.
        /// </summary>
        public void SignalWork(EasyWorkItem workItemToProcess) {
            // Wait for the main loop to be not busy before changing the current workItem.
            var spinWait = new SpinWait();
            while (signalWork) {
                spinWait.SpinOnce();
                Thread.MemoryBarrier();
            }
            Interlocked.Exchange(ref currentWorkItem, workItemToProcess);
            signalWork = true;
            Thread.MemoryBarrier();
        }

        /// <summary>
        ///     Signals the workers to close.
        /// </summary>
        public void SignalShutDown() {
            signalClose = true;
        }

        /// <summary>
        ///     Signals the workers to pause.
        /// </summary>
        public void SignalPause() {
            Thread.MemoryBarrier();
            signalWork = false;
            Thread.MemoryBarrier();
        }

        /// <summary>
        ///     Signals the workers to resume.
        /// </summary>
        public void SignalResume() {
            Thread.MemoryBarrier();
            signalWork = true;
            Thread.MemoryBarrier();
        }

        /// <summary>
        ///     Aborts this instance.
        /// </summary>
        public void Abort() {
            Thread.Abort();
        }
    }
    /// <summary>
    ///     This is an implementation of the <c>IWorkItemStateTypeless</c> interface.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public struct EasyWorkItemState<T> : IEasyWorkItemState<T>
    {
        private bool disposed;
        private readonly EasyWorkItemStateTypeless workItemStateTypeless;

        /// <summary>
        ///     Gets a value indicating whether this instance is completed gracefully.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is completed gracefully; otherwise, <c>false</c>.
        /// </value>
        public bool IsStopped => workItemStateTypeless.IsStopped;

        /// <summary>
        ///     Gets or sets the result.
        ///     等待运行结果
        ///     注意控制台取值死锁问题
        /// </summary>
        /// <value>The result.</value>
        public T Result => (T)workItemStateTypeless.Result;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyWorkItemState{TResult}" /> class.
        /// </summary>
        /// <param name="workItemStateTypeless">State of the work item.</param>
        public EasyWorkItemState(EasyWorkItemStateTypeless workItemStateTypeless) {
            this.workItemStateTypeless = workItemStateTypeless;
            disposed = false;
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and
        ///     unmanaged resources; <c>false</c> to release only unmanaged
        ///     resources.
        /// </param>
        public void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    workItemStateTypeless.WorkItem.SingleThreadRunner.ThreadPool.ReturnWorkItem(
                        workItemStateTypeless.WorkItem);
                }
                disposed = true;
            }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing,
        ///     releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
        }
    }

    /// <summary>
    ///     This is an implementation of the <c>IWorkItemStateTypeless</c> interface.
    /// </summary>
    public struct EasyWorkItemState : IEasyWorkItemState
    {
        private bool disposed;
        private readonly EasyWorkItemStateTypeless workItemStateTypeless;

        /// <summary>
        ///     Gets a value indicating whether this instance is completed gracefully.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is completed gracefully; otherwise, <c>false</c>.
        /// </value>
        public bool IsStopped => workItemStateTypeless.IsStopped;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyWorkItemState{TResult}" /> class.
        /// </summary>
        /// <param name="workItemStateTypeless">State of the work item.</param>
        public EasyWorkItemState(EasyWorkItemStateTypeless workItemStateTypeless) {
            this.workItemStateTypeless = workItemStateTypeless;
            disposed = false;
        }

        /// <summary>
        ///     Is a blocking operation.
        ///     Waits for the work item to finish.
        ///     等待运行结果
        ///     注意控制台取值死锁问题
        /// </summary>
        /// <value>The result.</value>
        public void Result() {
#pragma warning disable 168
            var o = workItemStateTypeless.Result;
#pragma warning restore 168
        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        ///     <c>true</c> to release both managed and
        ///     unmanaged resources; <c>false</c> to release only unmanaged
        ///     resources.
        /// </param>
        public void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    workItemStateTypeless.WorkItem.SingleThreadRunner.ThreadPool.ReturnWorkItem(
                        workItemStateTypeless.WorkItem);
                }
                disposed = true;
            }
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing,
        ///     releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
        }
        public override string ToString() {
            return string.Format("EasyWorkItemState IsStopped:{0} disposed:{1} ", workItemStateTypeless?.IsStopped,disposed);
        }
    }
    /// <summary>
    ///     This is a wrapper-class for a work item.
    /// </summary>
    public class EasyWorkItem
    {
        private object result;

        public bool IsCompleted { get; set; }
        internal EasyThreadPool.WorkItemCallback Delegate { get; set; }
        public object DelegateInputParameters { get; set; }
        public EasyWorkItemStateTypeless WorkItemStateTypeless { get; set; }
        public EasySingleThreadRunner SingleThreadRunner { get; set; }

        /// <summary>
        ///     Gets or sets the result.
        ///     等待运行结果
        ///     注意控制台取值死锁问题
        /// </summary>
        /// <value>The result.</value>
        public object Result {
            get {
                // SpinWait for the workItem to finish.
                var spinWait = new SpinWait();
                while (!IsCompleted) {
                    spinWait.SpinOnce();
                    Thread.MemoryBarrier();
                }
                return result;
            }
            set { result = value; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyWorkItem" /> class.
        /// </summary>
        internal EasyWorkItem() {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyWorkItem" /> class.
        /// </summary>
        internal EasyWorkItem(EasyThreadPool.WorkItemCallback functionDelegate, object delegateInputParameters) {
            Delegate = functionDelegate;
            DelegateInputParameters = delegateInputParameters;
            WorkItemStateTypeless = new EasyWorkItemStateTypeless(this);
        }

        /// <summary>
        ///     Gets or sets the async callback.
        /// </summary>
        /// <value>The async callback.</value>
        public EasyThreadPool.CallbackFunction AsyncCallback { get; set; }
    }
    /// <summary>
    ///     The state and the remote control for a work item.
    /// </summary>
    public class EasyWorkItemStateTypeless : IEasyWorkItemStateTypeless
    {
        public EasyWorkItem WorkItem { get; set; }

        /// <summary>
        ///     Gets a value indicating whether this instance is completed gracefully.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is completed gracefully; otherwise, <c>false</c>.
        /// </value>
        public bool IsStopped => WorkItem.IsCompleted;

        /// <summary>
        ///     Gets the result.
        ///     等待运行结果
        ///     注意控制台取值死锁问题
        /// </summary>
        /// <value>The result.</value>
        public object Result => WorkItem.Result;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EasyWorkItemStateTypeless" /> class.
        /// </summary>
        /// <param name="workItem">The work item.</param>
        public EasyWorkItemStateTypeless(EasyWorkItem workItem) {
            WorkItem = workItem;
        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing,
        ///     releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
        }
    }

    /// <summary>
    ///     This is an interface for a thread-workItem.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public interface IEasyWorkItemState<out T> : IDisposable
    {
        /// <summary>
        ///     Gets a value indicating whether this instance is completed.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is completed; otherwise, <c>false</c>.
        /// </value>
        bool IsStopped { get; }

        /// <summary>
        ///     Is a blocking operation.
        ///     Gets the result.
        /// </summary>
        /// <value>The result.</value>
        T Result { get; }
    }

    /// <summary>
    ///     This is an interface for a thread-workItem.
    /// </summary>
    public interface IEasyWorkItemState : IDisposable
    {
        /// <summary>
        ///     Gets a value indicating whether this instance is completed.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is completed; otherwise, <c>false</c>.
        /// </value>
        bool IsStopped { get; }

        /// <summary>
        ///     Is a blocking operation.
        ///     Waits for the work item to finish.
        /// </summary>
        /// <value>The result.</value>
        void Result();
    }
    /// <summary>
    ///     An interface for the type-less work-item-state.
    /// </summary>
    public interface IEasyWorkItemStateTypeless : IEasyWorkItemState<object>
    {
    }


}