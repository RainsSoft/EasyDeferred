using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using EasyDeferred.Coroutine;

namespace EasyDeferredCoroutineTest
{
    class TestCoroutineRunner
    {
        static int px = 0;
        static int py = 0;
        internal void test() {
            //Timer variables to run the update loop at 30 fps
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const float updateRate = 1f / 30f;
            float prevTime = watch.ElapsedMilliseconds / 1000f;
            float accumulator = 0f;

            //The little @ character's position

            //Run the coroutine
            var runner = new CoroutineRunner();
            var moving = runner.Run(Movement());

            //Run the update loop until we've finished moving
            while (moving.IsRunning) {
                //Track time
                float currTime = watch.ElapsedMilliseconds / 1000f;
                accumulator += currTime - prevTime;
                prevTime = currTime;

                //Update at our requested rate (30 fps)
                if (accumulator > updateRate) {
                    accumulator -= updateRate;
                    runner.Update(updateRate);
                    DrawMap();
                }
            }
        }
        class WaitSeconds : IEnumerator
        {
            public WaitSeconds(float seconds) {
                this.Seconds = seconds;
                this.m_Milliseconds = Convert.ToInt32(seconds * 1000f);
            }
            public object Current { get { return m_Current; } }
            private object m_Current;
            private System.Diagnostics.Stopwatch m_Start = new System.Diagnostics.Stopwatch();
            private bool m_first;
            private int m_Milliseconds;
            public float Seconds {
                get;
                private set;
            }
            public bool MoveNext() {
                if (!m_first) {
                    m_first = true;
                    m_Start.Start();
                }
                if (m_Start.ElapsedMilliseconds > m_Milliseconds) {
                    m_Current = false;
                    return false;
                }
                m_Current = true;
                return true;
            }

            public void Reset() {
                m_Start.Reset();
            }
        }
        //Routine to move horizontally
        internal static IEnumerator MoveX(int amount, float stepTime) {
            int dir = amount > 0 ? 1 : -1;
            while (amount != 0) {
                yield return new WaitSeconds(stepTime);
                px += dir;
                amount -= dir;
            }
        }

        //Routine to move vertically
        internal static IEnumerator MoveY(int amount, float stepTime) {
            int dir = amount > 0 ? 1 : -1;
            while (amount != 0) {
                yield return new WaitSeconds(stepTime);
                py += dir;
                amount -= dir;
            }
        }

        //Walk the little @ character on a path
        internal static IEnumerator Movement() {
            //Walk normally
            yield return MoveX(5, 1f);
            yield return MoveY(5, 1f);

            //Walk slowly
            yield return MoveX(2, 2f);
            yield return MoveY(2, 2f);
            yield return MoveX(-2, 2f);
            yield return MoveY(-2, 2f);

            //Run fast
            yield return MoveX(5, 2f);
            yield return MoveY(5, 2f);
        }

        //Render a little map with the @ character in the console
        internal static void DrawMap() {
            //return;
            Console.Clear();
            for (int y = 0; y < 16; ++y) {
                for (int x = 0; x < 16; ++x) {
                    if (x == px && y == py)
                        Console.Write('@');
                    else
                        Console.Write('.');
                }
                Console.WriteLine();
            }
        }

    }
}
