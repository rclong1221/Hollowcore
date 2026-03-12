using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: IEnableableComponent tracking respawn delay after death. Baked disabled.
    /// Enabled by PvPScoringSystem on PvP death, PvPSpawnSystem reads it.
    /// 4 bytes.
    /// </summary>
    public struct PvPRespawnTimer : IComponentData, IEnableableComponent
    {
        public uint RespawnAtTick;
    }
}
