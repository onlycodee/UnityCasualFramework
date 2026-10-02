using UnityEngine;

namespace HyperFrame.UI
{
    /// <summary>
    /// Fits this RectTransform to Screen.safeArea (UI-08), so notches and home indicators never cover UI.
    /// Re-applies when the resolution, orientation or safe area changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        /// <summary>Overrides Screen.safeArea; used by tests and the screenshot tool to simulate devices.</summary>
        public static Rect? SimulatedSafeArea;

        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply(force: true);
        void Update() => Apply(force: false);

        public void Apply(bool force)
        {
            var area = SimulatedSafeArea ?? Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == _applied && screen == _screen) return;
            _applied = area;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;

            var rt = (RectTransform)transform;
            var min = area.position;
            var max = area.position + area.size;
            min.x /= screen.x; min.y /= screen.y;
            max.x /= screen.x; max.y /= screen.y;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
