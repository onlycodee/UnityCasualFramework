using NUnit.Framework;

namespace HyperFrame.Core.Tests
{
    public class TweenEngineTests
    {
        GameClock _clock;
        TweenEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _clock = new GameClock();
            _engine = new TweenEngine(_clock);
        }

        void Step(float dt, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _clock.Advance(dt, dt);
                _engine.Tick(_clock.IsPaused ? 0f : dt, dt);
            }
        }

        [Test]
        public void To_ReachesEndValue_AndCompletes()
        {
            float value = -1;
            bool done = false;
            _engine.To(0f, 10f, 1f, v => value = v, Ease.Linear).OnComplete(() => done = true);
            Step(0.5f);
            Assert.AreEqual(5f, value, 1e-4);
            Assert.IsFalse(done);
            Step(0.6f);
            Assert.AreEqual(10f, value, 1e-4);
            Assert.IsTrue(done);
            Assert.AreEqual(0, _engine.ActiveCount);
        }

        [Test]
        public void GameTween_StopsWhilePaused_UnscaledKeepsRunning()
        {
            float game = 0, ui = 0;
            _engine.To(0f, 1f, 1f, v => game = v, Ease.Linear, TimeMode.Game);
            _engine.To(0f, 1f, 1f, v => ui = v, Ease.Linear, TimeMode.Unscaled);
            _clock.Pause("test");
            Step(0.5f);
            Assert.AreEqual(0f, game, 1e-4);
            Assert.AreEqual(0.5f, ui, 1e-4);
            _clock.Resume("test");
            Step(0.5f);
            Assert.AreEqual(0.5f, game, 1e-4);
        }

        [Test]
        public void Delay_FiresOnce_AfterTime()
        {
            int fired = 0;
            _engine.Delay(0.3f, () => fired++);
            Step(0.1f, 2);
            Assert.AreEqual(0, fired);
            Step(0.1f, 5);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Repeat_FiresCountTimes()
        {
            int fired = 0;
            _engine.Repeat(0.1f, () => fired++, count: 3);
            Step(0.1f, 10);
            Assert.AreEqual(3, fired);
            Assert.AreEqual(0, _engine.ActiveCount);
        }

        [Test]
        public void Kill_WithComplete_JumpsToEnd()
        {
            float value = 0;
            bool done = false;
            var h = _engine.To(0f, 1f, 5f, v => value = v, Ease.Linear).OnComplete(() => done = true);
            h.Kill(complete: true);
            Assert.AreEqual(1f, value);
            Assert.IsTrue(done);
            Assert.IsFalse(h.IsActive);
        }

        [Test]
        public void Kill_WithoutComplete_SkipsOnComplete_AndTaskCompletes()
        {
            bool done = false;
            var h = _engine.To(0f, 1f, 5f, _ => { }).OnComplete(() => done = true);
            var task = h.AsTask();
            h.Kill();
            Assert.IsFalse(done);
            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void KillTarget_KillsOnlyThatTargetsTweens()
        {
            var a = new object();
            var b = new object();
            _engine.To(0f, 1f, 1f, _ => { }).SetTarget(a);
            _engine.To(0f, 1f, 1f, _ => { }).SetTarget(a);
            var keep = _engine.To(0f, 1f, 1f, _ => { }).SetTarget(b);
            Assert.AreEqual(2, _engine.KillTarget(a));
            Assert.AreEqual(1, _engine.ActiveCount);
            Assert.IsTrue(keep.IsActive);
        }

        [Test]
        public void ThrowingTween_IsStopped()
        {
            HFLog.Sink = new TestLogSink();
            try
            {
                _engine.To(0f, 1f, 1f, _ => throw new System.Exception("bad"));
                Step(0.1f);
                Assert.AreEqual(0, _engine.ActiveCount);
            }
            finally { HFLog.Sink = null; }
        }

        [Test]
        public void ReusedTweenObject_DoesNotRevivOldHandle()
        {
            var old = _engine.To(0f, 1f, 0.1f, _ => { });
            Step(0.2f);
            var fresh = _engine.To(0f, 1f, 1f, _ => { });
            Assert.IsFalse(old.IsActive);
            Assert.IsTrue(fresh.IsActive);
        }

        [Test]
        public void Pause_IsReferenceCounted()
        {
            _clock.Pause("a");
            _clock.Pause("b");
            _clock.Resume("a");
            Assert.IsTrue(_clock.IsPaused);
            _clock.Resume("b");
            Assert.IsFalse(_clock.IsPaused);
        }

        [TestCase(Ease.Linear)] [TestCase(Ease.OutQuad)] [TestCase(Ease.OutBack)] [TestCase(Ease.OutBounce)] [TestCase(Ease.InOutCubic)]
        public void Easing_StartsAtZero_EndsAtOne(Ease ease)
        {
            Assert.AreEqual(0f, Easing.Evaluate(ease, 0f), 1e-5);
            Assert.AreEqual(1f, Easing.Evaluate(ease, 1f), 1e-5);
        }
    }
}
