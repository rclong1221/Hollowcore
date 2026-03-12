# EPIC 23.1: Run Lifecycle & State Machine

**Status:** PLANNED
**Priority:** Critical (foundation for all other 23.x sub-epics)
**Dependencies:**
- `DIG.Shared` assembly (existing — `Assets/Scripts/Shared/DIG.Shared.asmdef`)
- `LobbyState` (existing — `Assets/Scripts/Lobby/LobbyState.cs`, EPIC 17.4)
- `DifficultyDefinitionSO` (existing — `Assets/Scripts/Lobby/Config/DifficultyDefinitionSO.cs`, EPIC 17.4)
- `PlayerProgression` (existing — `Assets/Scripts/Progression/Components/PlayerProgression.cs`, EPIC 16.14)
- Unity Entities (`IComponentData`, `IEnableableComponent`, `SystemBase`, `BlobAssetReference`)
- Unity NetCode (`GhostComponent`, `GhostField` — optional, harmless when absent)

**Feature:** Core run state machine that drives any rogue-lite game loop. Manages run phases from lobby through gameplay to death/victory, with deterministic seed propagation, permadeath handling, and a clean UI bridge for game-specific HUD.

---

## Problem

No concept of a "run" exists in the framework. Games need a state machine to track: is a run active? What phase is it in? What zone are we on? How long has it been? Is the player dead? What was the score? Without this, every rogue-lite game reinvents the same lifecycle management.

---

## Core Types

```csharp
// File: Assets/Scripts/Roguelite/Components/RunPhase.cs
namespace DIG.Roguelite
{
    public enum RunPhase : byte
    {
        None = 0,             // No active run
        Lobby = 1,            // Pre-run: config selection
        Preparation = 2,      // Loadout, meta-upgrades applied
        ZoneLoading = 3,      // IZoneProvider loading zone
        Active = 4,           // Normal gameplay in zone
        BossEncounter = 5,    // Boss zone (bridges EncounterState)
        ZoneTransition = 6,   // Between zones: rewards, shop, path choice
        RunEnd = 7,           // Death or final boss killed
        MetaScreen = 8        // Post-run: stats, meta-currency, unlocks
    }

    public enum RunEndReason : byte
    {
        None = 0,
        PlayerDeath = 1,
        BossDefeated = 2,
        Abandoned = 3,
        TimedOut = 4
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Components/RunState.cs
namespace DIG.Roguelite
{
    /// <summary>
    /// Singleton on a dedicated entity (NOT the player entity — avoids 16KB archetype limit).
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
    /// IEnableableComponent toggled on phase transitions.
    /// Other systems RequireForUpdate + check RunState.Phase to react without polling.
    /// </summary>
    public struct RunPhaseChangedTag : IComponentData, IEnableableComponent { }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Utility/RunSeedUtility.cs
namespace DIG.Roguelite
{
    /// <summary>
    /// Deterministic seed derivation. Burst-compatible, stateless.
    /// Identical seeds produce identical runs regardless of system execution order.
    /// </summary>
    public static class RunSeedUtility
    {
        public static uint DeriveZoneSeed(uint masterSeed, byte zoneIndex)
            => math.hash(new uint2(masterSeed, zoneIndex));

        public static uint DeriveEncounterSeed(uint zoneSeed, int encounterIndex)
            => math.hash(new uint2(zoneSeed, (uint)encounterIndex));

        public static uint DeriveRewardSeed(uint zoneSeed, int rewardIndex)
            => math.hash(new uint2(zoneSeed, (uint)(rewardIndex + 10000)));

        public static uint DeriveShopSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 20000u));

        public static uint DeriveEventSeed(uint zoneSeed)
            => math.hash(new uint2(zoneSeed, 30000u));
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Definitions/RunConfigSO.cs
namespace DIG.Roguelite
{
    [CreateAssetMenu(menuName = "DIG/Roguelite/Run Configuration")]
    public class RunConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string ConfigName;
        public int ConfigId;

        [Header("Structure")]
        public int ZoneCount = 5;
        public ZoneSequenceSO ZoneSequence;              // 23.3 (nullable if not using zones)

        [Header("Difficulty")]
        public DifficultyDefinitionSO BaseDifficulty;
        public AnimationCurve DifficultyPerZone = AnimationCurve.Linear(0, 1, 1, 3);

        [Header("Time")]
        public float BaseZoneTimeLimit;                  // 0 = no limit
        public float RunTimeLimit;                       // 0 = no limit

        [Header("Economy")]
        public int StartingRunCurrency;
        public int RunCurrencyPerZoneClear;
        public float MetaCurrencyConversionRate = 0.5f;

        [Header("Ascension")]
        public AscensionDefinitionSO AscensionDefinition; // 23.4 (nullable)
    }
}
```

```csharp
// File: Assets/Scripts/Roguelite/Utility/RunConfigBlobBuilder.cs
namespace DIG.Roguelite
{
    public struct RunConfigBlob
    {
        public int ZoneCount;
        public float BaseZoneTimeLimit;
        public float RunTimeLimit;
        public int StartingRunCurrency;
        public int RunCurrencyPerZoneClear;
        public float MetaCurrencyConversionRate;
        public BlobArray<float> DifficultyMultiplierPerZone; // Sampled from AnimationCurve
    }

    public struct RunConfigSingleton : IComponentData
    {
        public BlobAssetReference<RunConfigBlob> Config;
    }
}
```

---

## Systems

| System | Group | World Filter | Burst | Purpose |
|--------|-------|--------------|-------|---------|
| `RunConfigBootstrapSystem` | InitializationSystemGroup | All | No | Loads `RunConfigSO` from Resources/, builds `RunConfigBlob` via BlobBuilder, creates `RunConfigSingleton`. Follows `ProgressionBootstrapSystem` pattern |
| `RunLifecycleSystem` | SimulationSystemGroup | Server\|Local | Partial | State machine. Ticks `ElapsedTime`, derives `ZoneSeed`, validates phase transitions, toggles `RunPhaseChangedTag`. Time limit enforcement |
| `RunInitSystem` | SimulationSystemGroup, UpdateAfter(RunLifecycleSystem) | Server\|Local | No | On phase changes: bridges to IZoneProvider (23.3), configures difficulty, sets up zone economy |
| `PermadeathSystem` | SimulationSystemGroup | Server\|Local | No | Queries players with `Health <= 0`. Sets `RunPhase = RunEnd`, `EndReason = PlayerDeath`. Existing death systems run independently |
| `RunEndSystem` | SimulationSystemGroup, UpdateAfter(RunLifecycleSystem) | Server\|Local | No | On RunEnd: calculates final score, queues meta-currency conversion (23.2). Transitions to MetaScreen |
| `RunUIBridgeSystem` | PresentationSystemGroup | Client\|Local | No | Managed SystemBase. Reads `RunState`, pushes to `RunUIRegistry` (static, like `CombatUIRegistry`). Game UI subscribes |

---

## Execution Order

```
SimulationSystemGroup
    RunLifecycleSystem          (tick time, validate transitions, toggle RunPhaseChangedTag)
        ↓
    RunInitSystem               (react to phase changes, bridge to IZoneProvider)
        ↓
    PermadeathSystem            (watch Health, trigger RunEnd)
        ↓
    RunEndSystem                (score, meta-currency queuing)
        ↓
    [23.3 Zone Systems]
    [23.4 Modifier Systems]
    [23.5 Reward Systems]

PresentationSystemGroup
    RunUIBridgeSystem           (ECS → managed UI bridge)
```

---

## Integration

- **LobbyState**: Add `int RunConfigId` field. Lobby UI selects run configuration. `RunConfigBootstrapSystem` loads matching `RunConfigSO`
- **DifficultyDefinitionSO**: `RunConfigSO.BaseDifficulty` references existing SO. 23.4 `DifficultyScalingSystem` applies per-zone curve on top
- **Death systems**: `PermadeathSystem` does NOT replace ragdoll/corpse systems. It watches for death and sets `RunPhase`. All visual death handling continues as-is
- **PlayerProgression**: Run XP flows through existing `XPAwardSystem` → `LevelUpSystem` chain. `RunEndSystem` can optionally reset progression at run start

---

## Performance

- `RunState`: single entity, ~64 bytes. Ghost-replicated for client HUD when NetCode present
- `RunConfigBlob`: built once. `BlobArray<float>` for per-zone difficulty — O(1) indexed lookup
- `RunLifecycleSystem`: queries one singleton per frame — near zero cost
- `RunSeedUtility`: `math.hash` is a Burst intrinsic (~2ns per call)

---

## File Manifest

| File | Type | Status |
|------|------|--------|
| `Assets/Scripts/Roguelite/DIG.Roguelite.asmdef` | Assembly Definition | **NEW** |
| `Assets/Scripts/Roguelite/Components/RunState.cs` | IComponentData | **NEW** |
| `Assets/Scripts/Roguelite/Components/RunPhase.cs` | Enums | **NEW** |
| `Assets/Scripts/Roguelite/Definitions/RunConfigSO.cs` | ScriptableObject | **NEW** |
| `Assets/Scripts/Roguelite/Utility/RunSeedUtility.cs` | Static class | **NEW** |
| `Assets/Scripts/Roguelite/Utility/RunConfigBlobBuilder.cs` | BlobBuilder | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunConfigBootstrapSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunLifecycleSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunInitSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/PermadeathSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunEndSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Systems/RunUIBridgeSystem.cs` | SystemBase | **NEW** |
| `Assets/Scripts/Roguelite/Bridges/RunUIRegistry.cs` | Static registry | **NEW** |
| `Assets/Scripts/Lobby/LobbyState.cs` | IComponentData | Modified (add RunConfigId) |

---

## Verification

1. **Run start**: Lobby selects `RunConfigSO` → `RunState` entity created with correct seed, zone count, starting currency
2. **Phase transitions**: `RunPhase` advances correctly: None → Lobby → Preparation → ZoneLoading → Active → ZoneTransition → ... → RunEnd → MetaScreen
3. **Permadeath**: Player health reaches 0 → `RunPhase = RunEnd`, `EndReason = PlayerDeath` within 1 frame
4. **Timer enforcement**: `ElapsedTime` increments. Zone/run time limits trigger RunEnd with `TimedOut`
5. **Seed determinism**: Two runs with identical seed produce identical `ZoneSeed` sequence
6. **No-NetCode**: Module loads and runs in standalone build. `[GhostComponent]` causes no errors
7. **With-NetCode**: `RunState` replicates to clients. Client HUD shows correct phase, timer, score
