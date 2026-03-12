using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Cooldown validation stub.
    /// Verifies that ability activations respect server-side cooldown timers.
    /// Currently a placeholder — will integrate when ability cooldown RPCs are standardized.
    /// Budget: &lt;0.02ms (only on ability activation frames).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CooldownValidationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ValidationConfig>();
            // Will activate when ability cooldown tracking is added
            Enabled = false;
        }

        protected override void OnUpdate()
        {
            // Placeholder: ability cooldown validation will be implemented
            // when ability activation RPCs are standardized (future EPIC).
            //
            // Pattern:
            // 1. Track server-side cooldown timestamps per ability type per player
            //    using NativeParallelHashMap<long, uint> (packed playerId+abilityId → lastUseTick)
            // 2. When ability activation RPC arrives, verify:
            //    currentTick - lastUseTick >= cooldownTicks
            // 3. If violated: RateLimitHelper.CreateViolation(ViolationType.Cooldown)
        }
    }
}
