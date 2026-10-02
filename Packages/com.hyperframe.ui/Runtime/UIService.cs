using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HyperFrame.Core;
using HyperFrame.Input;
using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.UI
{
    public struct PopupOptions
    {
        /// <summary>Higher shows first when several popups are queued.</summary>
        public int Priority;
        /// <summary>Show on top of the current popup instead of waiting in the queue (e.g. Settings from Pause).</summary>
        public bool Stack;

        public static PopupOptions Default => new PopupOptions();
        public static PopupOptions Stacked => new PopupOptions { Stack = true };
    }

    public struct ViewShownEvent { public UIView View; }
    public struct ViewHiddenEvent { public UIView View; }
    public struct ButtonClickedEvent { public UIView View; public string Button; }

    public interface IUIService
    {
        UIRoot Root { get; }
        UITheme Theme { get; }
        UIScreen CurrentScreen { get; }
        UIPopup TopPopup { get; }
        bool IsPopupOpen { get; }
        int QueuedPopupCount { get; }

        /// <summary>Clears the screen stack and shows this screen.</summary>
        T ShowScreen<T>(object args = null) where T : UIScreen;
        /// <summary>Shows a screen on top of the current one; <see cref="PopScreen"/> returns to it.</summary>
        T PushScreen<T>(object args = null) where T : UIScreen;
        void PopScreen();

        /// <summary>Shows (or queues) a popup and completes when it closes.</summary>
        Task<PopupResult> ShowPopup<T>(object args = null, PopupOptions options = default) where T : UIPopup;
        void ClosePopup(UIPopup popup, PopupResult result);
        void CloseAllPopups();

        /// <summary>Returns the view instance (created on first use).</summary>
        T GetView<T>() where T : UIView;

        /// <summary>Routes a back press: top popup → current screen → previous screen. True if handled.</summary>
        bool HandleBack();
        event Action BackUnhandled;

        void Toast(string message, float seconds = 2f);
        void SetLoading(bool visible, string message = null);
        bool IsLoading { get; }
        void FloatingText(string text, Vector2 screenPosition, ThemeColor color = ThemeColor.Accent);

        void SetTheme(UITheme theme);
        void NotifyButtonClicked(UIView view, string buttonName);
    }

    /// <summary>Screen stack + popup queue + overlays (UI-01, UI-02, UI-06, UI-09, UI-11).</summary>
    public sealed class UIService : IUIService
    {
        sealed class PendingPopup
        {
            public Type Type;
            public object Args;
            public TaskCompletionSource<PopupResult> Completion;
        }

        readonly TweenEngine _tweens;
        readonly InputLock _gameplayLock;
        readonly IEventBus _events;
        readonly IViewProvider _provider;
        readonly Dictionary<Type, UIView> _views = new Dictionary<Type, UIView>();
        readonly List<(UIScreen screen, object args)> _screens = new List<(UIScreen, object)>();
        readonly List<UIPopup> _popups = new List<UIPopup>();
        readonly PopupQueue<PendingPopup> _queue = new PopupQueue<PendingPopup>();
        IDisposable _popupLock;
        RectTransform _loading;

        public UIRoot Root { get; }
        public UITheme Theme { get; private set; }
        public UIScreen CurrentScreen => _screens.Count > 0 ? _screens[_screens.Count - 1].screen : null;
        public UIPopup TopPopup => _popups.Count > 0 ? _popups[_popups.Count - 1] : null;
        public bool IsPopupOpen => _popups.Count > 0;
        public int QueuedPopupCount => _queue.Count;
        public bool IsLoading => _loading != null && _loading.gameObject.activeSelf;
        public event Action BackUnhandled;

        public UIService(UIRoot root, TweenEngine tweens, InputLock gameplayLock, IEventBus events,
            IViewProvider provider = null, UITheme theme = null)
        {
            Root = root;
            _tweens = tweens;
            _gameplayLock = gameplayLock;
            _events = events;
            _provider = provider ?? new DefaultViewProvider(null);
            Theme = theme != null ? theme : UITheme.CreateDefault();
        }

        public T GetView<T>() where T : UIView => (T)GetView(typeof(T));

        UIView GetView(Type type)
        {
            if (_views.TryGetValue(type, out var view) && view != null) return view;
            bool isPopup = typeof(UIPopup).IsAssignableFrom(type);
            view = _provider.Create(type, isPopup ? Root.PopupLayer : Root.ScreenLayer);
            view.Initialize(this, _tweens);
            _views[type] = view;
            return view;
        }

        // ── Screens ─────────────────────────────────────────────────────────────────────

        public T ShowScreen<T>(object args = null) where T : UIScreen
        {
            var next = GetView<T>();
            foreach (var (screen, _) in _screens)
                if (screen != next) Hide(screen);
            _screens.Clear();
            _screens.Add((next, args));
            Show(next, args);
            return next;
        }

        public T PushScreen<T>(object args = null) where T : UIScreen
        {
            var next = GetView<T>();
            var current = CurrentScreen;
            if (current == next) return next;
            if (current != null) Hide(current);
            _screens.Add((next, args));
            Show(next, args);
            return next;
        }

        public void PopScreen()
        {
            if (_screens.Count <= 1) return;
            var top = _screens[_screens.Count - 1];
            _screens.RemoveAt(_screens.Count - 1);
            Hide(top.screen);
            var previous = _screens[_screens.Count - 1];
            Show(previous.screen, previous.args);
        }

        // ── Popups ──────────────────────────────────────────────────────────────────────

        public Task<PopupResult> ShowPopup<T>(object args = null, PopupOptions options = default) where T : UIPopup
        {
            var pending = new PendingPopup
            {
                Type = typeof(T),
                Args = args,
                Completion = new TaskCompletionSource<PopupResult>()
            };
            bool sameTypeOpen = _popups.Exists(p => p.GetType() == typeof(T));
            if (!sameTypeOpen && (_popups.Count == 0 || options.Stack)) Open(pending);
            else _queue.Enqueue(pending, options.Priority);
            return pending.Completion.Task;
        }

        void Open(PendingPopup pending)
        {
            var popup = (UIPopup)GetView(pending.Type);
            popup.Completion = pending.Completion;
            _popups.Add(popup);
            if (_popupLock == null) _popupLock = _gameplayLock?.Acquire("popup");
            RefreshDimmer();
            Show(popup, pending.Args);
        }

        public void ClosePopup(UIPopup popup, PopupResult result)
        {
            if (popup == null || !_popups.Remove(popup)) return;
            var completion = popup.Completion;
            popup.Completion = null;
            Hide(popup);
            RefreshDimmer();

            if (_popups.Count == 0)
            {
                if (_queue.TryDequeue(out var next)) Open(next);
                else
                {
                    _popupLock?.Dispose();
                    _popupLock = null;
                }
            }
            completion?.TrySetResult(result);
        }

        public void CloseAllPopups()
        {
            while (_queue.TryDequeue(out var pending)) pending.Completion.TrySetResult(new PopupResult(PopupResult.Close));
            for (int i = _popups.Count - 1; i >= 0; i--) ClosePopup(_popups[i], new PopupResult(PopupResult.Close));
        }

        void RefreshDimmer()
        {
            var dimmer = Root.PopupDimmer;
            dimmer.color = Theme.overlay;
            bool show = _popups.Count > 0;
            if (dimmer.gameObject.activeSelf == show) return;
            dimmer.gameObject.SetActive(show);
            if (show)
            {
                var group = dimmer.GetOrAddComponent<CanvasGroup>();
                group.alpha = 0f;
                _tweens.To(0f, 1f, 0.15f, a => group.alpha = a, Ease.Linear, TimeMode.Unscaled).SetTarget(dimmer);
            }
        }

        // ── Back button ─────────────────────────────────────────────────────────────────

        public bool HandleBack()
        {
            if (IsLoading) return true;
            var popup = TopPopup;
            if (popup != null) return popup.OnBack();
            var screen = CurrentScreen;
            if (screen != null && screen.OnBack()) return true;
            if (_screens.Count > 1)
            {
                PopScreen();
                return true;
            }
            BackUnhandled?.Invoke();
            return false;
        }

        // ── Overlays (UI-06) ────────────────────────────────────────────────────────────

        public void Toast(string message, float seconds = 2f)
        {
            var b = new UIBuilder(Theme);
            var panel = UIBuilder.CreateRect("Toast", Root.OverlayLayer);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 0.15f), Vector2.zero);
            panel.sizeDelta = new Vector2(900f, 140f);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            bg.raycastTarget = false;
            var label = b.Label(panel, "txt_Toast", message, 0, ThemeColor.TextOnPrimary);
            UIBuilder.Stretch((RectTransform)label.transform);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            _tweens.To(0f, 1f, 0.2f, a => group.alpha = a, Ease.OutQuad, TimeMode.Unscaled);
            _tweens.To(1f, 0f, 0.3f, a => group.alpha = a, Ease.InQuad, TimeMode.Unscaled, delay: Mathf.Max(0.3f, seconds))
                .OnComplete(() => { if (panel != null) UnityEngine.Object.Destroy(panel.gameObject); });
        }

        public void SetLoading(bool visible, string message = null)
        {
            if (_loading == null)
            {
                var b = new UIBuilder(Theme);
                _loading = UIBuilder.CreateRect("Loading", Root.OverlayLayer);
                UIBuilder.Stretch(_loading);
                var bg = _loading.gameObject.AddComponent<Image>();
                bg.color = Theme.background;
                bg.raycastTarget = true; // blocks all input while loading
                b.Label(_loading, "txt_Loading", "Loading...", Theme.titleFontSize, ThemeColor.TextOnPrimary);
            }
            if (message != null) UIBuilder.SetLabel(_loading.Find("txt_Loading"), message);
            _loading.SetAsLastSibling();
            _loading.gameObject.SetActive(visible);
        }

        public void FloatingText(string text, Vector2 screenPosition, ThemeColor color = ThemeColor.Accent)
        {
            var b = new UIBuilder(Theme);
            var label = b.Label(Root.OverlayLayer, "FloatingText", text, Theme.titleFontSize, color, 600f);
            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var start = Root.ScreenToOverlay(screenPosition);
            rt.anchoredPosition = start;
            _tweens.To(0f, 1f, 0.9f, t =>
            {
                rt.anchoredPosition = start + new Vector2(0f, 160f * t);
                var c = label.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                label.color = c;
            }, Ease.OutCubic, TimeMode.Unscaled).OnComplete(() => { if (rt != null) UnityEngine.Object.Destroy(rt.gameObject); });
        }

        // ── Theme ───────────────────────────────────────────────────────────────────────

        public void SetTheme(UITheme theme)
        {
            Theme = theme != null ? theme : UITheme.CreateDefault();
            foreach (var g in Root.GetComponentsInChildren<ThemedGraphic>(true)) g.Apply(Theme);
            Root.PopupDimmer.color = Theme.overlay;
        }

        public void NotifyButtonClicked(UIView view, string buttonName) =>
            _events?.Publish(new ButtonClickedEvent { View = view, Button = buttonName });

        // ── helpers ─────────────────────────────────────────────────────────────────────

        void Show(UIView view, object args)
        {
            view.ShowInternal(args).Forget("UI.Show");
            _events?.Publish(new ViewShownEvent { View = view });
        }

        void Hide(UIView view)
        {
            if (!view.IsVisible) return;
            view.HideInternal().Forget("UI.Hide");
            _events?.Publish(new ViewHiddenEvent { View = view });
        }
    }
}
