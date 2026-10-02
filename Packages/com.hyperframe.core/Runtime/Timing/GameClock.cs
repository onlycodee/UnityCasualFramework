using System;

namespace HyperFrame.Core
{
    /// <summary>Which clock a tween or timer follows.</summary>
    public enum TimeMode
    {
        /// <summary>Gameplay time: stops while the game is paused and follows the time scale (slow-mo).</summary>
        Game,
        /// <summary>Real time: keeps running during pause. Use for UI.</summary>
        Unscaled
    }

    public interface IGameClock
    {
        bool IsPaused { get; }
        /// <summary>Gameplay time scale (1 = normal). Applied to Time.timeScale by the runner.</summary>
        float TimeScale { get; set; }
        /// <summary>Total gameplay seconds elapsed (excludes paused time).</summary>
        float GameTime { get; }
        float UnscaledTime { get; }
        /// <summary>Pauses gameplay. Reference-counted: every Pause needs a matching Resume.</summary>
        void Pause(string reason);
        void Resume(string reason);
        event Action<bool> PausedChanged;
    }

    /// <summary>Pause-aware clock (CORE-06). Pause is reference-counted by reason so overlapping pauses are safe.</summary>
    public sealed class GameClock : IGameClock
    {
        readonly System.Collections.Generic.List<string> _pauseReasons = new System.Collections.Generic.List<string>();
        float _timeScale = 1f;

        public bool IsPaused => _pauseReasons.Count > 0;
        public float GameTime { get; private set; }
        public float UnscaledTime { get; private set; }
        public event Action<bool> PausedChanged;

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = value < 0f ? 0f : value;
        }

        /// <summary>Effective scale for gameplay this frame (0 while paused).</summary>
        public float EffectiveTimeScale => IsPaused ? 0f : _timeScale;

        public void Pause(string reason)
        {
            bool was = IsPaused;
            _pauseReasons.Add(reason ?? "unspecified");
            if (!was) PausedChanged?.Invoke(true);
        }

        public void Resume(string reason)
        {
            if (!_pauseReasons.Remove(reason ?? "unspecified"))
            {
                HFLog.Warn("GameClock", $"Resume('{reason}') without matching Pause.");
                return;
            }
            if (!IsPaused) PausedChanged?.Invoke(false);
        }

        public System.Collections.Generic.IReadOnlyList<string> PauseReasons => _pauseReasons;

        /// <summary>Advances the clock. gameDelta must already include the time scale.</summary>
        public void Advance(float gameDelta, float unscaledDelta)
        {
            if (!IsPaused) GameTime += gameDelta;
            UnscaledTime += unscaledDelta;
        }
    }
}
