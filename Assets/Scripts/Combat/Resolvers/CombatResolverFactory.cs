using System.Collections.Generic;

namespace DIG.Combat.Resolvers
{
    /// <summary>
    /// Factory for creating and caching combat resolver instances.
    /// Use this to get resolvers by type or ID.
    /// </summary>
    public static class CombatResolverFactory
    {
        private static readonly Dictionary<CombatResolverType, ICombatResolver> _resolvers = new();
        private static readonly Dictionary<string, ICombatResolver> _resolversByID = new();
        private static bool _initialized = false;
        
        /// <summary>
        /// Initialize the factory with default resolver instances.
        /// Called automatically on first access.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            // Create default instances
            RegisterResolver(CombatResolverType.PhysicsHitbox, new PhysicsHitboxResolver());
            RegisterResolver(CombatResolverType.StatBasedDirect, new StatBasedDirectResolver());
            RegisterResolver(CombatResolverType.StatBasedRoll, new StatBasedRollResolver());
            RegisterResolver(CombatResolverType.Hybrid, new HybridResolver());
            
            _initialized = true;
        }
        
        /// <summary>
        /// Register a resolver instance for a given type.
        /// </summary>
        public static void RegisterResolver(CombatResolverType type, ICombatResolver resolver)
        {
            _resolvers[type] = resolver;
            _resolversByID[resolver.ResolverID] = resolver;
        }
        
        /// <summary>
        /// Get a resolver by type.
        /// </summary>
        public static ICombatResolver GetResolver(CombatResolverType type)
        {
            if (!_initialized) Initialize();
            
            return _resolvers.TryGetValue(type, out var resolver) ? resolver : null;
        }
        
        /// <summary>
        /// Get a resolver by string ID.
        /// </summary>
        public static ICombatResolver GetResolver(string resolverID)
        {
            if (!_initialized) Initialize();
            
            return _resolversByID.TryGetValue(resolverID, out var resolver) ? resolver : null;
        }
        
        /// <summary>
        /// Get the default DIG resolver (PhysicsHitbox).
        /// </summary>
        public static ICombatResolver GetDIGDefault()
        {
            return GetResolver(CombatResolverType.PhysicsHitbox);
        }
        
        /// <summary>
        /// Get the default ARPG resolver (StatBasedDirect).
        /// </summary>
        public static ICombatResolver GetARPGDefault()
        {
            return GetResolver(CombatResolverType.StatBasedDirect);
        }
        
        /// <summary>
        /// Get the tactical ARPG resolver (StatBasedRoll).
        /// </summary>
        public static ICombatResolver GetTacticalDefault()
        {
            return GetResolver(CombatResolverType.StatBasedRoll);
        }
        
        /// <summary>
        /// Get the hybrid resolver.
        /// </summary>
        public static ICombatResolver GetHybridDefault()
        {
            return GetResolver(CombatResolverType.Hybrid);
        }
        
        /// <summary>
        /// Get all registered resolvers.
        /// </summary>
        public static IEnumerable<ICombatResolver> GetAllResolvers()
        {
            if (!_initialized) Initialize();
            return _resolvers.Values;
        }
        
        /// <summary>
        /// Reset the factory (for testing).
        /// </summary>
        public static void Reset()
        {
            _resolvers.Clear();
            _resolversByID.Clear();
            _initialized = false;
        }
    }
}
