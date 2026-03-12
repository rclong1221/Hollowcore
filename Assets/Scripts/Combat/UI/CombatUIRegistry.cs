using UnityEngine;
using DIG.Combat.UI.WorldSpace;

namespace DIG.Combat.UI
{
    /// <summary>
    /// EPIC 15.9: Central registry for combat UI providers.
    /// Allows any UI system to register itself for combat feedback.
    /// Extended with floating text, enemy health bars, kill feed, status effects, and interaction rings.
    /// </summary>
    public static class CombatUIRegistry
    {
        // Core providers
        private static IDamageNumberProvider _damageNumbers;
        private static ICombatFeedbackProvider _feedback;
        private static ICombatLogProvider _combatLog;
        
        // EPIC 15.9 providers
        private static IFloatingTextProvider _floatingText;
        private static IEnemyHealthBarProvider _enemyHealthBars;
        private static IKillFeedProvider _killFeed;
        private static IInteractionRingProvider _interactionRings;
        
        // ==================== PROPERTIES ====================
        
        /// <summary>
        /// The registered damage number provider (can be null if none registered).
        /// </summary>
        public static IDamageNumberProvider DamageNumbers => _damageNumbers;
        
        /// <summary>
        /// The registered combat feedback provider (can be null if none registered).
        /// </summary>
        public static ICombatFeedbackProvider Feedback => _feedback;
        
        /// <summary>
        /// The registered combat log provider (can be null if none registered).
        /// </summary>
        public static ICombatLogProvider CombatLog => _combatLog;
        
        /// <summary>
        /// EPIC 15.9: The registered floating text provider.
        /// </summary>
        public static IFloatingTextProvider FloatingText => _floatingText;
        
        /// <summary>
        /// EPIC 15.9: The registered enemy health bar provider.
        /// </summary>
        public static IEnemyHealthBarProvider EnemyHealthBars => _enemyHealthBars;
        
        /// <summary>
        /// EPIC 15.9: The registered kill feed provider.
        /// </summary>
        public static IKillFeedProvider KillFeed => _killFeed;
        
        /// <summary>
        /// EPIC 15.9: The registered interaction ring provider.
        /// </summary>
        public static IInteractionRingProvider InteractionRings => _interactionRings;
        
        // ==================== HAS CHECKS ====================
        
        /// <summary>
        /// Whether any damage number provider is registered.
        /// </summary>
        public static bool HasDamageNumbers => _damageNumbers != null;
        
        /// <summary>
        /// Whether any feedback provider is registered.
        /// </summary>
        public static bool HasFeedback => _feedback != null;
        
        /// <summary>
        /// Whether any combat log provider is registered.
        /// </summary>
        public static bool HasCombatLog => _combatLog != null;
        
        /// <summary>
        /// Whether any floating text provider is registered.
        /// </summary>
        public static bool HasFloatingText => _floatingText != null;
        
        /// <summary>
        /// Whether any enemy health bar provider is registered.
        /// </summary>
        public static bool HasEnemyHealthBars => _enemyHealthBars != null;
        
        /// <summary>
        /// Whether any kill feed provider is registered.
        /// </summary>
        public static bool HasKillFeed => _killFeed != null;
        
        /// <summary>
        /// Whether any interaction ring provider is registered.
        /// </summary>
        public static bool HasInteractionRings => _interactionRings != null;
        
        // ==================== REGISTRATION ====================
        
        /// <summary>
        /// Register a damage number provider.
        /// Call this from your UI system's initialization.
        /// </summary>
        public static void RegisterDamageNumbers(IDamageNumberProvider provider)
        {
            if (_damageNumbers != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing damage number provider.");
            }
            _damageNumbers = provider;
        }
        
        /// <summary>
        /// Register a combat feedback provider.
        /// Call this from your UI system's initialization.
        /// </summary>
        public static void RegisterFeedback(ICombatFeedbackProvider provider)
        {
            if (_feedback != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing feedback provider.");
            }
            _feedback = provider;
        }
        
        /// <summary>
        /// Register a combat log provider.
        /// Call this from your UI system's initialization.
        /// </summary>
        public static void RegisterCombatLog(ICombatLogProvider provider)
        {
            if (_combatLog != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing combat log provider.");
            }
            _combatLog = provider;
        }
        
        /// <summary>
        /// EPIC 15.9: Register a floating text provider.
        /// </summary>
        public static void RegisterFloatingText(IFloatingTextProvider provider)
        {
            if (_floatingText != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing floating text provider.");
            }
            _floatingText = provider;
        }
        
        /// <summary>
        /// EPIC 15.9: Register an enemy health bar provider.
        /// </summary>
        public static void RegisterEnemyHealthBars(IEnemyHealthBarProvider provider)
        {
            if (_enemyHealthBars != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing enemy health bar provider.");
            }
            _enemyHealthBars = provider;
        }
        
        /// <summary>
        /// EPIC 15.9: Register a kill feed provider.
        /// </summary>
        public static void RegisterKillFeed(IKillFeedProvider provider)
        {
            if (_killFeed != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing kill feed provider.");
            }
            _killFeed = provider;
        }
        
        /// <summary>
        /// EPIC 15.9: Register an interaction ring provider.
        /// </summary>
        public static void RegisterInteractionRings(IInteractionRingProvider provider)
        {
            if (_interactionRings != null && provider != null)
            {
                Debug.LogWarning("[CombatUIRegistry] Replacing existing interaction ring provider.");
            }
            _interactionRings = provider;
        }
        
        // ==================== UNREGISTRATION ====================
        
        /// <summary>
        /// Unregister all providers (call on scene unload or cleanup).
        /// </summary>
        public static void UnregisterAll()
        {
            _damageNumbers = null;
            _feedback = null;
            _combatLog = null;
            _floatingText = null;
            _enemyHealthBars = null;
            _killFeed = null;
            _interactionRings = null;

            // Clear visual queues to prevent stale data on scene transitions
            DamageVisualQueue.Clear();
            StatusVisualQueue.Clear();
        }
        
        /// <summary>
        /// Unregister a specific damage number provider.
        /// </summary>
        public static void UnregisterDamageNumbers(IDamageNumberProvider provider)
        {
            if (_damageNumbers == provider)
                _damageNumbers = null;
        }
        
        /// <summary>
        /// Unregister a specific feedback provider.
        /// </summary>
        public static void UnregisterFeedback(ICombatFeedbackProvider provider)
        {
            if (_feedback == provider)
                _feedback = null;
        }
        
        /// <summary>
        /// Unregister a specific combat log provider.
        /// </summary>
        public static void UnregisterCombatLog(ICombatLogProvider provider)
        {
            if (_combatLog == provider)
                _combatLog = null;
        }
        
        /// <summary>
        /// EPIC 15.9: Unregister a floating text provider.
        /// </summary>
        public static void UnregisterFloatingText(IFloatingTextProvider provider)
        {
            if (_floatingText == provider)
                _floatingText = null;
        }
        
        /// <summary>
        /// EPIC 15.9: Unregister an enemy health bar provider.
        /// </summary>
        public static void UnregisterEnemyHealthBars(IEnemyHealthBarProvider provider)
        {
            if (_enemyHealthBars == provider)
                _enemyHealthBars = null;
        }
        
        /// <summary>
        /// EPIC 15.9: Unregister a kill feed provider.
        /// </summary>
        public static void UnregisterKillFeed(IKillFeedProvider provider)
        {
            if (_killFeed == provider)
                _killFeed = null;
        }
        
        /// <summary>
        /// EPIC 15.9: Unregister an interaction ring provider.
        /// </summary>
        public static void UnregisterInteractionRings(IInteractionRingProvider provider)
        {
            if (_interactionRings == provider)
                _interactionRings = null;
        }
    }
}
