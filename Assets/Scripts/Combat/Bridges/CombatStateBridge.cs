using UnityEngine;
using Unity.Entities;
using Opsive.Shared.Events;
using DIG.Combat.Components;

namespace DIG.Combat.Bridges
{
    /// <summary>
    /// Bridges Opsive's damage events to ECS CombatState.
    /// Add this to any entity with both Opsive Health and CombatStateAuthoring.
    /// </summary>
    [RequireComponent(typeof(CombatStateAuthoring))]
    public class CombatStateBridge : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool logEvents = false;
        
        private Entity _entity;
        private World _serverWorld;
        private bool _initialized;
        
        private void Start()
        {
            // Always log registration so we know the bridge is alive
            Debug.Log($"[CombatStateBridge] Registering on {gameObject.name} (logEvents={logEvents})");
            
            // Register for Opsive damage events
            EventHandler.RegisterEvent<float, Vector3, Vector3, GameObject, Collider>(
                gameObject, "OnHealthDamage", OnDamageReceived);
            
            Debug.Log($"[CombatStateBridge] ✓ Registered for OnHealthDamage on {gameObject.name}");
        }
        
        private void OnDestroy()
        {
            EventHandler.UnregisterEvent<float, Vector3, Vector3, GameObject, Collider>(
                gameObject, "OnHealthDamage", OnDamageReceived);
        }
        
        private void OnDamageReceived(float amount, Vector3 position, Vector3 force, GameObject attacker, Collider hitCollider)
        {
            if (logEvents)
                Debug.Log($"[CombatStateBridge] {gameObject.name} took {amount} damage from {attacker?.name ?? "unknown"}");
            
            // Find the ECS entity and set combat state
            if (!TryInitialize()) return;
            
            var em = _serverWorld.EntityManager;
            
            // Set this entity (the target) into combat
            if (em.Exists(_entity) && em.HasComponent<CombatState>(_entity))
            {
                var state = em.GetComponentData<CombatState>(_entity);
                bool wasInCombat = state.IsInCombat;
                
                state.IsInCombat = true;
                state.TimeSinceLastCombatAction = 0f;
                state.CombatExitTime = float.NegativeInfinity;
                
                em.SetComponentData(_entity, state);
                
                if (!wasInCombat && em.HasComponent<EnteredCombatTag>(_entity))
                {
                    em.SetComponentEnabled<EnteredCombatTag>(_entity, true);
                }
                
                if (logEvents)
                    Debug.Log($"[CombatStateBridge] {gameObject.name} entered combat!");
            }
            
            // Also try to put the attacker into combat if it has a bridge
            if (attacker != null)
            {
                var attackerBridge = attacker.GetComponent<CombatStateBridge>();
                if (attackerBridge != null)
                {
                    attackerBridge.EnterCombat();
                }
            }
        }
        
        /// <summary>
        /// Called by other bridges to put this entity into combat (as attacker).
        /// </summary>
        public void EnterCombat()
        {
            if (!TryInitialize()) return;
            
            var em = _serverWorld.EntityManager;
            
            if (em.Exists(_entity) && em.HasComponent<CombatState>(_entity))
            {
                var state = em.GetComponentData<CombatState>(_entity);
                bool wasInCombat = state.IsInCombat;
                
                state.IsInCombat = true;
                state.TimeSinceLastCombatAction = 0f;
                state.CombatExitTime = float.NegativeInfinity;
                
                em.SetComponentData(_entity, state);
                
                if (!wasInCombat && em.HasComponent<EnteredCombatTag>(_entity))
                {
                    em.SetComponentEnabled<EnteredCombatTag>(_entity, true);
                }
                
                if (logEvents)
                    Debug.Log($"[CombatStateBridge] {gameObject.name} entered combat (as attacker)!");
            }
        }
        
        private bool TryInitialize()
        {
            if (_initialized) return _serverWorld != null && _serverWorld.IsCreated;
            
            // Find ServerWorld
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld" && world.IsCreated)
                {
                    _serverWorld = world;
                    break;
                }
            }
            
            if (_serverWorld == null)
            {
                if (logEvents)
                    Debug.LogWarning($"[CombatStateBridge] No ServerWorld found");
                return false;
            }
            
            // Find our entity - look for matching LinkedEntityGroup or use entity mapping
            _entity = FindMyEntity();
            _initialized = true;
            
            if (_entity == Entity.Null)
            {
                if (logEvents)
                    Debug.LogWarning($"[CombatStateBridge] Could not find ECS entity for {gameObject.name}");
                return false;
            }
            
            return true;
        }
        
        private Entity FindMyEntity()
        {
            var em = _serverWorld.EntityManager;
            
            // Try to find entity via companion link
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatState>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            // For now, find by name matching (simple approach)
            // In production, you'd use a proper entity linking system
            foreach (var entity in entities)
            {
                // Check if this entity has a name that matches our GameObject
                // This is a simplified approach - real implementation would use proper linking
                if (em.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                    var myPos = (Unity.Mathematics.float3)this.transform.position;
                    
                    // Match by position (within tolerance)
                    if (Unity.Mathematics.math.distance(transform.Position, myPos) < 1f)
                    {
                        if (logEvents)
                            Debug.Log($"[CombatStateBridge] Found entity {entity} for {gameObject.name} via position match");
                        return entity;
                    }
                }
            }
            
            // Fallback: just use first entity with CombatState if only one exists
            if (entities.Length == 1)
            {
                return entities[0];
            }
            
            return Entity.Null;
        }
    }
}
