using System;
using System.Collections.Generic;

namespace HyperFrame.Core
{
    /// <summary>
    /// Typed event bus (CORE-03). Events are structs, so publishing does not allocate.
    /// Subscribe returns an IDisposable; dispose it (or use <see cref="EventSubscriptions"/> /
    /// <c>SubscribeUntilDestroy</c> on a MonoBehaviour) so nothing leaks when a scene unloads.
    /// </summary>
    public interface IEventBus
    {
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
        void Publish<T>(T evt) where T : struct;
        int SubscriberCount<T>() where T : struct;
        void Clear();
    }

    public sealed class EventBus : IEventBus
    {
        readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetChannel<T>(true).Add(handler);
            return new Subscription<T>(this, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct => GetChannel<T>(false)?.Remove(handler);

        public void Publish<T>(T evt) where T : struct => GetChannel<T>(false)?.Publish(evt);

        public int SubscriberCount<T>() where T : struct => GetChannel<T>(false)?.Count ?? 0;

        public void Clear() => _channels.Clear();

        Channel<T> GetChannel<T>(bool create) where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out var channel)) return (Channel<T>)channel;
            if (!create) return null;
            var created = new Channel<T>();
            _channels.Add(typeof(T), created);
            return created;
        }

        interface IChannel { }

        sealed class Channel<T> : IChannel where T : struct
        {
            readonly List<Action<T>> _handlers = new List<Action<T>>();
            int _publishDepth;
            bool _needsCompaction;

            public int Count
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < _handlers.Count; i++) if (_handlers[i] != null) n++;
                    return n;
                }
            }

            public void Add(Action<T> handler) => _handlers.Add(handler);

            public void Remove(Action<T> handler)
            {
                int index = _handlers.IndexOf(handler);
                if (index < 0) return;
                if (_publishDepth > 0)
                {
                    _handlers[index] = null; // removed after the current publish finishes
                    _needsCompaction = true;
                }
                else
                {
                    _handlers.RemoveAt(index);
                }
            }

            public void Publish(T evt)
            {
                _publishDepth++;
                // Handlers added during this publish are not called until the next one.
                int count = _handlers.Count;
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var handler = _handlers[i];
                        if (handler == null) continue;
                        try { handler(evt); }
                        catch (Exception e) { HFLog.Exception(e, $"EventBus<{typeof(T).Name}>"); }
                    }
                }
                finally
                {
                    _publishDepth--;
                    if (_publishDepth == 0 && _needsCompaction)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _needsCompaction = false;
                    }
                }
            }
        }

        sealed class Subscription<T> : IDisposable where T : struct
        {
            EventBus _bus;
            Action<T> _handler;

            public Subscription(EventBus bus, Action<T> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                _bus?.Unsubscribe(_handler);
                _bus = null;
                _handler = null;
            }
        }
    }

    /// <summary>Collects subscriptions so they can be released together (e.g. in OnDestroy).</summary>
    public sealed class EventSubscriptions : IDisposable
    {
        readonly List<IDisposable> _items = new List<IDisposable>();

        public void Add(IDisposable subscription)
        {
            if (subscription != null) _items.Add(subscription);
        }

        public int Count => _items.Count;

        public void Dispose()
        {
            for (int i = _items.Count - 1; i >= 0; i--) _items[i].Dispose();
            _items.Clear();
        }
    }
}
