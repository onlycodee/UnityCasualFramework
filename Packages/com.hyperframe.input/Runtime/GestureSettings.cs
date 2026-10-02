using System;
using UnityEngine;

namespace HyperFrame.Input
{
    /// <summary>Gesture thresholds (IN-04). Distances are in density-independent pixels (dp, 160 dpi).</summary>
    [Serializable]
    public sealed class GestureSettings
    {
        [Tooltip("Max press duration for a tap (s).")] public float tapMaxDuration = 0.3f;
        [Tooltip("Movement allowed before a press becomes a drag (dp).")] public float tapSlopDp = 12f;
        [Tooltip("Max time between two taps for a double tap (s).")] public float doubleTapMaxInterval = 0.3f;
        [Tooltip("Max distance between two taps for a double tap (dp).")] public float doubleTapMaxDistanceDp = 40f;
        [Tooltip("Press time before LongPress / Hold begins (s).")] public float longPressDuration = 0.5f;
        [Tooltip("Min travel for a swipe (dp).")] public float swipeMinDistanceDp = 50f;
        [Tooltip("Max duration for a swipe (s).")] public float swipeMaxDuration = 0.4f;

        public GestureSettings Clone() => (GestureSettings)MemberwiseClone();
    }

    /// <summary>ScriptableObject wrapper so thresholds can be tuned per game without code.</summary>
    [CreateAssetMenu(menuName = "HyperFrame/Input/Gesture Settings", fileName = "GestureSettings")]
    public sealed class GestureSettingsAsset : ScriptableObject
    {
        public GestureSettings settings = new GestureSettings();
    }
}
