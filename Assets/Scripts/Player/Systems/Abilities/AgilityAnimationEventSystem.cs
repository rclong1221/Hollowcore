using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems.Abilities
{
    /// <summary>
    /// Consumes animation completion events from ClimbAnimatorBridge and clears ability states.
    /// This bridges MonoBehaviour animation callbacks to ECS state management.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    public partial struct AgilityAnimationEventSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // No special requirements
        }

        public void OnUpdate(ref SystemState state)
        {
            // Consume all pending animation events (thread-safe static queue)
            var events = AgilityAnimationEvents.ConsumeEvents();

            if (events == AgilityEventFlags.None)
                return;

            // Dodge, Roll, and Crawl are bridged from gameplay systems that control their lifecycle.
            // We DO NOT clear them here to avoid fighting with the authoritative bridge systems.
            // If we cleared them here, the bridge would re-enable them next frame, causing flickering.

            // Process vault complete (Vault is standalone, so we manage its lifecycle here)
            if ((events & AgilityEventFlags.VaultComplete) != 0)
            {
                foreach (var vaultState in SystemAPI.Query<RefRW<VaultState>>())
                {
                    ref var vs = ref vaultState.ValueRW;
                    if (vs.IsVaulting)
                    {
                        vs.IsVaulting = false;
                        vs.TimeRemaining = 0f;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates agility ability cooldowns and timers.
    /// Only manages standalone abilities (Vault). Bridged abilities (Dodge, Roll) are managed by their source systems.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct AgilityCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Dodge and Roll cooldowns/durations are managed by DodgeRollSystem/DodgeDiveSystem
            // and bridged to animation state. We don't touch them here to avoid conflicts.

            // Update vault timers (standalone)
            foreach (var vaultState in SystemAPI.Query<RefRW<VaultState>>().WithAll<Simulate>())
            {
                ref var vs = ref vaultState.ValueRW;

                if (vs.IsVaulting && vs.TimeRemaining > 0f)
                {
                    vs.TimeRemaining -= dt;
                    if (vs.TimeRemaining <= 0f)
                    {
                        vs.IsVaulting = false;
                    }
                }
            }
        }
    }
}
