using Unity.Entities;
using Unity.NetCode;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Core run state singleton on a dedicated entity (NOT the player — avoids 16KB archetype limit).
    /// [GhostComponent] is harmless without NetCode — ignored at compile time.
    /// With NetCode, all [GhostField] values auto-replicate to clients for HUD display.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RunState : IComponentData
    {
        [GhostField] public uint RunId;
        [GhostField] public uint Seed;
        [GhostField] public RunPhase Phase;
        [GhostField] public RunEndReason EndReason;
        [GhostField] public byte CurrentZoneIndex;     // 0-indexed
        [GhostField] public byte MaxZones;              // From RunConfigBlob
        [GhostField(Quantization = 100)] public float ElapsedTime;
        [GhostField] public int Score;
        [GhostField] public int RunCurrency;            // Resets on death
        [GhostField] public byte AscensionLevel;        // Heat/ascension tier (0 = normal)
        [GhostField] public uint ZoneSeed;              // Derived: Hash(Seed, CurrentZoneIndex)
    }

    /// <summary>
    /// EPIC 23.1: IEnableableComponent toggled on phase transitions.
    /// Other systems RequireForUpdate + check RunState.Phase to react without polling.
    /// Baked disabled. RunLifecycleSystem enables it on phase change, consumers disable it after reading.
    /// </summary>
    public struct RunPhaseChangedTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// EPIC 23.1: Signal component on RunState entity. Game-side systems enable this
    /// when a player dies (bridges game-specific Health/DeathState to framework).
    /// PermadeathSystem watches for this and transitions to RunEnd.
    /// </summary>
    public struct PermadeathSignal : IComponentData, IEnableableComponent { }
}
