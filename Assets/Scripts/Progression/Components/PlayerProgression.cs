using Unity.Entities;
using Unity.NetCode;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Player progression state. Placed on PLAYER entities.
    /// Tracks current XP, lifetime XP, stat points, and rested bonus pool.
    /// Ghost-replicated so owning client can show XP bar.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerProgression : IComponentData
    {
        /// <summary>XP toward next level. Resets (with carry-over) on level-up.</summary>
        [GhostField] public int CurrentXP;

        /// <summary>Lifetime total XP earned. Never decreases. For stats/achievements.</summary>
        [GhostField] public int TotalXPEarned;

        /// <summary>Unspent stat points from level-ups.</summary>
        [GhostField] public int UnspentStatPoints;

        /// <summary>
        /// Bonus XP pool. Depleted as kills award XP. Accumulates offline.
        /// Stored as float for fractional depletion.
        /// </summary>
        [GhostField(Quantization = 100)] public float RestedXP;
    }
}
