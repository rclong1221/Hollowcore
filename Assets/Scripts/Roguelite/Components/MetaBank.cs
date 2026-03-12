using Unity.Entities;
using Unity.NetCode;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Persistent account-level meta-progression data.
    /// Lives on a dedicated singleton entity (NOT the player — avoids 16KB archetype limit).
    /// Persisted across sessions via MetaProgressionSaveModule (TypeId=16).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct MetaBank : IComponentData
    {
        /// <summary>Current spendable permanent currency.</summary>
        [GhostField] public int MetaCurrency;

        /// <summary>Total meta-currency ever earned across all runs.</summary>
        [GhostField] public int LifetimeMetaEarned;

        /// <summary>Total runs started (incremented on RunPhase.Preparation).</summary>
        [GhostField] public int TotalRunsAttempted;

        /// <summary>Total runs ended with BossDefeated.</summary>
        [GhostField] public int TotalRunsWon;

        /// <summary>Highest score achieved in any single run.</summary>
        [GhostField] public int BestScore;

        /// <summary>Furthest zone reached in any single run.</summary>
        [GhostField] public byte BestZoneReached;

        /// <summary>Cumulative playtime across all runs (seconds).</summary>
        [GhostField(Quantization = 100)] public float TotalPlaytime;
    }

    /// <summary>
    /// EPIC 23.2: Signal component on the RunState entity. Enabled by MetaCurrencyConversionSystem
    /// after conversion completes so downstream systems know meta-currency has been awarded.
    /// Baked disabled. Consumed by MetaUIBridgeSystem.
    /// </summary>
    public struct MetaCurrencyConvertedTag : IComponentData, IEnableableComponent { }
}
