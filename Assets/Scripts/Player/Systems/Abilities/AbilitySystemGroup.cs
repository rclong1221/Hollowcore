using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Update group for all ability related systems.
    /// Runs within the predicted simulation group to ensure client-side prediction works.
    /// </summary>
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))] 
    [UpdateAfter(typeof(global::PlayerGroundCheckSystem))] // Use Ground results
    [UpdateBefore(typeof(global::PlayerMovementSystem))] // Apply ability velocities (Jump) before movement integration
    public partial class AbilitySystemGroup : ComponentSystemGroup
    {
    }
}
