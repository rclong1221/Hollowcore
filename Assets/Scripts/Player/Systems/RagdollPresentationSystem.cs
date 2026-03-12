using UnityEngine;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Player.Components;
using Player.Animation;

namespace Player.Systems
{
    /// <summary>
    /// Client-side ragdoll presentation system (EPIC 10.17 Part B).
    /// Watches DeathState and triggers ragdoll on presentation GameObjects.
    /// 
    /// Architecture: ECS system queries entities with DeathState,
    /// uses GhostPresentationGameObjectSystem to find linked visual,
    /// and calls RagdollPresentationBridge methods on that GameObject.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RagdollPresentationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;
        
        protected override void OnCreate()
        {
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate<DeathState>();
        }
        
        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null) return;
            }
            
            // Query all entities with DeathState
            foreach (var (deathState, entity) in SystemAPI.Query<RefRO<DeathState>>()
                .WithEntityAccess())
            {
                // Get the presentation GameObject for this entity
                var presentationObject = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentationObject == null)
                    continue;
                
                // Get the ragdoll bridge MonoBehaviour
                var bridge = presentationObject.GetComponent<RagdollPresentationBridge>();
                if (bridge == null)
                    continue;
                

                
                // GhostOwnerIsLocal is an ENABLEABLE component - must check IsComponentEnabled!
                bool hasGhostOwner = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);
                bool isOwned = hasGhostOwner && EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);

                // CRITICAL FIX: If RagdollHipsSync is active (server sending data), we MUST ragdoll
                // even if DeathState.Phase hasn't replicated yet or is lagging.
                // This prevents "standing ragdolls" where physics are kinematic but sync is trying to move them.
                bool isSyncActive = false;
                if (SystemAPI.HasComponent<RagdollHipsSync>(entity))
                {
                    isSyncActive = SystemAPI.GetComponent<RagdollHipsSync>(entity).IsActive;
                }
                
                bool shouldRagdoll = deathState.ValueRO.Phase == DeathPhase.Dead || 
                                     deathState.ValueRO.Phase == DeathPhase.Downed ||
                                     isSyncActive;
                
                // DEBUG: Force log for remote players (isOwned=false) who are dead/downed, or transitioning
                if (!isOwned && shouldRagdoll)
                {
                    Debug.Log($"[RagdollPresSystem] E={entity.Index} Phase={deathState.ValueRO.Phase} ShouldRagdoll={shouldRagdoll} IsOwned={isOwned} -> Updating {bridge.name}");
                }
                
                bridge.UpdateRagdollState(shouldRagdoll, isOwned);
            }
        }
    }
}
