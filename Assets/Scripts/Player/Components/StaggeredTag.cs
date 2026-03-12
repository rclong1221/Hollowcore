using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player.Components
{
    /// <summary>
    /// Enableable tag component for staggered state.
    /// Using IEnableableComponent allows state changes without structural changes.
    /// Epic 7.3.3: Parallel-safe state changes via EnabledRefRW&lt;T&gt;.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct Staggered : IComponentData, IEnableableComponent
    {
        // Tag component - no data needed
        // State is tracked via PlayerCollisionState.StaggerTimeRemaining
    }
    
    /// <summary>
    /// Enableable tag component for knocked down state.
    /// Using IEnableableComponent allows state changes without structural changes.
    /// Epic 7.3.3: Parallel-safe state changes via EnabledRefRW&lt;T&gt;.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct KnockedDown : IComponentData, IEnableableComponent
    {
        // Tag component - no data needed
        // State is tracked via PlayerCollisionState.KnockdownTimeRemaining
    }
    
    /// <summary>
    /// Enableable tag component for evading state (dodge roll/dive i-frames).
    /// Using IEnableableComponent allows state changes without structural changes.
    /// Epic 7.4.3: Collision immunity during dodge i-frames.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct Evading : IComponentData, IEnableableComponent
    {
        // Tag component - no data needed
        // State is determined by DodgeRollState/DodgeDiveState elapsed time
    }
}
