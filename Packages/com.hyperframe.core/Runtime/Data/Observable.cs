using System;
using System.Collections.Generic;

namespace HyperFrame.Core
{
    /// <summary>A value that notifies listeners when it changes. Used for settings, wallet balances and UI binding.</summary>
    public sealed class Observable<T>
    {
        T _value;
        readonly IEqualityComparer<T> _comparer;

        public event Action<T> Changed;

        public Observable(T initial = default, IEqualityComparer<T> comparer = null)
        {
            _value = initial;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                Changed?.Invoke(value);
            }
        }

        /// <summary>Calls the listener now with the current value and on every change. Dispose to stop.</summary>
        public IDisposable Bind(Action<T> listener)
        {
            listener(_value);
            Changed += listener;
            return new Binding(this, listener);
        }

        /// <summary>Sets the value without notifying (e.g. when loading saved data).</summary>
        public void SetSilently(T value) => _value = value;

        public static implicit operator T(Observable<T> o) => o._value;
        public override string ToString() => _value?.ToString() ?? "null";

        sealed class Binding : IDisposable
        {
            Observable<T> _owner;
            Action<T> _listener;
            public Binding(Observable<T> owner, Action<T> listener) { _owner = owner; _listener = listener; }
            public void Dispose()
            {
                if (_owner != null) _owner.Changed -= _listener;
                _owner = null;
                _listener = null;
            }
        }
    }
}
