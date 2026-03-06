using System;
using System.Collections.Generic;
using Core.Interfaces;
using UnityEngine;

namespace Core
{
    /// <summary>
    ///     Helps create and manage services in the game.
    ///     Static API facade with an internal runtime host.
    /// </summary>
    public class Services : MonoBehaviour
    {
        public const string GlobalContextId = "Global";
        private static readonly ServiceContainer ServiceContainerInstance = ServiceContainer.Create();
        private readonly Dictionary<Type, string> _contextByType = new(16);
        private readonly Dictionary<string, HashSet<Type>> _serviceTypesByContext = new(StringComparer.Ordinal);
        private static bool isQuitting;
        private static bool isInitialized;
        private static bool isInitializing;
        public static Services Instance { get; private set; }

        private static bool CanInitialize()
        {
            return !isQuitting && Application.isPlaying;
        }

        private static void EnsureInstance()
        {
            if (!CanInitialize())
                return;
            if (Instance != null)
                return;

            var go = new GameObject("Services");
            Instance = go.AddComponent<Services>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance != this || isQuitting)
                return;

            CleanupAllServices(false);
            Instance = null;
            isInitialized = false;
            isInitializing = false;
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            CleanupAllServices(false);
            isInitialized = false;
            isInitializing = false;
        }

        public void AddService<T>(T service) where T : IService
        {
            AddService(service, GlobalContextId);
        }

        public bool TryAddService<T>(T service) where T : IService
        {
            return TryAddService(service, GlobalContextId);
        }

        public void AddService<T>(T service, string contextId) where T : IService
        {
            ServiceContainerInstance.Add(service);
            TrackServiceOwnership(typeof(T), contextId);
        }

        public bool TryAddService<T>(T service, string contextId) where T : IService
        {
            if (service == null)
                return false;
            if (HasService<T>())
                return false;

            AddService(service, contextId);
            return true;
        }

        public bool HasService<T>() where T : IService
        {
            return ServiceContainerInstance.Has<T>();
        }

        public T GetService<T>() where T : IService
        {
            return ServiceContainerInstance.Get<T>();
        }

        public void RemoveService<T>() where T : IService
        {
            RemoveByType(typeof(T));
        }

        private void RemoveContextInternal(string contextId)
        {
            contextId = NormalizeContextId(contextId);
            if (!_serviceTypesByContext.TryGetValue(contextId, out var ownedTypes) || ownedTypes.Count == 0)
                return;

            var typesToRemove = new List<Type>(ownedTypes);
            for (var i = 0; i < typesToRemove.Count; i++)
                RemoveByType(typesToRemove[i]);
        }

        private void InitializeServices()
        {
            if (isInitialized || isInitializing)
                return;

            isInitializing = true;
            try
            {
                ServiceContainerInstance.InitializeServices();
                ServiceContainerInstance.StartServices();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Services] Initialization failed: {ex}");
            }
            finally
            {
                isInitializing = false;
            }
        }

        private static void EnsureServicesInitialized()
        {
            if (!CanInitialize())
                return;

            EnsureInstance();
            if (!isInitialized && !isInitializing && Instance != null)
                Instance.InitializeServices();
        }

        // Static helpers so callers don't have to reference Services.Instance
        public static void Add<T>(T service) where T : IService
        {
            Add(service, GlobalContextId);
        }

        public static bool TryAdd<T>(T service) where T : IService
        {
            return TryAdd(service, GlobalContextId);
        }

        public static void Add<T>(T service, string contextId) where T : IService
        {
            if (!CanInitialize())
                return;
            EnsureServicesInitialized();
            Instance.AddService(service, contextId);
        }

        public static bool TryAdd<T>(T service, string contextId) where T : IService
        {
            if (!CanInitialize())
                return false;
            EnsureServicesInitialized();
            return Instance.TryAddService(service, contextId);
        }

        public static T AddComponent<T>(string contextId = GlobalContextId) where T : MonoBehaviour, IService
        {
            if (!CanInitialize())
                return default;
            EnsureServicesInitialized();
            return Instance.AddMonoComponentService<T>(contextId);
        }

        public static T AddPrefab<T>(string prefabPath, bool parentToServices = true,
            string contextId = GlobalContextId) where T : MonoBehaviour, IService
        {
            if (!CanInitialize())
                return default;
            EnsureServicesInitialized();
            return Instance.AddPrefabService<T>(prefabPath, parentToServices, contextId);
        }

        public static bool Has<T>() where T : IService
        {
            if (!CanInitialize())
                return false;
            EnsureServicesInitialized();
            return ServiceContainerInstance.Has<T>();
        }

        public static T Get<T>() where T : IService
        {
            if (!CanInitialize())
                return default;
            EnsureServicesInitialized();
            return ServiceContainerInstance.Get<T>();
        }

        public static bool TryGet<T>(out T service) where T : IService
        {
            service = default;
            if (!CanInitialize())
                return false;
            EnsureServicesInitialized();
            return ServiceContainerInstance.TryGet(out service);
        }

        public static void Remove<T>() where T : IService
        {
            if (!CanInitialize())
                return;
            EnsureServicesInitialized();
            Instance.RemoveService<T>();
        }

        public static void RemoveContext(string contextId)
        {
            if (!CanInitialize())
                return;
            EnsureServicesInitialized();
            Instance.RemoveContextInternal(contextId);
        }

        internal static void PrepareForStartup()
        {
            isQuitting = false;
            isInitialized = false;
            isInitializing = false;

            var staleServices = ServiceContainerInstance.RemoveAll();
            for (var i = 0; i < staleServices.Count; i++)
            {
                var stale = staleServices[i];
                if (stale == null)
                    continue;

                try
                {
                    stale.CleanupService();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Services] Startup cleanup failed for {stale.GetType().Name}: {ex}");
                }
            }

            if (Instance != null)
            {
                Instance._contextByType.Clear();
                Instance._serviceTypesByContext.Clear();
            }

            EnsureInstance();
            EnsureServicesInitialized();
        }


        private T AddMonoComponentService<T>() where T : MonoBehaviour, IService
        {
            return AddMonoComponentService<T>(GlobalContextId);
        }

        private T AddMonoComponentService<T>(string contextId) where T : MonoBehaviour, IService
        {
            if (HasService<T>())
                return GetService<T>();

            contextId = NormalizeContextId(contextId);

            var host = gameObject;
            if (!string.Equals(contextId, GlobalContextId, StringComparison.Ordinal))
                host = new GameObject($"{typeof(T).Name} ({contextId})");

            var service = host.GetComponent<T>();
            if (service == null)
                service = host.AddComponent<T>();

            AddService(service, contextId);
            return service;
        }

        public void CreatePrefab(string prefabPath, bool parentToServices)
        {
            if (string.IsNullOrEmpty(prefabPath))
                return;

            var prefab = Resources.Load(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Services] Prefab at '{prefabPath}' not found in Resources. Skipping instantiate.");
                return;
            }

            var parent = parentToServices ? transform : null;
            var instance = Instantiate(prefab, parent);
            if (instance == null)
                Debug.Log($"Failed to instantiate startup prefab at path: {prefabPath}");
        }

        public T AddPrefabService<T>(string prefabPath, bool parentToServices) where T : MonoBehaviour, IService
        {
            return AddPrefabService<T>(prefabPath, parentToServices, GlobalContextId);
        }

        public T AddPrefabService<T>(string prefabPath, bool parentToServices, string contextId)
            where T : MonoBehaviour, IService
        {
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            if (HasService<T>())
                return GetService<T>();

            var prefab = Resources.Load<T>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Services] Service prefab '{prefabPath}' not loaded (type {typeof(T).Name}).");
                return null;
            }

            contextId = NormalizeContextId(contextId);
            var isGlobal = string.Equals(contextId, GlobalContextId, StringComparison.Ordinal);

            var parent = parentToServices && isGlobal ? transform : null;
            var instance = Instantiate(prefab, parent);
            instance.gameObject.name = prefab.gameObject.name;
            if (isGlobal)
                DontDestroyOnLoad(instance.gameObject);
            AddService(instance, contextId);
            return instance;
        }

        private string NormalizeContextId(string contextId)
        {
            return string.IsNullOrWhiteSpace(contextId) ? GlobalContextId : contextId.Trim();
        }

        private void TrackServiceOwnership(Type serviceType, string contextId)
        {
            contextId = NormalizeContextId(contextId);
            _contextByType[serviceType] = contextId;

            if (!_serviceTypesByContext.TryGetValue(contextId, out var set))
            {
                set = new HashSet<Type>();
                _serviceTypesByContext[contextId] = set;
            }

            set.Add(serviceType);
        }

        private void UntrackServiceOwnership(Type serviceType)
        {
            if (!_contextByType.TryGetValue(serviceType, out var contextId))
                return;

            _contextByType.Remove(serviceType);
            if (!_serviceTypesByContext.TryGetValue(contextId, out var set))
                return;

            set.Remove(serviceType);
            if (set.Count == 0)
                _serviceTypesByContext.Remove(contextId);
        }

        private bool RemoveByType(Type serviceType)
        {
            if (!ServiceContainerInstance.Remove(serviceType, out var removed))
                return false;

            UntrackServiceOwnership(serviceType);
            CleanupAndDestroyService(removed, true);
            return true;
        }

        private void CleanupAllServices(bool destroyObjects)
        {
            var removed = ServiceContainerInstance.RemoveAll();
            _contextByType.Clear();
            _serviceTypesByContext.Clear();

            for (var i = 0; i < removed.Count; i++)
                CleanupAndDestroyService(removed[i], destroyObjects);
        }

        private void CleanupAndDestroyService(IService service, bool destroyObjects)
        {
            if (service == null)
                return;

            try
            {
                service.CleanupService();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Services] Cleanup failed for {service.GetType().Name}: {ex}");
            }

            if (!destroyObjects || service is not MonoBehaviour behaviour || behaviour == null)
                return;

            if (Instance != null && behaviour.gameObject == Instance.gameObject)
            {
                Destroy(behaviour);
                return;
            }

            Destroy(behaviour.gameObject);
        }

        private sealed class ServiceContainer
        {
            private readonly Dictionary<Type, IService> _container = new(8);
            private bool _initialized;
            private bool _started;

            private ServiceContainer()
            {
            }

            public static ServiceContainer Create()
            {
                return new ServiceContainer();
            }

            public void Add<T>(T service) where T : IService
            {
                var key = typeof(T);
                if (service == null)
                    throw new ArgumentNullException(nameof(service));
                if (service is not T)
                    throw new ArgumentException("Type mismatch", nameof(service));
                if (!_container.TryAdd(key, service))
                    throw new InvalidOperationException($"{key} already registered");

                // If services are already initialized/started, bring late additions up to date.
                if (_initialized)
                    service.InitializeService();
                if (_started)
                    service.StartService();
            }

            public bool Remove<T>(out IService removed) where T : IService
            {
                return Remove(typeof(T), out removed);
            }

            public bool Remove(Type serviceType, out IService removed)
            {
                removed = default;
                if (!_container.TryGetValue(serviceType, out var existing))
                    return false;

                removed = existing;
                return _container.Remove(serviceType);
            }

            public bool Has<T>() where T : IService
            {
                return _container.ContainsKey(typeof(T));
            }

            public T Get<T>() where T : IService
            {
                var found = _container.TryGetValue(typeof(T), out var obj);
                if (found)
                    return (T)obj;
                Debug.LogError($"Service of type {typeof(T).FullName} not found");
                return default;
            }

            public bool TryGet<T>(out T service) where T : IService
            {
                service = default;
                if (!_container.TryGetValue(typeof(T), out var obj))
                    return false;

                service = (T)obj;
                return true;
            }

            public void InitializeServices()
            {
                if (_initialized)
                    return;

                foreach (var obj in _container.Values)
                    obj.InitializeService();
                _initialized = true;
            }

            public void StartServices()
            {
                if (_started)
                    return;

                foreach (var obj in _container.Values)
                    obj.StartService();
                _started = true;
            }

            public List<IService> RemoveAll()
            {
                var removed = new List<IService>(_container.Values);
                _container.Clear();
                _initialized = false;
                _started = false;
                return removed;
            }
        }
    }
}