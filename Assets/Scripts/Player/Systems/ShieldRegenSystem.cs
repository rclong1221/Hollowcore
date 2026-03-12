using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// System that regenerates shield after damage delay (13.16.3).
    /// Server-authoritative.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShieldRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            new ShieldRegenJob
            {
                CurrentTime = currentTime,
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ShieldRegenJob : IJobEntity
        {
            public float CurrentTime;
            public float DeltaTime;

            void Execute(ref ShieldComponent shield, in DeathState deathState)
            {
                // Don't regenerate if dead
                if (deathState.Phase != DeathPhase.Alive)
                {
                    return;
                }

                // Don't regenerate if shield is full or has no capacity
                if (shield.IsFull || shield.Max <= 0f)
                {
                    return;
                }

                // Check if enough time has passed since last damage
                float timeSinceDamage = CurrentTime - shield.LastDamageTime;
                if (timeSinceDamage < shield.RegenDelay)
                {
                    return;
                }

                // Regenerate shield
                float regenAmount = shield.RegenRate * DeltaTime;
                shield.Current = math.min(shield.Max, shield.Current + regenAmount);
            }
        }
    }
}
