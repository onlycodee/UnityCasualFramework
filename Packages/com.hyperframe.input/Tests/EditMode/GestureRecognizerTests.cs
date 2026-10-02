using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HyperFrame.Input.Tests
{
    public class GestureRecognizerTests
    {
        GestureRecognizer _g;
        readonly List<string> _log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _g = new GestureRecognizer(new GestureSettings(), dpi: 160f); // 1 dp = 1 px
            _g.Tapped += e => _log.Add("tap");
            _g.DoubleTapped += e => _log.Add("double");
            _g.LongPressed += e => _log.Add("long");
            _g.Held += e => _log.Add("hold:" + e.Phase);
            _g.Swiped += e => _log.Add("swipe:" + e.Direction);
            _g.Dragged += e => _log.Add("drag:" + e.Phase);
            _g.Pinched += e => _log.Add("pinch:" + e.Phase);
        }

        void Tap(Vector2 p, float t)
        {
            _g.PointerDown(0, p, t);
            _g.PointerUp(0, p, t + 0.05f);
        }

        [Test]
        public void QuickPressRelease_IsTap()
        {
            Tap(new Vector2(100, 100), 0f);
            CollectionAssert.AreEqual(new[] { "tap" }, _log);
        }

        [Test]
        public void TwoQuickTaps_AreTwoTapsPlusDoubleTap()
        {
            Tap(new Vector2(100, 100), 0f);
            Tap(new Vector2(105, 100), 0.2f);
            CollectionAssert.AreEqual(new[] { "tap", "tap", "double" }, _log);
        }

        [Test]
        public void SlowSecondTap_IsNotDoubleTap()
        {
            Tap(new Vector2(100, 100), 0f);
            Tap(new Vector2(100, 100), 1f);
            CollectionAssert.AreEqual(new[] { "tap", "tap" }, _log);
        }

        [Test]
        public void LongHold_IsLongPressAndHold_NotTap()
        {
            _g.PointerDown(0, new Vector2(10, 10), 0f);
            _g.Update(0.3f);
            Assert.IsEmpty(_log);
            _g.Update(0.6f);
            _g.PointerUp(0, new Vector2(10, 10), 1f);
            CollectionAssert.AreEqual(new[] { "long", "hold:Began", "hold:Ended" }, _log);
        }

        [Test]
        public void FastFlick_IsSwipe_WithDirection()
        {
            _g.PointerDown(0, new Vector2(0, 0), 0f);
            _g.PointerMove(0, new Vector2(0, 60), 0.05f);
            _g.PointerUp(0, new Vector2(0, 120), 0.1f);
            Assert.Contains("swipe:Up", _log);
            Assert.IsFalse(_log.Contains("tap"));
        }

        [Test]
        public void SlowMove_IsDrag_BeganMovedEnded()
        {
            _g.PointerDown(0, new Vector2(0, 0), 0f);
            _g.PointerMove(0, new Vector2(30, 0), 0.3f);
            _g.PointerMove(0, new Vector2(60, 0), 0.6f);
            _g.PointerUp(0, new Vector2(60, 0), 0.9f);
            CollectionAssert.AreEqual(new[] { "drag:Began", "drag:Moved", "drag:Ended" }, _log);
        }

        [Test]
        public void SmallJitter_StaysATap()
        {
            _g.PointerDown(0, new Vector2(0, 0), 0f);
            _g.PointerMove(0, new Vector2(5, 3), 0.05f);
            _g.PointerUp(0, new Vector2(5, 3), 0.1f);
            CollectionAssert.AreEqual(new[] { "tap" }, _log);
        }

        [Test]
        public void SecondFinger_StartsPinch_AndCancelsDrag()
        {
            _g.PointerDown(0, new Vector2(0, 0), 0f);
            _g.PointerMove(0, new Vector2(40, 0), 0.3f);
            _g.PointerDown(1, new Vector2(140, 0), 0.4f);
            _g.PointerMove(1, new Vector2(240, 0), 0.5f);
            _g.PointerUp(1, new Vector2(240, 0), 0.6f);
            _g.PointerUp(0, new Vector2(40, 0), 0.7f);
            CollectionAssert.AreEqual(new[] { "drag:Began", "drag:Cancelled", "pinch:Began", "pinch:Moved", "pinch:Ended" }, _log);
        }

        [Test]
        public void Pinch_ReportsScale()
        {
            float scale = 0;
            _g.Pinched += e => { if (e.Phase == GesturePhase.Moved) scale = e.Scale; };
            _g.PointerDown(0, new Vector2(0, 0), 0f);
            _g.PointerDown(1, new Vector2(100, 0), 0f);
            _g.PointerMove(1, new Vector2(200, 0), 0.1f);
            Assert.AreEqual(2f, scale, 1e-4);
        }

        [Test]
        public void Thresholds_ScaleWithDpi()
        {
            var g = new GestureRecognizer(new GestureSettings(), dpi: 480f); // 1 dp = 3 px, slop 36 px
            int taps = 0;
            g.Tapped += _ => taps++;
            g.PointerDown(0, Vector2.zero, 0f);
            g.PointerUp(0, new Vector2(30, 0), 0.1f);
            Assert.AreEqual(1, taps);
        }
    }

    public class InputLockAndServiceTests
    {
        [Test]
        public void Lock_IsReferenceCounted_AndReportsReasons()
        {
            var l = new InputLock();
            var a = l.Acquire("popup");
            var b = l.Acquire("anim");
            CollectionAssert.AreEquivalent(new[] { "popup", "anim" }, l.Reasons);
            a.Dispose();
            a.Dispose();
            Assert.IsTrue(l.IsLocked);
            b.Dispose();
            Assert.IsFalse(l.IsLocked);
        }

        [Test]
        public void LockedService_IgnoresInjectedInput()
        {
            var s = new InputService();
            int taps = 0;
            s.Gestures.Tapped += _ => taps++;
            using (s.Lock.Acquire("test"))
                s.InjectTap(new Vector2(10, 10));
            Assert.AreEqual(0, taps);
            s.InjectTap(new Vector2(10, 10));
            Assert.AreEqual(1, taps);
        }

        [Test]
        public void AcquiringLock_MidDrag_CancelsDrag()
        {
            var s = new InputService();
            var phases = new List<GesturePhase>();
            s.Gestures.Dragged += e => phases.Add(e.Phase);
            s.InjectPointerDown(1, Vector2.zero);
            s.InjectPointerMove(1, new Vector2(100, 0));
            s.Lock.Acquire("popup");
            CollectionAssert.AreEqual(new[] { GesturePhase.Began, GesturePhase.Cancelled }, phases);
        }

        [Test]
        public void InjectedTaps_AreNotMistakenForDoubleTaps()
        {
            var s = new InputService();
            int doubles = 0, taps = 0;
            s.Gestures.Tapped += _ => taps++;
            s.Gestures.DoubleTapped += _ => doubles++;
            s.InjectTap(new Vector2(10, 10));
            s.InjectTap(new Vector2(10, 10));
            Assert.AreEqual(2, taps);
            Assert.AreEqual(0, doubles);
        }

        [Test]
        public void InjectDrag_ProducesDragNotSwipe()
        {
            var s = new InputService();
            var log = new List<string>();
            s.Gestures.Dragged += e => log.Add(e.Phase.ToString());
            s.Gestures.Swiped += _ => log.Add("swipe");
            s.InjectDrag(Vector2.zero, new Vector2(300, 0));
            Assert.AreEqual("Began", log[0]);
            Assert.AreEqual("Ended", log[log.Count - 1]);
            Assert.IsFalse(log.Contains("swipe"));
        }

        [Test]
        public void Back_IsNotBlockedByLock()
        {
            var s = new InputService();
            int backs = 0;
            s.BackPressed += () => backs++;
            s.Lock.Acquire("popup");
            s.InjectBack();
            Assert.AreEqual(1, backs);
        }
    }
}
