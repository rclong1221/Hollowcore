using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: IEnableableComponent tag applied to entities with KillCredited
    /// when the kill was a PvP kill (victim had PlayerTag). XPAwardSystem checks
    /// this to apply PvPKillXPMultiplier. Baked disabled on player entity.
    /// 0 bytes (tag only).
    /// </summary>
    public struct PvPKillMarker : IComponentData, IEnableableComponent { }
}
