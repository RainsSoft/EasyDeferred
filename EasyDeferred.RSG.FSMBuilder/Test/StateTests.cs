﻿using Moq;
using System;
using Xunit;

namespace RSG.FluentStateMachineTests
{
    public class StateTests
    {
        private class TestState : AbstractState { }

        private class TestEventArgs : EventArgs
        {
            public string TestString { get; set; }
        }

        private class OtherTestEventArgs : EventArgs { }

        private static AbstractState CreateTestState()
        {
            return new TestState();
        }

        [Fact]
        public void new_state_has_correct_parent()
        {
            var rootState = CreateTestState();

            var childState = CreateTestState();
            rootState.AddChild(childState, "foo");

            Assert.Equal(rootState, childState.Parent);
        }

        [Fact]
        public void calling_update_on_state_triggers_update_action()
        {
            var state = CreateTestState();

            var timesUpdateCalled = 0;
            state.SetUpdateAction(dt => timesUpdateCalled++);

            state.Update(1.0f);

            Assert.Equal(1, timesUpdateCalled);
        }

        [Fact]
        public void calling_enter_on_state_triggers_enter_action()
        {
            var state = CreateTestState();

            var timesEnterCalled = 0;
            state.SetEnterAction(() => timesEnterCalled++);

            state.Enter();

            Assert.Equal(1, timesEnterCalled);
        }

        [Fact]
        public void calling_exit_on_state_triggers_exit_action()
        {
            var state = CreateTestState();

            var timesExitCalled = 0;
            state.SetExitAction(() => timesExitCalled++);

            state.Exit();

            Assert.Equal(1, timesExitCalled);
        }

        [Fact]
        public void update_action_has_correct_delta_time()
        {
            var state = CreateTestState();

            var expectedDelta = 123f;
            var actualDelta = -1f;

            state.SetUpdateAction(dt => actualDelta = dt);

            state.Update(expectedDelta);

            Assert.Equal(expectedDelta, actualDelta);
        }

        [Fact]
        public void enter_is_called_on_active_state()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");

            mockState.Verify(state => state.Enter(), Times.Once());
        }

        [Fact]
        public void change_to_non_existant_state_throws_exception()
        {
            var rootState = CreateTestState();

            Assert.Throws<ApplicationException>(() => rootState.ChangeState("unknown state"));
        }

        [Fact]
        public void changing_to_non_existant_state_leaves_state_intact()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");

            Assert.Throws<ApplicationException>(() => rootState.ChangeState("some other non-existant state"));

            mockState.Verify(state => state.Exit(), Times.Never());
        }

        [Fact]
        public void new_state_is_inactive_by_default()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.Update(1.0f);

            mockState.Verify(state => state.Enter(), Times.Never());
            mockState.Verify(state => state.Update(It.IsAny<float>()), Times.Never());
            mockState.Verify(state => state.Exit(), Times.Never());
        }

        [Fact]
        public void updating_root_state_updates_active_child()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");
            rootState.Update(1.0f);

            mockState.Verify(state => state.Update(1.0f), Times.Once());
        }

        [Fact]
        public void push_state_enters_new_state()
        {
            var rootState = CreateTestState();

            var childState = CreateTestState();
            rootState.AddChild(childState, "foo");

            var mockStateToPush = new Mock<IState>();
            rootState.AddChild(mockStateToPush.Object, "bar");

            rootState.ChangeState("foo");
            rootState.PushState("bar");

            mockStateToPush.Verify(state => state.Enter(), Times.Once());
        }

        [Fact]
        public void push_non_existant_state_throws_exception()
        {
            var rootState = CreateTestState();

            Assert.Throws<ApplicationException>(() => rootState.PushState("unknown state"));
        }

        [Fact]
        public void add_child_with_already_existing_name_throws_exception()
        {
            var rootState = CreateTestState();

            var firstChildState = CreateTestState();
            var secondChildState = CreateTestState();

            rootState.AddChild(firstChildState, "test");

            Assert.Throws<ApplicationException>(() => rootState.AddChild(secondChildState, "test"));
        }

        [Fact]
        public void pop_state_exits_the_current_state()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");
            rootState.PopState();

            mockState.Verify(state => state.Exit(), Times.Once());
        }

        [Fact]
        public void pop_state_returns_to_previous_state_in_stack()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");
            var pushedState = CreateTestState();
            rootState.AddChild(pushedState, "bar");

            rootState.ChangeState("foo");
            rootState.PushState("bar");
            rootState.Update(1.0f);

            rootState.PopState();
            rootState.Update(1.0f);

            mockState.Verify(state => state.Update(It.IsAny<float>()), Times.Once());
        }

        [Fact]
        public void pop_state_with_no_children_throws_exception()
        {
            var rootState = CreateTestState();

            Assert.Throws<ApplicationException>(() => rootState.PopState());
        }

        [Fact]
        public void enter_is_not_triggered_on_states_that_have_already_been_entered()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");
            var pushedState = CreateTestState();
            rootState.AddChild(pushedState, "bar");

            rootState.ChangeState("foo");
            rootState.PushState("bar");
            rootState.PopState();

            mockState.Verify(state => state.Enter(), Times.Once());
        }

        [Fact]
        public void update_is_only_called_on_top_of_stack()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            var topState = CreateTestState();

            rootState.AddChild(mockState.Object, "foo");
            rootState.AddChild(topState, "bar");

            rootState.ChangeState("foo");
            rootState.PushState("bar");

            rootState.Update(1.0f);

            mockState.Verify(m => m.Update(It.IsAny<float>()), Times.Never());
        }

        [Fact]
        public void update_is_no_longer_called_on_deactivated_states()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");
            rootState.Update(1.0f);
            rootState.PopState();
            rootState.Update(1.0f);

            mockState.Verify(state => state.Update(It.IsAny<float>()), Times.Once());
        }

        [Fact]
        public void condition_is_triggered_on_update()
        {
            var rootState = CreateTestState();

            var timesConditionMet = 0;

            rootState.SetCondition(() => true, () => timesConditionMet++);

            rootState.Update(1.0f);

            Assert.Equal(1, timesConditionMet);
        }

        [Fact]
        public void condition_is_only_triggered_when_met()
        {
            var rootState = CreateTestState();

            var testCondition = false;
            var timesConditionMet = 0;

            rootState.SetCondition(() => testCondition, () => timesConditionMet++);

            rootState.Update(1.0f);

            testCondition = true;

            rootState.Update(1.0f);

            Assert.Equal(1, timesConditionMet);
        }

        [Fact]
        public void update_action_is_only_invoked_on_active_child()
        {
            var rootState = CreateTestState();
            var childState = CreateTestState();

            var updateCalledOnRootState = false;
            var updateCalledOnChildState = false;

            rootState.SetUpdateAction(dt => updateCalledOnRootState = true);
            childState.SetUpdateAction(dt => updateCalledOnChildState = true);

            rootState.AddChild(childState, "foo");
            rootState.PushState("foo");

            rootState.Update(1.0f);

            Assert.False(updateCalledOnRootState);
            Assert.True(updateCalledOnChildState);
        }
        
        [Fact]
        public void add_child_without_name_takes_name_from_class_name()
        {
            var rootState = CreateTestState();
            var childState = CreateTestState();
            rootState.AddChild(childState);

            var timesEnterCalledOnChild = 0;

            childState.SetEnterAction(() => timesEnterCalledOnChild++);

            rootState.PushState(childState.GetType().Name);

            Assert.Equal(1, timesEnterCalledOnChild);
        }

        [Fact]
        public void exiting_state_exits_all_children()
        {
            var rootState = CreateTestState();

            var mockState1 = new Mock<IState>();
            var mockState2 = new Mock<IState>();

            rootState.AddChild(mockState1.Object, "foo");
            rootState.AddChild(mockState2.Object, "bar");

            rootState.PushState("foo");
            rootState.PushState("bar");

            rootState.Exit();

            mockState1.Verify(m => m.Exit(), Times.Once());
            mockState2.Verify(m => m.Exit(), Times.Once());
        }

        [Fact]
        public void set_event_makes_event_triggerable_by_name()
        {
            var rootState = CreateTestState();

            var calls = 0;

            rootState.SetEvent("foo", _ => calls++);

            rootState.TriggerEvent("foo");

            Assert.Equal(1, calls);
        }

        [Fact]
        public void events_are_only_triggered_on_top_of_stack()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            var topState = CreateTestState();

            rootState.AddChild(mockState.Object, "foo");
            rootState.AddChild(topState, "bar");

            rootState.ChangeState("foo");
            rootState.PushState("bar");

            rootState.TriggerEvent("someEvent");

            mockState.Verify(m => m.TriggerEvent(It.IsAny<string>()), Times.Never());
        }

        [Fact]
        public void events_are_only_triggered_on_active_child()
        {
            var rootState = CreateTestState();
            var childState = CreateTestState();

            var rootStateCalls = 0;
            var childStateCalls = 0;

            rootState.SetEvent("foo", _ => rootStateCalls++);
            childState.SetEvent("foo", _ => childStateCalls++);

            rootState.AddChild(childState, "child state");
            rootState.PushState("child state");

            rootState.TriggerEvent("foo");

            Assert.Equal(0, rootStateCalls);
            Assert.Equal(1, childStateCalls);
        }

        [Fact]
        public void events_are_no_longer_triggered_on_deactivated_states()
        {
            var rootState = CreateTestState();

            var mockState = new Mock<IState>();
            rootState.AddChild(mockState.Object, "foo");

            rootState.ChangeState("foo");
            rootState.TriggerEvent("someEvent");
            rootState.PopState();
            rootState.TriggerEvent("someEvent");

            mockState.Verify(state => state.TriggerEvent(It.IsAny<string>(), It.IsAny<EventArgs>()), Times.Once());
        }

        [Fact]
        public void event_args_are_empty_when_not_specified()
        {
            var rootState = CreateTestState();

            var calls = 0;

            rootState.SetEvent("foo", eventArgs => {
                if (eventArgs == EventArgs.Empty)
                {
                    calls++;
                }
            });

            rootState.TriggerEvent("foo");

            Assert.Equal(1, calls);
        }

        [Fact]
        public void event_args_are_passed_on_to_child_event()
        {
            var rootState = CreateTestState();

            var expectedString = "test";
            var actualString = string.Empty;

            var testEventArgs = new TestEventArgs { TestString = expectedString };

            var childState = CreateTestState();
            rootState.AddChild(childState, "Child");

            childState.SetEvent("foo", eventArgs => {
                var actualEventArgs = (TestEventArgs)eventArgs;

                actualString = actualEventArgs.TestString;
            });

            rootState.PushState("Child");

            rootState.TriggerEvent("foo", testEventArgs);

            Assert.Equal(expectedString, actualString);
        }

        [Fact]
        public void triggering_event_with_incorrect_type_of_EventArgs_throws_exception()
        {
            var rootState = CreateTestState();

            rootState.SetEvent<OtherTestEventArgs>("foo", _ => { });

            Assert.Throws<ApplicationException>(() =>
                rootState.TriggerEvent("foo", new TestEventArgs()));
        }
    }
}
