using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.Feedback
{
    public enum HapticType { Selection, Light, Medium, Heavy, Success, Failure }

    /// <summary>Haptic presets (HP-01). Respect the Settings toggle via <see cref="Enabled"/>.</summary>
    public interface IHapticsService
    {
        bool Enabled { get; set; }
        void Play(HapticType type);
    }

    /// <summary>
    /// Basic device vibration. Light presets are skipped because Handheld.Vibrate has no intensity
    /// control; a native adapter (Core Haptics / VibrationEffect) replaces this in Phase 2.
    /// </summary>
    public sealed class DeviceHapticsService : IHapticsService
    {
        public bool Enabled { get; set; } = true;

        public void Play(HapticType type)
        {
            if (!Enabled) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (type == HapticType.Medium || type == HapticType.Heavy || type == HapticType.Failure || type == HapticType.Success)
                Handheld.Vibrate();
#endif
        }
    }

    public sealed class MockHapticsService : IHapticsService
    {
        public bool Enabled { get; set; } = true;
        public readonly List<HapticType> Played = new List<HapticType>();
        public void Play(HapticType type) { if (Enabled) Played.Add(type); }
    }
}
