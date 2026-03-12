using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;

namespace DIG.Combat.Abilities
{
    /// <summary>
    /// System group for all player ability systems.
    /// Runs in PredictedFixedStepSimulationSystemGroup after physics,
    /// so ability targeting can use physics queries and movement gating
    /// integrates with the character controller.
    ///
    /// EPIC 18.19 - Phase 4
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class PlayerAbilitySystemGroup : ComponentSystemGroup
    {
    }
}
