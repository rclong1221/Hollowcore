using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Per-match PvP statistics on the player entity.
    /// AllPredicted so the owning client sees their own K/D/A without round-trip latency.
    /// Reset at match start, frozen at match end for results display.
    /// 24 bytes.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PvPPlayerStats : IComponentData
    {
        [GhostField] public short Kills;
        [GhostField] public short Deaths;
        [GhostField] public short Assists;
        [GhostField] public short ObjectiveScore;
        [GhostField(Quantization = 10)] public float DamageDealt;
        [GhostField(Quantization = 10)] public float DamageReceived;
        [GhostField(Quantization = 10)] public float HealingDone;
        [GhostField] public int MatchScore;
    }
}
