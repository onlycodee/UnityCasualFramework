using UnityEngine;

namespace HyperFrame.Core
{
    /// <summary>
    /// The one MonoBehaviour that drives the framework's plain C# systems: ticks the tween engine and
    /// game clock, applies pause/slow-mo to Time.timeScale, and forwards app pause/quit to the event bus.
    /// Created by the app bootstrap; survives scene loads.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class HyperFrameRunner : MonoBehaviour
    {
        public GameClock Clock { get; private set; }
        public TweenEngine Tweens { get; private set; }
        public IEventBus Events { get; private set; }

        /// <summary>Called every frame after tweens tick, with the gameplay delta.</summary>
        public event System.Action<float> Ticked;

        public static HyperFrameRunner Create(GameClock clock, TweenEngine tweens, IEventBus events)
        {
            var go = new GameObject("[HyperFrame]");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<HyperFrameRunner>();
            runner.Clock = clock;
            runner.Tweens = tweens;
            runner.Events = events;
            clock.PausedChanged += paused => events.Publish(new GamePausedEvent { Paused = paused });
            return runner;
        }

        void Update()
        {
            if (Clock == null) return;
            // Pause and slow-mo apply to the whole engine (physics, animators, particles).
            float scale = Clock.EffectiveTimeScale;
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;

            float gameDelta = Time.deltaTime;
            float unscaledDelta = Time.unscaledDeltaTime;
            Clock.Advance(gameDelta, unscaledDelta);
            Tweens.Tick(gameDelta, unscaledDelta);
            Ticked?.Invoke(gameDelta);
        }

        void OnApplicationPause(bool paused) => Events?.Publish(new AppPauseEvent { Paused = paused });

        void OnApplicationQuit() => Events?.Publish(new AppQuitEvent());

        void OnDestroy()
        {
            if (Clock != null) Time.timeScale = 1f;
        }
    }
}
