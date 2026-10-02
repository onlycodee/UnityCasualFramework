using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace HyperFrame.Core
{
    /// <summary>Optional callbacks for pooled objects (reset state in OnSpawned).</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public interface IPoolService
    {
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);
        T Spawn<T>(T prefab, Vector3 position, Transform parent = null) where T : Component;
        /// <summary>Returns an object to its pool. Objects not from a pool are destroyed.</summary>
        void Despawn(GameObject instance);
        void Despawn(GameObject instance, float delaySeconds);
        void Prewarm(GameObject prefab, int count);
        int CountInactive(GameObject prefab);
        void Clear();
    }

    /// <summary>Marks an instance with the pool it belongs to.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        internal GameObject Prefab;
        internal bool InPool;
    }

    /// <summary>
    /// GameObject/VFX pooling on top of UnityEngine.Pool (CORE-05). One pool per prefab.
    /// Particle systems are restarted on spawn; use <see cref="PooledParticles"/> to auto-return them.
    /// </summary>
    public sealed class PoolService : IPoolService
    {
        readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
        readonly Transform _root;
        readonly TweenEngine _tweens;
        static readonly List<IPoolable> PoolableBuffer = new List<IPoolable>();

        public PoolService(Transform root, TweenEngine tweens)
        {
            _root = root;
            _tweens = tweens;
        }

        ObjectPool<GameObject> GetPool(GameObject prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;
            pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = Object.Instantiate(prefab, _root);
                    var marker = go.GetOrAddComponent<PooledObject>();
                    marker.Prefab = prefab;
                    return go;
                },
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(_root, false);
                },
                actionOnDestroy: Object.Destroy,
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 256);
            _pools.Add(prefab, pool);
            return pool;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var go = GetPool(prefab).Get();
            go.GetComponent<PooledObject>().InPool = false;
            var t = go.transform;
            if (parent != null) t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
            go.GetComponentsInChildren(true, PoolableBuffer);
            foreach (var p in PoolableBuffer) p.OnSpawned();
            PoolableBuffer.Clear();
            return go;
        }

        public T Spawn<T>(T prefab, Vector3 position, Transform parent = null) where T : Component =>
            Spawn(prefab.gameObject, position, Quaternion.identity, parent).GetComponent<T>();

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            var marker = instance.GetComponent<PooledObject>();
            if (marker == null || marker.Prefab == null || !_pools.TryGetValue(marker.Prefab, out var pool))
            {
                Object.Destroy(instance);
                return;
            }
            if (marker.InPool) return; // double despawn is ignored
            instance.GetComponentsInChildren(true, PoolableBuffer);
            foreach (var p in PoolableBuffer) p.OnDespawned();
            PoolableBuffer.Clear();
            marker.InPool = true;
            pool.Release(instance);
        }

        public void Despawn(GameObject instance, float delaySeconds)
        {
            if (_tweens == null || delaySeconds <= 0f) { Despawn(instance); return; }
            _tweens.Delay(delaySeconds, () => Despawn(instance)).SetTarget(instance);
        }

        public void Prewarm(GameObject prefab, int count)
        {
            var pool = GetPool(prefab);
            var temp = new List<GameObject>(count);
            for (int i = 0; i < count; i++) temp.Add(pool.Get());
            foreach (var go in temp)
            {
                go.GetComponent<PooledObject>().InPool = true;
                pool.Release(go);
            }
        }

        public int CountInactive(GameObject prefab) => _pools.TryGetValue(prefab, out var p) ? p.CountInactive : 0;

        public void Clear()
        {
            foreach (var pool in _pools.Values) pool.Clear();
            _pools.Clear();
        }
    }

    /// <summary>Returns a pooled particle effect to its pool when all its particles are gone.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PooledParticles : MonoBehaviour
    {
        ParticleSystem _ps;

        void Awake() => _ps = GetComponent<ParticleSystem>();

        void LateUpdate()
        {
            if (_ps.IsAlive(true)) return;
            if (ServiceLocator.TryGet<IPoolService>(out var pools)) pools.Despawn(gameObject);
            else Destroy(gameObject);
        }
    }
}
