using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HyperFrame.UI
{
    /// <summary>
    /// The UI canvas and its layers: Screens (safe area) → PopupDimmer → Popups (safe area) → Overlay.
    /// Uses ScaleWithScreenSize + Expand so the reference resolution is always fully visible from
    /// 3:4 tablets to 9:21 phones (UI-08).
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static readonly Vector2 DefaultReferenceResolution = new Vector2(1080f, 1920f);

        public Canvas Canvas { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public Image PopupDimmer { get; private set; }
        public RectTransform PopupLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }

        public static UIRoot Create(Transform parent, Vector2? referenceResolution = null)
        {
            var go = new GameObject("UIRoot", typeof(RectTransform));
            go.layer = 5;
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? DefaultReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();

            var root = go.AddComponent<UIRoot>();
            root.Canvas = canvas;
            root.ScreenLayer = Layer("Screens", go.transform, safeArea: true);

            var dimmerRt = UIBuilder.CreateRect("PopupDimmer", go.transform);
            UIBuilder.Stretch(dimmerRt);
            root.PopupDimmer = dimmerRt.gameObject.AddComponent<Image>();
            root.PopupDimmer.color = new Color(0f, 0f, 0f, 0.6f);
            root.PopupDimmer.raycastTarget = true; // blocks clicks to screens behind popups (UI-02)
            dimmerRt.gameObject.SetActive(false);

            root.PopupLayer = Layer("Popups", go.transform, safeArea: true);
            root.OverlayLayer = Layer("Overlay", go.transform, safeArea: false);
            EnsureEventSystem();
            return root;
        }

        static RectTransform Layer(string name, Transform parent, bool safeArea)
        {
            var rt = UIBuilder.CreateRect(name, parent);
            UIBuilder.Stretch(rt);
            if (safeArea) rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Converts a screen position to a local position in the overlay layer.</summary>
        public Vector2 ScreenToOverlay(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(OverlayLayer, screenPosition, null, out var local);
            return local;
        }
    }
}
