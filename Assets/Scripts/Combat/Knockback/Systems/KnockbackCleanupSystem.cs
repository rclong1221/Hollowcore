using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Manages immunity timers after knockback expires.
    /// Starts immunity window when knockback ends, decrements active immunity timers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(KnockbackMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct KnockbackCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<KnockbackResistance>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (knockbackState, resistance) in
                SystemAPI.Query<RefRO<KnockbackState>, RefRW<KnockbackResistance>>()
                    .WithAll<Simulate>())
            {
                ref var resist = ref resistance.ValueRW;

                // Start immunity timer when knockback just expired (IsActive=false, Elapsed>0 means it was active)
                if (!knockbackState.ValueRO.IsActive && knockbackState.ValueRO.Elapsed > 0f &&
                    resist.ImmunityTimeRemaining <= 0f && resist.ImmunityDuration > 0f &&
                    knockbackState.ValueRO.IsExpired)
                {
                    resist.ImmunityTimeRemaining = resist.ImmunityDuration;
                }

                // Decrement immunity timer
                if (resist.ImmunityTimeRemaining > 0f)
                {
                    resist.ImmunityTimeRemaining -= deltaTime;
                    if (resist.ImmunityTimeRemaining < 0f)
                        resist.ImmunityTimeRemaining = 0f;
                }
            }
        }
    }
}
