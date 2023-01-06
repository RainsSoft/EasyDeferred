using EasyDeferred.RSG;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EasyDeferredTest
{
  
    class TestStateMachineBuilder
    {
        /// <summary>
        /// State while the shark is just swimming around.
        /// </summary>
        class NormalState : AbstractState
        {
            public void OnUpdate() {
                Console.WriteLine("1-Swimming around...");
                hunger++;
            }
        }

        /// <summary>
        /// State for when the shark is hungry and decides to look for something to eat.
        /// </summary>
        class HungryState : AbstractState
        {
            private Random random;

            public HungryState() {
                random = new Random();
            }

            public void OnUpdate() {
                if (random.Next(5) <= 1) {
                    Console.WriteLine("2-Feeding");
                    hunger -= 5; // Decrease hunger
                }
                else {
                    Console.WriteLine("2-3-Hunting");
                }
                hunger++;
            }
        }

        // How hungry the shark is now
        static int hunger = 0;

        internal static void Test() {
            Console.WriteLine("Shark state machine. Press Ctrl-C to exit.");
            var fsmBuilder = new StateMachineBuilder();
            // Set up the state machine
            var rootState = fsmBuilder
                .State<NormalState>("Swimming")
                    .Enter((state)=> {
                        Console.WriteLine("1111--enter Swimming------------");
                    })
                    .Update((state, time) => {
                        state.OnUpdate();
                        if (hunger > 5) {
                            state.PushState("Hunting");
                        }
                    }).Exit((Z) => {
                        Console.WriteLine("1111--exit Swimming State");
                    })
                    .State<HungryState>("Hunting")
                        .Enter((state) => {
                            Console.WriteLine("2222--------------");
                        })
                        .Update((state, time) => {
                            state.OnUpdate();
                            if (hunger <= 5) {
                                state.Parent.PopState();
                                return;
                            }
                        })
                        .Exit((Z) => {
                            Console.WriteLine("exit Hunting State");
                        })
                    .End()
                .End()
                .Build();

            // Set the initial state.
            rootState.ChangeState("Swimming");
            var states = (rootState as AbstractState).getRootAllChildStateInstance();
            // Update the state machine at a set interval.
            while (true) {
                rootState.Update(1.0f);
                Thread.Sleep(1000);
            }
        }

        internal static void Test2() {
            Console.WriteLine("Shark state machine. Press Ctrl-C to exit.");
            var fsmBuilder = new StateMachineBuilder();
            // Set up the state machine
            var rootState = fsmBuilder
                .State<NormalState>("Swimming")
                    .Enter((state) => {
                        Console.WriteLine("1111--------------");
                    })
                    .Update((state, time) => {
                        state.OnUpdate();
                        if (hunger > 5) {
                            state.getRoot().ChangeState("Hunting");
                        }
                    }).Exit((Z) => {
                        Console.WriteLine("1111--exit Swimming State");
                    })
                    .End()
                    .Build();
            //------------------------------------------
            var rootState2= fsmBuilder.State<HungryState>("Hunting")
                        .Enter((state) => {
                            Console.WriteLine("2222--enter Hunting------------");
                        })
                        .Update((state, time) => {
                            state.OnUpdate();
                            if (hunger <= 5) {
                                state.getRoot().ChangeState("Swimming");
                                return;
                            }
                        })
                        .Exit((Z) => {
                            Console.WriteLine("2222--exit Hunting State");
                        })
                    .End()             
                .Build();
            //---------------------
            System.Diagnostics.Debug.Assert(rootState==rootState2,"根对象不一致");

            // Set the initial state.
            rootState.ChangeState("Swimming");
            var states = (rootState as AbstractState).getRootAllChildStateInstance();
            // Update the state machine at a set interval.
            while (true) {
                rootState.Update(1.0f);
                Thread.Sleep(1000);
            }
        }
    }
    class TestStateMachineBuilder_U3D
    {
        /*
        public class Example2Actor : MonoBehaviour {
            /// <summary>
            /// Goal to move towards
            /// </summary>
            public Transform goal;

            /// <summary>
            /// State machine
            /// </summary>
            private IState rootState;

            /// <summary>
            /// Distance to retreat before approaching the target again.
            /// </summary>
            float resetDistance = 10f;

            private class MovingState : AbstractState {
                public float movementSpeed = 3f;
            }

            private class RetreatingState : MovingState {
                public Vector3 direction;
            }

            // Use this for initialization
            void Start() {
                rootState = new StateMachineBuilder()
                    // First, approach the goal
                    .State<MovingState>("Approach")
                        .Enter(state => {
                            Debug.Log("Entering Approach state");
                        })
                        // Move towards the goal
                        .Update((state, deltaTime) => {
                            var directionToTarget = transform.position - goal.position;
                            directionToTarget.y = 0; // Only calculate movement on a 2d plane
                    directionToTarget.Normalize();

                            transform.position -= directionToTarget * deltaTime * state.movementSpeed;
                        })
                        // Once the TargetReached event is triggered, retreat away again
                        .Event("TargetReached", state => {
                            state.PushState("Retreat");
                        })
                        .Exit(state => {
                            Debug.Log("Exiting Approach state");
                        })
                        // Retreating state
                        .State<RetreatingState>("Retreat")
                            // Set a new destination
                            .Enter(state => {
                                Debug.Log("Entering Retreat state");

                        // Work out a new target, away from the goal
                        var direction = new Vector3(Random.value, 0f, Random.value);
                                direction.Normalize();

                                state.direction = direction;
                            })
                            // Move towards the new destination
                            .Update((state, deltaTime) => {
                                transform.position -= state.direction * deltaTime * state.movementSpeed;
                            })
                            // If we go further away from the original target than the reset distance, exit and 
                            // go back to the previous state
                            .Condition(() => {
                                return Vector3.Distance(transform.position, goal.position) >= resetDistance;
                            },
                            state => {
                                state.Parent.PopState();
                            })
                            .Exit(state => {
                                Debug.Log("Exiting Retreat state");
                            })
                            .End()
                        .End()
                    .Build();

                rootState.ChangeState("Approach");
            }

            // Update is called once per frame
            void Update() {
                rootState.Update(Time.deltaTime);
            }

            /// <summary>
            /// Tell our state machine that the target has been reached once we hit the trigger
            /// </summary>
            void OnTriggerEnter(Collider collider) {
                if(collider.transform == goal) {
                    rootState.TriggerEvent("TargetReached");
                }
            }
        }
        */
    }
}
