
#if UNITY
namespace IRobotQ.RSG {
    
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IRobotQ;
    /// <summary>
    /// 主要功能:
    /// 1:固定fixedUpdate更新内部 _Timer
    /// 2:包装执行IEnumerator(fixedUpdate/update方式) 
    /// </summary>
    public class PromiseU3DRunner : MonoBehaviour {
        /// <summary>
        /// 时间等待类提供3种更新方式
        /// </summary>
        public enum ContentTimerMode {
            Update,
            LateUpdate,
            FixedUpdate
        }
        /// <summary>
        /// 执行顺序 Start()->{FixedUpdate()->WaitForFixedUpdate()...onTrigger/CollisionXXX}->OnMouseXXX->Update()-> 
        /// yield null->yield WaitForSeconds->yield WWW->yield StartCoroutine->LateUpdate()->
        /// {Render(...)-> OnGUI()}->yiele WaitForEndOfFrame
        /// </summary>
        public class Content {
            /// <summary>
            /// 固定物理帧更新
            /// </summary>
            private PromiseTimer m_Timer = new PromiseTimer();
            private PromiseTimer m_TimerLateUpdate = new PromiseTimer();
            private PromiseTimer m_TimerFixed = new PromiseTimer();
            private PromiseU3DRunner m_Owner;
            private bool m_Global;
            internal Content(PromiseU3DRunner owner, bool isGlobal) {
                this.m_Owner = owner;
                this.m_Global = isGlobal;
            }
            /// <summary>
            /// 受Time.timeScale影响
            /// </summary>
            /// <param name="updateMode"></param>
            /// <returns></returns>
            public PromiseTimer GetTimer(ContentTimerMode updateMode = ContentTimerMode.Update) {
                switch (updateMode) {
                    case ContentTimerMode.Update:
                        return this.m_Timer;
                        break;
                    case ContentTimerMode.LateUpdate:
                        return this.m_TimerLateUpdate;
                        break;
                    case ContentTimerMode.FixedUpdate:
                        return this.m_TimerFixed;
                        break;
                }
                return this.m_Timer;
            }
            /// <summary>
            /// 使用脚本启动协程
            /// </summary>
            /// <param name="ienum"></param>
            internal void StartCoroutine(IEnumerator ienum) {
                if (m_Owner != null) {
                    m_Owner.StartCoroutine(ienum);
                }
            }
#region IPromise<PromiseU3DIEnumWrapContent> Update
            /// <summary>
            /// 开协程中执行枚举
            /// </summary>
            /// <param name="ienum">枚举内部自己处理的异常必须使用throw抛出，否则promise执行完成后不知道是成功还是异常失败</param>
            /// <param name="ienum_Arg">枚举方法内的参数，可传递数据结果,注意，如果需要参数，则IEnumerator的参数也必须是Variable〈Type〉</param>
            /// <returns>Promise＜PromiseU3DIEnumWrapContent＞ </returns>
            public Promise<PromiseU3DIEnumWrapContext> DoByWrapT(IEnumerator ienum, Variable ienum_Arg = null) {
                Promise<PromiseU3DIEnumWrapContext> p = new Promise<PromiseU3DIEnumWrapContext>();
                PromiseU3DIEnumWrapContext pcontext = new PromiseU3DIEnumWrapContext(ienum, ienum_Arg, this.m_Global);
                StartCoroutine(waiteDoIEnum2Generic(p, pcontext));
                return p;
            }
            /// <summary>
            /// 协程中执行
            /// 
            /// </summary>
            /// <param name="p"></param>
            /// <param name="context"></param>
            /// <returns></returns>
            private IEnumerator waiteDoIEnum2Generic(Promise<PromiseU3DIEnumWrapContext> p, PromiseU3DIEnumWrapContext context) {
                //等待一帧，保证正常顺序执行
                yield return null;
                //
                bool keep_wait = true;
                bool hasError = false;
                //Exception eep = null;
                while (keep_wait) {
                    try {
                        keep_wait = context.enumTarget.MoveNext();
                        context.process += 1;
                        p.ReportProgress(context.process);
                    }
                    catch (Exception ee) {
                        //keep_wait = false;
                        //eep = ee;
                        hasError = true;
                        context.eep = new PromiseU3DRunnerException("PromiseU3DRunner:", ee) { Context = context };
                        context.hasError = hasError;
                        Debug.LogException(context.eep);
                    }
                    if (hasError) {
                        //if (onerr != null) {
                        //onerr(eep);
                        //}
                        p.Reject(context.eep);
                        yield break;
                    }
                    yield return context.enumTarget.Current;
                }
                p.Resolve(context);
            }
#endregion

#region IPromise<PromiseU3DIEnumWrapContent> Fixed
            /// <summary>
            /// 物理帧FixedUpdate中执行枚举。 注意: yield return WaitForEndOfFrame/WaitForSecondsRealtime等 是无效的
            /// </summary>
            /// <param name="ienum">枚举内部自己处理的异常必须使用throw抛出，否则promise执行完成后不知道是成功还是异常失败</param>
            /// <param name="ienum_Arg">枚举方法内的参数，可传递数据结果,注意，如果需要参数，则IEnumerator的参数也必须是Variable〈Type〉</param>
            /// <returns>Promise＜PromiseU3DIEnumWrapContent＞ </returns>
            public Promise<PromiseU3DIEnumWrapContext> DoByWrapTFixed(IEnumerator ienum, Variable ienum_Arg = null) {
                Promise<PromiseU3DIEnumWrapContext> p = new Promise<PromiseU3DIEnumWrapContext>();
                PromiseU3DIEnumWrapContext pcontext = new PromiseU3DIEnumWrapContext(ienum, ienum_Arg, this.m_Global);
                var ve = waiteDoIEnumFixed(pcontext);
                var p2 = this.m_TimerFixed.WaitWhile((Z) => {
                    p.ReportProgress(pcontext.process);
                    return ve.MoveNext();
                }, ienum_Arg);
                p2.Then(() => {
                    if (pcontext.hasError) {
                        p.Reject(pcontext.eep);
                    }
                    else {
                        p.Resolve(pcontext);
                    }

                }).Catch((ee) => {
                    p.Reject(ee);
                });
                //简化写法 (进度没办法报出来)
                //var p = new Promise<PromiseU3DIEnumWrapContent>((resolver, reject) => {
                //    var pcontent = new PromiseU3DIEnumWrapContent(ienum, ienum_Arg, this._global);
                //    var ve = WaiteDoIEnumFixed(pcontent);
                //    var p2 = this._Timer.WaitWhile((Z) => {
                //        pcontent._process += 1;                
                //        return ve.MoveNext();
                //    }).Then(() => {
                //        if (pcontent.HasError) {
                //            reject(pcontent.eep);
                //        }
                //        else {
                //            resolver(pcontent);
                //        }
                //    }).Catch((ee) => {
                //        reject(ee);
                //    });
                //});
                return p;
            }
            /// <summary>
            /// fixed中执行
            /// </summary>
            /// <param name="context"></param>
            /// <returns></returns>
            private IEnumerator waiteDoIEnumFixed(PromiseU3DIEnumWrapContext context) {
                //等待一帧，保证正常顺序执行
                yield return null;
                //
                bool keep_wait = true;
                bool hasError = false;
                //Exception eep = null;
                while (keep_wait) {
                    try {
                        keep_wait = context.enumTarget.MoveNext();
                        context.process += 1;
                    }
                    catch (Exception ee) {
                        //keep_wait = false;
                        //eep = ee;
                        hasError = true;
                        context.eep = new PromiseU3DRunnerException("PromiseU3DRunner:", ee) { Context = context };
                        context.hasError = hasError;
                        Debug.LogException(context.eep);
                    }
                    if (hasError) {
                        //if (onerr != null) {
                        //onerr(eep);
                        //}  
                        throw context.eep;
                        yield break;
                    }
                    yield return context.enumTarget.Current;
                }

            }
#endregion

#region IPromise Update
            /// <summary>
            /// 开协程执行枚举
            /// </summary>
            /// <param name="ienum">枚举内部自己处理的异常必须使用throw抛出，否则promise执行完成后不知道是成功还是异常失败</param>
            /// <param name="ienum_Arg">枚举方法内的参数，可传递数据结果,注意，如果需要参数，则IEnumerator的参数也必须是Variable〈Type〉</param>
            /// <returns>Promise＜PromiseU3DIEnumWrapContent＞ </returns>
            public Promise DoByWrap(IEnumerator ienum, Variable ienum_Arg = null) {
                Promise p = new Promise();
                PromiseU3DIEnumWrapContext pcontext = new PromiseU3DIEnumWrapContext(ienum, ienum_Arg, this.m_Global);
                StartCoroutine(waiteDoIEnum2NonGeneric(p, pcontext));
                return p;
            }
            /// <summary>
            /// 协程中执行枚举
            /// </summary>
            /// <param name="p"></param>
            /// <param name="context"></param>
            /// <returns></returns>
            private IEnumerator waiteDoIEnum2NonGeneric(Promise p, PromiseU3DIEnumWrapContext context) {
                //等待一帧，保证正常顺序执行
                yield return null;
                //
                bool keep_wait = true;
                bool hasError = false;
                //Exception eep = null;
                //int process = 0;
                while (keep_wait) {
                    try {
                        keep_wait = context.enumTarget.MoveNext();
                        context.process += 1;
                        p.ReportProgress(context.process);
                    }
                    catch (Exception ee) {
                        //keep_wait = false;
                        //eep = ee;
                        hasError = true;
                        context.eep = new PromiseU3DRunnerException("PromiseU3DRunner:", ee) { Context = context };
                        context.hasError = hasError;
                        Debug.LogException(context.eep);
                    }
                    if (hasError) {
                        //if (onerr != null) {
                        //onerr(eep);
                        //}
                        p.Reject(context.eep);
                        yield break;
                    }
                    yield return context.enumTarget.Current;
                }
                p.Resolve();
            }
#endregion

#region IPromise Fixed
            /// <summary>
            /// 物理帧FixedUpdate中执行枚举。注意不能执行嵌套枚举，比如 yield return WaitForEndOfFrame/WaitForSecondsRealtime等 是无效的
            /// </summary>
            /// <param name="ienum">枚举内部自己处理的异常必须使用throw抛出，否则promise执行完成后不知道是成功还是异常失败</param>
            /// <param name="ienum_Arg">枚举方法内的参数，可传递数据结果,注意，如果需要参数，则IEnumerator的参数也必须是Variable〈Type〉</param>
            /// <returns>Promise＜PromiseU3DIEnumWrapContent＞ </returns>
            public Promise DoByWrapFixed(IEnumerator ienum, Variable ienum_Arg = null) {
                Promise p = new Promise();
                PromiseU3DIEnumWrapContext pcontext = new PromiseU3DIEnumWrapContext(ienum, ienum_Arg, this.m_Global);
                var ve = waiteDoIEnumFixed(pcontext);
                var p2 = this.m_TimerFixed.WaitWhile((Z) => {
                    p.ReportProgress(pcontext.process);
                    return ve.MoveNext();
                }, ienum_Arg);
                p2.Then(() => {
                    if (pcontext.hasError) {
                        p.Reject(pcontext.eep);
                    }
                    else {
                        p.Resolve();
                    }

                }).Catch((ee) => {
                    p.Reject(ee);
                });
                //简化写法 (没法报进度)
                //var p = new Promise((resolver, reject) => {
                //    var pcontent = new PromiseU3DIEnumWrapContent(ienum, ienum_Arg, this._global);
                //    var ve = WaiteDoIEnumFixed(pcontent);
                //    var p2 = this._Timer.WaitWhile((Z) => {
                //        return ve.MoveNext();
                //    });
                //    p2.Then(() => {
                //        if (pcontent.HasError) {
                //            reject(pcontent.eep);
                //        }
                //        else {
                //            resolver();
                //        }
                //    }).Catch((ee) => {
                //        reject(ee);
                //    });
                //});
                return p;
            }

#endregion
            /// <summary>
            /// 开协程等待EndOfFrame后执行
            /// </summary>
            /// <param name="a"></param>
            /// <returns></returns>
            public IPromise DoOnEndOfFrame(Action a) {
                var promise = new Promise();
                StartCoroutine(waitForEndOfFrame(a, promise));
                return promise;
            }

            private IEnumerator waitForEndOfFrame(Action a, IPendingPromise p) {
                yield return new WaitForEndOfFrame();

                try {
                    a();
                    p.Resolve();
                }
                catch (Exception ex) {
                    p.Reject(ex);
                }
            }
            /// <summary>
            /// 开协程等待fixedUpdate后执行
            /// </summary>
            /// <param name="a"></param>
            /// <returns></returns>
            public IPromise DoOnFixedUpdate(Action a) {
                var promise = new Promise();
                StartCoroutine(waitForFixedUpdate(a, promise));
                return promise;
            }

            private IEnumerator waitForFixedUpdate(Action a, IPendingPromise p) {
                yield return new WaitForFixedUpdate();

                try {
                    a();
                    p.Resolve();
                }
                catch (Exception ex) {
                    p.Reject(ex);
                }
            }
        }

        /// <summary>
        /// 查找或者创建执行Go脚本 PromiseTimerRunner 
        /// </summary>
        /// <param name="global"></param>
        /// <returns></returns>
        public static Content CreateOrFind(bool global = false) {
            PromiseU3DRunner pr = null;
            string name = string.Format("IRobotQ.PromiseU3DRunner{0}", global ? "_Global" : "");
            var go = GameObject.Find(name);
            if (go == null) {
                go = new GameObject(name);
                pr = go.AddComponent<PromiseU3DRunner>();
                pr.m_Co = new Content(pr, global);
                pr.m_Global = global;
                if (global) {
                    DontDestroyOnLoad(go);
                }
            }
            else {
                pr = go.GetComponent<PromiseU3DRunner>();
            }
            return pr.m_Co;
        }
        private bool m_Global = false;
        private Content m_Co = null;
        //
        private void Start() {
            if (m_Global) {
                Promise.UnhandledException += Promise_UnhandledException;
            }
        }
        void OnDestroy() {
            if (m_Global) {
                Promise.UnhandledException -= Promise_UnhandledException;
            }
        }
        private void Promise_UnhandledException(object sender, ExceptionEventArgs e) {
            Debug.LogException(e.Exception);
        }
        private void FixedUpdate() {
            if (m_Co != null) {
                m_Co.GetTimer(ContentTimerMode.FixedUpdate).Update(Time.fixedDeltaTime);
            }

        }
        private void Update() {
            if (m_Co != null) {
                m_Co.GetTimer(ContentTimerMode.Update).Update(Time.deltaTime);
            }
        }
        private void LateUpdate() {
            if (m_Co != null) {
                m_Co.GetTimer(ContentTimerMode.LateUpdate).Update(Time.deltaTime);
            }
        }
    }
    //public enum PromiseU3DIEnumWrapContentState{
    //    None,
    //    Pending,
    //    Finished
    //}
    public class PromiseU3DIEnumWrapContext {

        public IEnumerator enumTarget {
            get;
            private set;
        }

        //public IPromise _promise {
        //    get;
        //    private set;
        //}
        public bool global {
            get;
            private set;
        }
        public bool hasError;
        public Exception eep;
        public Variable arg;
        public int process {
            get;
            internal set;
        }
        public PromiseU3DIEnumWrapContext(IEnumerator ienum, Variable arg, bool executeByGlobal) {
            this.global = executeByGlobal;
            this.enumTarget = ienum;
            this.arg = arg;
            //this._promise = new Promise();
        }
        //public static Promise<PromiseU3DIEnumWrapContent> WrapAndExecute(IEnumerator ienum, Variable ienum_Arg, bool executeByGlobal) {
        //    var runner = PromiseU3DRunner._createOrFind(executeByGlobal);
        //    return runner._wrapAndExecute(ienum, ienum_Arg);

        //}

    }

    public class PromiseU3DRunnerException : Exception {
        public PromiseU3DRunnerException() { }

        public PromiseU3DRunnerException(string message) : base(message) { }

        public PromiseU3DRunnerException(string message, Exception inner) : base(message, inner) { }
        public PromiseU3DIEnumWrapContext Context;
    }
    /*
    class promiseU3DRunnerTest {

        void test() {

            string url = "www.baidu.com";
            var promise = new Promise<string>((resolve, reject) => {
                using (var client = new System.Net.WebClient()) {
                    client.DownloadStringCompleted +=   // Monitor event for download completed.
                        (s, ev) => {
                            if (ev.Error != null) {
                                reject(ev.Error);       // Error during download, reject the promise.
                            }
                            else {
                                resolve(ev.Result);     // Downloaded completed successfully, resolve the promise.
                            }
                        };

                    client.DownloadStringAsync(new Uri(url), null); // Initiate async op.
                }
            });
            promise.Then((ret) => {
                Console.WriteLine(ret);
            }).Catch((err) => {
                Console.WriteLine("error:" + err.ToString());
            });

        }

        void test2() {
            string url = "https://imgsa.baidu.com/forum/w%3D580/sign=121bd4bff1039245a1b5e107b795a4a8/6c6f564e9258d109620b8bc0dc58ccbf6d814d97.jpg";
            IRobotQ.Variable<Texture2D> t2d = new Variable<Texture2D>();
            //方式一
            var p = PromiseU3DRunner._createOrFind()._doByWrapT(Download(url, t2d), t2d);

            p.Then((content) => {
                //Console.WriteLine((content.Arg as IRobotQ.Variable<Texture2D>).Value);
            }).Catch((ee) => {
                Console.WriteLine("promise发生异常：" + ee.ToString());
            }).Finally(() => {
                Console.WriteLine("promise执行完毕。");
            });
            //方式二
            var promise = new Promise<Texture2D>((resolver, reject) => {
                var p2 = PromiseU3DRunner._createOrFind()._doByWrapT(Download(url, t2d), t2d);
                p2.Then((context) => {
                    resolver((context.Arg as IRobotQ.Variable<Texture2D>).Value);
                }).Catch((ee) => {
                    reject(ee);
                });
            });
            //
            

        }
        IEnumerator Download(string url, Variable<Texture2D> t2d) {
            yield return null;
            UnityEngine.Networking.UnityWebRequest wr = new UnityEngine.Networking.UnityWebRequest(new Uri(url));
            UnityEngine.Networking.DownloadHandlerTexture downloadTexture = new UnityEngine.Networking.DownloadHandlerTexture(true);
            wr.downloadHandler = downloadTexture;
            var send = wr.SendWebRequest();
            yield return send;
            if (wr.isNetworkError || wr.isHttpError) {
                //有错误则抛出异常
                throw new Exception("下载失败");
            }
            t2d.Value = downloadTexture.texture;
        }

    }
    */
}
#endif