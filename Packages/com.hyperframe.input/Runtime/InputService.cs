using System;
using UnityEngine;

namespace HyperFrame.Input
{
    public interface IInputService
    {
        GestureRecognizer Gestures { get; }
        InputLock Lock { get; }
        /// <summary>False while locked or disabled; gameplay should ignore input then.</summary>
        bool IsGameplayInputEnabled { get; }
        /// <summary>Master switch (e.g. during scene loads).</summary>
        bool Enabled { get; set; }
        /// <summary>Android back button / Escape. Not blocked by the gameplay lock.</summary>
        event Action BackPressed;

        // ── Injection API for bots and tests (IN-06). Goes through the same recognizer and lock. ──
        void InjectPointerDown(int pointerId, Vector2 screenPosition);
        void InjectPointerMove(int pointerId, Vector2 screenPosition);
        void InjectPointerUp(int pointerId, Vector2 screenPosition);
        /// <summary>A complete tap at a screen position (down + up, 50 ms apart in input time).</summary>
        void InjectTap(Vector2 screenPosition);
        /// <summary>A complete drag from → to in <paramref name="steps"/> moves.</summary>
        void InjectDrag(Vector2 from, Vector2 to, int steps = 8);
        void InjectBack();
    }

    /// <summary>
    /// Input service: owns the gesture recognizer and the lock, and accepts pointer samples from the
    /// device driver (<see cref="InputDriver"/>) or from injection. Time is "input time" in seconds,
    /// supplied by a clock function so injected gestures are deterministic.
    /// </summary>
    public sealed class InputService : IInputService
    {
        readonly Func<float> _now;
        float _injectedTimeOffset;

        public GestureRecognizer Gestures { get; }
        public InputLock Lock { get; } = new InputLock();
        public bool Enabled { get; set; } = true;
        public bool IsGameplayInputEnabled => Enabled && !Lock.IsLocked;
        public event Action BackPressed;

        public InputService(GestureSettings settings = null, float dpi = 160f, Func<float> timeSource = null)
        {
            Gestures = new GestureRecognizer(settings, dpi);
            _now = timeSource ?? (() => 0f);
            Lock.LockChanged += locked => { if (locked) Gestures.Cancel(); };
        }

        /// <summary>Current input time (real time plus virtual time used by injected gestures).</summary>
        public float Now => _now() + _injectedTimeOffset;

        // Device samples (from InputDriver)
        public void OnPointerDown(int id, Vector2 pos)
        {
            if (IsGameplayInputEnabled) Gestures.PointerDown(id, pos, Now);
        }

        public void OnPointerMove(int id, Vector2 pos)
        {
            if (IsGameplayInputEnabled) Gestures.PointerMove(id, pos, Now);
        }

        public void OnPointerUp(int id, Vector2 pos)
        {
            if (IsGameplayInputEnabled) Gestures.PointerUp(id, pos, Now);
        }

        public void OnBack() => BackPressed?.Invoke();

        public void Update() => Gestures.Update(Now);

        // Injection
        public void InjectPointerDown(int pointerId, Vector2 screenPosition) => OnPointerDown(pointerId, screenPosition);
        public void InjectPointerMove(int pointerId, Vector2 screenPosition) => OnPointerMove(pointerId, screenPosition);
        public void InjectPointerUp(int pointerId, Vector2 screenPosition) => OnPointerUp(pointerId, screenPosition);
        public void InjectBack() => OnBack();

        public void InjectTap(Vector2 screenPosition)
        {
            const int botPointer = 100;
            OnPointerDown(botPointer, screenPosition);
            _injectedTimeOffset += 0.05f;
            OnPointerUp(botPointer, screenPosition);
            // Leave a gap so consecutive injected taps are not read as double taps.
            _injectedTimeOffset += Gestures.Settings.doubleTapMaxInterval + 0.01f;
        }

        public void InjectDrag(Vector2 from, Vector2 to, int steps = 8)
        {
            const int botPointer = 101;
            steps = Mathf.Max(1, steps);
            OnPointerDown(botPointer, from);
            // Spend longer than the swipe window so this reads as a drag, not a swipe.
            float stepTime = (Gestures.Settings.swipeMaxDuration + 0.1f) / steps;
            for (int i = 1; i <= steps; i++)
            {
                _injectedTimeOffset += stepTime;
                OnPointerMove(botPointer, Vector2.Lerp(from, to, (float)i / steps));
            }
            OnPointerUp(botPointer, to);
        }
    }
}
