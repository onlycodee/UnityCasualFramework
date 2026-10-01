using System.Threading.Tasks;
using HyperFrame.Core;
using HyperFrame.Input;
using NUnit.Framework;
using UnityEngine;

namespace HyperFrame.UI.Tests
{
    public class PopupQueueTests
    {
        [Test]
        public void HigherPriorityFirst_FifoWithinPriority()
        {
            var q = new PopupQueue<string>();
            q.Enqueue("low1", 0);
            q.Enqueue("high", 10);
            q.Enqueue("low2", 0);
            q.Enqueue("mid", 5);
            var order = new System.Collections.Generic.List<string>();
            while (q.TryDequeue(out var s)) order.Add(s);
            CollectionAssert.AreEqual(new[] { "high", "mid", "low1", "low2" }, order);
        }
    }

    /// <summary>UIService with real uGUI objects, ticked manually (no Play Mode needed).</summary>
    public class UIServiceTests
    {
        UIRoot _root;
        TweenEngine _tweens;
        InputLock _lock;
        UIService _ui;

        [SetUp]
        public void SetUp()
        {
            _root = UIRoot.Create(null);
            _tweens = new TweenEngine();
            _lock = new InputLock();
            _ui = new UIService(_root, _tweens, _lock, new EventBus());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root.gameObject);
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null) Object.DestroyImmediate(es.gameObject);
        }

        void FinishTransitions() => _tweens.Tick(1f, 1f);

        [Test]
        public void ShowScreen_ReplacesCurrent()
        {
            var a = _ui.ShowScreen<TestScreenA>();
            FinishTransitions();
            var b = _ui.ShowScreen<TestScreenB>();
            FinishTransitions();
            Assert.AreSame(b, _ui.CurrentScreen);
            Assert.IsFalse(a.IsVisible);
            Assert.IsFalse(a.gameObject.activeSelf);
            Assert.AreEqual("B", b.GetText("txt_Title"));
        }

        [Test]
        public void PushedScreen_ClosesWithBack()
        {
            _ui.ShowScreen<TestScreenA>();
            _ui.PushScreen<TestScreenB>();
            Assert.IsTrue(_ui.HandleBack());
            Assert.IsInstanceOf<TestScreenA>(_ui.CurrentScreen);
            Assert.IsFalse(_ui.HandleBack(), "nothing left to go back to");
        }

        [Test]
        public void Popup_LocksGameplayInput_UntilClosed_AndReturnsResult()
        {
            var task = _ui.ShowPopup<TestPopupA>();
            FinishTransitions();
            Assert.IsTrue(_lock.IsLocked);
            Assert.IsTrue(_root.PopupDimmer.gameObject.activeSelf);

            Assert.IsTrue(_ui.GetView<TestPopupA>().Click("btn_Ok"));
            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual("ok", task.Result.Action);
            Assert.IsFalse(_lock.IsLocked);
            Assert.IsFalse(_root.PopupDimmer.gameObject.activeSelf);
        }

        [Test]
        public void SecondPopup_IsQueued_ThenShown()
        {
            var first = _ui.ShowPopup<TestPopupA>();
            var second = _ui.ShowPopup<TestPopupB>();
            Assert.AreEqual(1, _ui.QueuedPopupCount);
            Assert.IsInstanceOf<TestPopupA>(_ui.TopPopup);

            _ui.TopPopup.Close("done");
            Assert.IsTrue(first.IsCompleted);
            Assert.IsInstanceOf<TestPopupB>(_ui.TopPopup);
            Assert.IsTrue(_lock.IsLocked, "lock stays while the queued popup shows");
            _ui.TopPopup.Close();
            Assert.IsTrue(second.IsCompleted);
            Assert.IsFalse(_lock.IsLocked);
        }

        [Test]
        public void StackedPopup_ShowsOverCurrent()
        {
            _ui.ShowPopup<TestPopupA>();
            _ui.ShowPopup<TestPopupB>(null, PopupOptions.Stacked);
            Assert.IsInstanceOf<TestPopupB>(_ui.TopPopup);
            Assert.AreEqual(0, _ui.QueuedPopupCount);
        }

        [Test]
        public void Back_ClosesTopPopup_WithBackAction()
        {
            var task = _ui.ShowPopup<TestPopupA>();
            Assert.IsTrue(_ui.HandleBack());
            Assert.AreEqual(PopupResult.Back, task.Result.Action);
        }

        [Test]
        public void Click_FailsWhileTransitionRunsOrHidden()
        {
            _ui.ShowPopup<TestPopupA>();
            var popup = _ui.GetView<TestPopupA>();
            Assert.IsFalse(popup.Click("btn_Ok"), "not interactable during the show transition");
            FinishTransitions();
            Assert.IsTrue(popup.Click("btn_Ok"));
            Assert.IsFalse(popup.Click("btn_Ok"), "hidden popups cannot be clicked");
        }

        [Test]
        public void CloseAllPopups_ResolvesQueuedOnesToo()
        {
            var a = _ui.ShowPopup<TestPopupA>();
            var b = _ui.ShowPopup<TestPopupB>();
            _ui.CloseAllPopups();
            Assert.IsTrue(a.IsCompleted && b.IsCompleted);
            Assert.IsFalse(_ui.IsPopupOpen);
            Assert.IsFalse(_lock.IsLocked);
        }

        [Test]
        public void SetTheme_RecolorsThemedGraphics()
        {
            _ui.ShowScreen<TestScreenA>();
            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.background = Color.red;
            _ui.SetTheme(theme);
            var bg = _ui.GetView<TestScreenA>().transform.Find("img_Background").GetComponent<UnityEngine.UI.Image>();
            Assert.AreEqual(Color.red, bg.color);
            Object.DestroyImmediate(theme);
        }
    }
}
