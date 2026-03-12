using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Components;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 1: Shared ground surface detection for all entities.
    /// Players: reads from PlayerGroundCheckSystem output (no duplicate raycast).
    /// NPCs: raycasts down with frame-spread to stay within budget.
    /// Writes SurfaceMaterialId to GroundSurfaceState; GroundSurfaceCacheSystem resolves cached properties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerGroundCheckSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct GroundSurfaceQuerySystem : ISystem
    {
        private ComponentLookup<SurfaceMaterialId> _surfaceMaterialLookup;
        private ComponentLookup<PlayerTag> _playerTagLookup;
        private ComponentLookup<PlayerState> _playerStateLookup;
        private uint _tickCount;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _surfaceMaterialLookup = state.GetComponentLookup<SurfaceMaterialId>(true);
            _playerTagLookup = state.GetComponentLookup<PlayerTag>(true);
            _playerStateLookup = state.GetComponentLookup<PlayerState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _tickCount++;
            _surfaceMaterialLookup.Update(ref state);
            _playerTagLookup.Update(ref state);
            _playerStateLookup.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            float dt = SystemAPI.Time.DeltaTime;

            // Environment-only collision filter for NPC ground raycasts
            var groundFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = CollisionLayers.Environment | CollisionLayers.Default,
                GroupIndex = 0
            };

            foreach (var (surfaceState, transform, entity) in
                SystemAPI.Query<RefRW<GroundSurfaceState>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                ref var ss = ref surfaceState.ValueRW;
                ss.TimeSinceLastQuery += dt;

                // Skip if not time yet
                if (ss.QueryInterval > 0f && ss.TimeSinceLastQuery < ss.QueryInterval)
                    continue;

                bool isPlayer = _playerTagLookup.HasComponent(entity);

                // Frame-spread for NPC entities to prevent thundering herd
                if (!isPlayer)
                {
                    const int spreadFactor = 4;
                    if (((uint)entity.Index + _tickCount) % spreadFactor != 0)
                        continue;
                }

                ss.TimeSinceLastQuery = 0f;

                if (isPlayer)
                {
                    // Player path: read from PlayerGroundCheckSystem output
                    if (_playerStateLookup.HasComponent(entity))
                    {
                        var playerState = _playerStateLookup[entity];
                        ss.IsGrounded = playerState.IsGrounded;

                        if (playerState.IsGrounded && _surfaceMaterialLookup.HasComponent(entity))
                        {
                            ss.SurfaceMaterialId = _surfaceMaterialLookup[entity].Id;
                        }
                        else if (!playerState.IsGrounded)
                        {
                            ss.SurfaceMaterialId = -1;
                        }
                    }
                }
                else
                {
                    // NPC path: raycast down from slightly above feet
                    float3 origin = transform.ValueRO.Position + new float3(0, 0.2f, 0);
                    float3 end = transform.ValueRO.Position - new float3(0, 1.5f, 0);

                    var rayInput = new RaycastInput
                    {
                        Start = origin,
                        End = end,
                        Filter = groundFilter
                    };

                    if (physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
                    {
                        ss.IsGrounded = true;
                        Entity hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                        if (_surfaceMaterialLookup.HasComponent(hitEntity))
                        {
                            ss.SurfaceMaterialId = _surfaceMaterialLookup[hitEntity].Id;
                        }
                    }
                    else
                    {
                        ss.IsGrounded = false;
                        ss.SurfaceMaterialId = -1;
                    }
                }
            }
        }
    }
}
