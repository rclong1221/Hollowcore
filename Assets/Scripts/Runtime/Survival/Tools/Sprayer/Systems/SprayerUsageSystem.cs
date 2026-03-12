using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Handles sprayer tool usage - creates blocking foam barriers.
    /// Runs on server only (authoritative) to spawn foam entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolRaycastSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SprayerUsageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (sprayer, usageState, durability, owner, entity) in
                     SystemAPI.Query<RefRW<SprayerTool>, RefRO<ToolUsageState>, RefRW<ToolDurability>, RefRO<ToolOwner>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var sprayerRef = ref sprayer.ValueRW;
                ref var durRef = ref durability.ValueRW;

                // Update cooldown timer
                sprayerRef.TimeSinceLastShot += deltaTime;

                // Skip if not in use or depleted
                if (!usageState.ValueRO.IsInUse || durRef.IsDepleted)
                    continue;

                // Skip if no valid target
                if (!usageState.ValueRO.HasTarget)
                    continue;

                // Check cooldown
                if (sprayerRef.TimeSinceLastShot < sprayerRef.Cooldown)
                    continue;

                // Check if enough ammo
                if (durRef.Current < sprayerRef.AmmoPerShot)
                {
                    durRef.IsDepleted = true;
                    continue;
                }

                // Consume ammo
                durRef.Current -= sprayerRef.AmmoPerShot;
                if (durRef.Current <= 0f)
                {
                    durRef.Current = 0f;
                    durRef.IsDepleted = true;
                }

                // Reset cooldown
                sprayerRef.TimeSinceLastShot = 0f;

                // Spawn foam entity if prefab is set
                if (sprayerRef.FoamPrefab != Entity.Null)
                {
                    var foamEntity = ecb.Instantiate(sprayerRef.FoamPrefab);

                    // Position foam at target point
                    ecb.SetComponent(foamEntity, LocalTransform.FromPosition(usageState.ValueRO.TargetPoint));

                    // Set foam data
                    ecb.SetComponent(foamEntity, new FoamEntity
                    {
                        TimeRemaining = 30f, // 30 second lifetime
                        CreatorEntity = owner.ValueRO.OwnerEntity
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Handles foam entity decay over time.
    /// Destroys foam when time runs out.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FoamDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (foam, entity) in
                     SystemAPI.Query<RefRW<FoamEntity>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                foam.ValueRW.TimeRemaining -= deltaTime;

                if (foam.ValueRO.TimeRemaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
