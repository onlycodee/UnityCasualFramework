using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HyperFrame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HyperFrame.UI
{
    public enum ViewTransition { None, Fade, Scale, SlideUp, SlideLeft }

    /// <summary>
    /// Base class for screens and popups. A view finds its children by name, following the
    /// Figma → Unity naming convention (AI-07): <c>btn_Name</c> buttons, <c>txt_Name</c> labels,
    /// <c>img_Name</c> images. The same code therefore works with a designed prefab or with the
    /// code-built placeholder from <see cref="BuildPlaceholder"/> (used when no prefab is registered).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIView : MonoBehaviour
    {
        [SerializeField] protected ViewTransition transition = ViewTransition.Fade;
        [SerializeField] protected float transitionDuration = 0.2f;

        readonly Dictionary<string, Transform> _named = new Dictionary<string, Transform>();
        CanvasGroup _group;
        TweenEngine _tweens;
        TweenHandle _transitionTween;

        protected IUIService UI { get; private set; }
        protected UITheme Theme => UI?.Theme;
        public RectTransform Rect => (RectTransform)transform;
        public bool IsVisible { get; private set; }
        public CanvasGroup Group => _group;
        public ViewTransition Transition => transition;
        public float TransitionDuration => transitionDuration;

        internal void Initialize(IUIService ui, TweenEngine tweens)
        {
            UI = ui;
            _tweens = tweens;
            _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            Stretch(Rect);
            if (transform.childCount == 0) BuildPlaceholder(new UIBuilder(ui.Theme));
            IndexChildren();
            OnCreated();
            gameObject.SetActive(false);
        }

        /// <summary>Builds a default layout in code when no prefab exists (placeholder mode, AI-14).</summary>
        protected virtual void BuildPlaceholder(UIBuilder ui) { }

        /// <summary>Called once after creation. Bind buttons here.</summary>
        protected virtual void OnCreated() { }

        /// <summary>Called every time the view is shown, with the args passed to Show.</summary>
        protected virtual void OnShow(object args) { }

        protected virtual void OnHide() { }

        /// <summary>Android back / Escape. Return true if handled.</summary>
        public virtual bool OnBack() => false;

        internal Task ShowInternal(object args)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            _group.interactable = false;
            _group.blocksRaycasts = true;
            try { OnShow(args); }
            catch (Exception e) { HFLog.Exception(e, GetType().Name + ".OnShow"); }
            return PlayTransition(true, () => { if (IsVisible) _group.interactable = true; });
        }

        internal Task HideInternal()
        {
            if (!IsVisible) return Task.CompletedTask;
            IsVisible = false;
            _group.interactable = false;
            try { OnHide(); }
            catch (Exception e) { HFLog.Exception(e, GetType().Name + ".OnHide"); }
            return PlayTransition(false, () => { if (!IsVisible) gameObject.SetActive(false); });
        }

        Task PlayTransition(bool show, Action done)
        {
            _transitionTween.Kill();
            var rt = Rect;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
            _group.alpha = 1f;

            if (transition == ViewTransition.None || transitionDuration <= 0f || _tweens == null)
            {
                done();
                return Task.CompletedTask;
            }

            var size = rt.rect.size;
            Action<float> apply;
            Ease ease = show ? Ease.OutCubic : Ease.InCubic;
            switch (transition)
            {
                case ViewTransition.Scale:
                    ease = show ? Ease.OutBack : Ease.InBack;
                    apply = t => { rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t); _group.alpha = Mathf.Clamp01(t); };
                    break;
                case ViewTransition.SlideUp:
                    apply = t => rt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-size.y, 0f, t));
                    break;
                case ViewTransition.SlideLeft:
                    apply = t => rt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(size.x, 0f, t), 0f);
                    break;
                default:
                    apply = t => _group.alpha = t;
                    break;
            }

            float from = show ? 0f : 1f, to = show ? 1f : 0f;
            apply(from);
            _transitionTween = _tweens.To(from, to, transitionDuration, apply, ease, TimeMode.Unscaled)
                .SetTarget(this)
                .OnComplete(done);
            return _transitionTween.AsTask();
        }

        // ── Child lookup by naming convention ───────────────────────────────────────────

        void IndexChildren()
        {
            _named.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (!_named.ContainsKey(t.name)) _named.Add(t.name, t);
            }
        }

        /// <summary>Finds a descendant by exact name. Returns null (and logs) when missing.</summary>
        protected Transform Find(string childName)
        {
            if (_named.TryGetValue(childName, out var t) && t != null) return t;
            IndexChildren();
            if (_named.TryGetValue(childName, out t)) return t;
            HFLog.Warn(GetType().Name, $"Child '{childName}' not found.");
            return null;
        }

        protected T Get<T>(string childName) where T : Component
        {
            var t = Find(childName);
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>Hooks a button's click. Plays the "ui_click" sound through the UI service.</summary>
        protected Button Bind(string buttonName, Action onClick)
        {
            var button = Get<Button>(buttonName);
            if (button == null) return null;
            button.onClick.AddListener(() =>
            {
                UI?.NotifyButtonClicked(this, buttonName);
                try { onClick?.Invoke(); }
                catch (Exception e) { HFLog.Exception(e, $"{GetType().Name}.{buttonName}"); }
            });
            return button;
        }

        /// <summary>Sets text on a legacy Text or TextMeshPro label.</summary>
        protected void SetText(string labelName, string value)
        {
            var t = Find(labelName);
            if (t == null) return;
            UIBuilder.SetLabel(t, value);
        }

        protected void SetActive(string childName, bool active)
        {
            var t = Find(childName);
            if (t != null) t.gameObject.SetActive(active);
        }

        /// <summary>
        /// Presses a button by name like a player would (only if visible and interactable).
        /// Used by PlayMode tests, bots and the agent's MCP driving. Returns false if it could not click.
        /// </summary>
        public bool Click(string buttonName)
        {
            if (!IsVisible || !gameObject.activeInHierarchy) return false;
            var button = Get<Button>(buttonName);
            if (button == null || !button.IsInteractable() || !button.gameObject.activeInHierarchy) return false;
            button.onClick.Invoke();
            return true;
        }

        /// <summary>Reads a label's text (tests).</summary>
        public string GetText(string labelName)
        {
            var t = Find(labelName);
            return t != null ? UIBuilder.GetLabel(t) : null;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>A full-screen view. Only one screen is visible at a time (the top of the stack).</summary>
    public abstract class UIScreen : UIView { }

    public readonly struct PopupResult
    {
        public readonly string Action;
        public readonly object Data;
        public PopupResult(string action, object data = null) { Action = action; Data = data; }
        public bool Is(string action) => Action == action;
        public override string ToString() => Action;

        public const string Close = "close";
        public const string Back = "back";
    }

    /// <summary>A modal dialog shown over screens. Close it with <see cref="Close"/>; the caller gets the result.</summary>
    public abstract class UIPopup : UIView
    {
        [SerializeField] protected bool closeOnBack = true;

        protected UIPopup() => transition = ViewTransition.Scale;

        internal TaskCompletionSource<PopupResult> Completion;

        public void Close(string action = PopupResult.Close, object data = null) =>
            UI?.ClosePopup(this, new PopupResult(action, data));

        public override bool OnBack()
        {
            if (!closeOnBack) return true; // swallow back: this popup must be answered
            Close(PopupResult.Back);
            return true;
        }
    }
}
