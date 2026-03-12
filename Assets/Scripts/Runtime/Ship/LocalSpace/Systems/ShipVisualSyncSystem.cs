using Unity.Entities;
using Unity.Transforms;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// System that syncs ship entity transform to the visual GameObject.
    /// This bridges the gap between ECS entity position and the rendered mesh.
    /// Uses GhostPresentationGameObjectSystem to find the linked visual.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ShipVisualSyncSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
        }

        protected override void OnUpdate()
        {
            // Ensure presentation system is available
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }

            // Sync transforms for all ships with ShipRoot component
            foreach (var (transform, shipRoot, entity) in 
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ShipRoot>>()
                     .WithEntityAccess())
            {
                // Get the presentation GameObject linked to this entity
                var presentationGO = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentationGO == null)
                {
                    // No presentation object - ship might not have GhostPresentationGameObjectAuthoring
                    continue;
                }

                // Sync the transform from ECS to GameObject
                var entityPos = transform.ValueRO.Position;
                var entityRot = transform.ValueRO.Rotation;

                presentationGO.transform.position = entityPos;
                presentationGO.transform.rotation = entityRot;
            }
        }
    }
}
