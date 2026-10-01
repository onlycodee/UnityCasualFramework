using System;
using System.Collections.Generic;

namespace HyperFrame.Core
{
    /// <summary>One state of a <see cref="StateMachine{TKey}"/>. Override only what you need.</summary>
    public abstract class State
    {
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>A state built from delegates, handy for small machines and tests.</summary>
    public sealed class DelegateState : State
    {
        readonly Action _enter, _exit;
        readonly Action<float> _tick;

        public DelegateState(Action enter = null, Action exit = null, Action<float> tick = null)
        {
            _enter = enter;
            _exit = exit;
            _tick = tick;
        }

        public override void Enter() => _enter?.Invoke();
        public override void Exit() => _exit?.Invoke();
        public override void Tick(float deltaTime) => _tick?.Invoke(deltaTime);
    }

    /// <summary>
    /// Data-driven state machine (CORE-04). States are registered by key; allowed transitions are a
    /// table (from → to). If no transition is declared for a state, any target is allowed from it.
    /// Requesting a change from inside Enter/Exit is queued and applied after the current change.
    /// </summary>
    public sealed class StateMachine<TKey>
    {
        readonly Dictionary<TKey, State> _states = new Dictionary<TKey, State>();
        readonly Dictionary<TKey, HashSet<TKey>> _transitions = new Dictionary<TKey, HashSet<TKey>>();
        readonly Queue<TKey> _pending = new Queue<TKey>();
        readonly IEqualityComparer<TKey> _comparer = EqualityComparer<TKey>.Default;
        bool _changing;

        public string Name { get; }
        public TKey Current { get; private set; }
        public TKey Previous { get; private set; }
        public bool HasState { get; private set; }
        public State CurrentState => HasState ? _states[Current] : null;

        /// <summary>Raised after a change completes: (from, to). On the first change, from is default.</summary>
        public event Action<TKey, TKey> Changed;

        public StateMachine(string name = "StateMachine") => Name = name;

        public StateMachine<TKey> Add(TKey key, State state)
        {
            _states[key] = state ?? throw new ArgumentNullException(nameof(state));
            return this;
        }

        public StateMachine<TKey> Allow(TKey from, params TKey[] targets)
        {
            if (!_transitions.TryGetValue(from, out var set))
            {
                set = new HashSet<TKey>(_comparer);
                _transitions[from] = set;
            }
            foreach (var t in targets) set.Add(t);
            return this;
        }

        public bool Contains(TKey key) => _states.ContainsKey(key);

        public bool CanTransition(TKey from, TKey to) =>
            !_transitions.TryGetValue(from, out var allowed) || allowed.Contains(to);

        /// <summary>Starts the machine in a state (no transition check).</summary>
        public void Start(TKey initial)
        {
            if (HasState) throw new InvalidOperationException($"{Name} already started.");
            if (!_states.ContainsKey(initial)) throw new ArgumentException($"{Name}: unknown state {initial}");
            ApplyChange(initial);
            Drain(); // changes requested from the initial Enter
        }

        /// <summary>Requests a change. Returns false (and logs) when the transition is not allowed.</summary>
        public bool ChangeState(TKey to)
        {
            if (!_states.ContainsKey(to))
            {
                HFLog.Error(Name, $"Unknown state {to}.");
                return false;
            }
            if (!HasState)
            {
                Start(to);
                return true;
            }
            var from = _pending.Count > 0 ? LastPending() : Current;
            if (!CanTransition(from, to))
            {
                HFLog.Error(Name, $"Transition {from} → {to} is not allowed.");
                return false;
            }
            _pending.Enqueue(to);
            if (!_changing) Drain();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (HasState && !_changing) _states[Current].Tick(deltaTime);
        }

        TKey LastPending()
        {
            TKey last = default;
            foreach (var key in _pending) last = key;
            return last;
        }

        void Drain()
        {
            while (_pending.Count > 0) ApplyChange(_pending.Dequeue());
        }

        void ApplyChange(TKey to)
        {
            _changing = true;
            var from = Current;
            try
            {
                if (HasState)
                {
                    try { _states[from].Exit(); }
                    catch (Exception e) { HFLog.Exception(e, $"{Name}.{from}.Exit"); }
                }
                Previous = from;
                Current = to;
                HasState = true;
                HFLog.Debug(Name, $"{from} → {to}");
                try { _states[to].Enter(); }
                catch (Exception e) { HFLog.Exception(e, $"{Name}.{to}.Enter"); }
            }
            finally
            {
                _changing = false;
            }
            Changed?.Invoke(from, to);
        }
    }
}
