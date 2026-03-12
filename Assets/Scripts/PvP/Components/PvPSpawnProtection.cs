using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: IEnableableComponent for spawn invulnerability. Baked disabled.
    /// Enabled by PvPSpawnSystem on respawn, disabled when ExpirationTick
    /// is reached or player deals damage (whichever comes first).
    /// 4 bytes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PvPSpawnProtection : IComponentData, IEnableableComponent
    {
        [GhostField] public uint ExpirationTick;
    }
}
