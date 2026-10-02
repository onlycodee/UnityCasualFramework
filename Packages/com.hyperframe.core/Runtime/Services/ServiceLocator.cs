using System;
using System.Collections.Generic;

namespace HyperFrame.Core
{
    /// <summary>
    /// Service locator (CORE-02). Register services by interface, resolve them anywhere.
    /// Global services live for the whole app. Scoped services (see <see cref="CreateScope"/>) live until
    /// their scope is disposed, e.g. a scene; the newest scope wins when resolving.
    /// </summary>
    /// <example>
    /// ServiceLocator.Register&lt;IAudioService&gt;(audio);
    /// ServiceLocator.Get&lt;IAudioService&gt;().PlaySfx("ui_click");
    /// </example>
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> Global = new Dictionary<Type, object>();
        static readonly List<ServiceScope> Scopes = new List<ServiceScope>();

        /// <summary>Registers a global service. Replaces (with a warning) an existing registration.</summary>
        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (Global.ContainsKey(typeof(T)))
                HFLog.Warn("Services", $"Replacing global service {typeof(T).Name}.");
            Global[typeof(T)] = instance;
        }

        public static void Unregister<T>() where T : class => Global.Remove(typeof(T));

        /// <summary>Resolves a service or throws an exception that says what is missing.</summary>
        public static T Get<T>() where T : class
        {
            if (TryGet(out T service)) return service;
            throw new InvalidOperationException(
                $"Service {typeof(T).Name} is not registered. Register it during boot (HyperFrameApp) or in a test SetUp.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            for (int i = Scopes.Count - 1; i >= 0; i--)
            {
                if (Scopes[i].TryGetLocal(typeof(T), out var scoped))
                {
                    service = (T)scoped;
                    return true;
                }
            }
            if (Global.TryGetValue(typeof(T), out var global))
            {
                service = (T)global;
                return true;
            }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class => TryGet<T>(out _);

        /// <summary>Creates a scope whose registrations override globals until it is disposed.</summary>
        public static ServiceScope CreateScope(string name)
        {
            var scope = new ServiceScope(name);
            Scopes.Add(scope);
            return scope;
        }

        internal static void RemoveScope(ServiceScope scope) => Scopes.Remove(scope);

        /// <summary>Clears every registration and scope. Use in test TearDown and on app shutdown.</summary>
        public static void Reset()
        {
            for (int i = Scopes.Count - 1; i >= 0; i--) Scopes[i].Dispose();
            Scopes.Clear();
            Global.Clear();
        }
    }

    /// <summary>A set of registrations removed together, e.g. services owned by one scene.</summary>
    public sealed class ServiceScope : IDisposable
    {
        readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        public string Name { get; }
        public bool IsDisposed { get; private set; }

        internal ServiceScope(string name) => Name = name;

        public ServiceScope Register<T>(T instance) where T : class
        {
            if (IsDisposed) throw new ObjectDisposedException(Name);
            _services[typeof(T)] = instance ?? throw new ArgumentNullException(nameof(instance));
            return this;
        }

        internal bool TryGetLocal(Type type, out object service) => _services.TryGetValue(type, out service);

        /// <summary>Removes the scope. Services that implement IDisposable are disposed.</summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch (Exception e) { HFLog.Exception(e, "Services"); }
                }
            }
            _services.Clear();
            ServiceLocator.RemoveScope(this);
        }
    }
}
