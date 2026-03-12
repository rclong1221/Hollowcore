using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Initializes SpawnPosition on first frame and handles leashing.
    /// When an AI strays too far from its spawn point, it drops all aggro and
    /// should return home (actual movement is handled by AI behavior systems).
    /// 
    /// Also sets a "leashed" flag that AI behavior can use to trigger return behavior.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatDecaySystem))]
    [BurstCompile]
    public partial struct LeashSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (config, aggroState, spawnPos, transform, threatBuffer, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, RefRW<AggroState>, RefRW<SpawnPosition>, 
                    RefRO<LocalTransform>, DynamicBuffer<ThreatEntry>>()
                .WithEntityAccess())
            {
                // Initialize spawn position on first update
                if (!spawnPos.ValueRO.IsInitialized)
                {
                    spawnPos.ValueRW.Position = transform.ValueRO.Position;
                    spawnPos.ValueRW.IsInitialized = true;
                    continue; // Skip leash check on initialization frame
                }
                
                float leashDistance = config.ValueRO.LeashDistance;
                
                // Skip leash check if disabled (0 or negative = infinite leash)
                if (leashDistance <= 0f)
                    continue;
                
                // Skip if not aggroed
                if (!aggroState.ValueRO.IsAggroed)
                    continue;
                
                // Calculate distance from spawn
                float3 currentPos = transform.ValueRO.Position;
                float3 spawnPosition = spawnPos.ValueRO.Position;
                float distanceFromSpawn = math.distance(currentPos, spawnPosition);
                
                // Check if beyond leash distance
                if (distanceFromSpawn > leashDistance)
                {
                    // Drop all aggro - clear threat table
                    threatBuffer.Clear();
                    
                    // Reset aggro state
                    aggroState.ValueRW.IsAggroed = false;
                    aggroState.ValueRW.CurrentThreatLeader = Entity.Null;
                    aggroState.ValueRW.CurrentLeaderThreat = 0f;
                    aggroState.ValueRW.TimeSinceLastValidTarget = 0f;
                    
                    // Debug logging (non-Burst would go in separate MonoBehaviour debug system)
                    // The AI behavior system should check IsAggroed == false + distance > 0 
                    // to trigger return-home behavior
                }
            }
        }
    }
}
