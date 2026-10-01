using System.Collections.Generic;
using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class StateMachineTests
    {
        enum S { A, B, C }

        [TearDown] public void TearDown() => HFLog.Sink = null;

        [Test]
        public void ChangeState_CallsExitThenEnter_AndRaisesChanged()
        {
            var log = new List<string>();
            var sm = new StateMachine<S>()
                .Add(S.A, new DelegateState(() => log.Add("enterA"), () => log.Add("exitA")))
                .Add(S.B, new DelegateState(() => log.Add("enterB")));
            sm.Changed += (f, t) => log.Add($"{f}->{t}");
            sm.Start(S.A);
            sm.ChangeState(S.B);
            CollectionAssert.AreEqual(new[] { "enterA", "A->A", "exitA", "enterB", "A->B" }, log);
            Assert.AreEqual(S.B, sm.Current);
            Assert.AreEqual(S.A, sm.Previous);
        }

        [Test]
        public void DisallowedTransition_IsRejected()
        {
            HFLog.Sink = new TestLogSink();
            var sm = new StateMachine<S>()
                .Add(S.A, new DelegateState()).Add(S.B, new DelegateState()).Add(S.C, new DelegateState())
                .Allow(S.A, S.B);
            sm.Start(S.A);
            Assert.IsFalse(sm.ChangeState(S.C));
            Assert.AreEqual(S.A, sm.Current);
            Assert.IsTrue(sm.ChangeState(S.B));
        }

        [Test]
        public void ChangeRequestedInsideEnter_IsQueuedAndApplied()
        {
            StateMachine<S> sm = null;
            var order = new List<S>();
            sm = new StateMachine<S>()
                .Add(S.A, new DelegateState(() => { order.Add(S.A); sm.ChangeState(S.B); }))
                .Add(S.B, new DelegateState(() => { order.Add(S.B); sm.ChangeState(S.C); }))
                .Add(S.C, new DelegateState(() => order.Add(S.C)));
            sm.Start(S.A);
            CollectionAssert.AreEqual(new[] { S.A, S.B, S.C }, order);
            Assert.AreEqual(S.C, sm.Current);
        }

        [Test]
        public void Tick_GoesToCurrentState()
        {
            float ticked = 0;
            var sm = new StateMachine<S>().Add(S.A, new DelegateState(tick: dt => ticked += dt));
            sm.Start(S.A);
            sm.Tick(0.5f);
            Assert.AreEqual(0.5f, ticked, 1e-6);
        }
    }
}
