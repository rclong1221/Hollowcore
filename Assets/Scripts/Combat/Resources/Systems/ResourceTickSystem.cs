using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Resources.Systems
{
    /// <summary>
    /// EPIC 16.8 Phase 1: Handles regeneration, decay, overflow drain, integer clamping.
    /// Burst-compiled, predicted on all worlds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerStaminaSystem))]
    public partial struct ResourceTickSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<ResourcePool>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var pool in SystemAPI.Query<RefRW<ResourcePool>>()
                .WithAll<Simulate>())
            {
                TickSlot(ref pool.ValueRW.Slot0, deltaTime, currentTime);
                TickSlot(ref pool.ValueRW.Slot1, deltaTime, currentTime);
            }
        }

        private static void TickSlot(ref ResourceSlot slot, float deltaTime, float currentTime)
        {
            if (slot.Type == ResourceType.None) return;

            // Decay when idle (rage/combo)
            if ((slot.Flags & ResourceFlags.DecaysWhenIdle) != 0 && slot.DecayRate > 0f)
            {
                float timeSinceDrain = currentTime - slot.LastDrainTime;
                if (timeSinceDrain >= slot.RegenDelay)
                {
                    slot.Current -= slot.DecayRate * deltaTime;
                    slot.Current = math.max(0f, slot.Current);
                }
            }

            // Overflow decay (temporary buffs draining back to max)
            if ((slot.Flags & ResourceFlags.DecaysWhenFull) != 0 && slot.Current > slot.Max)
            {
                float decayAmount = slot.DecayRate > 0f ? slot.DecayRate : slot.Max * 0.1f;
                slot.Current -= decayAmount * deltaTime;
                slot.Current = math.max(slot.Max, slot.Current);
            }

            // Regeneration (not for decay-type resources, not when paused)
            if ((slot.Flags & ResourceFlags.PausedRegen) == 0 &&
                (slot.Flags & ResourceFlags.DecaysWhenIdle) == 0 &&
                slot.RegenRate > 0f && slot.Current < slot.Max)
            {
                float timeSinceDrain = currentTime - slot.LastDrainTime;
                if (timeSinceDrain >= slot.RegenDelay)
                {
                    slot.Current += slot.RegenRate * deltaTime;
                    slot.Current = math.min(slot.Current, slot.Max);
                }
            }

            // Integer clamping (combo points)
            if ((slot.Flags & ResourceFlags.IsInteger) != 0)
            {
                slot.Current = math.floor(slot.Current);
            }
        }
    }
}
