using Unity.Entities;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Enableable tag that locks movement during ability casts.
    /// Enabled by AbilityExecutionSystem when MovementDuringCast == Locked.
    /// Checked by AICombatBehaviorSystem to skip movement writes.
    /// </summary>
    public struct MovementOverride : IComponentData, IEnableableComponent { }
}
