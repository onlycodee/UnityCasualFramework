using System;
using UnityEngine;

namespace HyperFrame.Core
{
    public static class UnityEventExtensions
    {
        /// <summary>
        /// Subscribes to an event and unsubscribes automatically when the component's GameObject is
        /// destroyed (e.g. on scene unload). Prefer this in MonoBehaviours.
        /// </summary>
        public static void SubscribeUntilDestroy<T>(this Component owner, IEventBus bus, Action<T> handler) where T : struct
        {
            var holder = owner.GetOrAddComponent<EventSubscriptionHolder>();
            holder.Subscriptions.Add(bus.Subscribe(handler));
        }

        /// <summary>Same, using the globally registered IEventBus.</summary>
        public static void SubscribeUntilDestroy<T>(this Component owner, Action<T> handler) where T : struct =>
            owner.SubscribeUntilDestroy(ServiceLocator.Get<IEventBus>(), handler);
    }

    /// <summary>Holds subscriptions for a GameObject and releases them in OnDestroy.</summary>
    [AddComponentMenu("")]
    public sealed class EventSubscriptionHolder : MonoBehaviour
    {
        public readonly EventSubscriptions Subscriptions = new EventSubscriptions();
        void OnDestroy() => Subscriptions.Dispose();
    }

    /// <summary>
    /// Put on a scene object to give that scene its own service scope (CORE-02 "scene-scoped").
    /// Register scene services from Awake of other components via <see cref="Scope"/>; they are
    /// removed (and disposed) when the scene unloads.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class SceneServiceScope : MonoBehaviour
    {
        public ServiceScope Scope { get; private set; }
        void Awake() => Scope = ServiceLocator.CreateScope(gameObject.scene.name);
        void OnDestroy() => Scope?.Dispose();
    }
}
