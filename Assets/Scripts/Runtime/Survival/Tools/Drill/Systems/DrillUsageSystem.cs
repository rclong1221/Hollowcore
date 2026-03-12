using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Voxel;

namespace DIG.Survival.Tools
{
    /// <summary>
    /// Handles drill tool usage - damages voxels and collects resources.
    /// Runs on server only (authoritative).
    /// EPIC 15.10: Integrated with unified destruction system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ToolRaycastSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DrillUsageSystem : ISystem
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

            foreach (var (drill, usageState, durability, transform, intentBuffer, entity) in 
                SystemAPI.Query<RefRO<DrillTool>, RefRW<ToolUsageState>, RefRW<ToolDurability>, RefRO<LocalTransform>, DynamicBuffer<DestructionIntentBuffer>>()
                    .WithAll<Simulate>()
                    .WithEntityAccess())
            {
                // Skip if not in use or depleted
                if (!usageState.ValueRO.IsInUse || durability.ValueRO.IsDepleted)
                    continue;

                // Skip if no valid target
                if (!usageState.ValueRO.HasTarget)
                    continue;

                // Degrade durability while drilling
                durability.ValueRW.Current -= durability.ValueRO.DegradeRatePerSecond * deltaTime;
                if (durability.ValueRO.Current <= 0f)
                {
                    durability.ValueRW.Current = 0f;
                    durability.ValueRW.IsDepleted = true;
                    continue;
                }

                // EPIC 15.10: Emit destruction intent
                var intent = new DestructionIntent
                {
                    SourceEntity = entity,
                    SourcePosition = transform.ValueRO.Position,
                    TargetPosition = usageState.ValueRO.TargetPoint,
                    TargetRotation = quaternion.identity,
                    ShapeType = drill.ValueRO.ShapeType,
                    DamageType = drill.ValueRO.DamageType,
                    Falloff = VoxelDamageFalloff.None,
                    Damage = drill.ValueRO.VoxelDamagePerSecond * deltaTime,
                    EdgeMultiplier = 1f,
                    Param1 = drill.ValueRO.DestructionRadius, // radius for sphere
                    Param2 = 0f,
                    Param3 = 0f,
                    IsValid = true
                };
                
                intentBuffer.Add(intent);
            }
        }
    }

    /// <summary>
    /// Request to collect resources.
    /// Placeholder for integration with inventory system (Epic 2.6).
    /// </summary>
    public struct ResourceCollectionRequest : IComponentData
    {
        public int ResourceType;
        public float Amount;
        public Entity CollectorEntity;
    }
}

