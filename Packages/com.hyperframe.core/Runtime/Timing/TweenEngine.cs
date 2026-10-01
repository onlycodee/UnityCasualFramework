using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyperFrame.Core
{
    /// <summary>Handle to a running tween or timer. Default handle is inactive.</summary>
    public readonly struct TweenHandle : IEquatable<TweenHandle>
    {
        readonly TweenEngine _engine;
        readonly int _id;

        internal TweenHandle(TweenEngine engine, int id)
        {
            _engine = engine;
            _id = id;
        }

        internal int Id => _id;
        public bool IsActive => _engine != null && _engine.IsActive(_id);

        /// <summary>Stops the tween. With complete = true it jumps to the end value and runs OnComplete.</summary>
        public void Kill(bool complete = false) => _engine?.Kill(this, complete);

        /// <summary>Adds a completion callback. Runs immediately if the tween already finished.</summary>
        public TweenHandle OnComplete(Action callback)
        {
            _engine?.AddOnComplete(_id, callback);
            return this;
        }

        /// <summary>Associates an owner so <see cref="TweenEngine.KillTarget"/> can stop all of its tweens.</summary>
        public TweenHandle SetTarget(object target)
        {
            _engine?.SetTarget(_id, target);
            return this;
        }

        /// <summary>A task that completes when the tween ends (completed or killed).</summary>
        public Task AsTask()
        {
            if (!IsActive) return Task.CompletedTask;
            var tcs = new TaskCompletionSource<bool>();
            _engine.AddOnEnd(_id, () => tcs.TrySetResult(true));
            return tcs.Task;
        }

        public bool Equals(TweenHandle other) => ReferenceEquals(_engine, other._engine) && _id == other._id;
        public override bool Equals(object obj) => obj is TweenHandle h && Equals(h);
        public override int GetHashCode() => _id;
    }

    /// <summary>
    /// Minimal tween and timer engine (CORE-06). Pure C#: the Unity runner calls <see cref="Tick"/> every
    /// frame. Game-mode tweens stop while the clock is paused; Unscaled tweens keep running (UI).
    /// Tween objects are pooled, so ticking does not allocate.
    /// </summary>
    public sealed class TweenEngine
    {
        sealed class Tween
        {
            public int Id;
            public float From, To, Duration, Elapsed, Delay;
            public Ease Ease;
            public TimeMode Mode;
            public Action<float> OnUpdate;
            public Action OnComplete;   // only on natural completion (or Kill(complete:true))
            public Action OnEnd;        // always, internal (tasks)
            public object Target;
            public int RepeatsLeft;     // timers: -1 infinite
            public bool IsTimer;
            public bool Dead;
        }

        readonly List<Tween> _active = new List<Tween>();
        readonly Stack<Tween> _pool = new Stack<Tween>();
        readonly Dictionary<int, Tween> _byId = new Dictionary<int, Tween>();
        readonly IGameClock _clock;
        int _nextId = 1;
        int _iterating; // >0 while looping over _active; compaction waits until it is 0

        public TweenEngine(IGameClock clock = null) => _clock = clock;

        public int ActiveCount => _byId.Count;

        /// <summary>Animates a float from → to over duration seconds, calling onUpdate with the value.</summary>
        public TweenHandle To(float from, float to, float duration, Action<float> onUpdate,
            Ease ease = Ease.OutQuad, TimeMode mode = TimeMode.Game, float delay = 0f)
        {
            var t = Rent();
            t.From = from;
            t.To = to;
            t.Duration = duration < 0f ? 0f : duration;
            t.Delay = delay;
            t.Ease = ease;
            t.Mode = mode;
            t.OnUpdate = onUpdate;
            return Activate(t);
        }

        /// <summary>Calls callback once after seconds.</summary>
        public TweenHandle Delay(float seconds, Action callback, TimeMode mode = TimeMode.Game)
        {
            var t = Rent();
            t.IsTimer = true;
            t.Duration = seconds < 0f ? 0f : seconds;
            t.Mode = mode;
            t.RepeatsLeft = 1;
            t.OnComplete = callback;
            return Activate(t);
        }

        /// <summary>Calls callback every interval seconds, count times (-1 = until killed).</summary>
        public TweenHandle Repeat(float interval, Action callback, int count = -1, TimeMode mode = TimeMode.Game)
        {
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval));
            var t = Rent();
            t.IsTimer = true;
            t.Duration = interval;
            t.Mode = mode;
            t.RepeatsLeft = count;
            t.OnUpdate = _ => callback();
            return Activate(t);
        }

        public void Tick(float gameDelta, float unscaledDelta)
        {
            bool paused = _clock != null && _clock.IsPaused;
            _iterating++;
            int count = _active.Count;
            for (int i = 0; i < count; i++)
            {
                var t = _active[i];
                if (t.Dead) continue;
                float dt = t.Mode == TimeMode.Unscaled ? unscaledDelta : (paused ? 0f : gameDelta);
                if (dt <= 0f && t.Duration > 0f) continue;
                Step(t, dt);
            }
            _iterating--;
            Compact();
        }

        void Step(Tween t, float dt)
        {
            if (t.Delay > 0f)
            {
                t.Delay -= dt;
                if (t.Delay > 0f) return;
                dt = -t.Delay;
                t.Delay = 0f;
            }
            t.Elapsed += dt;

            if (t.IsTimer)
            {
                while (!t.Dead && t.Elapsed >= t.Duration)
                {
                    t.Elapsed -= t.Duration;
                    if (t.RepeatsLeft > 0) t.RepeatsLeft--;
                    Invoke(t.OnUpdate, 0f, t);
                    if (t.RepeatsLeft == 0) { Finish(t, true); return; }
                    if (t.Duration <= 0f) break;
                }
                return;
            }

            float p = t.Duration <= 0f ? 1f : Math.Min(1f, t.Elapsed / t.Duration);
            Invoke(t.OnUpdate, t.From + (t.To - t.From) * Easing.Evaluate(t.Ease, p), t);
            if (p >= 1f) Finish(t, true);
        }

        void Invoke(Action<float> a, float v, Tween t)
        {
            if (a == null) return;
            try { a(v); }
            catch (Exception e)
            {
                HFLog.Exception(e, "Tween");
                Finish(t, false); // a throwing tween is stopped so it cannot spam every frame
            }
        }

        void Finish(Tween t, bool runComplete)
        {
            if (t.Dead || !_byId.ContainsKey(t.Id)) return;
            t.Dead = true;
            _byId.Remove(t.Id);
            if (runComplete && t.OnComplete != null)
            {
                try { t.OnComplete(); }
                catch (Exception e) { HFLog.Exception(e, "Tween.OnComplete"); }
            }
            var end = t.OnEnd;
            t.OnEnd = null;
            end?.Invoke();
            Compact();
        }

        internal bool IsActive(int id) => _byId.ContainsKey(id);

        internal void Kill(TweenHandle h, bool complete)
        {
            if (!_byId.TryGetValue(h.Id, out var t)) return;
            if (complete && !t.IsTimer) Invoke(t.OnUpdate, t.To, t);
            Finish(t, complete);
        }

        /// <summary>Kills every tween whose target is <paramref name="target"/>.</summary>
        public int KillTarget(object target, bool complete = false)
        {
            int killed = 0;
            _iterating++;
            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t.Dead || !ReferenceEquals(t.Target, target)) continue;
                Kill(new TweenHandle(this, t.Id), complete);
                killed++;
            }
            _iterating--;
            Compact();
            return killed;
        }

        public void KillAll()
        {
            _iterating++;
            for (int i = 0; i < _active.Count; i++)
                if (!_active[i].Dead) Finish(_active[i], false);
            _iterating--;
            Compact();
        }

        internal void AddOnComplete(int id, Action cb)
        {
            if (cb == null) return;
            if (_byId.TryGetValue(id, out var t)) t.OnComplete += cb;
            else cb();
        }

        internal void AddOnEnd(int id, Action cb)
        {
            if (_byId.TryGetValue(id, out var t)) t.OnEnd += cb;
            else cb();
        }

        internal void SetTarget(int id, object target)
        {
            if (_byId.TryGetValue(id, out var t)) t.Target = target;
        }

        Tween Rent()
        {
            var t = _pool.Count > 0 ? _pool.Pop() : new Tween();
            t.Id = _nextId++;
            t.From = t.To = t.Duration = t.Elapsed = t.Delay = 0f;
            t.Ease = Ease.Linear;
            t.Mode = TimeMode.Game;
            t.OnUpdate = null;
            t.OnComplete = null;
            t.OnEnd = null;
            t.Target = null;
            t.RepeatsLeft = 0;
            t.IsTimer = false;
            t.Dead = false;
            return t;
        }

        TweenHandle Activate(Tween t)
        {
            _active.Add(t);
            _byId[t.Id] = t;
            return new TweenHandle(this, t.Id);
        }

        void Compact()
        {
            if (_iterating > 0) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (!t.Dead) continue;
                _active.RemoveAt(i);
                t.OnUpdate = null;
                t.OnComplete = null;
                t.Target = null;
                _pool.Push(t);
            }
        }
    }
}
