using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Detects metal surfaces and attaches magnetic boots.
    /// Uses raycasts to find MetalSurface-tagged entities below the player.
    /// Updates AttachedNormal for gravity override.
    /// Sequential execution due to raycast requirements.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MagneticBootToggleSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MagneticBootAttachSystem : ISystem
    {
        private ComponentLookup<MetalSurface> _metalSurfaceLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _metalSurfaceLookup = state.GetComponentLookup<MetalSurface>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _metalSurfaceLookup.Update(ref state);
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            // Sequential execution due to raycast requirements
            foreach (var (transform, eva, bootState, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EVAState>, RefRW<MagneticBootState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var boots = ref bootState.ValueRW;

                // Only attempt attachment in EVA with boots enabled
                if (!eva.ValueRO.IsInEVA || !boots.IsEnabled)
                {
                    boots.IsAttached = false;
                    continue;
                }

                // Raycast down to find metal surfaces
                var position = transform.ValueRO.Position;
                var rayStart = position + new float3(0, 0.1f, 0); // Slightly above feet
                var rayEnd = position - new float3(0, boots.DetectRange, 0);

                var raycastInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayEnd,
                    Filter = new CollisionFilter
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                var hits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
                bool hasHit = physicsWorld.CollisionWorld.CastRay(raycastInput, ref hits);

                bool foundMetal = false;
                float closestFraction = float.MaxValue;
                float3 closestNormal = new float3(0, 1, 0);

                if (hasHit)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        var hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;

                        // Skip self
                        if (hitEntity == entity)
                            continue;

                        // Check if this is a metal surface
                        if (_metalSurfaceLookup.HasComponent(hitEntity))
                        {
                            if (hit.Fraction < closestFraction)
                            {
                                closestFraction = hit.Fraction;
                                closestNormal = hit.SurfaceNormal;
                                foundMetal = true;
                            }
                        }
                    }
                }

                hits.Dispose();

                if (foundMetal)
                {
                    boots.IsAttached = true;
                    boots.AttachedNormal = math.normalizesafe(closestNormal, new float3(0, 1, 0));
                }
                else
                {
                    boots.IsAttached = false;
                }
            }
        }
    }
}
