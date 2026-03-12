using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using DIG.Player.Abilities;
using DIG.Core.Feedback; // Replaces Audio.Systems usage for playback
using Audio.Systems;

namespace DIG.Player.Systems.Presentation
{
    /// <summary>
    /// Handles presentation effects for Jumping (Audio, VFX).
    /// Runs only on clients (or server with presentation).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class JumpPresentationSystem : SystemBase
    {
        // private AudioManager _audioManager;

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            // if (_audioManager == null) ...

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var em = EntityManager;

            // We only care about entities that "JustJumped"
            foreach (var (jumpAbility, transform, settings, entity) in 
                     SystemAPI.Query<RefRW<JumpAbility>, RefRO<LocalTransform>, RefRO<JumpSettings>>()
                     .WithEntityAccess())
            {
                if (jumpAbility.ValueRO.JustJumped)
                {
                    // Consumed
                    jumpAbility.ValueRW.JustJumped = false;

                    // Only play if enabled
                    if (settings.ValueRO.SpawnSurfaceEffect)
                    {
                        // GameplayFeedbackManager is static access
                        if (true) 
                        {
                             int matId = SurfaceDetectionService.GetDefaultId();
                             
                             // DOTS Physics Raycast
                             var rayStart = transform.ValueRO.Position + new float3(0, 0.5f, 0);
                             var rayEnd = transform.ValueRO.Position + new float3(0, -1.0f, 0); // 1.5m down check
                             
                             var rayInput = new Unity.Physics.RaycastInput
                             {
                                 Start = rayStart,
                                 End = rayEnd,
                                 Filter = new Unity.Physics.CollisionFilter
                                 {
                                     BelongsTo = ~0u,
                                     CollidesWith = ~0u,
                                     GroupIndex = 0
                                 }
                             };
                             
                             if (physicsWorld.CastRay(rayInput, out var hit))
                             {
                                 // Look for SurfaceMaterialId on the hit entity
                                 Entity hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                                 if (em.HasComponent<SurfaceMaterialId>(hitEntity))
                                 {
                                     matId = em.GetComponentData<SurfaceMaterialId>(hitEntity).Id;
                                 }
                             }
                            
                            
                             GameplayFeedbackManager.TriggerJump(matId, 1.0f, transform.ValueRO.Position);
                        }
                    }
                }
            }
        }
    }
}
