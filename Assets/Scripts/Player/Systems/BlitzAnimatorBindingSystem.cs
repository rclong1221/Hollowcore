using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Player.Components;
using Player.Bridges;
using UnityEngine;
using System.Collections.Generic;

namespace Player.Systems
{
    /// <summary>
    /// Binds BlitzAnimatorBridge to its ECS entity so it can read MountMovementInput.
    /// Uses GhostPresentationGameObjectSystem to find the presentation GameObject.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BlitzAnimatorBindingSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;
        private readonly HashSet<int> _boundInstances = new();

        protected override void OnCreate()
        {
            RequireForUpdate<RideableState>();
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                {
                    // Log once if not found
                    Debug.LogWarning("[BlitzAnimatorBindingSystem] GhostPresentationGameObjectSystem not found!");
                    return;
                }
            }

            // Find all rideable entities (Blitz, etc.) and bind their animator bridges
            foreach (var (rideableState, entity) in SystemAPI.Query<RefRO<RideableState>>().WithEntityAccess())
            {
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null)
                {
                    // This is normal during initial spawn - presentation GO may not exist yet
                    continue;
                }

                int instanceId = presentation.GetInstanceID();
                
                // Skip if already bound
                if (_boundInstances.Contains(instanceId))
                    continue;

                // Find BlitzAnimatorBridge and bind it
                var bridge = presentation.GetComponentInChildren<BlitzAnimatorBridge>(true);
                if (bridge != null)
                {
                    bridge.BindToEntity(entity);
                    _boundInstances.Add(instanceId);
                    Debug.Log($"[BlitzAnimatorBindingSystem] Bound {presentation.name} to entity {entity.Index} in {World.Name}");
                }
                else
                {
                    Debug.LogWarning($"[BlitzAnimatorBindingSystem] No BlitzAnimatorBridge found on {presentation.name}");
                }
            }
        }

        protected override void OnDestroy()
        {
            _boundInstances.Clear();
        }
    }
}

