
namespace RSG.Unity {
#if UNITY
using System;
using System.Collections;
using UnityEngine;
using IRobotQ;
    
    /// <summary>
    /// 内部自动开协程执行
    /// </summary>
    public class UnityPromise : Promise {
        public readonly IEnumerator Coroutine;
        public string Error;
        /// <summary>
        /// 开协程执行IEnumerator
        /// </summary>
        /// <param name="coroutine"></param>
        public UnityPromise(IEnumerator coroutine) {
            Coroutine = coroutine;
            //UnityPromiseRunner.Execute(this);
        }
        /// <summary>
        /// 开启全局协程执行
        /// </summary>
        public void ExecuteByUnityPromiseRunner() {
            UnityPromiseRunner.Execute(this);
        }
        /// <summary>
        /// 包装(内捕获异常)IEnumerator 并开启全局协程执行
        /// </summary>
        /// <typeparam name="U">CustomYieldInstruction</typeparam>
        /// <param name="ie">CustomYieldInstruction</param>
        /// <returns></returns>
        public static UnityPromise WrapperAndExecute<U>(U ie) where U : IEnumerator {
            //
            var ve = EnumWrapper.Create<U>(ie);
            UnityPromise p = new UnityPromise(ve.Wrap());
            ve.promise
                 .Then((v_ie) => p.Error = "")
                 .Catch((ee) => p.Error = ee.ToString());
            p.ExecuteByUnityPromiseRunner();

            return p;

            //UnityPromise<U> p2 = new UnityPromise<U>(ve.Wrap());
            //p2.Result = ie;
            //ve.promise
            //     .Then((v_ie) => { p2.Error = ""; })
            //     .Catch((ee) => { p2.Error = ee.ToString()});
            //p2.ExecuteByUnityPromiseRunner();
            //return p2;
        }
    }
    /// <summary>
    /// 内部自动开协程执行,泛型的带结果数据的需要按例子UnityPromiseRunner内test模式实现
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class UnityPromise<T> : Promise<T> {
        public readonly IEnumerator Coroutine;
        public string Error;
        public T Result;

        public UnityPromise(IEnumerator coroutine) {
            Coroutine = coroutine;
            //UnityPromiseRunner.Execute(this);
        }
        /// <summary>
        /// 开启全局协程执行
        /// </summary>
        public void ExecuteByUnityPromiseRunner() {
            UnityPromiseRunner.Execute(this);
        }
        ///// <summary>
        ///// 保证自定义协程，既然是自定义的，还需要继续这样保证吗？
        ///// </summary>
        ///// <typeparam name="U"></typeparam>
        ///// <param name="ie"></param>
        ///// <returns></returns>
        //public static UnityPromise<U> WrapperAndExecute<U>(U ie) where U : IEnumerator {
        //    //
        //    var ve = EnumWrapper.Create<U>(ie);
        //    UnityPromise<U> p2 = new UnityPromise<U>(ve.Wrap());
        //    p2.Result = ie;
        //    ve.promise
        //         .Then((v_ie) => { p2.Error = ""; })
        //         .Catch((ee) => { p2.Error = ee.ToString()});
        //    p2.ExecuteByUnityPromiseRunner();
        //    return p2;
        //}

    }
    public class UnityPromiseException : Exception {
        private readonly string _error;

        public UnityPromiseException(string error) {
            _error = error;
        }

        public override string Message { get { return "An error happened while running the Unity Promise : " + _error; } }
    }
    public static class UnityPromiseRunner {
        public static void Execute(UnityPromise unityPromise) {
            // ReSharper disable once ObjectCreationAsStatement
            //需要执行
            //new UPTask(PromiseCoroutine(unityPromise));
            var task = UCoTaskManager.CreateTask(PromiseCoroutine(unityPromise));
            task.Finished += (manual) => { };
            task.Start();
        }

        private static IEnumerator PromiseCoroutine(UnityPromise unityPromise) {
            yield return null; // To ensure that unityPromise is instantiated when accessing it in the unityPromise Coroutine.
            yield return unityPromise.Coroutine;

            if (string.IsNullOrEmpty(unityPromise.Error)) {
                unityPromise.Resolve();
            }
            else {
                unityPromise.Reject(new UnityPromiseException(unityPromise.Error));
            }
        }

        public static void Execute<T>(UnityPromise<T> unityPromise) {
            // ReSharper disable once ObjectCreationAsStatement
            //需要执行
            //new UPTask(PromiseCoroutine(unityPromise));
            var task = UCoTaskManager.CreateTask(PromiseCoroutine(unityPromise));
            task.Finished += (manual) => { };
            task.Start();
        }

        private static IEnumerator PromiseCoroutine<T>(UnityPromise<T> unityPromise) {
            yield return null; // To ensure that unityPromise is instantiated when accessing it in the unityPromise Coroutine.
            yield return unityPromise.Coroutine;

            if (string.IsNullOrEmpty(unityPromise.Error)) {
                unityPromise.Resolve(unityPromise.Result);
            }
            else {
                unityPromise.Reject(new UnityPromiseException(unityPromise.Error));
            }
        }



        class test {
            public class UnityPromiseUsageSample {
                public void UsePromise() {
                    new UnityPromiseSample().LoadSprite("mySprite")
                        .Then(x => Debug.Log("Sprite is loaded"))
                        .Catch(exception => Debug.Log(exception.Message));
                }

                public IPromise UsePromise2<T>(T ie) where T : IEnumerator {
                    var ve = EnumWrapper.Create<T>(ie);
                    UnityPromise p = new UnityPromise(ve.Wrap());
                    ve.promise
                         .Then((p_ie) => p.Error = "")
                         .Catch((ee) => p.Error = ee.ToString());
                    p.ExecuteByUnityPromiseRunner();

                    return p;


                }
                public void test(IEnumerator ie) {
                    //包装使用方式1
                    UsePromise2(ie).Then(()=> {
                        //todo:执行完成干什么
                        
                    });
                    //包装使用方式2
                    var ve2=EnumWrapper.Create<IEnumerator>(ie);
                    ve2.promise
                        .Then((v_ie) => { })
                        .Catch((ee)=> { }
                        );
                    UCoTaskManager._singleton.StartCoroutine(ve2.Wrap());
                    //包装使用方式3
                    UnityPromise.WrapperAndExecute<IEnumerator>(ie)
                        .Then(() => {
                            return UnityPromise.WrapperAndExecute(ie);
                    })
                    ;
                }


            }

            public class UnityPromiseSample {
                private UnityPromise<Sprite> _promise;

                public IPromise<Sprite> LoadSprite(string imagePath) {
                    _promise = new UnityPromise<Sprite>(LoadAsynchronousResource(imagePath));
                    _promise.ExecuteByUnityPromiseRunner();
                    return _promise;
                }

                private IEnumerator LoadAsynchronousResource(string imagePath) {
                    ResourceRequest request = Resources.LoadAsync(imagePath, typeof(Sprite));

                    while (!request.isDone) {
                        yield return null;
                    }

                    if (request.asset == null) {
                        _promise.Error = "Could not find asset at path " + imagePath;
                    }
                    else {
                        _promise.Result = (Sprite)request.asset;
                    }
                }
            }
        }


    }
    /// <summary>
    /// This is a new coroutine interface for Unity.
    ///
    /// The motivation for this is twofold:
    ///
    /// 1. The existing coroutine API provides no means of stopping specific
    ///    coroutines; StopCoroutine only takes a string argument, and it stops
    ///    all coroutines started with that same string; there is no way to stop
    ///    coroutines which were started directly from an enumerator.  This is
    ///    not robust enough and is also probably pretty inefficient.
    ///
    /// 2. StartCoroutine and friends are MonoBehaviour methods.  This means
    ///    that in order to start a coroutine, a user typically must have some
    ///    component reference handy.  There are legitimate cases where such a
    ///    constraint is inconvenient.  This implementation hides that
    ///    constraint from the user.
    ///
    /// Example usage:
    ///
    /// ----------------------------------------------------------------------------
    /// IEnumerator MyAwesomeTask()
    /// {
    ///     while(true) {
    ///         //Debug.Log("Logcat iz in ur consolez, spammin u wif messagez.");
    ///         yield return null;
    ////    }
    /// }
    ///
    /// IEnumerator TaskKiller(float delay, Task t)
    /// {
    ///     yield return new WaitForSeconds(delay);
    ///     t.Stop();
    /// }
    ///
    /// void SomeCodeThatCouldBeAnywhereInTheUniverse()
    /// {
    ///     Task spam = new Task(MyAwesomeTask());
    ///     new Task(TaskKiller(5, spam));
    /// }
    /// ----------------------------------------------------------------------------
    ///
    /// When SomeCodeThatCouldBeAnywhereInTheUniverse is called, the debug console
    /// will be spammed with annoying messages for 5 seconds.
    ///
    /// Simple, really.  There is no need to initialize or even refer to TaskManager.
    /// When the first Task is created in an application, a "TaskManager" GameObject
    /// will automatically be added to the scene root with the TaskManager component
    /// attached.  This component will be responsible for dispatching all coroutines
    /// behind the scenes.
    ///
    /// Task also provides an event that is triggered when the coroutine exits.
    /// </summary>
    public class UCoTaskManager : MonoBehaviour {

        public class TaskState {
            public bool Running { get { return _running; } }

            public bool Paused { get { return _paused; } }

            public delegate void FinishedHandler(bool manual);
            public event FinishedHandler Finished;

            IEnumerator coroutine;
            bool _running;
            bool _paused;
            bool _stopped;

            public TaskState(IEnumerator c) {
                coroutine = c;
            }

            public void Pause() {
                _paused = true;
            }

            public void Unpause() {
                _paused = false;
            }

            public void Start() {
                _running = true;
                _singleton.StartCoroutine(CallWrapper());
            }

            public void Stop() {
                _stopped = true;
                _running = false;
            }

            public IEnumerator StartAndWait() {
                _running = true;
                yield return _singleton.StartCoroutine(CallWrapper());
            }


            private IEnumerator CallWrapper() {
                IEnumerator e = coroutine;
                while (_running) {
                    if (_paused) {
                        yield return null;
                    }
                    else {
                        //这里不做异常捕获
                        if (e != null && e.MoveNext()) {
                            yield return e.Current;
                        }
                        else {
                            _running = false;
                        }
                    }
                }
                //todo:增加一帧，以便确保正确触发结束回调 
                yield return null;
                //
                FinishedHandler handler = Finished;
                if (handler != null) {
                    handler(_stopped);
                }

            }
        }

        internal static UCoTaskManager _singleton;
        /// <summary>
        /// 创建一个全局执行任务
        /// </summary>
        /// <param name="coroutine"></param>
        /// <returns></returns>
        public static TaskState CreateTask(IEnumerator coroutine) {
            if (_singleton == null) {
                GameObject go = new GameObject("UCoTaskManager");
                _singleton = go.AddComponent<UCoTaskManager>();
                //
                DontDestroyOnLoad(go);
            }
            return new TaskState(coroutine);
        }
    }
    //  
    /// <summary>
    /// A Task object represents a coroutine.  Tasks can be started, paused, and stopped.
    /// It is an error to attempt to start a task that has been stopped or which has
    /// naturally terminated.    
    /// </summary>
    /*
    public class UPTask {
        /// Returns true if and only if the coroutine is running.  Paused tasks
        /// are considered to be running.
        public bool Running { get { return task.Running; } }

        /// Returns true if and only if the coroutine is currently paused.
        public bool Paused { get { return task.Paused; } }

        /// Delegate for termination subscribers.  manual is true if and only if
        /// the coroutine was stopped with an explicit call to Stop().
        public delegate void FinishedHandler(bool manual);

        /// Termination event.  Triggered when the coroutine completes execution.
        public event FinishedHandler Finished;

        /// Creates a new Task object for the given coroutine.
        ///
        /// If autoStart is true (default) the task is automatically started
        /// upon construction.
        public UPTask(IEnumerator c, bool autoStart = true) {
            task = UPTaskManager.CreateTask(c);
            task.Finished += TaskFinished;
            if (autoStart) {
                Start();
            }
        }

        /// Begins execution of the coroutine
        public void Start() {
            task.Start();
        }

        /// Discontinues execution of the coroutine at its next yield.
        public void Stop() {
            task.Stop();
        }

        public void Pause() {
            task.Pause();
        }

        public void Unpause() {
            task.Unpause();
        }

        private void TaskFinished(bool manual) {
            FinishedHandler handler = Finished;
            if (handler != null)
                handler(manual);
        }

        public UPTaskManager.TaskState task;
    }
    */

    //}


    /*
    public class CoroutinePromise {
        IEnumerator coroutine;
        CoroutinePromise promise;
        bool isDone = false;

        public CoroutinePromise(IEnumerator c) {
            coroutine = c;
        }

        public CoroutinePromise Then(IEnumerator newCoroutine) {
            promise = new CoroutinePromise(newCoroutine);
            return promise;
        }

        public CoroutinePromise Promise {
            get { return promise; }
        }

        public bool IsDone {
            get { return isDone; }
        }

        public IEnumerator Coroutine {
            get { return coroutine; }
        }

        public void Resolve() {
            isDone = true;
        }

        public CoroutinePromise Resolve(IEnumerator newCoroutine) {
            promise = new CoroutinePromise(newCoroutine);
            isDone = true;
            return promise;
        }
    }

    public class CoroutinePromiseSet {
        CoroutinePromise currentPromise = null;

        public CoroutinePromiseSet(CoroutinePromise c) {
            currentPromise = c;
        }

        public IEnumerator SuperCoroutine() {
            while (currentPromise != null) {
                if (!(!currentPromise.IsDone && currentPromise.Coroutine.MoveNext())) {
                    if (currentPromise.Promise != null)
                        currentPromise = currentPromise.Promise;
                    else
                        currentPromise = null;
                }
                yield return null;
            }
        }
    }
    */
    /*
    class test {
        CoroutinePromise c;
	CoroutinePromise ca;
	void Start ()
	{
		c = new CoroutinePromise(CoroutineA());
		ca = c.Then(CoroutineB());
		CoroutinePromise cb = ca.Then(CoroutineC());
		StartPromiseSet(c);
		
		CoroutinePromise c2 = new CoroutinePromise(CoroutineA2());
		c2.Then(CoroutineB2()).Then(CoroutineC2());
		StartPromiseSet(c2);
	}
	
	float t = 0.0f;
	void Update()
	{
		t += Time.deltaTime;

		if (t >= 2f) c.Resolve();
		if (t >= 7f) ca.Resolve();
	}

	public void StartPromiseSet(CoroutinePromise c)
	{
		CoroutinePromiseSet cSet = new CoroutinePromiseSet(c);
		StartCoroutine(cSet.SuperCoroutine());
	}

	IEnumerator CoroutineA()
	{
		while (t <= 5f)
		{
		//yield return new WaitForSeconds(3f);
			Debug.Log("a: "+t);
			yield return null;
		}
	}

	IEnumerator CoroutineB()
	{
		while (t <= 10f)
		{
			//yield return new WaitForSeconds(3f);
			Debug.Log("b: "+t);
			yield return null;
		}
	}

	IEnumerator CoroutineC()
	{
		while (t <= 15f)
		{
			//yield return new WaitForSeconds(3f);
			Debug.Log("c: "+t);
			yield return null;
		}
	}

	IEnumerator CoroutineA2()
	{
		while (t <= 5f)
		{
			//yield return new WaitForSeconds(3f);
			Debug.Log("a2: "+t);
			yield return null;
		}
	}
	
	IEnumerator CoroutineB2()
	{
		while (t <= 10f)
		{
			//yield return new WaitForSeconds(3f);
			Debug.Log("b2: "+t);
			yield return null;
		}
	}
	
	IEnumerator CoroutineC2()
	{
		while (t <= 15f)
		{
			//yield return new WaitForSeconds(3f);
			Debug.Log("c2: "+t);
			yield return null;
		}
	}
    }
    */
#endif
}
