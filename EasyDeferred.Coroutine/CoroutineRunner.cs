using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace EasyDeferred.Coroutine
{
    /// <summary>
    /// A container for running multiple routines in parallel. Coroutines can be nested.
    /// https://github.com/ChevyRay/Coroutines/blob/master/Coroutines.cs
    /// </summary>
    public class CoroutineRunner
    {
        List<IEnumerator> running = new List<IEnumerator>();
        List<float> delays = new List<float>();

        /// <summary>
        /// Run a coroutine.
        /// </summary>
        /// <returns>A handle to the new coroutine.</returns>
        /// <param name="delay">How many seconds to delay before starting.</param>
        /// <param name="routine">The routine to run.</param>
        public CoroutineHandle Run(float delay, IEnumerator routine) {
            running.Add(routine);
            delays.Add(delay);
            return new CoroutineHandle(this, routine);
        }

        /// <summary>
        /// Run a coroutine.
        /// </summary>
        /// <returns>A handle to the new coroutine.</returns>
        /// <param name="routine">The routine to run.</param>
        public CoroutineHandle Run(IEnumerator routine) {
            return Run(0f, routine);
        }

        /// <summary>
        /// Stop the specified routine.
        /// </summary>
        /// <returns>True if the routine was actually stopped.</returns>
        /// <param name="routine">The routine to stop.</param>
        public bool Stop(IEnumerator routine) {
            int i = running.IndexOf(routine);
            if (i < 0)
                return false;
            running[i] = null;
            delays[i] = 0f;
            return true;
        }

        /// <summary>
        /// Stop the specified routine.
        /// </summary>
        /// <returns>True if the routine was actually stopped.</returns>
        /// <param name="routine">The routine to stop.</param>
        public bool Stop(CoroutineHandle routine) {
            return routine.Stop();
        }

        /// <summary>
        /// Stop all running routines.
        /// </summary>
        public void StopAll() {
            running.Clear();
            delays.Clear();
        }

        /// <summary>
        /// Check if the routine is currently running.
        /// </summary>
        /// <returns>True if the routine is running.</returns>
        /// <param name="routine">The routine to check.</param>
        public bool IsRunning(IEnumerator routine) {
            return running.Contains(routine);
        }

        /// <summary>
        /// Check if the routine is currently running.
        /// </summary>
        /// <returns>True if the routine is running.</returns>
        /// <param name="routine">The routine to check.</param>
        public bool IsRunning(CoroutineHandle routine) {
            return routine.IsRunning;
        }

        /// <summary>
        /// Update all running coroutines.
        /// </summary>
        /// <returns>True if any routines were updated.</returns>
        /// <param name="deltaTime">How many seconds have passed sinced the last update.</param>
        public bool Update(float deltaTime) {
#if DEBUG
            //Console.WriteLine("CoroutineMgr fixedUpdate  thread id:" + System.Threading.Thread.CurrentThread.ManagedThreadId);
#endif
            if (running.Count > 0) {
                for (int i = 0; i < running.Count; i++) {
                    if (delays.Count == 0 || i >= delays.Count) {
                        continue;
                    }
                    if (delays[i] > 0f) {
                        //延时等待第一次运行IEnumerator.MoveNext()
                        delays[i] -= deltaTime;
                    }
                    else if (running[i] == null || !MoveNext(running[i], i)) {
                        running.RemoveAt(i);
                        delays.RemoveAt(i--);
                    }
                }
                return true;
            }
            return false;
        }

        bool MoveNext(IEnumerator routine, int index) {
            if (routine.Current is IEnumerator) {
                if (MoveNext((IEnumerator)routine.Current, index))
                    return true;

                delays[index] = 0f;
            }

            bool result = routine.MoveNext();

            //if (routine.Current is float)
            //    delays[index] = (float)routine.Current;

            return result;
        }

        /// <summary>
        /// How many coroutines are currently running.
        /// </summary>
        public int Count {
            get { return running.Count; }
        }
    }

    /// <summary>
    /// A handle to a (potentially running) coroutine.
    /// </summary>
    public struct CoroutineHandle
    {
        /// <summary>
        /// Reference to the routine's runner.
        /// </summary>
        public CoroutineRunner Runner;

        /// <summary>
        /// Reference to the routine's enumerator.
        /// </summary>
        public IEnumerator Enumerator;

        /// <summary>
        /// Construct a coroutine. Never call this manually, only use return values from Coroutines.Run().
        /// </summary>
        /// <param name="runner">The routine's runner.</param>
        /// <param name="enumerator">The routine's enumerator.</param>
        public CoroutineHandle(CoroutineRunner runner, IEnumerator enumerator) {
            Runner = runner;
            Enumerator = enumerator;
        }

        /// <summary>
        /// Stop this coroutine if it is running.
        /// </summary>
        /// <returns>True if the coroutine was stopped.</returns>
        public bool Stop() {
            return IsRunning && Runner.Stop(Enumerator);
        }

        /// <summary>
        /// A routine to wait until this coroutine has finished running.
        /// </summary>
        /// <returns>The wait enumerator.</returns>
        public IEnumerator Wait() {
            if (Enumerator != null)
                while (Runner.IsRunning(Enumerator))
                    yield return null;
        }

        /// <summary>
        /// True if the enumerator is currently running.
        /// </summary>
        public bool IsRunning {
            get { return Enumerator != null && Runner.IsRunning(Enumerator); }
        }
    }

    //class TestCoroutineRunner
    //{
    //    int px = 0;
    //    int py = 0;
    //    void test() {
    //        //Timer variables to run the update loop at 30 fps
    //        var watch = System.Diagnostics.Stopwatch.StartNew();
    //        const float updateRate = 1f / 30f;
    //        float prevTime = watch.ElapsedMilliseconds / 1000f;
    //        float accumulator = 0f;

    //        //The little @ character's position

    //        //Run the coroutine
    //        var runner = new CoroutineRunner();
    //        var moving = runner.Run(Movement());

    //        //Run the update loop until we've finished moving
    //        while (moving.IsRunning) {
    //            //Track time
    //            float currTime = watch.ElapsedMilliseconds / 1000f;
    //            accumulator += currTime - prevTime;
    //            prevTime = currTime;

    //            //Update at our requested rate (30 fps)
    //            if (accumulator > updateRate) {
    //                accumulator -= updateRate;
    //                runner.Update(updateRate);
    //                DrawMap();
    //            }
    //        }
    //    }
    //    class WaitSeconds : IEnumerator
    //    {
    //        public WaitSeconds(float seconds) {
    //            this.Seconds = seconds;
    //            this.m_Milliseconds = Convert.ToInt32(seconds * 1000f);
    //        }
    //        public object Current { get { return m_Current; } }
    //        private object m_Current;
    //        private System.Diagnostics.Stopwatch m_Start = new System.Diagnostics.Stopwatch();
    //        private bool m_first;
    //        private int m_Milliseconds;
    //        public float Seconds {
    //            get;
    //            private set;
    //        }
    //        public bool MoveNext() {
    //            if (!m_first) {
    //                m_first = true;
    //                m_Start.Start();
    //            }
    //            if (m_Start.ElapsedMilliseconds > m_Milliseconds) {
    //                m_Current = false;
    //                return false;
    //            }
    //            m_Current = true;
    //            return true;
    //        }

    //        public void Reset() {
    //            m_Start.Reset();
    //        }
    //    }
    //    //Routine to move horizontally
    //    IEnumerator MoveX(int amount, float stepTime) {
    //        int dir = amount > 0 ? 1 : -1;
    //        while (amount != 0) {
    //            yield return new WaitSeconds(stepTime);
    //            px += dir;
    //            amount -= dir;
    //        }
    //    }

    //    //Routine to move vertically
    //    IEnumerator MoveY(int amount, float stepTime) {
    //        int dir = amount > 0 ? 1 : -1;
    //        while (amount != 0) {
    //            yield return new WaitSeconds(stepTime);
    //            py += dir;
    //            amount -= dir;
    //        }
    //    }

    //    //Walk the little @ character on a path
    //    IEnumerator Movement() {
    //        //Walk normally
    //        yield return MoveX(5, 0.25f);
    //        yield return MoveY(5, 0.25f);

    //        //Walk slowly
    //        yield return MoveX(2, 0.5f);
    //        yield return MoveY(2, 0.5f);
    //        yield return MoveX(-2, 0.5f);
    //        yield return MoveY(-2, 0.5f);

    //        //Run fast
    //        yield return MoveX(5, 0.1f);
    //        yield return MoveY(5, 0.1f);
    //    }

    //    //Render a little map with the @ character in the console
    //    void DrawMap() {
    //        Console.Clear();
    //        for (int y = 0; y < 16; ++y) {
    //            for (int x = 0; x < 16; ++x) {
    //                if (x == px && y == py)
    //                    Console.Write('@');
    //                else
    //                    Console.Write('.');
    //            }
    //            Console.WriteLine();
    //        }
    //    }

    //}


    /*
     IEnumerator GatherNPCs(Vector gatheringPoint)
{
    //Make three NPCs walk to the gathering point at the same time
    var move1 = runner.Run(npc1.WalkTo(gatheringPoint));
    var move2 = runner.Run(npc2.WalkTo(gatheringPoint));
    var move3 = runner.Run(npc3.WalkTo(gatheringPoint));

    //We don't know how long they'll take, so just wait until all three have finished
    while (move1.IsPlaying || move2.IsPlaying || move3.IsPlaying)
        yield return null;

    //Now they've all gathered!
}

    IEnumerator DownloadFile(string url, string toFile)
{
    //I actually don't know how to download files in C# so I just guessed this, but you get the point
    bool done = false;
    var client = new WebClient();
    client.DownloadFileCompleted += (e, b, o) => done = true;
    client.DownloadFileAsync(new Uri(url), toFile);
    while (!done)
        yield return null;
}

//Download the files one-by-one in sync
IEnumerator DownloadOneAtATime()
{
    yield return DownloadFile("http://site.com/file1.png", "file1.png");
    yield return DownloadFile("http://site.com/file2.png", "file2.png");
    yield return DownloadFile("http://site.com/file3.png", "file3.png");
    yield return DownloadFile("http://site.com/file4.png", "file4.png");
    yield return DownloadFile("http://site.com/file5.png", "file5.png");
}

//Download the files all at once asynchronously
IEnumerator DownloadAllAtOnce()
{
    //Start multiple async downloads and store their handles
    var downloads = new List<CoroutineHandle>();
    downloads.Add(runner.Run(DownloadFile("http://site.com/file1.png", "file1.png")));
    downloads.Add(runner.Run(DownloadFile("http://site.com/file2.png", "file2.png")));
    downloads.Add(runner.Run(DownloadFile("http://site.com/file3.png", "file3.png")));
    downloads.Add(runner.Run(DownloadFile("http://site.com/file4.png", "file4.png")));
    downloads.Add(runner.Run(DownloadFile("http://site.com/file5.png", "file5.png")));

    //Wait until all downloads are done
    while (downloads.Count > 0)
    {
        yield return null;
        for (int i = 0; i < downloads.Count; ++i)
            if (!downloads[i].IsRunning)
                downloads.RemoveAt(i--);
    }
}
     */
}
