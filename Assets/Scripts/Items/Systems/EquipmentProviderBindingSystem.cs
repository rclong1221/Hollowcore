using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// System that binds DIGEquipmentProvider instances on GameObjects (Ghosts) to their specific ECS Entity.
    /// This fixes the multiplayer issue where all providers would default to finding the local player.
    /// 
    /// Works for both Local Player and Remote Ghosts (Predicted or Interpolated).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EquipmentProviderBindingSystem : SystemBase
    {
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;
        private GhostPresentationGameObjectSystem _presentationSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<EquippedItemElement>();
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }
            
            // Iterate over all entities that have equipment slots
            foreach (var (equippedBuffer, entity) in 
                     SystemAPI.Query<DynamicBuffer<EquippedItemElement>>()
                     .WithEntityAccess())
            {
                // Get the GameObject associated with this entity (Local or Ghost)
                var go = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (go == null) 
                    continue;

                // Look for DIGEquipmentProvider on the GameObject
                // Use GetComponent (fast for root) and GetComponentInChildren (fallback)
                var provider = go.GetComponent<DIGEquipmentProvider>();
                if (provider == null)
                    provider = go.GetComponentInChildren<DIGEquipmentProvider>();
                
                if (provider != null)
                {
                    // If not bound or bound to wrong entity/world, update it
                    if (provider.PlayerEntity != entity || provider.EntityWorld != this.World)
                    {
                        provider.SetPlayerEntity(entity, this.World);
                    }
                }
            }
        }
    }
}
