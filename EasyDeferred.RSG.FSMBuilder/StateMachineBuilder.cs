using System;
using System.Threading;

namespace EasyDeferred.RSG
{
    /// <summary>
    /// Entry point for fluent API for constructing states.
    /// 构造状态的fluent API的入口点。
    /// </summary>
    public class StateMachineBuilder {
        /// <summary>
        /// Root level state.
        /// </summary>
        private readonly State rootState;

        /// <summary>
        /// Entry point for constructing new state machines.
        /// </summary>
        public StateMachineBuilder() {
            rootState = new State();
        }

        /// <summary>
        /// Create a new state of a specified type and add it as a child of the root state.
        /// </summary>
        /// <typeparam name="T">Type of the state to add</typeparam>
        /// <returns>Builder used to configure the new state</returns>
        public IStateBuilder<T, StateMachineBuilder> State<T>() where T : AbstractState, new() {
            return new StateBuilder<T, StateMachineBuilder>(this, rootState);
        }

        /// <summary>
        /// Create a new state of a specified type with a specified name and add it as a
        /// child of the root state.
        /// </summary>
        /// <typeparam name="T">Type of the state to add</typeparam>
        /// <param name="stateName">Name for the new state</param>
        /// <returns>Builder used to configure the new state</returns>
        public IStateBuilder<T, StateMachineBuilder> State<T>(string stateName) where T : AbstractState, new() {
            return new StateBuilder<T, StateMachineBuilder>(this, rootState, stateName);
        }

        /// <summary>
        /// Create a new state with a specified name and add it as a
        /// child of the root state.
        /// </summary>
        /// <param name="stateName">Name for the new state</param>
        /// <returns>Builder used to configure the new state</returns>
        public IStateBuilder<State, StateMachineBuilder> State(string stateName) {
            return new StateBuilder<State, StateMachineBuilder>(this, rootState, stateName);
        }

        /// <summary>
        /// Return the root state once everything has been set up.
        /// </summary>
        public State Build() {
            return rootState;
        }
    }


    class StateMachineBuilderTest1 {
        /// <summary>
        /// State while the shark is just swimming around.
        /// </summary>
        class NormalState : AbstractState {            
            public  void OnUpdate() {
                Console.WriteLine("Swimming around...");
                hunger++;
            }
        }

        /// <summary>
        /// State for when the shark is hungry and decides to look for something to eat.
        /// </summary>
        class HungryState : AbstractState {
            private Random random;

            public HungryState() {
                random = new Random();
            }

            public  void OnUpdate() {
                if(random.Next(5) <= 1) {
                    Console.WriteLine("Feeding");
                    hunger -= 5; // Decrease hunger
                }
                else {
                    Console.WriteLine("Hunting");
                }
                hunger++;
            }
        }

        // How hungry the shark is now
        static int hunger = 0;

        void Main(string[] args) {
            Console.WriteLine("Shark state machine. Press Ctrl-C to exit.");

            // Set up the state machine
            var rootState = new StateMachineBuilder()
                .State<NormalState>("Swimming")
                    .Update((state, time) => {
                        state.OnUpdate();
                        if(hunger > 5) {
                            state.PushState("Hunting");
                        }
                    })
                    .State<HungryState>("Hunting")
                        .Update((state, time) => {
                            state.OnUpdate();
                            if(hunger <= 5) {
                                state.Parent.PopState();
                                return;
                            }
                        })
                    .End()
                .End()
                .Build();

            // Set the initial state.
            rootState.ChangeState("Swimming");

            // Update the state machine at a set interval.
            while(true) {
                rootState.Update(1.0f);
                Thread.Sleep(1000);
            }
        }
    }
    class StateMachineBuilderU3DTest1 {
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