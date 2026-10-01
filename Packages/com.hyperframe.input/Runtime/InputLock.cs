using System;
using System.Collections.Generic;

namespace HyperFrame.Input
{
    /// <summary>
    /// Reference-counted gameplay input lock (IN-03). Popups and animations acquire it; gameplay input
    /// is ignored while any holder exists. Always release (dispose) what you acquire.
    /// </summary>
    /// <example>using (input.Lock.Acquire("ball_anim")) { await MoveBall(); }</example>
    public sealed class InputLock
    {
        readonly List<Holder> _holders = new List<Holder>();

        public bool IsLocked => _holders.Count > 0;
        public int Count => _holders.Count;
        public event Action<bool> LockChanged;

        public IDisposable Acquire(string reason)
        {
            var holder = new Holder(this, reason ?? "unspecified");
            _holders.Add(holder);
            if (_holders.Count == 1) LockChanged?.Invoke(true);
            return holder;
        }

        /// <summary>Names of current holders, for debugging soft-locks ("why can't I tap?").</summary>
        public IEnumerable<string> Reasons
        {
            get { foreach (var h in _holders) yield return h.Reason; }
        }

        /// <summary>Emergency release (e.g. returning to Home). Logs who still held the lock.</summary>
        public void ReleaseAll()
        {
            if (_holders.Count == 0) return;
            HyperFrame.Core.HFLog.Warn("InputLock", "ReleaseAll with holders: " + string.Join(", ", Reasons));
            foreach (var h in _holders) h.Released = true;
            _holders.Clear();
            LockChanged?.Invoke(false);
        }

        void Release(Holder holder)
        {
            if (!_holders.Remove(holder)) return;
            if (_holders.Count == 0) LockChanged?.Invoke(false);
        }

        sealed class Holder : IDisposable
        {
            readonly InputLock _owner;
            public readonly string Reason;
            public bool Released;
            public Holder(InputLock owner, string reason) { _owner = owner; Reason = reason; }
            public void Dispose()
            {
                if (Released) return;
                Released = true;
                _owner.Release(this);
            }
        }
    }
}
