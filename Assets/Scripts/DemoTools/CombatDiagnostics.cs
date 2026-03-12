#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Burst;
using UnityEngine;

namespace DIG.Diagnostics
{
    /// <summary>
    /// Centralized combat diagnostics for tracing damage flow in listen server mode.
    /// 
    /// Damage Flow:
    /// 1. [DAMAGE_SOURCE] Hit detection (SweptMeleeHitboxSystem, ProjectileSystem) → DamageEvent created
    /// 2. [DAMAGE_APPLY] SimpleDamageApplySystem → Health.Current reduced (ServerWorld)
    /// 3. [GHOST_SYNC] NetCode replication → Health synced to ClientWorld (MAY BE BROKEN)
    /// 4. [HEALTH_BAR] EnemyHealthBarBridgeSystem → UI updated (ClientWorld)
    /// 
    /// Logs are ONLY emitted when health changes, not every frame.
    /// </summary>
    public static class CombatDiagnostics
    {
        /// <summary>Master toggle for all combat diagnostics. Disable for production.</summary>
        public static bool Enabled = false;
        
        /// <summary>Log individual damage events being created.</summary>
        public static bool LogDamageCreation = false;
        
        /// <summary>Log when damage is applied to health.</summary>
        public static bool LogDamageApplication = false;
        
        /// <summary>Log ghost sync comparisons (server vs client health).</summary>
        public static bool LogGhostSync = false;
        
        /// <summary>Log health bar updates.</summary>
        public static bool LogHealthBarUpdates = false;
        
        /// <summary>Log at startup only once.</summary>
        public static bool LogStartupDiagnostics = false;
        
        // Track entities we've already logged startup for
        private static HashSet<int> _loggedStartup = new HashSet<int>();
        
        // Track last logged health per entity to avoid spam
        private static Dictionary<int, float> _lastLoggedHealth = new Dictionary<int, float>();
        
        /// <summary>
        /// Call from systems at startup to log world/entity configuration.
        /// Only logs once per unique key.
        /// </summary>
        [BurstDiscard]
        public static void LogOnce(string key, string message)
        {
            if (!Enabled || !LogStartupDiagnostics) return;
            
            int hash = key.GetHashCode();
            if (_loggedStartup.Contains(hash)) return;
            _loggedStartup.Add(hash);
            
            UnityEngine.Debug.Log($"[COMBAT_INIT] {message}");
        }
        
        /// <summary>
        /// Log when a DamageEvent is created (melee hit, projectile hit, etc.)
        /// </summary>
        [BurstDiscard]
        public static void LogDamageEventCreated(
            string source,
            Entity targetEntity,
            float damage,
            uint serverTick,
            string worldName)
        {
            if (!Enabled || !LogDamageCreation) return;
            
            UnityEngine.Debug.Log(
                $"[DAMAGE_SOURCE] {source} → Entity {targetEntity.Index} " +
                $"| Damage={damage:F1} | Tick={serverTick} | World={worldName}");
        }
        
        /// <summary>
        /// Log when damage is applied to health (SimpleDamageApplySystem).
        /// Only logs when health actually changes.
        /// </summary>
        [BurstDiscard]
        public static void LogDamageApplied(
            Entity entity,
            float healthBefore,
            float healthAfter,
            float totalDamage,
            int eventCount,
            string worldName)
        {
            if (!Enabled || !LogDamageApplication) return;
            
            // Avoid logging if health didn't change
            if (Mathf.Approximately(healthBefore, healthAfter)) return;
            
            float healthPercent = healthAfter / Mathf.Max(1f, healthBefore) * 100f;
            
            UnityEngine.Debug.Log(
                $"[DAMAGE_APPLY] Entity {entity.Index} | {healthBefore:F0} → {healthAfter:F0} HP " +
                $"(-{totalDamage:F1} from {eventCount} events) | {healthPercent:F0}% | World={worldName}");
        }
        
        /// <summary>
        /// Log ghost sync status - compares server and client health values.
        /// Only logs when there's a mismatch.
        /// </summary>
        [BurstDiscard]
        public static void LogGhostSyncMismatch(
            Entity clientEntity,
            int ghostId,
            float clientHealth,
            float serverHealth,
            float maxHealth)
        {
            if (!Enabled || !LogGhostSync) return;
            
            // Only log if there's an actual mismatch
            if (Mathf.Approximately(clientHealth, serverHealth)) return;
            
            // Avoid spam - only log if health has changed since last log
            int key = ghostId * 1000 + (int)(serverHealth * 10);
            if (_lastLoggedHealth.TryGetValue(ghostId, out float lastLogged))
            {
                if (Mathf.Approximately(lastLogged, serverHealth)) return;
            }
            _lastLoggedHealth[ghostId] = serverHealth;
            
            UnityEngine.Debug.LogWarning(
                $"[GHOST_SYNC] MISMATCH! GhostId={ghostId} | " +
                $"Client={clientHealth:F0}/{maxHealth:F0} | Server={serverHealth:F0}/{maxHealth:F0} " +
                $"| Δ={Mathf.Abs(clientHealth - serverHealth):F1}");
        }
        
        /// <summary>
        /// Log when a health bar is updated (only on actual health changes).
        /// </summary>
        [BurstDiscard]
        public static void LogHealthBarUpdated(
            Entity entity,
            int ghostId,
            float currentHealth,
            float maxHealth,
            bool usedServerWorld)
        {
            if (!Enabled || !LogHealthBarUpdates) return;
            
            // Only log if health changed
            int key = entity.Index;
            if (_lastLoggedHealth.TryGetValue(key + 100000, out float lastLogged))
            {
                if (Mathf.Approximately(lastLogged, currentHealth)) return;
            }
            _lastLoggedHealth[key + 100000] = currentHealth;
            
            string source = usedServerWorld ? "ServerWorld" : "ClientWorld(Replicated)";
            UnityEngine.Debug.Log(
                $"[HEALTH_BAR] Entity {entity.Index} (Ghost={ghostId}) | " +
                $"{currentHealth:F0}/{maxHealth:F0} HP | Source={source}");
        }
        
        /// <summary>
        /// Log entity death.
        /// </summary>
        [BurstDiscard]
        public static void LogEntityDeath(Entity entity, int ghostId, string worldName)
        {
            if (!Enabled) return;
            
            UnityEngine.Debug.Log(
                $"[DEATH] Entity {entity.Index} (Ghost={ghostId}) died | World={worldName}");
        }
        
        /// <summary>
        /// Log a diagnostic summary for a specific entity on demand.
        /// Call this from debug UI or console command.
        /// </summary>
        [BurstDiscard]
        public static void LogEntityDiagnostics(
            Entity entity,
            int ghostId,
            float serverHealth,
            float clientHealth,
            float maxHealth,
            bool hasHealthBar)
        {
            if (!Enabled) return;
            
            bool synced = Mathf.Approximately(serverHealth, clientHealth);
            string syncStatus = synced ? "✓ SYNCED" : "✗ OUT OF SYNC";
            
            UnityEngine.Debug.Log(
                $"[ENTITY_DIAG] Entity={entity.Index} Ghost={ghostId}\n" +
                $"  Server HP: {serverHealth:F1}/{maxHealth:F1}\n" +
                $"  Client HP: {clientHealth:F1}/{maxHealth:F1}\n" +
                $"  Sync: {syncStatus}\n" +
                $"  HealthBar: {(hasHealthBar ? "Active" : "None")}");
        }
        
        /// <summary>
        /// Clear cached state (call on scene load).
        /// </summary>
        [BurstDiscard]
        public static void Reset()
        {
            _loggedStartup.Clear();
            _lastLoggedHealth.Clear();
        }
    }
}
#endif
