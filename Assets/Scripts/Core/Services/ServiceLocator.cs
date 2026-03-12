using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Core
{
    /// <summary>
    /// Simple service locator for decoupling.
    /// Wraps existing singletons behind interfaces.
    /// Can be replaced with proper DI container later.
    /// 
    /// EPIC 15.20 - Input Paradigm Framework
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static bool _isInitialized;

        /// <summary>
        /// Register a service implementation.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                Debug.LogWarning($"[ServiceLocator] Attempted to register null for {typeof(T).Name}");
                return;
            }

            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Replacing existing registration for {type.Name}");
            }

            _services[type] = service;
        }

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>
        /// Get a registered service. Returns null if not found.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            return null;
        }

        /// <summary>
        /// Try to get a registered service.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        public static bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Clear all registered services. Call on scene unload or shutdown.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
            _isInitialized = false;
        }

        /// <summary>
        /// Initialize the service locator. Called automatically on first use.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _services.Clear();
        }
    }
}
